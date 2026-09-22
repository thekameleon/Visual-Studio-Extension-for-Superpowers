using System.Text.Json.Serialization;
using TheKameleon.Superpowers.Core.Contracts.Settings;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record AdapterRunRecord
{
    public const int CurrentSchemaVersion = 1;

    [JsonConstructor]
    public AdapterRunRecord(
        string runId,
        string selectedReleaseTag,
        int adapterSchemaVersion,
        ExecutionMode executionMode,
        AdapterRunState state,
        int schemaVersion = CurrentSchemaVersion,
        string? trustBasis = null,
        IReadOnlyList<AdapterTaskRecord>? tasks = null)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new ArgumentException("Run identifier is required.", nameof(runId));
        }

        if (string.IsNullOrWhiteSpace(selectedReleaseTag))
        {
            throw new ArgumentException("Selected release tag is required.", nameof(selectedReleaseTag));
        }

        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Schema version must be greater than zero.");
        }

        if (adapterSchemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(adapterSchemaVersion), "Adapter schema version must be greater than zero.");
        }

        if (!Enum.IsDefined(executionMode))
        {
            throw new ArgumentOutOfRangeException(nameof(executionMode), "Execution mode is invalid.");
        }

        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), "Run state is invalid.");
        }

        RunId = runId;
        SelectedReleaseTag = selectedReleaseTag;
        AdapterSchemaVersion = adapterSchemaVersion;
        ExecutionMode = executionMode;
        State = state;
        SchemaVersion = schemaVersion;
        TrustBasis = trustBasis;
        Tasks = (tasks ?? Array.Empty<AdapterTaskRecord>()).ToArray();
    }

    public string RunId { get; }

    public string SelectedReleaseTag { get; }

    public int AdapterSchemaVersion { get; }

    public ExecutionMode ExecutionMode { get; }

    public AdapterRunState State { get; }

    public int SchemaVersion { get; }

    public string? TrustBasis { get; }

    public IReadOnlyList<AdapterTaskRecord> Tasks { get; }

    public bool HasEvidenceGateViolations => Tasks.Any(task => task.State == AdapterTaskState.Completed && !task.IsEvidenceGateSatisfied);
}
