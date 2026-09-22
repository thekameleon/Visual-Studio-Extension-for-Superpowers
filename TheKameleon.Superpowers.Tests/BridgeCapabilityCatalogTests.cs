using TheKameleon.Superpowers.Bridge.Contracts;
using TheKameleon.Superpowers.InProcess;

namespace TheKameleon.Superpowers.Tests;

public sealed class BridgeCapabilityCatalogTests
{
    [Fact]
    public void GetCapabilitiesIncludesExplicitUnavailableTestExplorerCapability()
    {
        var result = Assert.Single(BridgeCapabilityCatalog.GetCapabilities(hasRoslynWorkspace: true),
            capability => capability.Capability == BridgeCapability.TestExplorer);

        Assert.Equal(BridgeProtocol.CurrentVersion, result.ProtocolVersion);
        Assert.False(result.IsAvailable);
        Assert.Contains("not publicly acquirable", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnavailableRoslynCapabilitiesPreserveSpecificDetails()
    {
        var compilerDiagnostics = Assert.Single(BridgeCapabilityCatalog.GetCapabilities(hasRoslynWorkspace: false),
            capability => capability.Capability == BridgeCapability.CompilerDiagnostics);

        Assert.False(compilerDiagnostics.IsAvailable);
        Assert.Equal("Roslyn VisualStudioWorkspace compilation diagnostics.", compilerDiagnostics.Detail);
    }

    [Fact]
    public void GetCapabilitiesIncludesActiveDocumentTextCapability()
    {
        var result = Assert.Single(BridgeCapabilityCatalog.GetCapabilities(hasRoslynWorkspace: true),
            capability => capability.Capability == BridgeCapability.DocumentText);

        Assert.True(result.IsAvailable);
        Assert.Contains("Active-document text capture", result.Detail, StringComparison.OrdinalIgnoreCase);
    }
}