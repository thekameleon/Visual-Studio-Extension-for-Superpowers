using TheKameleon.Superpowers.Vsix.Bridge;

namespace TheKameleon.Superpowers.Tests;

public sealed class UnsupportedCopilotBridgeTests
{
    [Fact]
    public async Task GetCapabilitiesAsyncReportsAllCapabilitiesUnavailable()
    {
        var bridge = new UnsupportedCopilotBridge();

        var capabilities = await bridge.GetCapabilitiesAsync(CancellationToken.None);

        Assert.False(capabilities.SupportsPromptSubmission);
        Assert.False(capabilities.SupportsResponseRetrieval);
        Assert.False(capabilities.SupportsEditApplication);
        Assert.False(capabilities.SupportsSessionAutomation);
        Assert.NotNull(capabilities.Detail);
    }

    [Fact]
    public async Task SubmitPromptAsyncReportsFailureRatherThanFabricatingSuccess()
    {
        var bridge = new UnsupportedCopilotBridge();

        var result = await bridge.SubmitPromptAsync("do the thing", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Detail);
    }
}
