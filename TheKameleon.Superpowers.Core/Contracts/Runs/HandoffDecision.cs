using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record HandoffDecision
{
    [JsonConstructor]
    public HandoffDecision(
        string requiredCapabilityId,
        CapabilityAvailability availability,
        HandoffFallbackKind fallbackKind,
        AdapterRunState resultingRunState,
        string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(requiredCapabilityId))
        {
            throw new ArgumentException("Required capability identifier is required.", nameof(requiredCapabilityId));
        }

        if (!Enum.IsDefined(availability))
        {
            throw new ArgumentOutOfRangeException(nameof(availability), "Capability availability is invalid.");
        }

        if (!Enum.IsDefined(fallbackKind))
        {
            throw new ArgumentOutOfRangeException(nameof(fallbackKind), "Fallback kind is invalid.");
        }

        if (!Enum.IsDefined(resultingRunState))
        {
            throw new ArgumentOutOfRangeException(nameof(resultingRunState), "Resulting run state is invalid.");
        }

        RequiredCapabilityId = requiredCapabilityId;
        Availability = availability;
        FallbackKind = fallbackKind;
        ResultingRunState = resultingRunState;
        Reason = reason;
    }

    public string RequiredCapabilityId { get; }

    public CapabilityAvailability Availability { get; }

    public HandoffFallbackKind FallbackKind { get; }

    public AdapterRunState ResultingRunState { get; }

    public string? Reason { get; }
}
