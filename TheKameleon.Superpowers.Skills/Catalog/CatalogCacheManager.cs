using TheKameleon.Superpowers.Core.Contracts.Catalog;

namespace TheKameleon.Superpowers.Skills.Catalog;

public static class CatalogCacheManager
{
    private const string CurrentPointerFileName = "current.txt";
    private const string ReleasesDirectoryName = "releases";

    public static CatalogCacheState ImportDownloadedRelease(
        DownloadActivationResult activationResult,
        string cacheRootDirectory,
        int retentionCount,
        CatalogReloadState? reloadState)
    {
        ArgumentNullException.ThrowIfNull(activationResult);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheRootDirectory);

        var diagnostics = new List<ParseDiagnostic>();
        if (activationResult.HasErrors || activationResult.Stage is null || string.IsNullOrWhiteSpace(activationResult.Stage.ActivatedDirectory))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT701", "Only a validated, activated download can be imported into the local cache."));
            return new CatalogCacheState(cacheRootDirectory, null, Array.Empty<CachedCatalogRelease>(), diagnostics);
        }

        if (retentionCount <= 0)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT702", "Cache retention count must be greater than zero."));
            return new CatalogCacheState(cacheRootDirectory, null, Array.Empty<CachedCatalogRelease>(), diagnostics);
        }

        Directory.CreateDirectory(cacheRootDirectory);
        var releasesRoot = Path.Combine(cacheRootDirectory, ReleasesDirectoryName);
        Directory.CreateDirectory(releasesRoot);

        var releaseTag = activationResult.Stage.ReleaseTag;
        var cachedReleaseDirectory = Path.Combine(releasesRoot, releaseTag);
        if (Directory.Exists(cachedReleaseDirectory))
        {
            Directory.Delete(cachedReleaseDirectory, recursive: true);
        }

        CopyDirectory(activationResult.Stage.ActivatedDirectory!, cachedReleaseDirectory);
        File.WriteAllText(Path.Combine(cacheRootDirectory, CurrentPointerFileName), releaseTag);

        PruneUnpinnedReleases(releasesRoot, retentionCount, reloadState, diagnostics);
        return GetCacheState(cacheRootDirectory, reloadState, diagnostics);
    }

    public static CatalogRollbackResult RollbackToCachedRelease(
        string cacheRootDirectory,
        string releaseTag,
        CatalogReloadState? reloadState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheRootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(releaseTag);

        var diagnostics = new List<ParseDiagnostic>();
        var releasesRoot = Path.Combine(cacheRootDirectory, ReleasesDirectoryName);
        var targetDirectory = Path.Combine(releasesRoot, releaseTag);
        if (!Directory.Exists(targetDirectory))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT703", $"Cached release '{releaseTag}' does not exist for rollback."));
            return new CatalogRollbackResult(GetCacheState(cacheRootDirectory, reloadState, diagnostics), null, diagnostics);
        }

        File.WriteAllText(Path.Combine(cacheRootDirectory, CurrentPointerFileName), releaseTag);
        var cacheState = GetCacheState(cacheRootDirectory, reloadState, diagnostics);
        return new CatalogRollbackResult(cacheState, releaseTag, diagnostics);
    }

    public static CatalogCacheState GetCacheState(
        string cacheRootDirectory,
        CatalogReloadState? reloadState,
        IReadOnlyList<ParseDiagnostic>? additionalDiagnostics = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheRootDirectory);

        var diagnostics = additionalDiagnostics?.ToList() ?? new List<ParseDiagnostic>();
        var releases = new List<CachedCatalogRelease>();
        var activeReleaseTag = ReadCurrentReleaseTag(cacheRootDirectory);
        var releasesRoot = Path.Combine(cacheRootDirectory, ReleasesDirectoryName);
        if (!Directory.Exists(releasesRoot))
        {
            return new CatalogCacheState(cacheRootDirectory, activeReleaseTag, releases, diagnostics);
        }

        var pinnedPrefixes = new HashSet<string>(
            (reloadState?.ActiveRunPins ?? Array.Empty<ActiveRunPin>())
                .Select(pin => Path.GetFullPath(pin.FullPath)),
            StringComparer.OrdinalIgnoreCase);

        foreach (var directory in Directory.EnumerateDirectories(releasesRoot))
        {
            var info = new DirectoryInfo(directory);
            var isPinned = pinnedPrefixes.Any(prefix => prefix.StartsWith(info.FullName, StringComparison.OrdinalIgnoreCase));
            releases.Add(new CachedCatalogRelease(
                info.Name,
                info.FullName,
                info.CreationTimeUtc,
                string.Equals(info.Name, activeReleaseTag, StringComparison.OrdinalIgnoreCase),
                isPinned));
        }

        releases.Sort((left, right) => right.CachedAtUtc.CompareTo(left.CachedAtUtc));
        return new CatalogCacheState(cacheRootDirectory, activeReleaseTag, releases, diagnostics);
    }

    private static void PruneUnpinnedReleases(
        string releasesRoot,
        int retentionCount,
        CatalogReloadState? reloadState,
        List<ParseDiagnostic> diagnostics)
    {
        var releases = Directory.Exists(releasesRoot)
            ? Directory.EnumerateDirectories(releasesRoot)
                .Select(path => new DirectoryInfo(path))
                .OrderByDescending(info => info.CreationTimeUtc)
                .ToArray()
            : Array.Empty<DirectoryInfo>();

        var protectedPrefixes = new HashSet<string>(
            (reloadState?.ActiveRunPins ?? Array.Empty<ActiveRunPin>())
                .Select(pin => Path.GetFullPath(pin.FullPath)),
            StringComparer.OrdinalIgnoreCase);
        var currentReleaseTag = ReadCurrentReleaseTag(Path.GetDirectoryName(releasesRoot)!);

        var kept = 0;
        foreach (var release in releases)
        {
            var isPinned = protectedPrefixes.Any(prefix => prefix.StartsWith(release.FullName, StringComparison.OrdinalIgnoreCase));
            var isActive = string.Equals(release.Name, currentReleaseTag, StringComparison.OrdinalIgnoreCase);
            if (isPinned || isActive || kept < retentionCount)
            {
                kept++;
                continue;
            }

            try
            {
                Directory.Delete(release.FullName, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Warning, "SPCAT704", $"Cached release '{release.Name}' could not be removed during retention pruning: {exception.Message}"));
            }
        }
    }

    private static string? ReadCurrentReleaseTag(string cacheRootDirectory)
    {
        var currentPointerPath = Path.Combine(cacheRootDirectory, CurrentPointerFileName);
        return File.Exists(currentPointerPath)
            ? File.ReadAllText(currentPointerPath).Trim()
            : null;
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destinationDirectory, Path.GetRelativePath(sourceDirectory, directory)));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var destinationFile = Path.Combine(destinationDirectory, Path.GetRelativePath(sourceDirectory, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(file, destinationFile, overwrite: true);
        }
    }
}
