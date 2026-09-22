using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using TheKameleon.Superpowers.Bridge.Contracts;
using TheKameleon.Superpowers.Vsix.Bridge;

namespace TheKameleon.Superpowers.Tests;

/// <summary>
/// End-to-end tests for the named-pipe bridge transport: a real <see cref="NamedPipeServerStream"/>
/// dispatch loop (mirroring <c>BridgePipeServer</c>'s protocol handling) exercised through the
/// production <see cref="PipeBridgeClient"/>. No VS-private APIs are involved on either side.
/// </summary>
public sealed class BridgePipeTransportIntegrationTests
{
    [Fact]
    public async Task ClientReceivesCapabilitiesFromRealPipeServer()
    {
        var pipeName = UniquePipeName();
        var expectedCapabilities = new[]
        {
            new BridgeCapabilityResult
            {
                ProtocolVersion = BridgeProtocol.CurrentVersion,
                Capability = BridgeCapability.DocumentText,
                IsAvailable = true,
                Detail = "ok"
            }
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var serverTask = RunServerOnceAsync(pipeName, request => new BridgeResponseEnvelope
        {
            RequestId = request.RequestId,
            Success = true,
            Capabilities = expectedCapabilities
        }, cts.Token);

        var client = new PipeBridgeClient();
        var result = await client.GetCapabilitiesAsync(cts.Token);

        await serverTask;

        var capability = Assert.Single(result);
        Assert.Equal(BridgeCapability.DocumentText, capability.Capability);
        Assert.True(capability.IsAvailable);
    }

    [Fact]
    public async Task ClientReceivesActiveDocumentTextFromRealPipeServer()
    {
        var pipeName = UniquePipeName();
        var expectedDocument = new DocumentTextInfo
        {
            FilePath = "C:\\repo\\File.cs",
            DisplayName = "File.cs",
            Text = "class C {}",
            OriginalLength = 10,
            CapturedLength = 10,
            IsOpen = true
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var serverTask = RunServerOnceAsync(pipeName, request => new BridgeResponseEnvelope
        {
            RequestId = request.RequestId,
            Success = true,
            DocumentText = expectedDocument
        }, cts.Token);

        var client = new PipeBridgeClient();
        var result = await client.GetActiveDocumentTextAsync(cts.Token);

        await serverTask;

        Assert.NotNull(result);
        Assert.Equal(expectedDocument.FilePath, result!.FilePath);
        Assert.Equal(expectedDocument.Text, result.Text);
    }

    [Fact]
    public async Task ClientFallsBackToUnavailableWhenNoServerIsListening()
    {
        // No server started for this pipe name; the client must not throw and must
        // return the same structured "unavailable" shape as UnavailableBridgeClient.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var client = new PipeBridgeClient();

        var capabilities = await client.GetCapabilitiesAsync(cts.Token);
        var documentText = await client.GetActiveDocumentTextAsync(cts.Token);

        var capability = Assert.Single(capabilities, item => item.Capability == BridgeCapability.DocumentText);
        Assert.False(capability.IsAvailable);
        Assert.Null(documentText);
    }

    private static string UniquePipeName()
    {
        return $"{BridgePipeNaming.PipeNamePrefix}.{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Minimal single-connection server loop mirroring BridgePipeServer's dispatch semantics
    /// (length-prefixed JSON framing, protocol-version check) without depending on
    /// TheKameleon.Superpowers.InProcess (net472-only, VS-hosted).
    /// </summary>
    private static async Task RunServerOnceAsync(
        string pipeName,
        Func<BridgeRequestEnvelope, BridgeResponseEnvelope> respond,
        CancellationToken cancellationToken)
    {
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

        var request = await BridgeFraming.ReadMessageAsync<BridgeRequestEnvelope>(server, cancellationToken).ConfigureAwait(false);
        Assert.NotNull(request);

        var response = request!.ProtocolVersion == BridgeProtocol.CurrentVersion
            ? respond(request)
            : new BridgeResponseEnvelope
            {
                RequestId = request.RequestId,
                Success = false,
                FailureKind = BridgeFailureKind.VersionMismatch,
                FailureDetail = "Protocol version mismatch."
            };

        await BridgeFraming.WriteMessageAsync(server, response, cancellationToken).ConfigureAwait(false);
        server.WaitForPipeDrain();
    }
}
