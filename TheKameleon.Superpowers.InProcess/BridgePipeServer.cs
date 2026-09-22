using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using TheKameleon.Superpowers.Bridge.Contracts;

namespace TheKameleon.Superpowers.InProcess;

/// <summary>
/// Named-pipe server hosting <see cref="BridgeHost"/> operations for the out-of-process
/// VSIX client. Built entirely from public .NET APIs (System.IO.Pipes); no VS-private
/// broker or service is used. Scoped to this Visual Studio process only, ACL-restricted
/// to the current Windows user.
/// </summary>
public sealed class BridgePipeServer : IDisposable
{
    private readonly BridgeHost bridgeHost;
    private readonly string pipeName;
    private readonly CancellationTokenSource stopTokenSource = new();
    private Task? listenLoop;

    public BridgePipeServer(BridgeHost bridgeHost)
    {
        this.bridgeHost = bridgeHost ?? throw new ArgumentNullException(nameof(bridgeHost));
        pipeName = BridgePipeNaming.GetPipeName(Process.GetCurrentProcess().Id);
    }

    public void Start()
    {
        if (listenLoop is not null)
        {
            return;
        }

        SuperpowersBridgePackage.LogStatic($"BridgePipeServer.Start: starting listen loop for pipe '{pipeName}'.");
        listenLoop = Task.Run(() => AcceptLoopAsync(stopTokenSource.Token));
    }

    public void Dispose()
    {
        stopTokenSource.Cancel();
        stopTokenSource.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = CreateServerStream();
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                _ = Task.Run(() => HandleConnectionAsync(server, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                server?.Dispose();
                return;
            }
            catch (Exception exception)
            {
                // Server-side failures are isolated per connection attempt; keep listening
                // for the next client rather than tearing down the whole bridge package.
                SuperpowersBridgePackage.LogStatic($"BridgePipeServer.AcceptLoopAsync: FAILED - {exception}");
                server?.Dispose();
            }
        }
    }

    private NamedPipeServerStream CreateServerStream()
    {
        var pipeSecurity = new PipeSecurity();
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            WindowsIdentity.GetCurrent().User!,
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        return new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            pipeSecurity,
            HandleInheritability.None,
            PipeAccessRights.ReadWrite);
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream server, CancellationToken cancellationToken)
    {
        using (server)
        {
            try
            {
                while (server.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    var request = await BridgeFraming.ReadMessageAsync<BridgeRequestEnvelope>(server, cancellationToken).ConfigureAwait(false);
                    if (request is null)
                    {
                        return;
                    }

                    var response = await DispatchAsync(request, cancellationToken).ConfigureAwait(false);
                    await BridgeFraming.WriteMessageAsync(server, response, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                // Failure isolation: never let a single misbehaving client crash the
                // package; the client observes a closed pipe as an "unavailable" failure.
            }
        }
    }

    private async Task<BridgeResponseEnvelope> DispatchAsync(BridgeRequestEnvelope request, CancellationToken cancellationToken)
    {
        if (request.ProtocolVersion != BridgeProtocol.CurrentVersion)
        {
            return Failure(request.RequestId, BridgeFailureKind.VersionMismatch,
                $"Server protocol version {BridgeProtocol.CurrentVersion} does not match request version {request.ProtocolVersion}.");
        }

        try
        {
            switch (request.Operation)
            {
                case BridgeOperation.GetCapabilities:
                    return new BridgeResponseEnvelope
                    {
                        RequestId = request.RequestId,
                        Success = true,
                        Capabilities = bridgeHost.GetCapabilities()
                    };

                case BridgeOperation.GetActiveDocumentText:
                    var documentText = await bridgeHost.GetActiveDocumentTextAsync(cancellationToken).ConfigureAwait(false);
                    return new BridgeResponseEnvelope
                    {
                        RequestId = request.RequestId,
                        Success = true,
                        DocumentText = documentText
                    };

                default:
                    return Failure(request.RequestId, BridgeFailureKind.ProtocolError, $"Unsupported operation '{request.Operation}'.");
            }
        }
        catch (OperationCanceledException)
        {
            return Failure(request.RequestId, BridgeFailureKind.Canceled, "Operation was canceled.");
        }
        catch (Exception exception)
        {
            return Failure(request.RequestId, BridgeFailureKind.InternalError, exception.Message);
        }
    }

    private static BridgeResponseEnvelope Failure(string requestId, BridgeFailureKind kind, string detail)
    {
        return new BridgeResponseEnvelope
        {
            RequestId = requestId,
            Success = false,
            FailureKind = kind,
            FailureDetail = detail
        };
    }
}
