namespace TheKameleon.Superpowers.Core.Contracts.Catalog;

public sealed record CatalogReloadResult(
    DiscoveryResult Discovery,
    CatalogReloadState State,
    IReadOnlyList<ParseDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == ParseDiagnosticSeverity.Error)
        || Discovery.HasErrors;
}
