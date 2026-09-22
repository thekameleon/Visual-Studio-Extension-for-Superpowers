using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record AdapterTaskEvidenceRequirement
{
    [JsonConstructor]
    public AdapterTaskEvidenceRequirement(string evidenceId, bool allowImportedEvidence = true)
    {
        if (string.IsNullOrWhiteSpace(evidenceId))
        {
            throw new ArgumentException("Evidence identifier is required.", nameof(evidenceId));
        }

        EvidenceId = evidenceId;
        AllowImportedEvidence = allowImportedEvidence;
    }

    public string EvidenceId { get; }

    public bool AllowImportedEvidence { get; }
}
