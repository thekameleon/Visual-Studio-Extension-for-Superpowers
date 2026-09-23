using TheKameleon.Superpowers.Core.Contracts.Runs;

namespace TheKameleon.Superpowers.Skills.Execution;

/// <summary>
/// Pure coordinator that decides how to route a Copilot handoff request given independently
/// reported bridge capabilities. Never assumes prompt submission implies response retrieval, edit
/// application, or session automation; each capability is consulted separately, and a truthful
/// partial-capability decision is produced when only some capabilities are available.
/// </summary>
public static class CopilotHandoffCoordinator
{
    public static HandoffDecision Evaluate(
        bool supportsPromptSubmission,
        bool supportsResponseRetrieval,
        bool supportsEditApplication,
        bool supportsSessionAutomation,
        string? detail = null)
    {
        if (!supportsPromptSubmission)
        {
            return new HandoffDecision(
                "Copilot.PromptSubmission",
                CapabilityAvailability.Unavailable,
                HandoffFallbackKind.PreviewCopy,
                AdapterRunState.AwaitingHandoff,
                detail ?? "Supported Copilot prompt submission is unavailable; falling back to preview/copy handoff.");
        }

        if (!supportsResponseRetrieval || !supportsEditApplication || !supportsSessionAutomation)
        {
            // Prompt submission is available but at least one downstream capability (response
            // retrieval, edit application, or session automation) is not. Route the prompt
            // through the bridge, but be explicit that the run must still wait for a manual
            // result import rather than claiming full automation.
            return new HandoffDecision(
                "Copilot.PromptSubmission",
                CapabilityAvailability.ManualOnly,
                HandoffFallbackKind.ManualImport,
                AdapterRunState.AwaitingHandoff,
                detail ?? "Prompt submission is supported, but response retrieval, edit application, or session automation is not; a manual result import is required to complete this action.");
        }

        return new HandoffDecision(
            "Copilot.PromptSubmission",
            CapabilityAvailability.Available,
            HandoffFallbackKind.None,
            AdapterRunState.Running,
            detail ?? "Full supported Copilot handoff automation is available.");
    }
}
