namespace TheKameleon.Superpowers.Core.Contracts.Catalog;

public sealed record CatalogRollbackResult(
    CatalogCacheState CacheState,
    string? ActivatedReleaseTag,
    IReadOnlyList<ParseDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == ParseDiagnosticSeverity.Error)
        || CacheState.HasErrors;
}
