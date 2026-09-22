using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Core.Contracts.Runs;
using TheKameleon.Superpowers.Core.Contracts.Settings;
using TheKameleon.Superpowers.Skills.Composition;

namespace TheKameleon.Superpowers.Tests;

public sealed class RunLifecycleCoordinatorTests
{
    [Fact]
    public void PauseMovesRunningRunToPausedAndRecordsControl()
    {
        var run = CreateRun(AdapterRunState.Running, CreateTask("task-1", AdapterTaskState.InProgress));

        var result = RunLifecycleCoordinator.Pause(run, "user requested pause");

        Assert.False(result.HasErrors);
        Assert.Equal(AdapterRunState.Paused, result.Run.State);
        var control = Assert.Single(result.Run.Controls);
        Assert.Equal(RunControlAction.Pause, control.Action);
        Assert.Equal("user requested pause", control.Reason);
    }

    [Fact]
    public void CancelMovesWaitingRunToCanceledAndCancelsOpenTasks()
    {
        var run = CreateRun(
            AdapterRunState.AwaitingHandoff,
            CreateTask("task-1", AdapterTaskState.InProgress),
            CreateTask("task-2", AdapterTaskState.Completed));

        var result = RunLifecycleCoordinator.Cancel(run, "user canceled");

        Assert.False(result.HasErrors);
        Assert.Equal(AdapterRunState.Canceled, result.Run.State);
        Assert.Equal(AdapterTaskState.Canceled, result.Run.Tasks.Single(task => task.TaskId == "task-1").State);
        Assert.Equal(AdapterTaskState.Completed, result.Run.Tasks.Single(task => task.TaskId == "task-2").State);
        Assert.Equal(RunControlAction.Cancel, Assert.Single(result.Run.Controls).Action);
    }

    [Fact]
    public void PauseAllowsWaitingForEvidenceTransition()
    {
        var run = CreateRun(AdapterRunState.WaitingForEvidence, CreateTask("task-1", AdapterTaskState.WaitingForEvidence));

        var result = RunLifecycleCoordinator.Pause(run, "wait for review");

        Assert.False(result.HasErrors);
        Assert.Equal(AdapterRunState.Paused, result.Run.State);
        Assert.Equal(RunControlAction.Pause, Assert.Single(result.Run.Controls).Action);
    }

    [Fact]
    public void CancelRejectsTerminalRunState()
    {
        var result = RunLifecycleCoordinator.Cancel(CreateRun(AdapterRunState.Completed, CreateTask("task-1", AdapterTaskState.Completed)), "too late");

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPRUN202");
    }

    [Fact]
    public void RetryFailedTaskIsAllowedAndAddsAttempt()
    {
        var task = CreateTask(
            "task-1",
            AdapterTaskState.Failed,
            new ActionAttemptRecord(1, ActionSideEffectKind.BuildExecution, isIdempotent: true, ActionAttemptState.Failed, DateTimeOffset.UtcNow.AddMinutes(-2), DateTimeOffset.UtcNow.AddMinutes(-1), "build failed"));
        var run = CreateRun(AdapterRunState.Failed, task);

        var result = RunLifecycleCoordinator.RetryTask(run, "task-1", "retry build");

        Assert.False(result.HasErrors);
        Assert.NotNull(result.RetryEvaluation);
        Assert.True(result.RetryEvaluation!.CanRetry);
        Assert.Equal(AdapterRunState.Running, result.Run.State);
        var retriedTask = Assert.Single(result.Run.Tasks);
        Assert.Equal(AdapterTaskState.Pending, retriedTask.State);
        Assert.Equal(2, retriedTask.Attempts.Count);
        Assert.Equal(ActionAttemptState.Started, retriedTask.Attempts[1].ActionAttemptState);
        Assert.Equal(RunControlAction.RetryTask, Assert.Single(result.Run.Controls).Action);
    }

    [Fact]
    public void RetryIsBlockedWhenNonIdempotentSideEffectAlreadySucceeded()
    {
        var task = CreateTask(
            "task-1",
            AdapterTaskState.Failed,
            new ActionAttemptRecord(1, ActionSideEffectKind.EditApplication, isIdempotent: false, ActionAttemptState.Succeeded, DateTimeOffset.UtcNow.AddMinutes(-2), DateTimeOffset.UtcNow.AddMinutes(-1), "edit applied"));

        var result = RunLifecycleCoordinator.RetryTask(CreateRun(AdapterRunState.Failed, task), "task-1", "retry edit");

        Assert.True(result.HasErrors);
        Assert.NotNull(result.RetryEvaluation);
        Assert.Equal(RetryDisposition.BlockedDuplicateSideEffect, result.RetryEvaluation!.Disposition);
        Assert.Contains(result.RetryEvaluation.Diagnostics, diagnostic => diagnostic.Code == "SPRUN205");
        Assert.Equal(AdapterRunState.Failed, result.Run.State);
    }

    [Fact]
    public void RetryAllowsIdempotentSucceededSideEffect()
    {
        var task = CreateTask(
            "task-1",
            AdapterTaskState.Blocked,
            new ActionAttemptRecord(1, ActionSideEffectKind.BuildExecution, isIdempotent: true, ActionAttemptState.Succeeded, DateTimeOffset.UtcNow.AddMinutes(-2), DateTimeOffset.UtcNow.AddMinutes(-1), "build already ran"));

        var result = RunLifecycleCoordinator.RetryTask(CreateRun(AdapterRunState.Paused, task), "task-1", "retry build");

        Assert.False(result.HasErrors);
        Assert.NotNull(result.RetryEvaluation);
        Assert.Equal(RetryDisposition.Allowed, result.RetryEvaluation!.Disposition);
        Assert.Equal(AdapterRunState.Running, result.Run.State);
    }

    [Fact]
    public void PauseRejectsInvalidTransition()
    {
        var result = RunLifecycleCoordinator.Pause(CreateRun(AdapterRunState.Completed, CreateTask("task-1", AdapterTaskState.Completed)), "too late");

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPRUN201");
    }

    [Fact]
    public void RetryRejectsTaskInInvalidState()
    {
        var result = RunLifecycleCoordinator.RetryTask(CreateRun(AdapterRunState.Running, CreateTask("task-1", AdapterTaskState.InProgress)), "task-1", "retry while running");

        Assert.True(result.HasErrors);
        Assert.NotNull(result.RetryEvaluation);
        Assert.Equal(RetryDisposition.BlockedInvalidState, result.RetryEvaluation!.Disposition);
        Assert.Contains(result.RetryEvaluation.Diagnostics, diagnostic => diagnostic.Code == "SPRUN204");
    }

    [Fact]
    public void RetryRejectsMissingTask()
    {
        var result = RunLifecycleCoordinator.RetryTask(CreateRun(AdapterRunState.Failed, CreateTask("task-1", AdapterTaskState.Failed)), "task-2", "retry missing task");

        Assert.True(result.HasErrors);
        Assert.NotNull(result.RetryEvaluation);
        Assert.Equal(RetryDisposition.BlockedInvalidState, result.RetryEvaluation!.Disposition);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPRUN203");
    }

    private static AdapterRunRecord CreateRun(AdapterRunState state, params AdapterTaskRecord[] tasks)
    {
        return new AdapterRunRecord(
            runId: "run-1",
            selectedReleaseTag: "v6.4.1",
            adapterSchemaVersion: 1,
            executionMode: ExecutionMode.Guided,
            state: state,
            tasks: tasks);
    }

    private static AdapterTaskRecord CreateTask(string taskId, AdapterTaskState state, params ActionAttemptRecord[] attempts)
    {
        return new AdapterTaskRecord(
            taskId: taskId,
            title: taskId,
            state: state,
            attempts: attempts,
            requiredEvidence: Array.Empty<AdapterTaskEvidenceRequirement>(),
            evidence: Array.Empty<AdapterEvidenceRecord>(),
            diagnostics: Array.Empty<ParseDiagnostic>());
    }
}
