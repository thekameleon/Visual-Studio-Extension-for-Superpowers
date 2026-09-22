using TheKameleon.Superpowers.Core.Contracts.Catalog;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public static class AdapterEvidenceGateEvaluator
{
    public static AdapterTaskEvidenceGateResult Evaluate(AdapterTaskRecord task)
    {
        ArgumentNullException.ThrowIfNull(task);

        var diagnostics = new List<ParseDiagnostic>();

        foreach (var requirement in task.RequiredEvidence)
        {
            var matchingEvidence = task.Evidence
                .Where(entry => string.Equals(entry.EvidenceId, requirement.EvidenceId, StringComparison.Ordinal))
                .OrderByDescending(entry => entry.RecordedAtUtc ?? DateTimeOffset.MinValue)
                .FirstOrDefault();

            if (matchingEvidence is null)
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN001", $"Required evidence '{requirement.EvidenceId}' is missing."));
                continue;
            }

            switch (matchingEvidence.State)
            {
                case AdapterEvidenceState.Observed:
                    break;
                case AdapterEvidenceState.ImportedManual when requirement.AllowImportedEvidence:
                    break;
                case AdapterEvidenceState.ImportedManual:
                    diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN002", $"Required evidence '{requirement.EvidenceId}' must be observed directly."));
                    break;
                case AdapterEvidenceState.Missing:
                    diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN003", $"Required evidence '{requirement.EvidenceId}' is marked missing."));
                    break;
                case AdapterEvidenceState.Stale:
                    diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN004", $"Required evidence '{requirement.EvidenceId}' is stale."));
                    break;
                case AdapterEvidenceState.Blocked:
                    diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN005", $"Required evidence '{requirement.EvidenceId}' is blocked."));
                    break;
                default:
                    diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN006", $"Required evidence '{requirement.EvidenceId}' has an unknown state."));
                    break;
            }
        }

        return new AdapterTaskEvidenceGateResult(diagnostics.Count == 0, diagnostics);
    }
}
