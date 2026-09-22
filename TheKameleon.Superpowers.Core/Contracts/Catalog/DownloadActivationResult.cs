namespace TheKameleon.Superpowers.Core.Contracts.Catalog;

public sealed record DownloadActivationResult(
    DownloadedReleaseStage? Stage,
    BundledCatalogLoadResult? Validation,
    IReadOnlyList<ParseDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == ParseDiagnosticSeverity.Error)
        || (Validation?.HasErrors ?? false);
}
