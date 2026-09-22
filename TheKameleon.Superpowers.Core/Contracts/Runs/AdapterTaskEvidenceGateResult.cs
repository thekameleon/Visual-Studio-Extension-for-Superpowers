using TheKameleon.Superpowers.Core.Contracts.Catalog;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record AdapterTaskEvidenceGateResult(
    bool IsSatisfied,
    IReadOnlyList<ParseDiagnostic> Diagnostics);
