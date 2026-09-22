using TheKameleon.Superpowers.Bridge.Contracts;
using TheKameleon.Superpowers.Vsix.Bridge;

namespace TheKameleon.Superpowers.Tests;

public sealed class UnavailableBridgeClientTests
{
    [Fact]
    public async Task GetCapabilitiesReportsExplicitDocumentTextTransportGap()
    {
        var result = await UnavailableBridgeClient.Instance.GetCapabilitiesAsync(CancellationToken.None);

        var capability = Assert.Single(result, item => item.Capability == BridgeCapability.DocumentText);
        Assert.False(capability.IsAvailable);
        Assert.Contains("transport", capability.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetActiveDocumentTextReturnsNull()
    {
        var result = await UnavailableBridgeClient.Instance.GetActiveDocumentTextAsync(CancellationToken.None);

        Assert.Null(result);
    }
}