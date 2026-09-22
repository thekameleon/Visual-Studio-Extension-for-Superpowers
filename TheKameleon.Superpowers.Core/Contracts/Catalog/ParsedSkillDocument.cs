namespace TheKameleon.Superpowers.Core.Contracts.Catalog;

public sealed record ParsedSkillDocument(
    string Name,
    string Description,
    string Body,
    IReadOnlyList<SkillReference> References,
    IReadOnlyList<ParseDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == ParseDiagnosticSeverity.Error);
}
