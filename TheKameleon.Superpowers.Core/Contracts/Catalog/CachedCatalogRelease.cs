namespace TheKameleon.Superpowers.Core.Contracts.Catalog;

public sealed record CachedCatalogRelease(
    string ReleaseTag,
    string CacheDirectory,
    DateTimeOffset CachedAtUtc,
    bool IsActive,
    bool IsPinnedByActiveRun);
