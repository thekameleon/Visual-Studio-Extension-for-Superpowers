using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Core.Contracts.Runs;

namespace TheKameleon.Superpowers.Skills.Workflow;

public static class WorkflowResumeService
{
    public static WorkflowResumeResult Resume(string workspaceDirectory, WorkflowResumeRequest request, string? reason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        ArgumentNullException.ThrowIfNull(request);

        var diagnostics = new List<ParseDiagnostic>();
        var load = WorkflowStore.Load(workspaceDirectory);
        diagnostics.AddRange(load.Diagnostics);

        if (load.Envelope is null)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN409", "Workflow resume was blocked because no persisted workflow state was found."));
            return new WorkflowResumeResult(null, diagnostics);
        }

        if (load.HasErrors)
        {
            return new WorkflowResumeResult(null, diagnostics);
        }

        var validation = WorkflowResumeValidator.Validate(load.Envelope, request);
        diagnostics.AddRange(validation.Diagnostics);
        if (!validation.CanResume)
        {
            return new WorkflowResumeResult(null, diagnostics);
        }

        var resumedEnvelope = NormalizeEnvelope(load.Envelope, reason);
        diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Info, "SPRUN410", $"Workflow '{resumedEnvelope.Run.RunId}' was normalized for safe resume."));

        var save = WorkflowStore.Save(workspaceDirectory, resumedEnvelope);
        diagnostics.AddRange(save.Diagnostics);
        if (save.HasErrors)
        {
            return new WorkflowResumeResult(null, diagnostics);
        }

        return new WorkflowResumeResult(resumedEnvelope, diagnostics);
    }

    private static WorkflowPersistenceEnvelope NormalizeEnvelope(WorkflowPersistenceEnvelope envelope, string? reason)
    {
        var resumeReason = string.IsNullOrWhiteSpace(reason)
            ? "Workflow resumed from persisted state after restart."
            : reason;
        var targetRunState = RequiresPauseForResume(envelope.Run.State)
            ? AdapterRunState.Paused
            : envelope.Run.State;
        var controls = envelope.Run.Controls.Concat(new[]
        {
            new RunControlRecord(RunControlAction.Resume, DateTimeOffset.UtcNow, resumeReason),
        }).ToArray();
        var run = new AdapterRunRecord(
            envelope.Run.RunId,
            envelope.Run.SelectedReleaseTag,
            envelope.Run.AdapterSchemaVersion,
            envelope.Run.ExecutionMode,
            targetRunState,
            envelope.Run.SchemaVersion,
            envelope.Run.TrustBasis,
            envelope.Run.Tasks,
            envelope.Run.Composition,
            envelope.Run.Capabilities,
            controls);

        return new WorkflowPersistenceEnvelope(
            envelope.WorkspaceId,
            run,
            envelope.SchemaVersion,
            envelope.ContextSnapshot,
            envelope.SelectedSkillId,
            envelope.ReleaseCommit,
            envelope.PolicyFingerprint);
    }

    private static bool RequiresPauseForResume(AdapterRunState state)
    {
        return state is AdapterRunState.Created
            or AdapterRunState.AwaitingHandoff
            or AdapterRunState.Running
            or AdapterRunState.WaitingForEvidence;
    }
}
