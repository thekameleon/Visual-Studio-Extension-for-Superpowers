namespace TheKameleon.Superpowers.Core.Contracts.Catalog;

public sealed record ParseDiagnostic(
    ParseDiagnosticSeverity Severity,
    string Code,
    string Message);
