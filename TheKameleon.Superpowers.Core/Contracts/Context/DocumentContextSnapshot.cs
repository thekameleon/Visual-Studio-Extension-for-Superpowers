using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Context;

public sealed record DocumentContextSnapshot
{
    [JsonConstructor]
    public DocumentContextSnapshot(
        ContextValueState state,
        ContextProvenance provenance,
        string? filePath = null,
        string? displayName = null,
        bool isOpen = false,
        bool isDirty = false,
        CapturedTextValue? content = null)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), "Context value state is invalid.");
        }

        ArgumentNullException.ThrowIfNull(provenance);

        State = state;
        Provenance = provenance;
        FilePath = filePath;
        DisplayName = displayName;
        IsOpen = isOpen;
        IsDirty = isDirty;
        Content = content;
    }

    public ContextValueState State { get; }

    public ContextProvenance Provenance { get; }

    public string? FilePath { get; }

    public string? DisplayName { get; }

    public bool IsOpen { get; }

    public bool IsDirty { get; }

    public CapturedTextValue? Content { get; }
}
