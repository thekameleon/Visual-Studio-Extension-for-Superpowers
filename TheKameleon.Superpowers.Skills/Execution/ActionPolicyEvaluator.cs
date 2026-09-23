using TheKameleon.Superpowers.Core.Contracts.Runs;
using TheKameleon.Superpowers.Core.Contracts.Settings;

namespace TheKameleon.Superpowers.Skills.Execution;

/// <summary>
/// Pure evaluator for the shared capability/permission policy used by all three execution modes
/// (Guided, ApprovalRequired, Full). This is the single source of truth for whether a given
/// action (side-effect kind) may proceed automatically, requires explicit per-action approval,
/// or is denied outright, so that no execution path can silently bypass policy.
/// </summary>
public static class ActionPolicyEvaluator
{
    /// <summary>
    /// Evaluates whether <paramref name="sideEffectKind"/> may proceed under <paramref name="executionMode"/>.
    /// </summary>
    /// <param name="sideEffectKind">The action's side-effect classification.</param>
    /// <param name="executionMode">The run's effective execution mode.</param>
    /// <param name="isAllowlistedCustomCommand">
    /// Whether the action is a custom command that has been explicitly allowlisted. Ignored for
    /// non-<see cref="ActionSideEffectKind.None"/> built-in side effects.
    /// </param>
    public static ActionPolicyDecision Evaluate(
        ActionSideEffectKind sideEffectKind,
        ExecutionMode executionMode,
        bool isAllowlistedCustomCommand = false)
    {
        if (!Enum.IsDefined(sideEffectKind))
        {
            throw new ArgumentOutOfRangeException(nameof(sideEffectKind), "Side-effect kind is invalid.");
        }

        if (!Enum.IsDefined(executionMode))
        {
            throw new ArgumentOutOfRangeException(nameof(executionMode), "Execution mode is invalid.");
        }

        // None represents read-only/no-side-effect actions (e.g. context capture, planning),
        // which are always allowed regardless of mode.
        if (sideEffectKind == ActionSideEffectKind.None)
        {
            return new ActionPolicyDecision(
                sideEffectKind,
                executionMode,
                ActionPolicyDecisionKind.Allowed,
                "No side effects are produced by this action.");
        }

        return executionMode switch
        {
            ExecutionMode.Guided => new ActionPolicyDecision(
                sideEffectKind,
                executionMode,
                ActionPolicyDecisionKind.RequiresApproval,
                "Guided mode requires explicit approval before every action with side effects."),

            ExecutionMode.ApprovalRequired => new ActionPolicyDecision(
                sideEffectKind,
                executionMode,
                ActionPolicyDecisionKind.RequiresApproval,
                "Approval-required mode requires explicit approval before every action with side effects."),

            ExecutionMode.Full => EvaluateFullMode(sideEffectKind, executionMode, isAllowlistedCustomCommand),

            _ => throw new ArgumentOutOfRangeException(nameof(executionMode), "Execution mode is invalid.")
        };
    }

    private static ActionPolicyDecision EvaluateFullMode(
        ActionSideEffectKind sideEffectKind,
        ExecutionMode executionMode,
        bool isAllowlistedCustomCommand)
    {
        // Full automation may build, test, and edit within agreed scope automatically. Custom
        // command execution is only permitted when explicitly allowlisted; this cannot be
        // bypassed by Full mode.
        if (sideEffectKind == ActionSideEffectKind.PromptHandoff)
        {
            // Copilot handoff always requires a capability-checked bridge decision, not blanket
            // automatic policy allowance, because supported automation may be partial.
            return new ActionPolicyDecision(
                sideEffectKind,
                executionMode,
                ActionPolicyDecisionKind.RequiresApproval,
                "Copilot handoff requires a capability-checked bridge decision even in Full mode.");
        }

        if (!isAllowlistedCustomCommand && RequiresAllowlist(sideEffectKind))
        {
            return new ActionPolicyDecision(
                sideEffectKind,
                executionMode,
                ActionPolicyDecisionKind.Denied,
                "Custom command execution requires an explicit allowlist entry; Full mode is not a sandbox.");
        }

        return new ActionPolicyDecision(
            sideEffectKind,
            executionMode,
            ActionPolicyDecisionKind.Allowed,
            "Full automation permits this action within the agreed policy scope.");
    }

    private static bool RequiresAllowlist(ActionSideEffectKind sideEffectKind)
    {
        return sideEffectKind == ActionSideEffectKind.CustomCommandExecution;
    }
}
