using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Core.Contracts.Context;
using TheKameleon.Superpowers.Core.Contracts.Runs;
using TheKameleon.Superpowers.Core.Contracts.Settings;
using TheKameleon.Superpowers.Skills.Workflow;

namespace TheKameleon.Superpowers.Tests;

public sealed class WorkflowOrchestratorTests
{
    private static DiscoveredSkillEntry CreateSkill(bool withErrors = false)
    {
        var diagnostics = withErrors
            ? new[] { new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT001", "Bad skill.") }
            : Array.Empty<ParseDiagnostic>();

        var document = new ParsedSkillDocument("Plan", "Plan first.", "Body", Array.Empty<SkillReference>(), diagnostics);
        return new DiscoveredSkillEntry("plan", "plan/SKILL.md", @"C:\skills\plan\SKILL.md", DiscoverySourceKind.Packaged, DiscoveryTrustState.Implicit, document, Array.Empty<ParseDiagnostic>());
    }

    private static ContextCaptureSnapshot CreateContext()
    {
        var provenance = new ContextProvenance("test", DateTimeOffset.UtcNow);
        var solution = new SolutionContextSnapshot(ContextValueState.Available, provenance, "MySolution", @"C:\repo\My.sln");
        return new ContextCaptureSnapshot(provenance, solution);
    }

    [Fact]
    public void StartComposesPromptAndCreatesRunningRun()
    {
        var result = WorkflowOrchestrator.Start("run-1", CreateSkill(), ExecutionMode.Guided, "v1.0.0", 1, CreateContext());

        Assert.True(result.Succeeded);
        Assert.Equal(AdapterRunState.Running, result.Run!.State);
        Assert.Equal("run-1", result.Run.RunId);
        Assert.Contains("# Plan", result.Prompt!.Text);
    }

    [Fact]
    public void StartRejectsSkillWithParseErrors()
    {
        var result = WorkflowOrchestrator.Start("run-1", CreateSkill(withErrors: true), ExecutionMode.Guided, "v1.0.0", 1, CreateContext());

        Assert.False(result.Succeeded);
        Assert.Null(result.Run);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPWF701");
    }

    [Fact]
    public void PauseDelegatesToRunLifecycleCoordinator()
    {
        var start = WorkflowOrchestrator.Start("run-1", CreateSkill(), ExecutionMode.Guided, "v1.0.0", 1, CreateContext());

        var paused = WorkflowOrchestrator.Pause(start.Run!, "user requested");

        Assert.Empty(paused.Diagnostics);
        Assert.Equal(AdapterRunState.Paused, paused.Run.State);
    }

    [Fact]
    public void CancelDelegatesToRunLifecycleCoordinator()
    {
        var start = WorkflowOrchestrator.Start("run-1", CreateSkill(), ExecutionMode.Guided, "v1.0.0", 1, CreateContext());

        var canceled = WorkflowOrchestrator.Cancel(start.Run!, "user requested");

        Assert.Equal(AdapterRunState.Canceled, canceled.Run.State);
    }

    [Fact]
    public void NextReComposesPromptWithoutMutatingRunState()
    {
        var prompt = WorkflowOrchestrator.Next(CreateSkill(), ExecutionMode.Full, CreateContext());

        Assert.Equal("Plan", prompt.SkillName);
        Assert.Equal(ExecutionMode.Full, prompt.ExecutionMode);
    }
}
