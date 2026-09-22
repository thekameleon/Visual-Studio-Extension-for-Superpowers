using TheKameleon.Superpowers.Core.Contracts.Catalog;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record WorkflowResumeValidationResult(
    bool CanResume,
    IReadOnlyList<ParseDiagnostic> Diagnostics);
