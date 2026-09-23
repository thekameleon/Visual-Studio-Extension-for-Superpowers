using TheKameleon.Superpowers.Core.Contracts.Catalog;

namespace TheKameleon.Superpowers.Skills.Catalog;

public static class ReleaseSelection
{
    public static LoadedCatalogRelease? DefaultRelease(IEnumerable<LoadedCatalogRelease> releases)
    {
        ArgumentNullException.ThrowIfNull(releases);
        return releases
            .Where(release => !release.HasErrors && !release.IsPrerelease)
            .OrderByDescending(release => release.PublishedAtUtc ?? DateTimeOffset.MinValue)
            .FirstOrDefault();
    }
}
