using TheKameleon.Superpowers.Core.Contracts.Catalog;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record RunLifecycleResult(
    AdapterRunRecord Run,
    RetryEvaluationResult? RetryEvaluation,
    IReadOnlyList<ParseDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == ParseDiagnosticSeverity.Error)
        || (RetryEvaluation?.Diagnostics.Any(diagnostic => diagnostic.Severity == ParseDiagnosticSeverity.Error) ?? false);
}
