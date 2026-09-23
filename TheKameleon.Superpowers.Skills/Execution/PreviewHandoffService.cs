using TheKameleon.Superpowers.Core.Contracts.Runs;

namespace TheKameleon.Superpowers.Skills.Execution;

/// <summary>
/// Pure state-transition service for the explicit preview/copy/manual-result handoff path used
/// when direct Copilot bridge automation is unsupported, unavailable, or declined. Preserves the
/// same workflow state and prompt payload in an editable preview/copy record, and allows manual
/// result import without claiming unsupported automation executed.
/// </summary>
public static class PreviewHandoffService
{
    public static PreviewHandoffRecord Create(string runId, string promptPayload, HandoffFallbackKind fallbackKind, DateTimeOffset createdAtUtc)
    {
        return new PreviewHandoffRecord(runId, promptPayload, fallbackKind, PreviewHandoffState.AwaitingExternalAction, createdAtUtc);
    }

    /// <summary>
    /// Records a manually imported result (e.g. pasted back from Copilot Chat) against a run that
    /// is awaiting external action. Fails if the record is not currently awaiting action.
    /// </summary>
    public static PreviewHandoffTransitionResult ImportResult(PreviewHandoffRecord record, string importedResult, DateTimeOffset importedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(importedResult))
        {
            return PreviewHandoffTransitionResult.Failure(record, "Imported result text is required.");
        }

        if (record.State != PreviewHandoffState.AwaitingExternalAction)
        {
            return PreviewHandoffTransitionResult.Failure(record, $"Preview handoff for run '{record.RunId}' cannot import a result from state '{record.State}'.");
        }

        return PreviewHandoffTransitionResult.Success(new PreviewHandoffRecord(
            record.RunId,
            record.PromptPayload,
            record.FallbackKind,
            PreviewHandoffState.ResultImported,
            record.CreatedAtUtc,
            importedResult,
            importedAtUtc));
    }

    /// <summary>
    /// Discards a preview handoff, for example when the user cancels the run instead of acting on
    /// it externally. Fails if the record is not currently awaiting action.
    /// </summary>
    public static PreviewHandoffTransitionResult Discard(PreviewHandoffRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.State != PreviewHandoffState.AwaitingExternalAction)
        {
            return PreviewHandoffTransitionResult.Failure(record, $"Preview handoff for run '{record.RunId}' cannot be discarded from state '{record.State}'.");
        }

        return PreviewHandoffTransitionResult.Success(new PreviewHandoffRecord(
            record.RunId,
            record.PromptPayload,
            record.FallbackKind,
            PreviewHandoffState.Discarded,
            record.CreatedAtUtc));
    }
}

public sealed class PreviewHandoffTransitionResult
{
    private PreviewHandoffTransitionResult(bool succeeded, PreviewHandoffRecord record, string? failureReason)
    {
        Succeeded = succeeded;
        Record = record;
        FailureReason = failureReason;
    }

    public bool Succeeded { get; }

    public PreviewHandoffRecord Record { get; }

    public string? FailureReason { get; }

    public static PreviewHandoffTransitionResult Success(PreviewHandoffRecord record) => new(true, record, null);

    public static PreviewHandoffTransitionResult Failure(PreviewHandoffRecord record, string reason) => new(false, record, reason);
}
