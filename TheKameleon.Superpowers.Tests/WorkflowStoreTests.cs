using System.Text.Json;
using TheKameleon.Superpowers.Core.Contracts.Context;
using TheKameleon.Superpowers.Core.Contracts.Runs;
using TheKameleon.Superpowers.Core.Contracts.Settings;
using TheKameleon.Superpowers.Skills.Workflow;

namespace TheKameleon.Superpowers.Tests;

public sealed class WorkflowStoreTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), "SuperpowersTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveAndLoadRoundTripWorkflowEnvelope()
    {
        var envelope = CreateEnvelope();

        var save = WorkflowStore.Save(tempRoot, envelope);
        var load = WorkflowStore.Load(tempRoot);
        var loadDiagnostics = string.Join(Environment.NewLine, load.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));

        Assert.False(save.HasErrors);
        Assert.False(load.HasErrors, loadDiagnostics);
        Assert.NotNull(load.Envelope);
        Assert.Equal(envelope.WorkspaceId, load.Envelope!.WorkspaceId);
        Assert.Equal(envelope.Run.RunId, load.Envelope.Run.RunId);
        Assert.Equal(envelope.SelectedSkillId, load.Envelope.SelectedSkillId);
    }

    [Fact]
    public void LoadRecoversFromBackupWhenPrimaryFileCannotBeReadAsText()
    {
        var envelope = CreateEnvelope();
        WorkflowStore.Save(tempRoot, envelope);
        var activePath = Path.Combine(tempRoot, "active-run.json");
        File.WriteAllBytes(activePath, new byte[] { 0xFF, 0x00, 0xFF });

        var load = WorkflowStore.Load(tempRoot);

        Assert.NotNull(load.Envelope);
        Assert.Contains(load.Diagnostics, diagnostic => diagnostic.Code is "SPRUN308" or "SPRUN311");
    }

    [Fact]
    public void LoadRecoversFromBackupWhenPrimaryFileContainsCorruptJson()
    {
        var envelope = CreateEnvelope();
        WorkflowStore.Save(tempRoot, envelope);
        var activePath = Path.Combine(tempRoot, "active-run.json");
        File.WriteAllText(activePath, "{");

        var load = WorkflowStore.Load(tempRoot);

        Assert.NotNull(load.Envelope);
        Assert.Contains(load.Diagnostics, diagnostic => diagnostic.Code == "SPRUN311");
    }

    [Fact]
    public void LoadRejectsFutureSchemaVersion()
    {
        var activePath = Path.Combine(tempRoot, "active-run.json");
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(activePath, """
        {
          "WorkspaceId": "workspace-a",
          "Run": {
            "RunId": "run-1",
            "SelectedReleaseTag": "v1.0.0",
            "AdapterSchemaVersion": 1,
            "ExecutionMode": 0,
            "State": 2,
            "SchemaVersion": 1,
            "Tasks": [],
            "Capabilities": [],
            "Controls": []
          },
          "SchemaVersion": 999,
          "SavedAtUtc": "2026-10-01T00:00:00Z"
        }
        """);

        var load = WorkflowStore.Load(tempRoot);

        Assert.Null(load.Envelope);
        Assert.Contains(load.Diagnostics, diagnostic => diagnostic.Code == "SPRUN310");
    }

    [Fact]
    public void SaveReportsFailureWhenWorkspacePathCannotBeCreated()
    {
        Directory.CreateDirectory(tempRoot);
        var blockingPath = Path.Combine(tempRoot, "workspace-file");
        File.WriteAllText(blockingPath, "occupied");

        var save = WorkflowStore.Save(blockingPath, CreateEnvelope());

        Assert.True(save.HasErrors);
        Assert.Contains(save.Diagnostics, diagnostic => diagnostic.Code == "SPRUN301");
    }

    [Fact]
    public void AppendQueryExportAndDeleteHistory()
    {
        WorkflowStore.AppendHistory(tempRoot, new WorkflowHistoryEntry("run-1", "v1.0.0", ExecutionMode.Guided.ToString(), AdapterRunState.Running, DateTimeOffset.UtcNow.AddMinutes(-1), "Plan"));
        WorkflowStore.AppendHistory(tempRoot, new WorkflowHistoryEntry("run-2", "v1.0.0", ExecutionMode.Full.ToString(), AdapterRunState.Failed, DateTimeOffset.UtcNow, "Debug"));

        var query = WorkflowHistoryService.Query(tempRoot, new WorkflowHistoryQuery(state: AdapterRunState.Failed));
        var exportPath = Path.Combine(tempRoot, "exports", "history.json");
        var export = WorkflowHistoryService.Export(tempRoot, exportPath, new WorkflowHistoryExportOptions());
        var delete = WorkflowHistoryService.Delete(tempRoot, new WorkflowHistoryQuery(runId: "run-1"));

        Assert.False(query.HasErrors);
        Assert.Single(query.Snapshot.Entries);
        Assert.Equal("run-2", query.Snapshot.Entries[0].RunId);
        Assert.False(export.HasErrors);
        Assert.True(File.Exists(export.ExportPath));
        Assert.False(delete.HasErrors);
        Assert.Single(delete.Snapshot.Entries);
        Assert.Equal("run-2", delete.Snapshot.Entries[0].RunId);
    }

    [Fact]
    public void AppendHistoryDropsSensitiveContentByDefault()
    {
        var mutation = WorkflowStore.AppendHistory(
            tempRoot,
            new WorkflowHistoryEntry(
                "run-1",
                "v1.0.0",
                ExecutionMode.Guided.ToString(),
                AdapterRunState.Running,
                DateTimeOffset.UtcNow,
                selectedSkillId: "Plan",
                retainedContent: "secret prompt contents"));

        Assert.False(mutation.HasErrors);
        var entry = Assert.Single(mutation.Snapshot.Entries);
        Assert.True(entry.HadSensitiveContent);
        Assert.Null(entry.RetainedContent);
    }

    [Fact]
    public void AppendHistoryRetainsSensitiveContentWhenOptedIn()
    {
        var mutation = WorkflowHistoryService.Append(
            tempRoot,
            new WorkflowHistoryEntry(
                "run-1",
                "v1.0.0",
                ExecutionMode.Guided.ToString(),
                AdapterRunState.Running,
                DateTimeOffset.UtcNow,
                selectedSkillId: "Plan",
                retainedContent: "secret prompt contents"),
            new SuperpowersSettings(retainSensitiveHistoryContent: true));

        Assert.False(mutation.HasErrors);
        var entry = Assert.Single(mutation.Snapshot.Entries);
        Assert.True(entry.HadSensitiveContent);
        Assert.Equal("secret prompt contents", entry.RetainedContent);
    }

    [Fact]
    public void ExportOmitsSensitiveContentUnlessExplicitlyRequested()
    {
        WorkflowHistoryService.Append(
            tempRoot,
            new WorkflowHistoryEntry(
                "run-1",
                "v1.0.0",
                ExecutionMode.Guided.ToString(),
                AdapterRunState.Completed,
                DateTimeOffset.UtcNow,
                selectedSkillId: "Plan",
                retainedContent: "secret prompt contents"),
            new SuperpowersSettings(retainSensitiveHistoryContent: true));

        var redactedPath = Path.Combine(tempRoot, "exports", "history-redacted.json");
        var fullPath = Path.Combine(tempRoot, "exports", "history-full.json");
        var redacted = WorkflowHistoryService.Export(tempRoot, redactedPath, new WorkflowHistoryExportOptions());
        var full = WorkflowHistoryService.Export(tempRoot, fullPath, new WorkflowHistoryExportOptions(includeSensitiveContent: true));

        Assert.False(redacted.HasErrors);
        Assert.False(full.HasErrors);

        var redactedSnapshot = JsonSerializer.Deserialize<WorkflowHistorySnapshot>(File.ReadAllText(redactedPath));
        var fullSnapshot = JsonSerializer.Deserialize<WorkflowHistorySnapshot>(File.ReadAllText(fullPath));

        Assert.NotNull(redactedSnapshot);
        Assert.NotNull(fullSnapshot);
        Assert.Null(Assert.Single(redactedSnapshot!.Entries).RetainedContent);
        Assert.Equal("secret prompt contents", Assert.Single(fullSnapshot!.Entries).RetainedContent);
    }

    [Fact]
    public void AppendHistoryPrunesEntriesOutsideRetentionWindow()
    {
        WorkflowHistoryService.Append(
            tempRoot,
            new WorkflowHistoryEntry("run-old", "v1.0.0", ExecutionMode.Guided.ToString(), AdapterRunState.Completed, DateTimeOffset.UtcNow.AddDays(-40), "Plan"),
            new SuperpowersSettings(historyRetentionDays: 30));

        var mutation = WorkflowHistoryService.Append(
            tempRoot,
            new WorkflowHistoryEntry("run-new", "v1.0.0", ExecutionMode.Guided.ToString(), AdapterRunState.Completed, DateTimeOffset.UtcNow, "Plan"),
            new SuperpowersSettings(historyRetentionDays: 30));

        Assert.False(mutation.HasErrors);
        var entry = Assert.Single(mutation.Snapshot.Entries);
        Assert.Equal("run-new", entry.RunId);
    }

    [Fact]
    public void AppendHistoryReportsFailureWhenWorkspacePathCannotBeCreated()
    {
        Directory.CreateDirectory(tempRoot);
        var blockingPath = Path.Combine(tempRoot, "history-file");
        File.WriteAllText(blockingPath, "occupied");

        var mutation = WorkflowStore.AppendHistory(
            blockingPath,
            new WorkflowHistoryEntry("run-1", "v1.0.0", ExecutionMode.Guided.ToString(), AdapterRunState.Completed, DateTimeOffset.UtcNow, "Plan"));

        Assert.True(mutation.HasErrors);
        Assert.Contains(mutation.Diagnostics, diagnostic => diagnostic.Code == "SPRUN305");
    }

    [Fact]
    public void DeleteHistoryReportsFailureWhenWorkspacePathCannotBeCreated()
    {
        Directory.CreateDirectory(tempRoot);
        var blockingPath = Path.Combine(tempRoot, "delete-file");
        File.WriteAllText(blockingPath, "occupied");

        var mutation = WorkflowStore.DeleteHistory(blockingPath);

        Assert.True(mutation.HasErrors);
        Assert.Contains(mutation.Diagnostics, diagnostic => diagnostic.Code == "SPRUN306");
    }

    [Fact]
    public void ExportReportsFailureWhenDestinationDirectoryCannotBeCreated()
    {
        WorkflowHistoryService.Append(
            tempRoot,
            new WorkflowHistoryEntry("run-1", "v1.0.0", ExecutionMode.Guided.ToString(), AdapterRunState.Completed, DateTimeOffset.UtcNow, "Plan"));

        Directory.CreateDirectory(tempRoot);
        var blockingPath = Path.Combine(tempRoot, "export-parent");
        File.WriteAllText(blockingPath, "occupied");
        var exportPath = Path.Combine(blockingPath, "history.json");

        var export = WorkflowHistoryService.Export(tempRoot, exportPath, new WorkflowHistoryExportOptions());

        Assert.True(export.HasErrors);
        Assert.Contains(export.Diagnostics, diagnostic => diagnostic.Code == "SPRUN407");
    }

    [Fact]
    public void ResumeValidatorRejectsMismatchedWorkspaceOrPolicy()
    {
        var envelope = CreateEnvelope(policyFingerprint: "policy-a");

        var result = WorkflowResumeValidator.Validate(envelope, new WorkflowResumeRequest(
            workspaceId: "workspace-b",
            releaseTag: envelope.Run.SelectedReleaseTag,
            releaseCommit: envelope.ReleaseCommit,
            selectedSkillId: envelope.SelectedSkillId,
            policyFingerprint: "policy-b"));

        Assert.False(result.CanResume);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPRUN401");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPRUN405");
    }

    [Fact]
    public void ResumeValidatorRejectsMissingSavedPolicyFingerprintWhenCurrentPolicyIsProvided()
    {
        var envelope = CreateEnvelope(policyFingerprint: null);

        var result = WorkflowResumeValidator.Validate(envelope, new WorkflowResumeRequest(
            workspaceId: envelope.WorkspaceId,
            releaseTag: envelope.Run.SelectedReleaseTag,
            releaseCommit: envelope.ReleaseCommit,
            selectedSkillId: envelope.SelectedSkillId,
            policyFingerprint: "policy-a",
            activeDocumentPath: "C:/repo/Program.cs",
            currentSnapshotCapturedAtUtc: envelope.ContextSnapshot!.Provenance.CapturedAtUtc.AddMinutes(1)));

        Assert.False(result.CanResume);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPRUN405");
    }

    [Fact]
    public void ResumeValidatorAllowsResumeWhenSnapshotSkillAndPolicyStillMatch()
    {
        var envelope = CreateEnvelope(policyFingerprint: "policy-a");

        var result = WorkflowResumeValidator.Validate(envelope, new WorkflowResumeRequest(
            workspaceId: "workspace-a",
            releaseTag: envelope.Run.SelectedReleaseTag,
            releaseCommit: envelope.ReleaseCommit,
            selectedSkillId: envelope.SelectedSkillId,
            policyFingerprint: "policy-a",
            activeDocumentPath: "C:/repo/Program.cs",
            currentSnapshotCapturedAtUtc: envelope.ContextSnapshot!.Provenance.CapturedAtUtc.AddMinutes(1)));

        Assert.True(result.CanResume);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void ResumeValidatorRejectsChangedActiveDocument()
    {
        var envelope = CreateEnvelope();

        var result = WorkflowResumeValidator.Validate(envelope, new WorkflowResumeRequest(
            workspaceId: envelope.WorkspaceId,
            releaseTag: envelope.Run.SelectedReleaseTag,
            releaseCommit: envelope.ReleaseCommit,
            selectedSkillId: envelope.SelectedSkillId,
            policyFingerprint: envelope.PolicyFingerprint,
            activeDocumentPath: "C:/repo/Other.cs",
            currentSnapshotCapturedAtUtc: envelope.ContextSnapshot!.Provenance.CapturedAtUtc.AddMinutes(1)));

        Assert.False(result.CanResume);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPRUN407");
    }

    [Fact]
    public void ResumeValidatorRejectsOlderCurrentSnapshot()
    {
        var envelope = CreateEnvelope();

        var result = WorkflowResumeValidator.Validate(envelope, new WorkflowResumeRequest(
            workspaceId: envelope.WorkspaceId,
            releaseTag: envelope.Run.SelectedReleaseTag,
            releaseCommit: envelope.ReleaseCommit,
            selectedSkillId: envelope.SelectedSkillId,
            policyFingerprint: envelope.PolicyFingerprint,
            activeDocumentPath: "C:/repo/Program.cs",
            currentSnapshotCapturedAtUtc: envelope.ContextSnapshot!.Provenance.CapturedAtUtc.AddMinutes(-1)));

        Assert.False(result.CanResume);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPRUN408");
    }

    [Fact]
    public void ResumeValidatorRejectsMissingCurrentSnapshotIdentityWhenSavedSnapshotRequiresIt()
    {
        var envelope = CreateEnvelope();

        var result = WorkflowResumeValidator.Validate(envelope, new WorkflowResumeRequest(
            workspaceId: envelope.WorkspaceId,
            releaseTag: envelope.Run.SelectedReleaseTag,
            releaseCommit: envelope.ReleaseCommit,
            selectedSkillId: envelope.SelectedSkillId,
            policyFingerprint: envelope.PolicyFingerprint));

        Assert.False(result.CanResume);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPRUN407");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPRUN408");
    }

    [Fact]
    public void ResumeValidatorRejectsTerminalRunState()
    {
        var envelope = CreateEnvelope(runState: AdapterRunState.Completed);

        var result = WorkflowResumeValidator.Validate(envelope, new WorkflowResumeRequest(
            workspaceId: envelope.WorkspaceId,
            releaseTag: envelope.Run.SelectedReleaseTag,
            releaseCommit: envelope.ReleaseCommit,
            selectedSkillId: envelope.SelectedSkillId,
            policyFingerprint: envelope.PolicyFingerprint,
            activeDocumentPath: "C:/repo/Program.cs",
            currentSnapshotCapturedAtUtc: envelope.ContextSnapshot!.Provenance.CapturedAtUtc.AddMinutes(1)));

        Assert.False(result.CanResume);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPRUN406");
    }

    [Fact]
    public void ResumeServiceLoadsValidStatePausesActiveRunAndPersistsResumeControl()
    {
        var envelope = CreateEnvelope(runState: AdapterRunState.Running);
        WorkflowStore.Save(tempRoot, envelope);

        var result = WorkflowResumeService.Resume(
            tempRoot,
            new WorkflowResumeRequest(
                workspaceId: envelope.WorkspaceId,
                releaseTag: envelope.Run.SelectedReleaseTag,
                releaseCommit: envelope.ReleaseCommit,
                selectedSkillId: envelope.SelectedSkillId,
                policyFingerprint: envelope.PolicyFingerprint,
                activeDocumentPath: "C:/repo/Program.cs",
                currentSnapshotCapturedAtUtc: envelope.ContextSnapshot!.Provenance.CapturedAtUtc.AddMinutes(1)),
            reason: "resume after restart");

        Assert.True(result.CanResume, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
        Assert.NotNull(result.Envelope);
        Assert.Equal(AdapterRunState.Paused, result.Envelope!.Run.State);
        var control = Assert.Single(result.Envelope.Run.Controls);
        Assert.Equal(RunControlAction.Resume, control.Action);
        Assert.Equal("resume after restart", control.Reason);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPRUN410");

        var reloaded = WorkflowStore.Load(tempRoot);
        Assert.False(reloaded.HasErrors);
        Assert.NotNull(reloaded.Envelope);
        Assert.Equal(AdapterRunState.Paused, reloaded.Envelope!.Run.State);
        Assert.Equal(RunControlAction.Resume, Assert.Single(reloaded.Envelope.Run.Controls).Action);
    }

    [Fact]
    public void ResumeServiceBlocksWhenValidationFails()
    {
        var envelope = CreateEnvelope(policyFingerprint: "policy-a");
        WorkflowStore.Save(tempRoot, envelope);

        var result = WorkflowResumeService.Resume(
            tempRoot,
            new WorkflowResumeRequest(
                workspaceId: envelope.WorkspaceId,
                releaseTag: envelope.Run.SelectedReleaseTag,
                releaseCommit: envelope.ReleaseCommit,
                selectedSkillId: envelope.SelectedSkillId,
                policyFingerprint: "policy-b",
                activeDocumentPath: "C:/repo/Program.cs",
                currentSnapshotCapturedAtUtc: envelope.ContextSnapshot!.Provenance.CapturedAtUtc.AddMinutes(1)));

        Assert.False(result.CanResume);
        Assert.Null(result.Envelope);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPRUN405");

        var reloaded = WorkflowStore.Load(tempRoot);
        Assert.False(reloaded.HasErrors);
        Assert.NotNull(reloaded.Envelope);
        Assert.Equal(AdapterRunState.Running, reloaded.Envelope!.Run.State);
        Assert.Empty(reloaded.Envelope.Run.Controls);
    }

    [Fact]
    public void ResumeServicePropagatesLoadFailures()
    {
        var activePath = Path.Combine(tempRoot, "active-run.json");
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(activePath, """
        {
          "WorkspaceId": "workspace-a",
          "Run": {
            "RunId": "run-1",
            "SelectedReleaseTag": "v1.0.0",
            "AdapterSchemaVersion": 1,
            "ExecutionMode": 0,
            "State": 2,
            "SchemaVersion": 1,
            "Tasks": [],
            "Capabilities": [],
            "Controls": []
          },
          "SchemaVersion": 999,
          "SavedAtUtc": "2026-10-01T00:00:00Z"
        }
        """);

        var result = WorkflowResumeService.Resume(
            tempRoot,
            new WorkflowResumeRequest(
                workspaceId: "workspace-a",
                releaseTag: "v1.0.0"));

        Assert.False(result.CanResume);
        Assert.Null(result.Envelope);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPRUN310");
    }

    [Fact]
    public void ResumeServiceKeepsFailedRunStateWhileRecordingResumeControl()
    {
        var envelope = CreateEnvelope(runState: AdapterRunState.Failed);
        WorkflowStore.Save(tempRoot, envelope);

        var result = WorkflowResumeService.Resume(
            tempRoot,
            new WorkflowResumeRequest(
                workspaceId: envelope.WorkspaceId,
                releaseTag: envelope.Run.SelectedReleaseTag,
                releaseCommit: envelope.ReleaseCommit,
                selectedSkillId: envelope.SelectedSkillId,
                policyFingerprint: envelope.PolicyFingerprint,
                activeDocumentPath: "C:/repo/Program.cs",
                currentSnapshotCapturedAtUtc: envelope.ContextSnapshot!.Provenance.CapturedAtUtc.AddMinutes(1)));

        Assert.True(result.CanResume, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
        Assert.NotNull(result.Envelope);
        Assert.Equal(AdapterRunState.Failed, result.Envelope!.Run.State);
        Assert.Equal(RunControlAction.Resume, Assert.Single(result.Envelope.Run.Controls).Action);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static WorkflowPersistenceEnvelope CreateEnvelope(string? policyFingerprint = "policy-a", AdapterRunState runState = AdapterRunState.Running)
    {
        var provenance = new ContextProvenance("test", DateTimeOffset.UtcNow);
        var context = new ContextCaptureSnapshot(
            provenance,
            new SolutionContextSnapshot(ContextValueState.Available, provenance, "Solution", "C:/repo/TheKameleon.Superpowers.slnx"),
            activeDocument: new DocumentContextSnapshot(
                ContextValueState.Available,
                provenance,
                "C:/repo/Program.cs",
                "Program.cs",
                isOpen: true,
                isDirty: true,
                new CapturedTextValue(ContextValueState.Available, "class Program { }", 17, 17)),
            diagnostics: Array.Empty<ContextCaptureDiagnostic>());

        var run = new AdapterRunRecord(
            runId: "run-1",
            selectedReleaseTag: "v1.0.0",
            adapterSchemaVersion: 1,
            executionMode: ExecutionMode.Guided,
            state: runState);

        return new WorkflowPersistenceEnvelope(
            workspaceId: "workspace-a",
            run: run,
            contextSnapshot: context,
            selectedSkillId: "Plan",
            releaseCommit: "abc123",
            policyFingerprint: policyFingerprint);
    }
}
