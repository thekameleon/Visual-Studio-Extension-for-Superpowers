using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Core.Contracts.Runs;

namespace TheKameleon.Superpowers.Skills.Workflow;

public static class WorkflowResumeValidator
{
    public static WorkflowResumeValidationResult Validate(WorkflowPersistenceEnvelope envelope, WorkflowResumeRequest request)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(request);

        var diagnostics = new List<ParseDiagnostic>();

        if (!string.Equals(envelope.WorkspaceId, request.WorkspaceId, StringComparison.Ordinal))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN401", "Workflow resume was blocked because the workspace identifier changed."));
        }

        if (!string.Equals(envelope.Run.SelectedReleaseTag, request.ReleaseTag, StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN402", "Workflow resume was blocked because the selected release tag changed."));
        }

        if (!string.IsNullOrWhiteSpace(request.ReleaseCommit)
            && !string.IsNullOrWhiteSpace(envelope.ReleaseCommit)
            && !string.Equals(envelope.ReleaseCommit, request.ReleaseCommit, StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN403", "Workflow resume was blocked because the selected release commit changed."));
        }

        if (!string.IsNullOrWhiteSpace(request.SelectedSkillId)
            && !string.Equals(envelope.SelectedSkillId, request.SelectedSkillId, StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN404", "Workflow resume was blocked because the selected skill changed."));
        }

        if (!string.IsNullOrWhiteSpace(request.PolicyFingerprint)
            && !string.Equals(envelope.PolicyFingerprint, request.PolicyFingerprint, StringComparison.Ordinal))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN405", "Workflow resume was blocked because the policy fingerprint changed."));
        }

        if (envelope.ContextSnapshot is not null)
        {
            var savedActiveDocumentPath = envelope.ContextSnapshot.ActiveDocument?.FilePath;
            if (!string.IsNullOrWhiteSpace(savedActiveDocumentPath))
            {
                if (string.IsNullOrWhiteSpace(request.ActiveDocumentPath))
                {
                    diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN407", "Workflow resume was blocked because the current active document could not be validated against the saved run snapshot."));
                }
                else if (!string.Equals(savedActiveDocumentPath, request.ActiveDocumentPath, StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN407", "Workflow resume was blocked because the active document changed since the run was saved."));
                }
            }

            if (!request.CurrentSnapshotCapturedAtUtc.HasValue)
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN408", "Workflow resume was blocked because the current snapshot timestamp was not provided for validation."));
            }
            else if (request.CurrentSnapshotCapturedAtUtc.Value < envelope.ContextSnapshot.Provenance.CapturedAtUtc)
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN408", "Workflow resume was blocked because the current snapshot is older than the saved run snapshot."));
            }
        }

        if (envelope.Run.State is AdapterRunState.Completed or AdapterRunState.Canceled)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN406", $"Workflow resume was blocked because the saved run is already in terminal state '{envelope.Run.State}'."));
        }

        return new WorkflowResumeValidationResult(diagnostics.Count == 0, diagnostics);
    }
}
