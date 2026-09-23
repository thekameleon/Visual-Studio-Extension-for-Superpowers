using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using TheKameleon.Superpowers.Core.Contracts.Catalog;

namespace TheKameleon.Superpowers.Skills.Catalog;

public sealed class ApprovedReleaseDownloadService(HttpClient httpClient)
{
    public const int MaxDownloadBytes = 25 * 1024 * 1024;
    public const int MaxArchiveEntries = 10_000;
    public const long MaxExtractedBytes = 100L * 1024 * 1024;

    private readonly HttpClient httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<DownloadActivationResult> DownloadAndActivateAsync(
        DiscoveredRemoteRelease release,
        string targetDirectory,
        bool approvalGranted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        var diagnostics = new List<ParseDiagnostic>();
        if (!approvalGranted)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT601", "Remote release download requires explicit approval before staging or activation."));
            return new DownloadActivationResult(null, null, diagnostics);
        }

        if (!IsApprovedRelease(release))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT602", $"Remote release '{release.ReleaseTag}' is not from the approved source."));
            return new DownloadActivationResult(null, null, diagnostics);
        }

        var stagingRoot = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(targetDirectory))!, ".staging", Guid.NewGuid().ToString("N"));
        var releaseRoot = Path.Combine(stagingRoot, "releases", release.ReleaseTag);
        var sourceZipPath = Path.Combine(releaseRoot, "source.zip");
        var extractedRoot = Path.Combine(stagingRoot, "expanded");
        Directory.CreateDirectory(releaseRoot);
        Directory.CreateDirectory(extractedRoot);

        DownloadedReleaseStage? stage = null;
        BundledCatalogLoadResult? validation = null;
        try
        {
            var downloadBytes = await DownloadArchiveAsync(release, diagnostics, cancellationToken).ConfigureAwait(false);
            if (downloadBytes is null)
            {
                return new DownloadActivationResult(null, null, diagnostics);
            }

            await File.WriteAllBytesAsync(sourceZipPath, downloadBytes, cancellationToken).ConfigureAwait(false);
            ExtractArchiveSafely(downloadBytes, extractedRoot, diagnostics);
            if (diagnostics.Any(diagnostic => diagnostic.Severity == ParseDiagnosticSeverity.Error))
            {
                stage = new DownloadedReleaseStage(release.ReleaseTag, release.SourceRepositoryUrl, release.ZipballUrl, stagingRoot, stagingRoot, null);
                return new DownloadActivationResult(stage, null, diagnostics);
            }

            var licenseSourcePath = FindLicenseFile(extractedRoot);
            if (licenseSourcePath is null)
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT605", $"Downloaded release '{release.ReleaseTag}' does not contain a bundled license file."));
                stage = new DownloadedReleaseStage(release.ReleaseTag, release.SourceRepositoryUrl, release.ZipballUrl, stagingRoot, stagingRoot, null);
                return new DownloadActivationResult(stage, null, diagnostics);
            }

            var licensePath = Path.Combine(releaseRoot, "LICENSE.txt");
            File.Copy(licenseSourcePath, licensePath, overwrite: true);

            var adapterManifestPath = Path.Combine(releaseRoot, "adapter-manifest.json");
            await WriteAdapterManifestAsync(adapterManifestPath, cancellationToken).ConfigureAwait(false);

            var planMetadataPath = Path.Combine(releaseRoot, "plan-metadata.json");
            await WritePlanMetadataAsync(release, planMetadataPath, cancellationToken).ConfigureAwait(false);

            var provenancePath = Path.Combine(releaseRoot, "provenance.json");
            await WriteProvenanceAsync(release, sourceZipPath, licensePath, adapterManifestPath, planMetadataPath, provenancePath, cancellationToken).ConfigureAwait(false);

            var catalogPath = Path.Combine(stagingRoot, "catalog.json");
            await WriteCatalogAsync(release, catalogPath, cancellationToken).ConfigureAwait(false);

            stage = new DownloadedReleaseStage(release.ReleaseTag, release.SourceRepositoryUrl, release.ZipballUrl, stagingRoot, stagingRoot, null);
            validation = BundledCatalogLoader.LoadFromDirectory(stagingRoot);
            if (validation.HasErrors)
            {
                return new DownloadActivationResult(stage, validation, diagnostics);
            }

            var activatedDirectory = ActivateAtomically(stagingRoot, targetDirectory, diagnostics);
            var activatedStage = stage with { ActivatedDirectory = activatedDirectory };
            return new DownloadActivationResult(activatedStage, validation, diagnostics);
        }
        finally
        {
            if (stage?.ActivatedDirectory is null && Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }

            var stagingParent = Path.GetDirectoryName(stagingRoot)!;
            if (Directory.Exists(stagingParent) && !Directory.EnumerateFileSystemEntries(stagingParent).Any())
            {
                Directory.Delete(stagingParent);
            }
        }
    }

    private async Task<byte[]?> DownloadArchiveAsync(
        DiscoveredRemoteRelease release,
        List<ParseDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, release.ZipballUrl);
        if (!httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("GitHubCopilot");
        }

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Warning, "SPCAT603", $"Approved release download is unavailable offline or due to a network failure: {exception.Message}"));
            return null;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT604", $"Approved release download failed with HTTP status {(int)response.StatusCode} ({response.ReasonPhrase})."));
                return null;
            }

            if (response.Content.Headers.ContentLength > MaxDownloadBytes)
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT610", $"Approved release download exceeds the supported size of {MaxDownloadBytes} bytes."));
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var memory = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (memory.Length + read > MaxDownloadBytes)
                {
                    diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT610", $"Approved release download exceeds the supported size of {MaxDownloadBytes} bytes."));
                    return null;
                }

                memory.Write(buffer, 0, read);
            }

            return memory.ToArray();
        }
    }

    private static string ActivateAtomically(string stagingRoot, string targetDirectory, List<ParseDiagnostic> diagnostics)
    {
        var targetFullPath = Path.GetFullPath(targetDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(targetFullPath)!);
        var backupPath = targetFullPath + ".backup-" + Guid.NewGuid().ToString("N");
        var movedOriginal = false;

        try
        {
            if (Directory.Exists(targetFullPath))
            {
                Directory.Move(targetFullPath, backupPath);
                movedOriginal = true;
            }

            Directory.Move(stagingRoot, targetFullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (movedOriginal && !Directory.Exists(targetFullPath))
            {
                Directory.Move(backupPath, targetFullPath);
            }

            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT611", $"Approved release activation failed and the previous selection was preserved: {exception.Message}"));
            return string.Empty;
        }

        try
        {
            if (Directory.Exists(backupPath))
            {
                Directory.Delete(backupPath, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Warning, "SPCAT613", $"The previous release could not be cleaned up: {exception.Message}"));
        }

        return targetFullPath;
    }

    private static void ExtractArchiveSafely(byte[] archiveBytes, string destinationRoot, List<ParseDiagnostic> diagnostics)
    {
        using var archiveStream = new MemoryStream(archiveBytes, writable: false);
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false);
        var root = Path.GetFullPath(destinationRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (archive.Entries.Count > MaxArchiveEntries)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT612", $"Downloaded release archive is too large when extracted (limit {MaxArchiveEntries} entries, {MaxExtractedBytes} bytes)."));
            return;
        }

        long totalBytes = 0;
        foreach (var entry in archive.Entries)
        {
            var destinationPath = Path.GetFullPath(Path.Combine(root, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
            if (!destinationPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT606", $"Downloaded release archive contains an unsafe path '{entry.FullName}'."));
                return;
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            using var source = entry.Open();
            using (var destination = File.Create(destinationPath))
            {
                if (!TryCopyWithinLimit(source, destination, MaxExtractedBytes, ref totalBytes))
                {
                    destination.Dispose();
                    File.Delete(destinationPath);
                    diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT612", $"Downloaded release archive is too large when extracted (limit {MaxArchiveEntries} entries, {MaxExtractedBytes} bytes)."));
                    return;
                }
            }
        }
    }

    /// <summary>Copies source to destination in bounded chunks, enforcing maxBytes against ACTUAL bytes
    /// read rather than trusting any declared/metadata size, so a stream that decompresses to more than
    /// it claimed is still caught. Returns false (copy aborted) if the limit is exceeded.</summary>
    private static bool TryCopyWithinLimit(Stream source, Stream destination, long maxBytes, ref long totalBytesCopied)
    {
        var buffer = new byte[81920];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalBytesCopied += read;
            if (totalBytesCopied > maxBytes)
            {
                return false;
            }

            destination.Write(buffer, 0, read);
        }

        return true;
    }

    private static string? FindLicenseFile(string extractedRoot)
    {
        return Directory.EnumerateFiles(extractedRoot, "LICENSE*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static async Task WriteAdapterManifestAsync(string path, CancellationToken cancellationToken)
    {
        var manifest = new
        {
            schemaVersion = 1,
            actions = new[]
            {
                new { actionId = "plan-brainstorm", skillPath = "skills/brainstorming/SKILL.md", requiresApproval = false },
                new { actionId = "plan-write", skillPath = "skills/writing-plans/SKILL.md", requiresApproval = false },
            }
        };

        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(manifest), cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteProvenanceAsync(
        DiscoveredRemoteRelease release,
        string sourceZipPath,
        string licensePath,
        string adapterManifestPath,
        string planMetadataPath,
        string provenancePath,
        CancellationToken cancellationToken)
    {
        var provenance = new
        {
            schemaVersion = 1,
            releaseTag = release.ReleaseTag,
            resolvedCommit = release.ReleaseTag,
            files = new[]
            {
                new { path = "source.zip", sha256 = ComputeSha256(sourceZipPath) },
                new { path = "LICENSE.txt", sha256 = ComputeSha256(licensePath) },
                new { path = "adapter-manifest.json", sha256 = ComputeSha256(adapterManifestPath) },
                new { path = "plan-metadata.json", sha256 = ComputeSha256(planMetadataPath) },
            }
        };

        await File.WriteAllTextAsync(provenancePath, JsonSerializer.Serialize(provenance), cancellationToken).ConfigureAwait(false);
    }

    private static async Task WritePlanMetadataAsync(
        DiscoveredRemoteRelease release,
        string path,
        CancellationToken cancellationToken)
    {
        var planMetadata = new
        {
            schemaVersion = PlanEntryPointMetadata.CurrentSchemaVersion,
            entryPoint = "Plan",
            sourceRepository = release.SourceRepositoryUrl,
            releaseTag = release.ReleaseTag,
            composition = new[]
            {
                new { order = 1, skillPath = "skills/brainstorming/SKILL.md", purpose = "Brainstorm the requested work and collect context." },
                new { order = 2, skillPath = "skills/writing-plans/SKILL.md", purpose = "Write the resulting execution plan." },
            },
            notes = new[]
            {
                "Generated during approved remote release staging."
            }
        };

        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(planMetadata), cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteCatalogAsync(
        DiscoveredRemoteRelease release,
        string catalogPath,
        CancellationToken cancellationToken)
    {
        var catalog = new
        {
            schemaVersion = 1,
            sourceRepositoryUrl = release.SourceRepositoryUrl,
            cutoffCapturedAtUtc = DateTimeOffset.UtcNow,
            releases = new[]
            {
                new
                {
                    releaseTag = release.ReleaseTag,
                    resolvedCommit = release.ReleaseTag,
                    licensePath = $"releases/{release.ReleaseTag}/LICENSE.txt",
                    archivePath = $"releases/{release.ReleaseTag}/source.zip",
                    adapterManifestPath = $"releases/{release.ReleaseTag}/adapter-manifest.json",
                    planMetadataPath = $"releases/{release.ReleaseTag}/plan-metadata.json",
                    provenancePath = $"releases/{release.ReleaseTag}/provenance.json",
                }
            }
        };

        await File.WriteAllTextAsync(catalogPath, JsonSerializer.Serialize(catalog), cancellationToken).ConfigureAwait(false);
    }

    private static string ComputeSha256(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    }

    private static bool IsApprovedRelease(DiscoveredRemoteRelease release)
    {
        return Uri.TryCreate(release.DetailsUrl, UriKind.Absolute, out var detailsUri)
            && Uri.TryCreate(release.ZipballUrl, UriKind.Absolute, out var zipballUri)
            && detailsUri.Scheme == Uri.UriSchemeHttps
            && zipballUri.Scheme == Uri.UriSchemeHttps
            && string.Equals(release.SourceRepositoryUrl, ApprovedReleaseDiscoveryService.ApprovedRepositoryUrl, StringComparison.OrdinalIgnoreCase)
            && string.Equals(detailsUri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            && detailsUri.AbsolutePath.StartsWith("/obra/superpowers/releases/", StringComparison.OrdinalIgnoreCase)
            && string.Equals(zipballUri.Host, "api.github.com", StringComparison.OrdinalIgnoreCase)
            && zipballUri.AbsolutePath.StartsWith("/repos/obra/superpowers/zipball/", StringComparison.OrdinalIgnoreCase);
    }
}
