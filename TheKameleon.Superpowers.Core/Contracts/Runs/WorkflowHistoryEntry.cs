using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record WorkflowHistoryEntry
{
    [JsonConstructor]
    public WorkflowHistoryEntry(
        string runId,
        string releaseTag,
        string executionMode,
        AdapterRunState state,
        DateTimeOffset capturedAtUtc,
        string? selectedSkillId = null,
        bool hadSensitiveContent = false,
        string? retainedContent = null)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new ArgumentException("Run identifier is required.", nameof(runId));
        }

        if (string.IsNullOrWhiteSpace(releaseTag))
        {
            throw new ArgumentException("Release tag is required.", nameof(releaseTag));
        }

        if (string.IsNullOrWhiteSpace(executionMode))
        {
            throw new ArgumentException("Execution mode is required.", nameof(executionMode));
        }

        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), "Run state is invalid.");
        }

        RunId = runId;
        ReleaseTag = releaseTag;
        ExecutionMode = executionMode;
        State = state;
        CapturedAtUtc = capturedAtUtc;
        SelectedSkillId = selectedSkillId;
        HadSensitiveContent = hadSensitiveContent || !string.IsNullOrWhiteSpace(retainedContent);
        RetainedContent = retainedContent;
    }

    public string RunId { get; }

    public string ReleaseTag { get; }

    public string ExecutionMode { get; }

    public AdapterRunState State { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public string? SelectedSkillId { get; }

    public bool HadSensitiveContent { get; }

    public string? RetainedContent { get; }
}
