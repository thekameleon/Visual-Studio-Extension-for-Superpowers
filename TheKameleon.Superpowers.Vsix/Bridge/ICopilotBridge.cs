using System.Threading;
using System.Threading.Tasks;

namespace TheKameleon.Superpowers.Vsix.Bridge;

/// <summary>
/// VSIX-layer abstraction for a supported Copilot handoff surface. Implementations must
/// capability-check at runtime and never assume that prompt submission implies response
/// retrieval, edit application, or session automation; each capability is reported and invoked
/// independently.
/// </summary>
public interface ICopilotBridge
{
    /// <summary>
    /// Reports which Copilot handoff capabilities are currently available through a supported
    /// host API.
    /// </summary>
    Task<CopilotBridgeCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to submit a prompt into GitHub Copilot Chat through a supported host API. Only
    /// call this when <see cref="CopilotBridgeCapabilities.SupportsPromptSubmission"/> is true.
    /// </summary>
    Task<CopilotPromptSubmissionResult> SubmitPromptAsync(string prompt, CancellationToken cancellationToken);
}

/// <summary>
/// Independently reported Copilot handoff capabilities. Each capability must be proven through a
/// supported API before being reported as available; none may be inferred from another.
/// </summary>
public sealed record CopilotBridgeCapabilities(
    bool SupportsPromptSubmission,
    bool SupportsResponseRetrieval,
    bool SupportsEditApplication,
    bool SupportsSessionAutomation,
    string? Detail = null);

/// <summary>
/// Result of attempting to submit a prompt into Copilot Chat through a supported host API.
/// </summary>
public sealed record CopilotPromptSubmissionResult(bool Succeeded, string? Detail = null);
