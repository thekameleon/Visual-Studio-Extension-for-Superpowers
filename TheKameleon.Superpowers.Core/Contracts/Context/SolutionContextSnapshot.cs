using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Context;

public sealed record SolutionContextSnapshot
{
    [JsonConstructor]
    public SolutionContextSnapshot(ContextValueState state, ContextProvenance provenance, string? name = null, string? path = null)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), "Context value state is invalid.");
        }

        ArgumentNullException.ThrowIfNull(provenance);

        State = state;
        Provenance = provenance;
        Name = name;
        Path = path;
    }

    public ContextValueState State { get; }

    public ContextProvenance Provenance { get; }

    public string? Name { get; }

    public string? Path { get; }
}
