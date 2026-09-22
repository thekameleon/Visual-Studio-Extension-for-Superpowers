namespace TheKameleon.Superpowers.Core.Contracts.Catalog;

public sealed record AdapterManifest(
    int SchemaVersion,
    IReadOnlyList<AdapterManifestAction> Actions,
    IReadOnlyList<ParseDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == ParseDiagnosticSeverity.Error);
}
