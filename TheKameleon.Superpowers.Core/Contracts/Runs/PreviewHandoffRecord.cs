using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

/// <summary>
/// Preserves workflow state and prompt payload for a run awaiting external Copilot action, when
/// direct handoff is unsupported, unavailable, or declined. Records that the run is waiting for
/// manual action and, once available, the imported result, without claiming unsupported
/// automation executed.
/// </summary>
public sealed record PreviewHandoffRecord
{
    [JsonConstructor]
    public PreviewHandoffRecord(
        string runId,
        string promptPayload,
        HandoffFallbackKind fallbackKind,
        PreviewHandoffState state,
        DateTimeOffset createdAtUtc,
        string? importedResult = null,
        DateTimeOffset? resultImportedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new ArgumentException("Run identifier is required.", nameof(runId));
        }

        if (string.IsNullOrWhiteSpace(promptPayload))
        {
            throw new ArgumentException("Prompt payload is required.", nameof(promptPayload));
        }

        if (!Enum.IsDefined(fallbackKind))
        {
            throw new ArgumentOutOfRangeException(nameof(fallbackKind), "Fallback kind is invalid.");
        }

        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), "Preview handoff state is invalid.");
        }

        RunId = runId;
        PromptPayload = promptPayload;
        FallbackKind = fallbackKind;
        State = state;
        CreatedAtUtc = createdAtUtc;
        ImportedResult = importedResult;
        ResultImportedAtUtc = resultImportedAtUtc;
    }

    public string RunId { get; }

    public string PromptPayload { get; }

    public HandoffFallbackKind FallbackKind { get; }

    public PreviewHandoffState State { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public string? ImportedResult { get; }

    public DateTimeOffset? ResultImportedAtUtc { get; }
}

public enum PreviewHandoffState
{
    AwaitingExternalAction = 0,
    ResultImported = 1,
    Discarded = 2
}
