using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Skills.Parsing;

namespace TheKameleon.Superpowers.Skills.Catalog;

public static class BundledCatalogLoader
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxCatalogLength = 256 * 1024;
    public const int MaxProvenanceLength = 128 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static BundledCatalogLoadResult LoadFromDirectory(string rootPath)
    {
        ArgumentNullException.ThrowIfNull(rootPath);
        return Load(new DirectoryCatalogSource(rootPath));
    }

    public static BundledCatalogLoadResult LoadFromPackage(string packagePath, string catalogRoot)
    {
        ArgumentNullException.ThrowIfNull(packagePath);
        ArgumentNullException.ThrowIfNull(catalogRoot);

        using var package = ZipFile.OpenRead(packagePath);
        return Load(new PackageCatalogSource(package, catalogRoot));
    }

    private static BundledCatalogLoadResult Load(ICatalogSource source)
    {
        var diagnostics = new List<ParseDiagnostic>();
        var catalogText = ReadRequiredText(source, "catalog.json", diagnostics, "SPCAT401", "Bundled catalog metadata is missing.", MaxCatalogLength);
        if (catalogText is null)
        {
            return new BundledCatalogLoadResult(string.Empty, default, Array.Empty<LoadedCatalogRelease>(), diagnostics);
        }

        CatalogModel? catalog;
        try
        {
            catalog = JsonSerializer.Deserialize<CatalogModel>(catalogText, JsonOptions);
        }
        catch (JsonException exception)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT402", $"Bundled catalog metadata is not valid JSON: {exception.Message}"));
            return new BundledCatalogLoadResult(string.Empty, default, Array.Empty<LoadedCatalogRelease>(), diagnostics);
        }

        if (catalog is null)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT403", "Bundled catalog metadata is empty."));
            return new BundledCatalogLoadResult(string.Empty, default, Array.Empty<LoadedCatalogRelease>(), diagnostics);
        }

        if (catalog.SchemaVersion != CurrentSchemaVersion)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT404", $"Unsupported bundled catalog schema version '{catalog.SchemaVersion}'."));
        }

        if (!DateTimeOffset.TryParse(catalog.CutoffCapturedAtUtc, out var cutoffCapturedAtUtc))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT405", $"Bundled catalog cutoff '{catalog.CutoffCapturedAtUtc}' is invalid."));
        }

        var releases = new List<LoadedCatalogRelease>();
        foreach (var release in catalog.Releases ?? Array.Empty<CatalogReleaseModel>())
        {
            releases.Add(LoadRelease(source, release));
        }

        return new BundledCatalogLoadResult(catalog.SourceRepositoryUrl ?? string.Empty, cutoffCapturedAtUtc, releases, diagnostics);
    }

    private static LoadedCatalogRelease LoadRelease(ICatalogSource source, CatalogReleaseModel release)
    {
        var diagnostics = new List<ParseDiagnostic>();
        var releaseTag = release.ReleaseTag ?? string.Empty;
        var releaseRoot = GetParentDirectory(release.ProvenancePath ?? string.Empty);

        var licenseText = ReadRequiredText(source, release.LicensePath, diagnostics, "SPCAT410", $"Release '{releaseTag}' is missing its bundled license text.", null) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(licenseText))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT411", $"Release '{releaseTag}' has an empty license file."));
        }

        var adapterText = ReadRequiredText(source, release.AdapterManifestPath, diagnostics, "SPCAT412", $"Release '{releaseTag}' is missing its adapter manifest.", AdapterManifestParser.MaxManifestLength) ?? string.Empty;
        var adapterManifest = string.IsNullOrEmpty(adapterText)
            ? new AdapterManifest(0, Array.Empty<AdapterManifestAction>(), diagnostics.Where(diagnostic => diagnostic.Code == "SPCAT412").ToArray())
            : AdapterManifestParser.Parse(adapterText);

        var planMetadata = LoadPlanMetadata(source, releaseTag, release.PlanMetadataPath, diagnostics);

        var provenanceText = ReadRequiredText(source, release.ProvenancePath, diagnostics, "SPCAT413", $"Release '{releaseTag}' is missing its provenance metadata.", MaxProvenanceLength);
        ProvenanceModel? provenance = null;
        if (provenanceText is not null)
        {
            try
            {
                provenance = JsonSerializer.Deserialize<ProvenanceModel>(provenanceText, JsonOptions);
            }
            catch (JsonException exception)
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT414", $"Release '{releaseTag}' provenance is not valid JSON: {exception.Message}"));
            }
        }

        if (provenance is null)
        {
            provenance = new ProvenanceModel();
        }
        else
        {
            ValidateProvenance(source, releaseTag, releaseRoot, release.ResolvedCommit, provenance, diagnostics);
        }

        var archiveBytes = ReadRequiredBytes(source, release.ArchivePath, diagnostics, "SPCAT415", $"Release '{releaseTag}' is missing its bundled source archive.");
        var skills = new List<DiscoveredSkillEntry>();
        var assets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (archiveBytes is not null && adapterManifest.Actions.Count > 0)
        {
            using var archiveStream = new MemoryStream(archiveBytes, writable: false);
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false);
            var archiveEntries = BuildArchiveEntryMap(archive);
            ValidateManifestSkillsAndDependencies(releaseTag, adapterManifest, archiveEntries, diagnostics, skills, assets);
        }

        DateTimeOffset? publishedAtUtc = DateTimeOffset.TryParse(
            release.PublishedAtUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var published)
            ? published
            : null;

        return new LoadedCatalogRelease(
            releaseTag,
            provenance.ResolvedCommit ?? release.ResolvedCommit ?? string.Empty,
            licenseText,
            adapterManifest,
            planMetadata,
            skills,
            assets.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray(),
            diagnostics)
        {
            ArchivePath = release.ArchivePath ?? string.Empty,
            IsPrerelease = release.Prerelease,
            PublishedAtUtc = publishedAtUtc,
        };
    }

    private static PlanEntryPointMetadata? LoadPlanMetadata(
        ICatalogSource source,
        string releaseTag,
        string? planMetadataPath,
        List<ParseDiagnostic> diagnostics)
    {
        var planMetadataText = ReadRequiredText(source, planMetadataPath, diagnostics, "SPCAT424", $"Release '{releaseTag}' is missing its plan metadata.", MaxCatalogLength);
        if (planMetadataText is null)
        {
            return null;
        }

        PlanMetadataModel? planMetadata;
        try
        {
            planMetadata = JsonSerializer.Deserialize<PlanMetadataModel>(planMetadataText, JsonOptions);
        }
        catch (JsonException exception)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT425", $"Release '{releaseTag}' plan metadata is not valid JSON: {exception.Message}"));
            return null;
        }

        if (planMetadata is null)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT426", $"Release '{releaseTag}' plan metadata is empty."));
            return null;
        }

        if (planMetadata.SchemaVersion != PlanEntryPointMetadata.CurrentSchemaVersion)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT427", $"Release '{releaseTag}' plan metadata schema version '{planMetadata.SchemaVersion}' is unsupported."));
        }

        if (!string.Equals(planMetadata.ReleaseTag, releaseTag, StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT428", $"Release '{releaseTag}' plan metadata tag '{planMetadata.ReleaseTag ?? string.Empty}' does not match the release tag."));
        }

        var composition = new List<PlanCompositionStep>();
        foreach (var step in planMetadata.Composition ?? Array.Empty<PlanCompositionStepModel>())
        {
            if (step.Order <= 0 || string.IsNullOrWhiteSpace(step.SkillPath) || string.IsNullOrWhiteSpace(step.Purpose))
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT429", $"Release '{releaseTag}' plan metadata contains an incomplete composition step."));
                continue;
            }

            composition.Add(new PlanCompositionStep(step.Order, step.SkillPath, step.Purpose));
        }

        try
        {
            return new PlanEntryPointMetadata(
                planMetadata.EntryPoint ?? string.Empty,
                planMetadata.SourceRepository ?? string.Empty,
                planMetadata.ReleaseTag ?? string.Empty,
                composition,
                planMetadata.Notes,
                planMetadata.SchemaVersion);
        }
        catch (ArgumentException exception)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT430", $"Release '{releaseTag}' plan metadata is invalid: {exception.Message}"));
            return null;
        }
    }

    private static void ValidateProvenance(
        ICatalogSource source,
        string releaseTag,
        string releaseRoot,
        string? expectedCommit,
        ProvenanceModel provenance,
        List<ParseDiagnostic> diagnostics)
    {
        if (provenance.SchemaVersion != CurrentSchemaVersion)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT416", $"Release '{releaseTag}' provenance schema version '{provenance.SchemaVersion}' is unsupported."));
        }

        if (!string.Equals(provenance.ReleaseTag, releaseTag, StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT417", $"Release '{releaseTag}' provenance tag '{provenance.ReleaseTag ?? string.Empty}' does not match catalog metadata."));
        }

        if (!string.IsNullOrWhiteSpace(expectedCommit)
            && !string.Equals(provenance.ResolvedCommit, expectedCommit, StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT418", $"Release '{releaseTag}' provenance commit '{provenance.ResolvedCommit ?? string.Empty}' does not match catalog metadata '{expectedCommit}'."));
        }

        foreach (var file in provenance.Files ?? Array.Empty<ProvenanceFileModel>())
        {
            if (string.IsNullOrWhiteSpace(file.Path) || string.IsNullOrWhiteSpace(file.Sha256))
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT419", $"Release '{releaseTag}' provenance contains an incomplete file hash record."));
                continue;
            }

            var relativePath = CombineRelativePath(releaseRoot, file.Path);
            var content = ReadRequiredBytes(source, relativePath, diagnostics, "SPCAT420", $"Release '{releaseTag}' provenance file '{file.Path}' is missing.");
            if (content is null)
            {
                continue;
            }

            var hash = ComputeSha256(content);
            if (!string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT421", $"Release '{releaseTag}' file '{file.Path}' failed SHA-256 validation."));
            }
        }
    }

    private static void ValidateManifestSkillsAndDependencies(
        string releaseTag,
        AdapterManifest manifest,
        IReadOnlyDictionary<string, ZipArchiveEntry> archiveEntries,
        List<ParseDiagnostic> diagnostics,
        List<DiscoveredSkillEntry> skills,
        HashSet<string> assets)
    {
        var visitedSkillPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in manifest.Actions)
        {
            ValidateSkillPath(
                releaseTag,
                action.ActionId,
                action.SkillPath,
                archiveEntries,
                diagnostics,
                skills,
                assets,
                visitedSkillPaths);
        }
    }

    private static void ValidateSkillPath(
        string releaseTag,
        string actionId,
        string skillPath,
        IReadOnlyDictionary<string, ZipArchiveEntry> archiveEntries,
        List<ParseDiagnostic> diagnostics,
        List<DiscoveredSkillEntry> skills,
        HashSet<string> assets,
        HashSet<string> visitedSkillPaths)
    {
        if (!archiveEntries.TryGetValue(skillPath, out var entry))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT422", $"Release '{releaseTag}' is missing adapter skill '{skillPath}' for action '{actionId}'."));
            return;
        }

        var parsed = SkillDocumentParser.Parse(ReadEntryText(entry));
        var entryDiagnostics = parsed.Diagnostics.ToList();
        var skillId = string.IsNullOrWhiteSpace(parsed.Name) ? actionId : parsed.Name;
        skills.Add(new DiscoveredSkillEntry(
            skillId,
            skillPath,
            $"zip:{releaseTag}:{skillPath}",
            DiscoverySourceKind.Packaged,
            DiscoveryTrustState.Implicit,
            parsed,
            entryDiagnostics));

        if (!visitedSkillPaths.Add(skillPath))
        {
            return;
        }

        foreach (var reference in parsed.References)
        {
            var resolvedPath = ResolveRelativeArchivePath(skillPath, reference.RelativePath);
            if (resolvedPath is null || !archiveEntries.ContainsKey(resolvedPath))
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT423", $"Release '{releaseTag}' skill '{skillPath}' references missing asset '{reference.RelativePath}'."));
                continue;
            }

            assets.Add(resolvedPath);
            if (resolvedPath.EndsWith("/SKILL.md", StringComparison.OrdinalIgnoreCase))
            {
                ValidateSkillPath(releaseTag, resolvedPath, resolvedPath, archiveEntries, diagnostics, skills, assets, visitedSkillPaths);
            }
        }
    }

    private static Dictionary<string, ZipArchiveEntry> BuildArchiveEntryMap(ZipArchive archive)
    {
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            var normalized = NormalizeArchiveEntry(entry.FullName);
            if (string.IsNullOrWhiteSpace(normalized) || normalized.EndsWith("/", StringComparison.Ordinal))
            {
                continue;
            }

            entries[normalized] = entry;
        }

        return entries;
    }

    private static string NormalizeArchiveEntry(string fullName)
    {
        var normalized = fullName.Replace('\\', '/').Trim('/');
        var firstSeparator = normalized.IndexOf('/');
        if (firstSeparator < 0 || firstSeparator == normalized.Length - 1)
        {
            return string.Empty;
        }

        return normalized[(firstSeparator + 1)..];
    }

    private static string? ResolveRelativeArchivePath(string sourcePath, string referencePath)
    {
        var sourceDirectory = GetParentDirectory(sourcePath);
        var segments = new List<string>();
        foreach (var segment in CombineRelativePath(sourceDirectory, referencePath).Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    return null;
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        return string.Join('/', segments);
    }

    private static string CombineRelativePath(string directory, string path)
    {
        var left = (directory ?? string.Empty).Replace('\\', '/').Trim('/');
        var right = (path ?? string.Empty).Replace('\\', '/').Trim('/');
        if (string.IsNullOrEmpty(left))
        {
            return right;
        }

        if (string.IsNullOrEmpty(right))
        {
            return left;
        }

        return left + "/" + right;
    }

    private static string GetParentDirectory(string path)
    {
        var normalized = (path ?? string.Empty).Replace('\\', '/').Trim('/');
        var lastSeparator = normalized.LastIndexOf('/');
        return lastSeparator <= 0 ? string.Empty : normalized[..lastSeparator];
    }

    private static string ReadEntryText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static string ComputeSha256(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }

    private static string? ReadRequiredText(
        ICatalogSource source,
        string? relativePath,
        List<ParseDiagnostic> diagnostics,
        string code,
        string message,
        int? maxLength)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, code, message));
            return null;
        }

        if (!source.TryReadText(relativePath, out var content))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, code, message));
            return null;
        }

        if (maxLength.HasValue && content.Length > maxLength.Value)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, code, $"{message} The file exceeds the supported length of {maxLength.Value} characters."));
            return null;
        }

        return content;
    }

    private static byte[]? ReadRequiredBytes(
        ICatalogSource source,
        string? relativePath,
        List<ParseDiagnostic> diagnostics,
        string code,
        string message)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, code, message));
            return null;
        }

        if (!source.TryReadBytes(relativePath, out var content))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, code, message));
            return null;
        }

        return content;
    }

    private interface ICatalogSource
    {
        bool TryReadText(string relativePath, out string content);

        bool TryReadBytes(string relativePath, out byte[] content);
    }

    private sealed class DirectoryCatalogSource(string rootPath) : ICatalogSource
    {
        private readonly string rootPath = Path.GetFullPath(rootPath);

        public bool TryReadText(string relativePath, out string content)
        {
            var path = GetFullPath(relativePath);
            if (!File.Exists(path))
            {
                content = string.Empty;
                return false;
            }

            content = File.ReadAllText(path);
            return true;
        }

        public bool TryReadBytes(string relativePath, out byte[] content)
        {
            var path = GetFullPath(relativePath);
            if (!File.Exists(path))
            {
                content = Array.Empty<byte>();
                return false;
            }

            content = File.ReadAllBytes(path);
            return true;
        }

        private string GetFullPath(string relativePath)
        {
            var path = relativePath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(rootPath, path);
        }
    }

    private sealed class PackageCatalogSource : ICatalogSource
    {
        private readonly Dictionary<string, ZipArchiveEntry> entries;
        private readonly string root;

        public PackageCatalogSource(ZipArchive package, string root)
        {
            this.root = root.Replace('\\', '/').Trim('/');
            entries = package.Entries.ToDictionary(entry => entry.FullName.Replace('\\', '/').Trim('/'), StringComparer.OrdinalIgnoreCase);
        }

        public bool TryReadText(string relativePath, out string content)
        {
            if (!entries.TryGetValue(CombineRelativePath(root, relativePath), out var entry))
            {
                content = string.Empty;
                return false;
            }

            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            content = reader.ReadToEnd();
            return true;
        }

        public bool TryReadBytes(string relativePath, out byte[] content)
        {
            if (!entries.TryGetValue(CombineRelativePath(root, relativePath), out var entry))
            {
                content = Array.Empty<byte>();
                return false;
            }

            using var stream = entry.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            content = memory.ToArray();
            return true;
        }
    }

    private sealed class CatalogModel
    {
        public int SchemaVersion { get; init; }

        public string? SourceRepositoryUrl { get; init; }

        public string? CutoffCapturedAtUtc { get; init; }

        public CatalogReleaseModel[]? Releases { get; init; }
    }

    private sealed class CatalogReleaseModel
    {
        public string? ReleaseTag { get; init; }

        public string? ResolvedCommit { get; init; }

        public string? LicensePath { get; init; }

        public string? ArchivePath { get; init; }

        public string? AdapterManifestPath { get; init; }

        public string? PlanMetadataPath { get; init; }

        public string? ProvenancePath { get; init; }

        public bool Prerelease { get; init; }

        public string? PublishedAtUtc { get; init; }
    }

    private sealed class PlanMetadataModel
    {
        public int SchemaVersion { get; init; }

        public string? EntryPoint { get; init; }

        public string? SourceRepository { get; init; }

        public string? ReleaseTag { get; init; }

        public PlanCompositionStepModel[]? Composition { get; init; }

        public string[]? Notes { get; init; }
    }

    private sealed class PlanCompositionStepModel
    {
        public int Order { get; init; }

        public string? SkillPath { get; init; }

        public string? Purpose { get; init; }
    }

    private sealed class ProvenanceModel
    {
        public int SchemaVersion { get; init; }

        public string? ReleaseTag { get; init; }

        public string? ResolvedCommit { get; init; }

        public ProvenanceFileModel[]? Files { get; init; }
    }

    private sealed class ProvenanceFileModel
    {
        public string? Path { get; init; }

        public string? Sha256 { get; init; }
    }
}
