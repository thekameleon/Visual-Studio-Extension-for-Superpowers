using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Context;

public sealed record CapturedTextValue
{
    [JsonConstructor]
    public CapturedTextValue(
        ContextValueState state,
        string? value,
        int originalLength = 0,
        int capturedLength = 0,
        bool containsSensitiveContent = false,
        string? detail = null)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), "Context value state is invalid.");
        }

        if (originalLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(originalLength), "Original length cannot be negative.");
        }

        if (capturedLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capturedLength), "Captured length cannot be negative.");
        }

        State = state;
        Value = value;
        OriginalLength = originalLength;
        CapturedLength = capturedLength;
        ContainsSensitiveContent = containsSensitiveContent;
        Detail = detail;
    }

    public ContextValueState State { get; }

    public string? Value { get; }

    public int OriginalLength { get; }

    public int CapturedLength { get; }

    public bool ContainsSensitiveContent { get; }

    public string? Detail { get; }
}
