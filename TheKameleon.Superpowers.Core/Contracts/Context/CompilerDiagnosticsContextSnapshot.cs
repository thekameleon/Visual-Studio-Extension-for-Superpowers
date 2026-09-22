using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Context;

public sealed record CompilerDiagnosticsContextSnapshot
{
    [JsonConstructor]
    public CompilerDiagnosticsContextSnapshot(
        ContextValueState state,
        ContextProvenance provenance,
        int totalCount = 0,
        IReadOnlyList<CompilerDiagnosticContextItem>? diagnostics = null)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), "Context value state is invalid.");
        }

        if (totalCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount), "Diagnostic count cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(provenance);

        State = state;
        Provenance = provenance;
        TotalCount = totalCount;
        Diagnostics = (diagnostics ?? Array.Empty<CompilerDiagnosticContextItem>()).ToArray();
    }

    public ContextValueState State { get; }

    public ContextProvenance Provenance { get; }

    public int TotalCount { get; }

    public IReadOnlyList<CompilerDiagnosticContextItem> Diagnostics { get; }
}
