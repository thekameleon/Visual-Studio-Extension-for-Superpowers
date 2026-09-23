using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record ActionPolicyDecision
{
    [JsonConstructor]
    public ActionPolicyDecision(
        ActionSideEffectKind sideEffectKind,
        Settings.ExecutionMode executionMode,
        ActionPolicyDecisionKind decision,
        string reason)
    {
        if (!Enum.IsDefined(sideEffectKind))
        {
            throw new ArgumentOutOfRangeException(nameof(sideEffectKind), "Side-effect kind is invalid.");
        }

        if (!Enum.IsDefined(executionMode))
        {
            throw new ArgumentOutOfRangeException(nameof(executionMode), "Execution mode is invalid.");
        }

        if (!Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision), "Policy decision is invalid.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason is required.", nameof(reason));
        }

        SideEffectKind = sideEffectKind;
        ExecutionMode = executionMode;
        Decision = decision;
        Reason = reason;
    }

    public ActionSideEffectKind SideEffectKind { get; }

    public Settings.ExecutionMode ExecutionMode { get; }

    public ActionPolicyDecisionKind Decision { get; }

    public string Reason { get; }
}
