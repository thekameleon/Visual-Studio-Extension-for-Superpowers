using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Core.Contracts.Runs;
using TheKameleon.Superpowers.Core.Contracts.Settings;
using TheKameleon.Superpowers.Skills.Catalog;
using TheKameleon.Superpowers.Skills.Composition;

namespace TheKameleon.Superpowers.Tests;

public sealed class SkillCompositionCoordinatorTests
{
    [Fact]
    public void ComposePlanBuildsOrderedStepsAndAcceptedTasks()
    {
        var release = CreateRelease();
        var run = CreateRun();
        var acceptedTasks = new[]
        {
            new AcceptedPlanTask("plan-1", "Gather requirements", 1),
            new AcceptedPlanTask("plan-2", "Write implementation plan", 2)
        };
        var capabilities = new[]
        {
            new CapabilitySnapshot("copilot-handoff", CapabilityAvailability.Available, HandoffFallbackKind.None, detail: "Direct handoff available")
        };

        var result = SkillCompositionCoordinator.ComposePlan(release, run, acceptedTasks, capabilities);

        Assert.False(result.HasErrors);
        Assert.NotNull(result.Composition);
        Assert.Equal("Plan", result.Composition.EntryPoint);
        Assert.Equal(2, result.Composition.Steps.Count);
        Assert.Equal("skills/brainstorming/SKILL.md", result.Composition.Steps[0].Skill.RelativePath);
        Assert.Equal("skills/writing-plans/SKILL.md", result.Composition.Steps[1].Skill.RelativePath);
        Assert.Equal(2, result.Composition.AcceptedTasks.Count);
        Assert.Equal(AdapterRunState.Running, result.Run.State);
        Assert.NotNull(result.Run.Composition);
        Assert.Single(result.Run.Capabilities);
    }

    [Fact]
    public void ComposePlanWaitsForManualHandoffWhenCapabilityIsManualOnly()
    {
        var release = CreateRelease();
        var run = CreateRun();
        var capabilities = new[]
        {
            new CapabilitySnapshot("copilot-handoff", CapabilityAvailability.ManualOnly, HandoffFallbackKind.PreviewCopy, detail: "Preview/copy required")
        };

        var result = SkillCompositionCoordinator.ComposePlan(release, run, Array.Empty<AcceptedPlanTask>(), capabilities);

        Assert.False(result.HasErrors);
        Assert.NotNull(result.Composition);
        Assert.NotNull(result.Composition.Handoff);
        Assert.Equal(AdapterRunState.AwaitingHandoff, result.Run.State);
        Assert.Equal(HandoffFallbackKind.PreviewCopy, result.Composition.Handoff!.FallbackKind);
    }

    [Fact]
    public void ComposePlanReportsMissingSkillReference()
    {
        var release = CreateRelease(planMetadata: new PlanEntryPointMetadata(
            "Plan",
            "https://github.com/obra/superpowers",
            "v6.4.1",
            new[]
            {
                new PlanCompositionStep(1, "skills/missing/SKILL.md", "Missing")
            }));

        var result = SkillCompositionCoordinator.ComposePlan(release, CreateRun(), Array.Empty<AcceptedPlanTask>(), Array.Empty<CapabilitySnapshot>());

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPRUN103");
    }

    [Fact]
    public void ComposePlanReportsDuplicateAcceptedTaskIdentifiers()
    {
        var acceptedTasks = new[]
        {
            new AcceptedPlanTask("plan-1", "First", 1),
            new AcceptedPlanTask("plan-1", "Duplicate", 2)
        };

        var result = SkillCompositionCoordinator.ComposePlan(CreateRelease(), CreateRun(), acceptedTasks, Array.Empty<CapabilitySnapshot>());

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPRUN104");
    }

    [Fact]
    public void ComposePlanUsesManualFallbackWhenCapabilitySnapshotIsMissing()
    {
        var result = SkillCompositionCoordinator.ComposePlan(CreateRelease(), CreateRun(), Array.Empty<AcceptedPlanTask>(), Array.Empty<CapabilitySnapshot>());

        Assert.False(result.HasErrors);
        Assert.NotNull(result.Composition);
        Assert.NotNull(result.Composition.Handoff);
        Assert.Equal(CapabilityAvailability.ManualOnly, result.Composition.Handoff!.Availability);
        Assert.Equal(AdapterRunState.AwaitingHandoff, result.Run.State);
    }

    private static LoadedCatalogRelease CreateRelease(PlanEntryPointMetadata? planMetadata = null)
    {
        var skills = new[]
        {
            CreateSkill("brainstorming", "skills/brainstorming/SKILL.md"),
            CreateSkill("writing-plans", "skills/writing-plans/SKILL.md")
        };

        return new LoadedCatalogRelease(
            "v6.4.1",
            "commit",
            "MIT",
            new AdapterManifest(1, Array.Empty<AdapterManifestAction>(), Array.Empty<ParseDiagnostic>()),
            planMetadata ?? new PlanEntryPointMetadata(
                "Plan",
                "https://github.com/obra/superpowers",
                "v6.4.1",
                new[]
                {
                    new PlanCompositionStep(1, "skills/brainstorming/SKILL.md", "Gather requirements"),
                    new PlanCompositionStep(2, "skills/writing-plans/SKILL.md", "Draft plan")
                }),
            skills,
            Array.Empty<string>(),
            Array.Empty<ParseDiagnostic>());
    }

    private static DiscoveredSkillEntry CreateSkill(string skillId, string relativePath)
    {
        return new DiscoveredSkillEntry(
            skillId,
            relativePath,
            $"zip:v6.4.1:{relativePath}",
            DiscoverySourceKind.Packaged,
            DiscoveryTrustState.Implicit,
            new ParsedSkillDocument(skillId, $"{skillId} description", string.Empty, Array.Empty<SkillReference>(), Array.Empty<ParseDiagnostic>()),
            Array.Empty<ParseDiagnostic>());
    }

    private static AdapterRunRecord CreateRun()
    {
        return new AdapterRunRecord(
            runId: "run-1",
            selectedReleaseTag: "v6.4.1",
            adapterSchemaVersion: 1,
            executionMode: ExecutionMode.Guided,
            state: AdapterRunState.Created);
    }

    [Theory]
    [InlineData("Execute")]
    [InlineData("Debug")]
    [InlineData("TDD")]
    [InlineData("Review")]
    [InlineData("Verify")]
    [InlineData("Refactor")]
    [InlineData("Finish")]
    public void ComposeEntryPointResolvesEachWiredEntryPoint(string entryPointId)
    {
        var skill = CreateSkill(entryPointId, $"skills/{entryPointId.ToLowerInvariant()}/SKILL.md");
        var entryPointMetadata = new PlanEntryPointMetadata(
            entryPointId,
            "https://github.com/obra/superpowers",
            "v6.4.1",
            new[]
            {
                new PlanCompositionStep(1, skill.RelativePath, "Step purpose")
            });

        var release = new LoadedCatalogRelease(
            "v6.4.1",
            "commit",
            "MIT",
            new AdapterManifest(1, Array.Empty<AdapterManifestAction>(), Array.Empty<ParseDiagnostic>()),
            null,
            new[] { skill },
            Array.Empty<string>(),
            Array.Empty<ParseDiagnostic>(),
            new Dictionary<string, PlanEntryPointMetadata> { [entryPointId] = entryPointMetadata });

        var run = CreateRun();
        var result = SkillCompositionCoordinator.ComposeEntryPoint(release, run, entryPointId, Array.Empty<AcceptedPlanTask>(), Array.Empty<CapabilitySnapshot>());

        Assert.False(result.HasErrors);
        Assert.NotNull(result.Composition);
        Assert.Equal(entryPointId, result.Composition!.EntryPoint);
        Assert.Single(result.Composition.Steps);
    }

    [Fact]
    public void ComposeEntryPointReportsMissingMetadataForUnknownEntryPoint()
    {
        var release = CreateRelease();
        var run = CreateRun();

        var result = SkillCompositionCoordinator.ComposeEntryPoint(release, run, "Unknown", Array.Empty<AcceptedPlanTask>(), Array.Empty<CapabilitySnapshot>());

        Assert.True(result.HasErrors);
        Assert.Null(result.Composition);
    }
}
