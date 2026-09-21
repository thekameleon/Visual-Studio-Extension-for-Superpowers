namespace TheKameleon.Superpowers.Bridge.Contracts;

public sealed class BridgeCapabilityResult
{
    public int ProtocolVersion { get; set; }

    public BridgeCapability Capability { get; set; }

    public bool IsAvailable { get; set; }

    public string Detail { get; set; } = string.Empty;
}
