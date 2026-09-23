using TheKameleon.Superpowers.Core.Contracts.Runs;
using TheKameleon.Superpowers.Core.Contracts.Settings;
using TheKameleon.Superpowers.Skills.Execution;

namespace TheKameleon.Superpowers.Tests;

public sealed class ActionPolicyEvaluatorTests
{
    [Fact]
    public void NoSideEffectActionsAreAlwaysAllowed()
    {
        foreach (var mode in new[] { ExecutionMode.Guided, ExecutionMode.ApprovalRequired, ExecutionMode.Full })
        {
            var decision = ActionPolicyEvaluator.Evaluate(ActionSideEffectKind.None, mode);
            Assert.Equal(ActionPolicyDecisionKind.Allowed, decision.Decision);
        }
    }

    [Theory]
    [InlineData(ExecutionMode.Guided)]
    [InlineData(ExecutionMode.ApprovalRequired)]
    public void GuidedAndApprovalRequiredModesRequireApprovalForSideEffects(ExecutionMode mode)
    {
        var decision = ActionPolicyEvaluator.Evaluate(ActionSideEffectKind.BuildExecution, mode);
        Assert.Equal(ActionPolicyDecisionKind.RequiresApproval, decision.Decision);
    }

    [Theory]
    [InlineData(ActionSideEffectKind.BuildExecution)]
    [InlineData(ActionSideEffectKind.TestExecution)]
    [InlineData(ActionSideEffectKind.EditApplication)]
    public void FullModeAllowsBuildTestAndEditActionsAutomatically(ActionSideEffectKind sideEffectKind)
    {
        var decision = ActionPolicyEvaluator.Evaluate(sideEffectKind, ExecutionMode.Full);
        Assert.Equal(ActionPolicyDecisionKind.Allowed, decision.Decision);
    }

    [Fact]
    public void FullModeStillRequiresBridgeDecisionForPromptHandoff()
    {
        var decision = ActionPolicyEvaluator.Evaluate(ActionSideEffectKind.PromptHandoff, ExecutionMode.Full);
        Assert.Equal(ActionPolicyDecisionKind.RequiresApproval, decision.Decision);
    }

    [Fact]
    public void FullModeDeniesCustomCommandExecutionWithoutAllowlistEntry()
    {
        var decision = ActionPolicyEvaluator.Evaluate(ActionSideEffectKind.CustomCommandExecution, ExecutionMode.Full, isAllowlistedCustomCommand: false);
        Assert.Equal(ActionPolicyDecisionKind.Denied, decision.Decision);
    }

    [Fact]
    public void FullModeAllowsCustomCommandExecutionWhenAllowlisted()
    {
        var decision = ActionPolicyEvaluator.Evaluate(ActionSideEffectKind.CustomCommandExecution, ExecutionMode.Full, isAllowlistedCustomCommand: true);
        Assert.Equal(ActionPolicyDecisionKind.Allowed, decision.Decision);
    }

    [Fact]
    public void RejectsUndefinedExecutionMode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ActionPolicyEvaluator.Evaluate(ActionSideEffectKind.BuildExecution, (ExecutionMode)99));
    }

    [Fact]
    public void RejectsUndefinedSideEffectKind()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ActionPolicyEvaluator.Evaluate((ActionSideEffectKind)99, ExecutionMode.Guided));
    }
}
