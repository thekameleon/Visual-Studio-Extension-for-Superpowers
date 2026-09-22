using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Context;

public sealed record SelectionContextSnapshot
{
    [JsonConstructor]
    public SelectionContextSnapshot(
        string scope,
        ContextValueState state,
        ContextProvenance provenance,
        string? kind = null,
        string? name = null,
        string? filePath = null,
        int? startLine = null,
        int? startColumn = null)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            throw new ArgumentException("Selection scope is required.", nameof(scope));
        }

        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), "Context value state is invalid.");
        }

        ArgumentNullException.ThrowIfNull(provenance);

        Scope = scope;
        State = state;
        Provenance = provenance;
        Kind = kind;
        Name = name;
        FilePath = filePath;
        StartLine = startLine;
        StartColumn = startColumn;
    }

    public string Scope { get; }

    public ContextValueState State { get; }

    public ContextProvenance Provenance { get; }

    public string? Kind { get; }

    public string? Name { get; }

    public string? FilePath { get; }

    public int? StartLine { get; }

    public int? StartColumn { get; }
}
