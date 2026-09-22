using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Core.Contracts.Runs;

namespace TheKameleon.Superpowers.Skills.Composition;

public static class RunLifecycleCoordinator
{
    public static RunLifecycleResult Pause(AdapterRunRecord run, string? reason)
    {
        ArgumentNullException.ThrowIfNull(run);

        var diagnostics = new List<ParseDiagnostic>();
        if (run.State is not (AdapterRunState.Running or AdapterRunState.AwaitingHandoff or AdapterRunState.WaitingForEvidence))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN201", $"Run '{run.RunId}' cannot be paused from state '{run.State}'."));
            return new RunLifecycleResult(run, RetryEvaluation: null, diagnostics);
        }

        return new RunLifecycleResult(
            CloneRun(run, AdapterRunState.Paused, AppendControl(run.Controls, new RunControlRecord(RunControlAction.Pause, DateTimeOffset.UtcNow, reason))),
            RetryEvaluation: null,
            diagnostics);
    }

    public static RunLifecycleResult Cancel(AdapterRunRecord run, string? reason)
    {
        ArgumentNullException.ThrowIfNull(run);

        var diagnostics = new List<ParseDiagnostic>();
        if (run.State is AdapterRunState.Completed or AdapterRunState.Failed or AdapterRunState.Canceled)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN202", $"Run '{run.RunId}' cannot be canceled from state '{run.State}'."));
            return new RunLifecycleResult(run, RetryEvaluation: null, diagnostics);
        }

        var canceledTasks = run.Tasks
            .Select(task => task.State is AdapterTaskState.Completed or AdapterTaskState.Failed or AdapterTaskState.Canceled
                ? task
                : new AdapterTaskRecord(task.TaskId, task.Title, AdapterTaskState.Canceled, task.RequiredEvidence, task.Evidence, task.Diagnostics, task.Attempts))
            .ToArray();

        return new RunLifecycleResult(
            new AdapterRunRecord(
                run.RunId,
                run.SelectedReleaseTag,
                run.AdapterSchemaVersion,
                run.ExecutionMode,
                AdapterRunState.Canceled,
                run.SchemaVersion,
                run.TrustBasis,
                canceledTasks,
                run.Composition,
                run.Capabilities,
                AppendControl(run.Controls, new RunControlRecord(RunControlAction.Cancel, DateTimeOffset.UtcNow, reason))),
            RetryEvaluation: null,
            diagnostics);
    }

    public static RunLifecycleResult RetryTask(AdapterRunRecord run, string taskId, string? reason)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        var diagnostics = new List<ParseDiagnostic>();
        var task = run.Tasks.FirstOrDefault(candidate => string.Equals(candidate.TaskId, taskId, StringComparison.OrdinalIgnoreCase));
        if (task is null)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN203", $"Run '{run.RunId}' does not contain task '{taskId}'."));
            return new RunLifecycleResult(run, new RetryEvaluationResult(RetryDisposition.BlockedInvalidState, diagnostics), diagnostics);
        }

        var retryEvaluation = EvaluateRetry(task);
        if (!retryEvaluation.CanRetry)
        {
            return new RunLifecycleResult(run, retryEvaluation, diagnostics);
        }

        var nextAttemptNumber = task.Attempts.Count == 0 ? 1 : task.Attempts.Max(attempt => attempt.AttemptNumber) + 1;
        var retriedTask = new AdapterTaskRecord(
            task.TaskId,
            task.Title,
            AdapterTaskState.Pending,
            task.RequiredEvidence,
            task.Evidence,
            task.Diagnostics,
            task.Attempts.Concat(new[]
            {
                new ActionAttemptRecord(nextAttemptNumber, ActionSideEffectKind.None, isIdempotent: true, ActionAttemptState.Started, DateTimeOffset.UtcNow, reason: reason)
            }).ToArray());

        var updatedTasks = run.Tasks
            .Select(candidate => string.Equals(candidate.TaskId, taskId, StringComparison.OrdinalIgnoreCase) ? retriedTask : candidate)
            .ToArray();

        return new RunLifecycleResult(
            new AdapterRunRecord(
                run.RunId,
                run.SelectedReleaseTag,
                run.AdapterSchemaVersion,
                run.ExecutionMode,
                AdapterRunState.Running,
                run.SchemaVersion,
                run.TrustBasis,
                updatedTasks,
                run.Composition,
                run.Capabilities,
                AppendControl(run.Controls, new RunControlRecord(RunControlAction.RetryTask, DateTimeOffset.UtcNow, reason))),
            retryEvaluation,
            diagnostics);
    }

    public static RetryEvaluationResult EvaluateRetry(AdapterTaskRecord task)
    {
        ArgumentNullException.ThrowIfNull(task);

        var diagnostics = new List<ParseDiagnostic>();
        if (task.State is not (AdapterTaskState.Failed or AdapterTaskState.Canceled or AdapterTaskState.Blocked))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN204", $"Task '{task.TaskId}' cannot be retried from state '{task.State}'."));
            return new RetryEvaluationResult(RetryDisposition.BlockedInvalidState, diagnostics);
        }

        var duplicateSideEffect = task.Attempts.Any(attempt =>
            attempt.ActionAttemptState == ActionAttemptState.Succeeded
            && attempt.SideEffectKind != ActionSideEffectKind.None
            && !attempt.IsIdempotent);

        if (duplicateSideEffect)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN205", $"Task '{task.TaskId}' already completed a non-idempotent side effect and cannot be retried automatically."));
            return new RetryEvaluationResult(RetryDisposition.BlockedDuplicateSideEffect, diagnostics);
        }

        return new RetryEvaluationResult(RetryDisposition.Allowed, diagnostics);
    }

    private static AdapterRunRecord CloneRun(AdapterRunRecord run, AdapterRunState state, IReadOnlyList<RunControlRecord> controls)
    {
        return new AdapterRunRecord(
            run.RunId,
            run.SelectedReleaseTag,
            run.AdapterSchemaVersion,
            run.ExecutionMode,
            state,
            run.SchemaVersion,
            run.TrustBasis,
            run.Tasks,
            run.Composition,
            run.Capabilities,
            controls);
    }

    private static IReadOnlyList<RunControlRecord> AppendControl(IReadOnlyList<RunControlRecord> controls, RunControlRecord control)
    {
        return controls.Concat(new[] { control }).ToArray();
    }
}
