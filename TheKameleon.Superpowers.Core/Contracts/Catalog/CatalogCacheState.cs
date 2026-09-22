namespace TheKameleon.Superpowers.Core.Contracts.Catalog;

public sealed record CatalogCacheState(
    string CacheRootDirectory,
    string? ActiveReleaseTag,
    IReadOnlyList<CachedCatalogRelease> Releases,
    IReadOnlyList<ParseDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == ParseDiagnosticSeverity.Error);
}
