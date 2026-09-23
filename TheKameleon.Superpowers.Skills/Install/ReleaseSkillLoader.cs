using TheKameleon.Superpowers.Core.Contracts.Catalog;

namespace TheKameleon.Superpowers.Skills.Install;

public static class ReleaseSkillLoader
{
    /// <summary>Loads skill packages for a release the catalog loader has already hash-validated.</summary>
    public static SkillArchiveReadResult Load(string catalogRoot, LoadedCatalogRelease release)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogRoot);
        ArgumentNullException.ThrowIfNull(release);

        if (release.HasErrors)
        {
            return Failure($"Release '{release.ReleaseTag}' failed catalog validation and cannot be installed.");
        }

        if (string.IsNullOrWhiteSpace(release.ArchivePath))
        {
            return Failure($"Release '{release.ReleaseTag}' has no archive path.");
        }

        var root = Path.GetFullPath(catalogRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var archivePath = Path.GetFullPath(Path.Combine(root, release.ArchivePath));
        if (!archivePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return Failure($"Release '{release.ReleaseTag}' archive path points outside the catalog.");
        }

        if (!File.Exists(archivePath))
        {
            return Failure($"Release '{release.ReleaseTag}' archive is missing.");
        }

        using var stream = File.OpenRead(archivePath);
        return SkillArchiveReader.Read(stream);
    }

    private static SkillArchiveReadResult Failure(string problem)
    {
        return new SkillArchiveReadResult(Array.Empty<SkillPackage>(), new[] { problem });
    }
}
