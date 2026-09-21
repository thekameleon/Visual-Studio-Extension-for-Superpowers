using System.Collections.Generic;
using TheKameleon.Superpowers.Bridge.Contracts;

namespace TheKameleon.Superpowers.InProcess;

public static class BridgeCapabilityCatalog
{
    public static IReadOnlyList<BridgeCapabilityResult> GetCapabilities(bool hasRoslynWorkspace)
    {
        return new[]
        {
            Result(
                BridgeCapability.CompilerDiagnostics,
                hasRoslynWorkspace,
                "Roslyn VisualStudioWorkspace compilation diagnostics."),
            Result(
                BridgeCapability.SemanticTarget,
                hasRoslynWorkspace,
                "Roslyn VisualStudioWorkspace semantic model."),
            Available(BridgeCapability.ContextMenuPlacement, "Public VSSDK command-table context menu identifiers.")
        };
    }

    private static BridgeCapabilityResult Available(BridgeCapability capability, string detail)
    {
        return Result(capability, true, detail);
    }

    private static BridgeCapabilityResult Result(
        BridgeCapability capability,
        bool isAvailable,
        string detail)
    {
        return new BridgeCapabilityResult
        {
            ProtocolVersion = BridgeProtocol.CurrentVersion,
            Capability = capability,
            IsAvailable = isAvailable,
            Detail = isAvailable ? detail : "Required host service is unavailable."
        };
    }
}