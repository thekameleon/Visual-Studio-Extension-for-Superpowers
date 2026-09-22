using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TheKameleon.Superpowers.Bridge.Contracts;

namespace TheKameleon.Superpowers.Vsix.Bridge;

/// <summary>
/// Named-pipe implementation of <see cref="IBridgeClient"/>. Connects to the
/// <c>TheKameleon.Superpowers.InProcess</c> package's <c>BridgePipeServer</c> using only
/// public .NET APIs (System.IO.Pipes). Falls back to <see cref="UnavailableBridgeClient"/>
/// behavior (structured "unavailable" results, no exceptions) whenever the pipe cannot be
/// found, opened, or reconnected.
/// </summary>
public sealed class PipeBridgeClient : IBridgeClient
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);

    private readonly object connectionLock = new();
    private NamedPipeClientStream? connection;

    public async Task<IReadOnlyList<BridgeCapabilityResult>> GetCapabilitiesAsync(CancellationToken cancellationToken)
    {
        var response = await SendAsync(BridgeOperation.GetCapabilities, cancellationToken).ConfigureAwait(false);
        if (response is { Success: true, Capabilities: not null })
        {
            return response.Capabilities;
        }

        return await UnavailableBridgeClient.Instance.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<DocumentTextInfo?> GetActiveDocumentTextAsync(CancellationToken cancellationToken)
    {
        var response = await SendAsync(BridgeOperation.GetActiveDocumentText, cancellationToken).ConfigureAwait(false);
        return response is { Success: true } ? response.DocumentText : null;
    }

    private async Task<BridgeResponseEnvelope?> SendAsync(BridgeOperation operation, CancellationToken cancellationToken)
    {
        var request = new BridgeRequestEnvelope
        {
            ProtocolVersion = BridgeProtocol.CurrentVersion,
            Operation = operation,
            RequestId = Guid.NewGuid().ToString("N"),
            RequestedAtUtc = DateTimeOffset.UtcNow
        };

        for (var attempt = 0; attempt < 2; attempt++)
        {
            NamedPipeClientStream? stream;
            try
            {
                stream = await GetOrConnectAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // No supported pipe transport is currently reachable; treat as unavailable
                // rather than propagating an unhandled exception to the caller.
                return null;
            }

            if (stream is null)
            {
                return null;
            }

            try
            {
                await BridgeFraming.WriteMessageAsync(stream, request, cancellationToken).ConfigureAwait(false);
                var response = await BridgeFraming.ReadMessageAsync<BridgeResponseEnvelope>(stream, cancellationToken).ConfigureAwait(false);
                if (response is not null)
                {
                    return response;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (IOException)
            {
                // Broken/closed pipe: reset and allow a single bounded reconnect attempt.
                ResetConnection();
                continue;
            }
            catch (Exception)
            {
                ResetConnection();
                return null;
            }

            ResetConnection();
        }

        return null;
    }

    private Task<NamedPipeClientStream?> GetOrConnectAsync(CancellationToken cancellationToken)
    {
        lock (connectionLock)
        {
            if (connection is { IsConnected: true })
            {
                return Task.FromResult<NamedPipeClientStream?>(connection);
            }
        }

        return ConnectAsync(cancellationToken);
    }

    private async Task<NamedPipeClientStream?> ConnectAsync(CancellationToken cancellationToken)
    {
        foreach (var pipeName in DiscoverCandidatePipeNames())
        {
            var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(ConnectTimeout);
                await client.ConnectAsync(Timeout.Infinite, timeout.Token).ConfigureAwait(false);

                lock (connectionLock)
                {
                    connection = client;
                }

                return client;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                client.Dispose();
            }
            catch (Exception)
            {
                client.Dispose();
            }
        }

        return null;
    }

    /// <summary>
    /// Diagnostic helper: lists the named-pipe candidates currently visible to this process
    /// that match the bridge's naming convention, without attempting to connect to them.
    /// Used by probe commands to distinguish "no server is listening" from "a server is
    /// listening but the handshake failed".
    /// </summary>
    public static IReadOnlyList<string> DiscoverCandidatePipeNamesForDiagnostics() => DiscoverCandidatePipeNames().ToList();

    private static IEnumerable<string> DiscoverCandidatePipeNames()
    {
        // Public, supported enumeration of named-pipe files; no VS-private API involved.
        // Multiple Visual Studio instances may each host their own pipe; every candidate
        // matching the shared prefix is attempted in turn.
        const string pipeRoot = @"\\.\pipe\";
        var prefix = BridgePipeNaming.PipeNamePrefix + ".";
        IEnumerable<string> names;
        try
        {
            names = Directory.EnumerateFiles(pipeRoot)
                .Select(Path.GetFileName)
                .Where(name => name is not null && name.StartsWith(prefix, StringComparison.Ordinal))!;
        }
        catch (Exception)
        {
            names = Array.Empty<string>();
        }

        return names!;
    }

    private void ResetConnection()
    {
        lock (connectionLock)
        {
            connection?.Dispose();
            connection = null;
        }
    }
}
