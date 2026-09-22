using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Context;

public sealed record ContextProvenance
{
    [JsonConstructor]
    public ContextProvenance(string source, DateTimeOffset capturedAtUtc, bool isBridgeData = false, string? detail = null)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Context provenance source is required.", nameof(source));
        }

        Source = source;
        CapturedAtUtc = capturedAtUtc;
        IsBridgeData = isBridgeData;
        Detail = detail;
    }

    public string Source { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public bool IsBridgeData { get; }

    public string? Detail { get; }
}
