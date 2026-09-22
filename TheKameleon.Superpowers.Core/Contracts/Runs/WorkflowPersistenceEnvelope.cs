using System.Text.Json.Serialization;
using TheKameleon.Superpowers.Core.Contracts.Context;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record WorkflowPersistenceEnvelope
{
    public const int CurrentSchemaVersion = 1;

    [JsonConstructor]
    public WorkflowPersistenceEnvelope(
        string workspaceId,
        AdapterRunRecord run,
        int schemaVersion = CurrentSchemaVersion,
        ContextCaptureSnapshot? contextSnapshot = null,
        string? selectedSkillId = null,
        string? releaseCommit = null,
        string? policyFingerprint = null,
        DateTimeOffset savedAtUtc = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            throw new ArgumentException("Workspace identifier is required.", nameof(workspaceId));
        }

        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Schema version must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(run);

        WorkspaceId = workspaceId;
        Run = run;
        SchemaVersion = schemaVersion;
        ContextSnapshot = contextSnapshot;
        SelectedSkillId = selectedSkillId;
        ReleaseCommit = releaseCommit;
        PolicyFingerprint = policyFingerprint;
        SavedAtUtc = savedAtUtc == default ? DateTimeOffset.UtcNow : savedAtUtc;
    }

    public string WorkspaceId { get; }

    public AdapterRunRecord Run { get; }

    public int SchemaVersion { get; }

    public ContextCaptureSnapshot? ContextSnapshot { get; }

    public string? SelectedSkillId { get; }

    public string? ReleaseCommit { get; }

    public string? PolicyFingerprint { get; }

    public DateTimeOffset SavedAtUtc { get; }
}
