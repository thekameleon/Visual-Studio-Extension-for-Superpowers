using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TheKameleon.Superpowers.Bridge.Contracts;

namespace TheKameleon.Superpowers.Vsix.Bridge;

public sealed class UnavailableBridgeClient : IBridgeClient
{
    public static UnavailableBridgeClient Instance { get; } = new();

    private static readonly IReadOnlyList<BridgeCapabilityResult> capabilities =
    [
        new BridgeCapabilityResult
        {
            ProtocolVersion = BridgeProtocol.CurrentVersion,
            Capability = BridgeCapability.DocumentText,
            IsAvailable = false,
            Detail = "No supported VSIX-to-in-process bridge transport is implemented yet."
        }
    ];

    private UnavailableBridgeClient()
    {
    }

    public Task<IReadOnlyList<BridgeCapabilityResult>> GetCapabilitiesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(capabilities);
    }

    public Task<DocumentTextInfo?> GetActiveDocumentTextAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<DocumentTextInfo?>(null);
    }

    public Task<SemanticTargetInfo?> GetSemanticTargetAsync(string filePath, string documentText, int position, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<SemanticTargetInfo?>(null);
    }

    public Task<DocumentCompilerDiagnosticsInfo?> GetCompilerDiagnosticsAsync(string filePath, string documentText, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<DocumentCompilerDiagnosticsInfo?>(null);
    }
}