using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record AdapterEvidenceRecord
{
    [JsonConstructor]
    public AdapterEvidenceRecord(
        string evidenceId,
        string evidenceKind,
        AdapterEvidenceState state,
        DateTimeOffset? recordedAtUtc = null,
        string? artifactId = null,
        string? detail = null)
    {
        if (string.IsNullOrWhiteSpace(evidenceId))
        {
            throw new ArgumentException("Evidence identifier is required.", nameof(evidenceId));
        }

        if (string.IsNullOrWhiteSpace(evidenceKind))
        {
            throw new ArgumentException("Evidence kind is required.", nameof(evidenceKind));
        }

        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), "Evidence state is invalid.");
        }

        EvidenceId = evidenceId;
        EvidenceKind = evidenceKind;
        State = state;
        RecordedAtUtc = recordedAtUtc;
        ArtifactId = artifactId;
        Detail = detail;
    }

    public string EvidenceId { get; }

    public string EvidenceKind { get; }

    public AdapterEvidenceState State { get; }

    public DateTimeOffset? RecordedAtUtc { get; }

    public string? ArtifactId { get; }

    public string? Detail { get; }
}
