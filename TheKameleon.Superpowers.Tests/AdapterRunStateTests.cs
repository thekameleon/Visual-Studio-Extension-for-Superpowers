using System.Text.Json;
using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Core.Contracts.Runs;
using TheKameleon.Superpowers.Core.Contracts.Settings;

namespace TheKameleon.Superpowers.Tests;

public sealed class AdapterRunStateTests
{
    [Fact]
    public void RunRecordDefaultsTaskListAndAcceptsValidPortableState()
    {
        var run = new AdapterRunRecord(
            runId: "run-1",
            selectedReleaseTag: "v1.0.0",
            adapterSchemaVersion: 1,
            executionMode: ExecutionMode.Guided,
            state: AdapterRunState.Created);

        Assert.Equal(AdapterRunRecord.CurrentSchemaVersion, run.SchemaVersion);
        Assert.Equal("run-1", run.RunId);
        Assert.Equal("v1.0.0", run.SelectedReleaseTag);
        Assert.Equal(ExecutionMode.Guided, run.ExecutionMode);
        Assert.Equal(AdapterRunState.Created, run.State);
        Assert.Empty(run.Tasks);
        Assert.False(run.HasEvidenceGateViolations);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsInvalidRunSchemaVersion(int schemaVersion)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new AdapterRunRecord(
            runId: "run-1",
            selectedReleaseTag: "v1.0.0",
            adapterSchemaVersion: 1,
            executionMode: ExecutionMode.Guided,
            state: AdapterRunState.Created,
            schemaVersion: schemaVersion));

        Assert.Equal("schemaVersion", exception.ParamName);
    }

    [Fact]
    public void RejectsUnknownRunState()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new AdapterRunRecord(
            runId: "run-1",
            selectedReleaseTag: "v1.0.0",
            adapterSchemaVersion: 1,
            executionMode: ExecutionMode.Guided,
            state: (AdapterRunState)99));

        Assert.Equal("state", exception.ParamName);
    }

    [Fact]
    public void EvidenceGateAllowsObservedEvidence()
    {
        var task = new AdapterTaskRecord(
            taskId: "task-1",
            title: "Verify build",
            state: AdapterTaskState.Completed,
            requiredEvidence: new[] { new AdapterTaskEvidenceRequirement("build") },
            evidence: new[]
            {
                new AdapterEvidenceRecord("build", "build-summary", AdapterEvidenceState.Observed, DateTimeOffset.UtcNow)
            });

        var result = AdapterEvidenceGateEvaluator.Evaluate(task);

        Assert.True(result.IsSatisfied);
        Assert.Empty(result.Diagnostics);
        Assert.True(task.IsEvidenceGateSatisfied);
    }

    [Fact]
    public void EvidenceGateRejectsMissingEvidenceForCompletedTask()
    {
        var task = new AdapterTaskRecord(
            taskId: "task-1",
            title: "Verify build",
            state: AdapterTaskState.Completed,
            requiredEvidence: new[] { new AdapterTaskEvidenceRequirement("build") });

        var result = AdapterEvidenceGateEvaluator.Evaluate(task);
        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.False(result.IsSatisfied);
        Assert.Equal("SPRUN001", diagnostic.Code);
        Assert.False(task.IsEvidenceGateSatisfied);
    }

    [Fact]
    public void EvidenceGateRejectsStaleEvidence()
    {
        var task = new AdapterTaskRecord(
            taskId: "task-1",
            title: "Verify build",
            state: AdapterTaskState.Completed,
            requiredEvidence: new[] { new AdapterTaskEvidenceRequirement("build") },
            evidence: new[]
            {
                new AdapterEvidenceRecord("build", "build-summary", AdapterEvidenceState.Stale, DateTimeOffset.UtcNow)
            });

        var result = AdapterEvidenceGateEvaluator.Evaluate(task);
        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.False(result.IsSatisfied);
        Assert.Equal("SPRUN004", diagnostic.Code);
    }

    [Fact]
    public void EvidenceGateRejectsImportedEvidenceWhenDirectObservationIsRequired()
    {
        var task = new AdapterTaskRecord(
            taskId: "task-1",
            title: "Verify build",
            state: AdapterTaskState.Completed,
            requiredEvidence: new[] { new AdapterTaskEvidenceRequirement("build", allowImportedEvidence: false) },
            evidence: new[]
            {
                new AdapterEvidenceRecord("build", "build-summary", AdapterEvidenceState.ImportedManual, DateTimeOffset.UtcNow)
            });

        var result = AdapterEvidenceGateEvaluator.Evaluate(task);
        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.False(result.IsSatisfied);
        Assert.Equal("SPRUN002", diagnostic.Code);
    }

    [Fact]
    public void CompletedRunReportsEvidenceGateViolations()
    {
        var run = new AdapterRunRecord(
            runId: "run-1",
            selectedReleaseTag: "v1.0.0",
            adapterSchemaVersion: 1,
            executionMode: ExecutionMode.Guided,
            state: AdapterRunState.Completed,
            tasks: new[]
            {
                new AdapterTaskRecord(
                    taskId: "task-1",
                    title: "Verify build",
                    state: AdapterTaskState.Completed,
                    requiredEvidence: new[] { new AdapterTaskEvidenceRequirement("build") })
            });

        Assert.True(run.HasEvidenceGateViolations);
    }

    [Fact]
    public void RunContractsRoundTripThroughJson()
    {
        var run = new AdapterRunRecord(
            runId: "run-1",
            selectedReleaseTag: "v1.0.0",
            adapterSchemaVersion: 2,
            executionMode: ExecutionMode.ApprovalRequired,
            state: AdapterRunState.WaitingForEvidence,
            trustBasis: "workspace-trusted",
            tasks: new[]
            {
                new AdapterTaskRecord(
                    taskId: "task-1",
                    title: "Verify build",
                    state: AdapterTaskState.WaitingForEvidence,
                    requiredEvidence: new[] { new AdapterTaskEvidenceRequirement("build", allowImportedEvidence: false) },
                    evidence: new[]
                    {
                        new AdapterEvidenceRecord("build", "build-summary", AdapterEvidenceState.Observed, DateTimeOffset.UtcNow, artifactId: "artifact-1", detail: "Build passed")
                    },
                    diagnostics: new[]
                    {
                        new ParseDiagnostic(ParseDiagnosticSeverity.Warning, "SPRUN900", "sample")
                    })
            });

        var json = JsonSerializer.Serialize(run);
        var roundTripped = JsonSerializer.Deserialize<AdapterRunRecord>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(run.SchemaVersion, roundTripped.SchemaVersion);
        Assert.Equal(run.RunId, roundTripped.RunId);
        Assert.Equal(run.SelectedReleaseTag, roundTripped.SelectedReleaseTag);
        Assert.Equal(run.AdapterSchemaVersion, roundTripped.AdapterSchemaVersion);
        Assert.Equal(run.ExecutionMode, roundTripped.ExecutionMode);
        Assert.Equal(run.State, roundTripped.State);
        Assert.Equal(run.TrustBasis, roundTripped.TrustBasis);
        var task = Assert.Single(roundTripped.Tasks);
        Assert.Equal("task-1", task.TaskId);
        Assert.Equal(AdapterTaskState.WaitingForEvidence, task.State);
        Assert.Single(task.RequiredEvidence);
        Assert.Single(task.Evidence);
        Assert.Single(task.Diagnostics);
    }

    [Fact]
    public void UnknownJsonPropertiesAreIgnoredForRunContracts()
    {
        const string json = """
        {
          "SchemaVersion": 1,
          "RunId": "run-1",
          "SelectedReleaseTag": "v1.0.0",
          "AdapterSchemaVersion": 1,
          "ExecutionMode": 0,
          "State": 0,
          "TrustBasis": "workspace-trusted",
          "Tasks": [],
          "FutureProperty": "ignored"
        }
        """;

        var deserialized = JsonSerializer.Deserialize<AdapterRunRecord>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("run-1", deserialized.RunId);
        Assert.Equal("v1.0.0", deserialized.SelectedReleaseTag);
        Assert.Equal(ExecutionMode.Guided, deserialized.ExecutionMode);
        Assert.Equal(AdapterRunState.Created, deserialized.State);
        Assert.Empty(deserialized.Tasks);
    }
}
