using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Context;

public sealed record BuildSummaryContextSnapshot
{
    [JsonConstructor]
    public BuildSummaryContextSnapshot(
        ContextValueState state,
        ContextProvenance provenance,
        string? status = null,
        CapturedTextValue? summary = null)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), "Context value state is invalid.");
        }

        ArgumentNullException.ThrowIfNull(provenance);

        State = state;
        Provenance = provenance;
        Status = status;
        Summary = summary;
    }

    public ContextValueState State { get; }

    public ContextProvenance Provenance { get; }

    public string? Status { get; }

    public CapturedTextValue? Summary { get; }
}
