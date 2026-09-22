using TheKameleon.Superpowers.Core.Contracts.Catalog;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record RetryEvaluationResult(
    RetryDisposition Disposition,
    IReadOnlyList<ParseDiagnostic> Diagnostics)
{
    public bool CanRetry => Disposition == RetryDisposition.Allowed;
}
