namespace TheKameleon.Superpowers.Core.Contracts.Catalog;

public sealed record DiscoveryResult(
    IReadOnlyList<DiscoveredSkillEntry> Skills,
    IReadOnlyList<ParseDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == ParseDiagnosticSeverity.Error)
        || Skills.Any(skill => skill.Diagnostics.Any(diagnostic => diagnostic.Severity == ParseDiagnosticSeverity.Error));
}
