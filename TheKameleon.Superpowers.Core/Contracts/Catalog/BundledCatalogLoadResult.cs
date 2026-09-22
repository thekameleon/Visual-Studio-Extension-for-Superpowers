namespace TheKameleon.Superpowers.Core.Contracts.Catalog;

public sealed record BundledCatalogLoadResult(
    string SourceRepositoryUrl,
    DateTimeOffset CutoffCapturedAtUtc,
    IReadOnlyList<LoadedCatalogRelease> Releases,
    IReadOnlyList<ParseDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == ParseDiagnosticSeverity.Error)
        || Releases.Any(release => release.HasErrors);
}
