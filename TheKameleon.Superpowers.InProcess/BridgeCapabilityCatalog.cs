using System.Collections.Generic;
using TheKameleon.Superpowers.Bridge.Contracts;

namespace TheKameleon.Superpowers.InProcess;

public static class BridgeCapabilityCatalog
{
    public static IReadOnlyList<BridgeCapabilityResult> GetCapabilities(bool hasRoslynWorkspace)
    {
        return new[]
        {
            Available(
                BridgeCapability.DocumentText,
                "Active-document text capture through supported editor snapshot services."),
            Result(
                BridgeCapability.CompilerDiagnostics,
                hasRoslynWorkspace,
                "Roslyn VisualStudioWorkspace compilation diagnostics."),
            Result(
                BridgeCapability.SemanticTarget,
                hasRoslynWorkspace,
                "Roslyn VisualStudioWorkspace semantic model."),
            Available(BridgeCapability.ContextMenuPlacement, "Public VSSDK command-table context menu identifiers."),
            Unavailable(
                BridgeCapability.TestExplorer,
                "Installed Test Window service/result interfaces are not publicly acquirable through a supported extension contract in the current IDE surface.")
        };
    }

    private static BridgeCapabilityResult Available(BridgeCapability capability, string detail)
    {
        return Result(capability, true, detail);
    }

    private static BridgeCapabilityResult Unavailable(BridgeCapability capability, string detail)
    {
        return Result(capability, false, detail);
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
            Detail = detail
        };
    }
}