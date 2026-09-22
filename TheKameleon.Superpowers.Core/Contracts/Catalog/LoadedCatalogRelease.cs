namespace TheKameleon.Superpowers.Core.Contracts.Catalog;

public sealed record LoadedCatalogRelease(
    string ReleaseTag,
    string ResolvedCommit,
    string LicenseText,
    AdapterManifest AdapterManifest,
    IReadOnlyList<DiscoveredSkillEntry> Skills,
    IReadOnlyList<string> Assets,
    IReadOnlyList<ParseDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == ParseDiagnosticSeverity.Error)
        || AdapterManifest.HasErrors
        || Skills.Any(skill => skill.Diagnostics.Any(diagnostic => diagnostic.Severity == ParseDiagnosticSeverity.Error));
}
