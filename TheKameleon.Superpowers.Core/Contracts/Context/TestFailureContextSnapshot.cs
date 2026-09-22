using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Context;

public sealed record TestFailureContextSnapshot
{
    [JsonConstructor]
    public TestFailureContextSnapshot(
        ContextValueState state,
        ContextProvenance provenance,
        string? status = null,
        int failedCount = 0,
        CapturedTextValue? summary = null)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), "Context value state is invalid.");
        }

        if (failedCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(failedCount), "Failed test count cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(provenance);

        State = state;
        Provenance = provenance;
        Status = status;
        FailedCount = failedCount;
        Summary = summary;
    }

    public ContextValueState State { get; }

    public ContextProvenance Provenance { get; }

    public string? Status { get; }

    public int FailedCount { get; }

    public CapturedTextValue? Summary { get; }
}
