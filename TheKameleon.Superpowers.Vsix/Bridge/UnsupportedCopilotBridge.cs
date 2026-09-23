using System.Threading;
using System.Threading.Tasks;

namespace TheKameleon.Superpowers.Vsix.Bridge;

/// <summary>
/// Default <see cref="ICopilotBridge"/> implementation for the current approved SDK surface.
/// Per docs/superpowers/specs/host-capabilities.md, no supported Extensibility contract exposes
/// Copilot prompt submission, response retrieval, edit application, or session automation today;
/// this implementation truthfully reports every capability as unavailable rather than assuming
/// any automation is possible, so callers fall back to the explicit preview/copy/manual-result
/// handoff path.
/// </summary>
public sealed class UnsupportedCopilotBridge : ICopilotBridge
{
    private const string UnsupportedDetail =
        "No supported Copilot prompt/response/edit/session contract is exposed by the referenced Extensibility SDK in the current IDE surface.";

    public Task<CopilotBridgeCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new CopilotBridgeCapabilities(
            SupportsPromptSubmission: false,
            SupportsResponseRetrieval: false,
            SupportsEditApplication: false,
            SupportsSessionAutomation: false,
            Detail: UnsupportedDetail));
    }

    public Task<CopilotPromptSubmissionResult> SubmitPromptAsync(string prompt, CancellationToken cancellationToken)
    {
        return Task.FromResult(new CopilotPromptSubmissionResult(false, UnsupportedDetail));
    }
}
