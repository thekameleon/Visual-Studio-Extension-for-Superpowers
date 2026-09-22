using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record CapabilitySnapshot
{
    public const int CurrentSchemaVersion = 1;

    [JsonConstructor]
    public CapabilitySnapshot(
        string capabilityId,
        CapabilityAvailability availability,
        HandoffFallbackKind fallbackKind,
        int schemaVersion = CurrentSchemaVersion,
        string? detail = null)
    {
        if (string.IsNullOrWhiteSpace(capabilityId))
        {
            throw new ArgumentException("Capability identifier is required.", nameof(capabilityId));
        }

        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Schema version must be greater than zero.");
        }

        if (!Enum.IsDefined(availability))
        {
            throw new ArgumentOutOfRangeException(nameof(availability), "Capability availability is invalid.");
        }

        if (!Enum.IsDefined(fallbackKind))
        {
            throw new ArgumentOutOfRangeException(nameof(fallbackKind), "Fallback kind is invalid.");
        }

        CapabilityId = capabilityId;
        Availability = availability;
        FallbackKind = fallbackKind;
        SchemaVersion = schemaVersion;
        Detail = detail;
    }

    public string CapabilityId { get; }

    public CapabilityAvailability Availability { get; }

    public HandoffFallbackKind FallbackKind { get; }

    public int SchemaVersion { get; }

    public string? Detail { get; }
}
