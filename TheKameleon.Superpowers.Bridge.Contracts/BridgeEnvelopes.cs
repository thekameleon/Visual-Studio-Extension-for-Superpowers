using System;
using System.Collections.Generic;

namespace TheKameleon.Superpowers.Bridge.Contracts;

public enum BridgeFailureKind
{
    None = 0,
    Unavailable = 1,
    VersionMismatch = 2,
    Canceled = 3,
    ProtocolError = 4,
    InternalError = 5
}

public sealed class BridgeRequestEnvelope
{
    public int ProtocolVersion { get; set; } = BridgeProtocol.CurrentVersion;

    public BridgeOperation Operation { get; set; }

    public string RequestId { get; set; } = string.Empty;

    public string? WorkspaceId { get; set; }

    public DateTimeOffset RequestedAtUtc { get; set; }
}

public sealed class BridgeResponseEnvelope
{
    public int ProtocolVersion { get; set; } = BridgeProtocol.CurrentVersion;

    public string RequestId { get; set; } = string.Empty;

    public bool Success { get; set; }

    public BridgeFailureKind FailureKind { get; set; } = BridgeFailureKind.None;

    public string? FailureDetail { get; set; }

    public IReadOnlyList<BridgeCapabilityResult>? Capabilities { get; set; }

    public DocumentTextInfo? DocumentText { get; set; }
}
