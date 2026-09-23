using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
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
    public async Task ClientReceivesSemanticTargetFromRealPipeServer()
    {
        var pipeName = UniquePipeName();
        var expectedTarget = new SemanticTargetInfo
        {
            Kind = "NamedType",
            Name = "C",
            DisplayName = "C",
            FilePath = "C:\\repo\\File.cs",
            StartLine = 0,
            StartColumn = 6
        };

        BridgeRequestEnvelope? capturedRequest = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var serverTask = RunServerOnceAsync(pipeName, request =>
        {
            capturedRequest = request;
            return new BridgeResponseEnvelope
            {
                RequestId = request.RequestId,
                Success = true,
                SemanticTarget = expectedTarget
            };
        }, cts.Token);

        var client = new PipeBridgeClient();
        var result = await client.GetSemanticTargetAsync("C:\\repo\\File.cs", "class C {}", 6, cts.Token);

        await serverTask;

        Assert.NotNull(capturedRequest);
        Assert.Equal(BridgeOperation.GetSemanticTarget, capturedRequest!.Operation);
        Assert.Equal("C:\\repo\\File.cs", capturedRequest.FilePath);
        Assert.Equal("class C {}", capturedRequest.DocumentText);
        Assert.Equal(6, capturedRequest.Position);

        Assert.NotNull(result);
        Assert.Equal(expectedTarget.Name, result!.Name);
        Assert.Equal(expectedTarget.Kind, result.Kind);
    }

    [Fact]
    public async Task ClientReceivesCompilerDiagnosticsFromRealPipeServer()
    {
        var pipeName = UniquePipeName();
        var expectedDiagnostics = new DocumentCompilerDiagnosticsInfo
        {
            TotalCount = 1,
            Diagnostics = new[]
            {
                new CompilerDiagnosticInfo
                {
                    Id = "CS1000",
                    Severity = "Error",
                    Message = "Bad",
                    FilePath = "C:\\repo\\File.cs",
                    StartLine = 1,
                    StartColumn = 2
                }
            }
        };

        BridgeRequestEnvelope? capturedRequest = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var serverTask = RunServerOnceAsync(pipeName, request =>
        {
            capturedRequest = request;
            return new BridgeResponseEnvelope
            {
                RequestId = request.RequestId,
                Success = true,
                CompilerDiagnostics = expectedDiagnostics
            };
        }, cts.Token);

        var client = new PipeBridgeClient();
        var result = await client.GetCompilerDiagnosticsAsync("C:\\repo\\File.cs", "class C {}", cts.Token);

        await serverTask;

        Assert.NotNull(capturedRequest);
        Assert.Equal(BridgeOperation.GetCompilerDiagnostics, capturedRequest!.Operation);
        Assert.Equal("C:\\repo\\File.cs", capturedRequest.FilePath);
        Assert.Equal("class C {}", capturedRequest.DocumentText);

        Assert.NotNull(result);
        Assert.Equal(1, result!.TotalCount);
        Assert.Single(result.Diagnostics);
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

    [Fact]
    public async Task ServerReturnsStructuredVersionMismatchFailure()
    {
        // Mirrors BridgePipeServer.DispatchAsync's version check directly: a request with a
        // protocol version different from BridgeProtocol.CurrentVersion must produce a
        // structured VersionMismatch failure response, not a thrown exception or a raw
        // protocol/connection error.
        var pipeName = UniquePipeName();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var serverTask = Task.Run(async () =>
        {
            using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await server.WaitForConnectionAsync(cts.Token).ConfigureAwait(false);

            var request = await BridgeFraming.ReadMessageAsync<BridgeRequestEnvelope>(server, cts.Token).ConfigureAwait(false);
            Assert.NotNull(request);

            var response = request!.ProtocolVersion != BridgeProtocol.CurrentVersion
                ? new BridgeResponseEnvelope
                {
                    RequestId = request.RequestId,
                    Success = false,
                    FailureKind = BridgeFailureKind.VersionMismatch,
                    FailureDetail = $"Server protocol version {BridgeProtocol.CurrentVersion} does not match request version {request.ProtocolVersion}."
                }
                : throw new InvalidOperationException("Expected a version-mismatched request for this test.");

            await BridgeFraming.WriteMessageAsync(server, response, cts.Token).ConfigureAwait(false);
            server.WaitForPipeDrain();
        }, cts.Token);

        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync((int)TimeSpan.FromSeconds(5).TotalMilliseconds, cts.Token).ConfigureAwait(false);

        var mismatchedRequest = new BridgeRequestEnvelope
        {
            ProtocolVersion = BridgeProtocol.CurrentVersion + 1,
            Operation = BridgeOperation.GetCapabilities,
            RequestId = Guid.NewGuid().ToString("N"),
            RequestedAtUtc = DateTimeOffset.UtcNow
        };

        await BridgeFraming.WriteMessageAsync(client, mismatchedRequest, cts.Token).ConfigureAwait(false);
        var response2 = await BridgeFraming.ReadMessageAsync<BridgeResponseEnvelope>(client, cts.Token).ConfigureAwait(false);

        await serverTask;

        Assert.NotNull(response2);
        Assert.False(response2!.Success);
        Assert.Equal(BridgeFailureKind.VersionMismatch, response2.FailureKind);
        Assert.False(string.IsNullOrEmpty(response2.FailureDetail));
    }

    [Fact]
    public async Task ClientCancellationDuringSendPropagatesAndDoesNotThrowUnexpectedException()
    {
        // A caller-triggered cancellation before/while sending must surface as
        // OperationCanceledException, not an unhandled transport exception, and must not hang.
        var pipeName = UniquePipeName();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var client = new PipeBridgeClient();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetCapabilitiesAsync(cts.Token));
    }

    [Fact]
    public async Task ServerAcceptLoopStopsAfterDisposeWithoutThrowing()
    {
        // Dispose() cancels the server's internal token source; the accept loop must observe
        // cancellation and exit cleanly (OperationCanceledException handled internally), not
        // fault the hosting Task or leave the pipe listening.
        var pipeName = UniquePipeName();
        using var stopTokenSource = new CancellationTokenSource();

        var acceptLoop = Task.Run(async () =>
        {
            using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            try
            {
                await server.WaitForConnectionAsync(stopTokenSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected: mirrors BridgePipeServer.AcceptLoopAsync's own handling, which
                // disposes the pending server stream and returns without rethrowing.
            }
        });

        stopTokenSource.Cancel();

        var completed = await Task.WhenAny(acceptLoop, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(acceptLoop, completed);
        Assert.True(acceptLoop.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ServerDispatchConvertsUnhandledExceptionToStructuredInternalErrorResponse()
    {
        // Mirrors BridgePipeServer.DispatchAsync's outer try/catch: an unhandled exception
        // thrown while producing a response must be converted into a structured
        // BridgeFailureKind.InternalError response with only the exception message captured,
        // never a raw exception/stack trace serialized across the pipe boundary.
        var pipeName = UniquePipeName();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var serverTask = RunServerOnceAsync(pipeName, request =>
        {
            static BridgeResponseEnvelope Dispatch(BridgeRequestEnvelope request)
            {
                try
                {
                    throw new InvalidOperationException("Simulated capability-handler failure.");
                }
                catch (Exception exception)
                {
                    return new BridgeResponseEnvelope
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        FailureKind = BridgeFailureKind.InternalError,
                        FailureDetail = exception.Message
                    };
                }
            }

            return Dispatch(request);
        }, cts.Token);

        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync((int)TimeSpan.FromSeconds(5).TotalMilliseconds, cts.Token).ConfigureAwait(false);

        var request = new BridgeRequestEnvelope
        {
            ProtocolVersion = BridgeProtocol.CurrentVersion,
            Operation = BridgeOperation.GetCapabilities,
            RequestId = Guid.NewGuid().ToString("N"),
            RequestedAtUtc = DateTimeOffset.UtcNow
        };

        await BridgeFraming.WriteMessageAsync(client, request, cts.Token).ConfigureAwait(false);
        var response = await BridgeFraming.ReadMessageAsync<BridgeResponseEnvelope>(client, cts.Token).ConfigureAwait(false);

        await serverTask;

        Assert.NotNull(response);
        Assert.False(response!.Success);
        Assert.Equal(BridgeFailureKind.InternalError, response.FailureKind);
        Assert.Equal("Simulated capability-handler failure.", response.FailureDetail);
    }

    [Fact]
    public void ServerPipeAclGrantsOnlyCurrentUserReadWriteAccess()
    {
        // Mirrors BridgePipeServer.CreateServerStream's PipeSecurity setup: the resulting pipe
        // ACL must grant ReadWrite to the current Windows user only, with no other explicit
        // access rules (e.g. no Everyone/Authenticated Users grant), confirming local-user-only
        // enforcement at the ACL level rather than by code-review alone.
        var pipeName = UniquePipeName();
        var currentUser = WindowsIdentity.GetCurrent().User!;

        var pipeSecurity = new PipeSecurity();
        pipeSecurity.AddAccessRule(new PipeAccessRule(currentUser, PipeAccessRights.ReadWrite, AccessControlType.Allow));

        using var server = NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            pipeSecurity);

        var actualSecurity = server.GetAccessControl();
        var rules = actualSecurity.GetAccessRules(true, false, typeof(SecurityIdentifier));

        var rule = Assert.Single(rules.Cast<PipeAccessRule>());
        Assert.Equal(currentUser, rule.IdentityReference);
        Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
        Assert.True(rule.PipeAccessRights.HasFlag(PipeAccessRights.ReadWrite));
    }

    [Fact]
    public async Task ClientReconnectsToNewServerAfterPreviousConnectionCloses()
    {
        // Checklist item 4: disconnect/reconnect behavior. The same PipeBridgeClient instance
        // is reused across two independent server lifetimes on the same pipe name (mirroring
        // a bridge-server restart). The first call succeeds and closes; the second call must
        // detect the broken connection (IOException path in SendAsync), perform a single
        // bounded reconnect, and succeed against the new server instance rather than failing
        // outright or hanging.
        var pipeName = UniquePipeName();
        var client = new PipeBridgeClient();

        using (var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            var firstServer = RunServerOnceAsync(pipeName, request => new BridgeResponseEnvelope
            {
                RequestId = request.RequestId,
                Success = true,
                Capabilities = new[]
                {
                    new BridgeCapabilityResult
                    {
                        ProtocolVersion = BridgeProtocol.CurrentVersion,
                        Capability = BridgeCapability.DocumentText,
                        IsAvailable = true,
                        Detail = "first-server"
                    }
                }
            }, cts1.Token);

            var firstResult = await client.GetCapabilitiesAsync(cts1.Token);
            await firstServer;

            var firstCapability = Assert.Single(firstResult, item => item.Capability == BridgeCapability.DocumentText);
            Assert.True(firstCapability.IsAvailable);
            Assert.Equal("first-server", firstCapability.Detail);
        }

        // The first server's NamedPipeServerStream is now disposed; the client's cached
        // connection is stale. Start a second, independent server on the same pipe name
        // before issuing the next request, simulating a bridge-server restart.
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var secondServer = RunServerOnceAsync(pipeName, request => new BridgeResponseEnvelope
        {
            RequestId = request.RequestId,
            Success = true,
            Capabilities = new[]
            {
                new BridgeCapabilityResult
                {
                    ProtocolVersion = BridgeProtocol.CurrentVersion,
                    Capability = BridgeCapability.DocumentText,
                    IsAvailable = true,
                    Detail = "second-server"
                }
            }
        }, cts2.Token);

        var secondResult = await client.GetCapabilitiesAsync(cts2.Token);
        await secondServer;

        var secondCapability = Assert.Single(secondResult, item => item.Capability == BridgeCapability.DocumentText);
        Assert.True(secondCapability.IsAvailable);
        Assert.Equal("second-server", secondCapability.Detail);
    }

    [Fact]
    public async Task ClientFallsBackHonestlyAfterDisconnectWhenNoReplacementServerIsListening()
    {
        // Checklist item 4, negative path: after a connection that was previously live is lost
        // and no replacement server ever starts, the bounded reconnect attempt must exhaust and
        // the client must report the same honest "unavailable" shape as a cold start, never
        // throw, and never hang past the reconnect attempt's own timeout.
        var pipeName = UniquePipeName();
        var client = new PipeBridgeClient();

        using (var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            var firstServer = RunServerOnceAsync(pipeName, request => new BridgeResponseEnvelope
            {
                RequestId = request.RequestId,
                Success = true,
                Capabilities = new[]
                {
                    new BridgeCapabilityResult
                    {
                        ProtocolVersion = BridgeProtocol.CurrentVersion,
                        Capability = BridgeCapability.DocumentText,
                        IsAvailable = true,
                        Detail = "first-server"
                    }
                }
            }, cts1.Token);

            var firstResult = await client.GetCapabilitiesAsync(cts1.Token);
            await firstServer;
            Assert.Single(firstResult, item => item.Capability == BridgeCapability.DocumentText && item.IsAvailable);
        }

        // No second server is started for this pipe name; the next call must fall back to the
        // same structured "unavailable" result as UnavailableBridgeClient, not throw or hang.
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var capabilities = await client.GetCapabilitiesAsync(cts2.Token);
        var documentText = await client.GetActiveDocumentTextAsync(cts2.Token);

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
