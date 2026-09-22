using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Context;

public sealed record SemanticTargetContextSnapshot
{
    [JsonConstructor]
    public SemanticTargetContextSnapshot(
        ContextValueState state,
        ContextProvenance provenance,
        string? kind = null,
        string? name = null,
        string? displayName = null,
        string? filePath = null,
        int? startLine = null,
        int? startColumn = null)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), "Context value state is invalid.");
        }

        ArgumentNullException.ThrowIfNull(provenance);

        State = state;
        Provenance = provenance;
        Kind = kind;
        Name = name;
        DisplayName = displayName;
        FilePath = filePath;
        StartLine = startLine;
        StartColumn = startColumn;
    }

    public ContextValueState State { get; }

    public ContextProvenance Provenance { get; }

    public string? Kind { get; }

    public string? Name { get; }

    public string? DisplayName { get; }

    public string? FilePath { get; }

    public int? StartLine { get; }

    public int? StartColumn { get; }
}
