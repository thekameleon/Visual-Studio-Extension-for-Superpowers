using System.Text.RegularExpressions;
using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Skills.Catalog;

public sealed record AvailableRelease(string CatalogRoot, LoadedCatalogRelease Release, string Source);

/// <summary>Each downloaded release is its own catalog root under %LOCALAPPDATA%\TheKameleon.Superpowers\downloads\&lt;tag&gt;.</summary>
public static class DownloadedReleases
{
    private static readonly Regex SafeTag = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.CultureInvariant);

    public static string TargetDirectory(ProfilePaths paths, string releaseTag)
    {
        if (string.IsNullOrEmpty(releaseTag) || !SafeTag.IsMatch(releaseTag) || releaseTag.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Release tag '{releaseTag}' is not a safe folder name.", nameof(releaseTag));
        }

        return Path.Combine(paths.DownloadsRoot, releaseTag);
    }

    public static IReadOnlyList<AvailableRelease> Load(ProfilePaths paths)
    {
        if (!Directory.Exists(paths.DownloadsRoot))
        {
            return Array.Empty<AvailableRelease>();
        }

        return Directory.EnumerateDirectories(paths.DownloadsRoot)
            .Where(directory => !Path.GetFileName(directory).StartsWith('.'))
            .SelectMany(directory => BundledCatalogLoader.LoadFromDirectory(directory).Releases
                .Where(release => !release.HasErrors)
                .Select(release => new AvailableRelease(directory, release, "download")))
            .ToArray();
    }
}
