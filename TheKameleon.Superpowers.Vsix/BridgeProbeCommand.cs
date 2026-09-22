using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using TheKameleon.Superpowers.Bridge.Contracts;
using TheKameleon.Superpowers.Vsix.Bridge;

namespace TheKameleon.Superpowers.Vsix
{
    /// <summary>
    /// Exercises the named-pipe bridge transport end to end from inside a real hosted
    /// Visual Studio process: connects <see cref="PipeBridgeClient"/> to the in-process
    /// <c>BridgePipeServer</c> (if the bridge package has loaded) and reports whether
    /// capabilities/active-document-text came back from a real bridge response or the
    /// structured "unavailable" fallback shape. This is the runtime proof required by the
    /// named-pipe transport checklist in docs/superpowers/specs/p01-probe-design.md.
    /// </summary>
    [VisualStudioContribution]
    public sealed class BridgeProbeCommand : Command
    {
        public override CommandConfiguration CommandConfiguration => new("%Superpowers.BridgeProbeCommand.DisplayName%");

        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            var results = new List<string>();
            var failures = new List<string>();
            var client = new PipeBridgeClient();

            var candidatePipeNames = PipeBridgeClient.DiscoverCandidatePipeNamesForDiagnostics();
            results.Add(candidatePipeNames.Count == 0
                ? "Candidate bridge pipes visible to this process: none (no in-process bridge package appears to be listening)"
                : $"Candidate bridge pipes visible to this process: {string.Join(", ", candidatePipeNames)}");

            await CaptureAsync("Capabilities", async () =>
            {
                var capabilities = await client.GetCapabilitiesAsync(cancellationToken);
                results.Add($"Capabilities returned: {capabilities.Count}");
                foreach (var capability in capabilities)
                {
                    results.Add($"  {capability.Capability}: available={capability.IsAvailable}, detail=\"{capability.Detail}\"");
                }

                var documentTextCapability = FindDocumentTextCapability(capabilities);
                results.Add(documentTextCapability is null
                    ? "DocumentText capability: not reported"
                    : $"DocumentText capability reported by: {(documentTextCapability.Detail.Contains("bridge transport is implemented", StringComparison.OrdinalIgnoreCase) ? "UnavailableBridgeClient fallback (no live server reached)" : "live bridge response")}");
            }, failures);

            await CaptureAsync("Active document text", async () =>
            {
                var documentText = await client.GetActiveDocumentTextAsync(cancellationToken);
                results.Add(documentText is null
                    ? "Active document text: not available (no live server reached, or no active document)"
                    : $"Active document text: file=\"{documentText.FilePath}\", length={documentText.CapturedLength}, isPartial={documentText.IsPartial}");
            }, failures);

            var status = failures.Count == 0
                ? "All bridge transport calls completed."
                : $"Completed with {failures.Count} unavailable or failed call(s).";
            if (failures.Count > 0)
            {
                results.AddRange(failures);
            }

            SuperpowersToolWindow.ProbeResults.Update(
                "Bridge transport probe",
                status,
                string.Join(Environment.NewLine, results),
                "A live bridge response (not the UnavailableBridgeClient fallback) proves the named-pipe transport is reachable from this hosted VS process. Repeat on both supported IDE versions, then after closing/restarting the experimental instance to check reconnect behavior.");

            await this.Extensibility.Shell().ShowToolWindowAsync<SuperpowersToolWindow>(activate: true, cancellationToken);
        }

        private static BridgeCapabilityResult? FindDocumentTextCapability(IReadOnlyList<BridgeCapabilityResult> capabilities)
        {
            foreach (var capability in capabilities)
            {
                if (capability.Capability == BridgeCapability.DocumentText)
                {
                    return capability;
                }
            }

            return null;
        }

        private static async Task CaptureAsync(string operation, Func<Task> capture, ICollection<string> failures)
        {
            try
            {
                await capture();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                failures.Add($"{operation}: failed ({exception.GetType().Name}: {exception.Message})");
            }
        }
    }
}
