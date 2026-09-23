using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Core.Contracts.Context;
using TheKameleon.Superpowers.Core.Contracts.Runs;
using TheKameleon.Superpowers.Core.Contracts.Settings;
using TheKameleon.Superpowers.Skills.Composition;

namespace TheKameleon.Superpowers.Skills.Workflow;

/// <summary>
/// Pure coordinator that unifies skill selection, execution mode, a captured context snapshot,
/// prompt composition and run-lifecycle control into a single "current run" state transition
/// surface for the tool window. Delegates the actual state machine to
/// <see cref="RunLifecycleCoordinator"/> so lifecycle rules are defined in exactly one place.
/// </summary>
public static class WorkflowOrchestrator
{
    public static WorkflowStartResult Start(
        string runId,
        DiscoveredSkillEntry skill,
        ExecutionMode executionMode,
        string releaseTag,
        int adapterSchemaVersion,
        ContextCaptureSnapshot context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentException.ThrowIfNullOrWhiteSpace(releaseTag);
        ArgumentNullException.ThrowIfNull(context);

        var diagnostics = new List<ParseDiagnostic>();
        if (skill.Document.HasErrors)
        {
            diagnostics.Add(new ParseDiagnostic(
                ParseDiagnosticSeverity.Error,
                "SPWF701",
                $"Skill '{skill.SkillId}' has parsing errors and cannot start a run."));
            return new WorkflowStartResult(null, null, diagnostics);
        }

        var run = new AdapterRunRecord(
            runId,
            releaseTag,
            adapterSchemaVersion,
            executionMode,
            AdapterRunState.Running);

        var prompt = PromptComposer.Compose(skill.Document, executionMode, context);

        return new WorkflowStartResult(run, prompt, diagnostics);
    }

    public static RunLifecycleResult Pause(AdapterRunRecord run, string? reason) => RunLifecycleCoordinator.Pause(run, reason);

    public static RunLifecycleResult Cancel(AdapterRunRecord run, string? reason) => RunLifecycleCoordinator.Cancel(run, reason);

    public static RunLifecycleResult RetryTask(AdapterRunRecord run, string taskId, string? reason) => RunLifecycleCoordinator.RetryTask(run, taskId, reason);

    /// <summary>
    /// Re-composes the prompt for the current skill/mode/context without altering run state,
    /// used by the "Next" control to advance the visible prompt when context has changed.
    /// </summary>
    public static ComposedPrompt Next(DiscoveredSkillEntry skill, ExecutionMode executionMode, ContextCaptureSnapshot context)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(context);

        return PromptComposer.Compose(skill.Document, executionMode, context);
    }
}

public sealed record WorkflowStartResult(
    AdapterRunRecord? Run,
    ComposedPrompt? Prompt,
    IReadOnlyList<ParseDiagnostic> Diagnostics)
{
    public bool Succeeded => Run is not null && Prompt is not null && !Diagnostics.Any(diagnostic => diagnostic.Severity == ParseDiagnosticSeverity.Error);
}
