using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Skills.Discovery;

namespace TheKameleon.Superpowers.Tests;

public sealed class CatalogReloadServiceTests
{
    [Fact]
    public void PreservesSelectedSkillWhenStillAvailable()
    {
        var discovery = CreateDiscovery("Plan", "Debug");
        var previous = new CatalogReloadState("Debug", Array.Empty<ActiveRunPin>());

        var result = CatalogReloadService.Reload(discovery, previous);

        Assert.False(result.HasErrors);
        Assert.Equal("Debug", result.State.SelectedSkillId);
    }

    [Fact]
    public void FallsBackWhenSelectedSkillDisappears()
    {
        var discovery = CreateDiscovery("Plan");
        var previous = new CatalogReloadState("Debug", Array.Empty<ActiveRunPin>());

        var result = CatalogReloadService.Reload(discovery, previous);

        Assert.False(result.HasErrors);
        Assert.Equal("Plan", result.State.SelectedSkillId);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT302");
    }

    [Fact]
    public void BlocksReloadWhenPinnedContentIsMissing()
    {
        var discovery = CreateDiscovery("Plan");
        var previous = new CatalogReloadState(
            "Plan",
            new[] { new ActiveRunPin("run-1", "Plan", "C:/missing/SKILL.md") });

        var result = CatalogReloadService.Reload(discovery, previous);

        Assert.True(result.HasErrors);
        Assert.Equal(previous, result.State);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT301");
    }

    [Fact]
    public void ChoosesFirstSkillWhenNoSelectionExists()
    {
        var discovery = CreateDiscovery("Plan", "Debug");

        var result = CatalogReloadService.Reload(discovery, previousState: null);

        Assert.False(result.HasErrors);
        Assert.Equal("Plan", result.State.SelectedSkillId);
    }

    [Fact]
    public void PreservesSelectedSkillAcrossCatalogUpgradeWhenStillAvailable()
    {
        var previous = new CatalogReloadState("Plan", Array.Empty<ActiveRunPin>());
        var upgradedDiscovery = CreateDiscovery("Debug", "Plan", "Review");

        var result = CatalogReloadService.Reload(upgradedDiscovery, previous);

        Assert.False(result.HasErrors);
        Assert.Equal("Plan", result.State.SelectedSkillId);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT302");
    }

    private static DiscoveryResult CreateDiscovery(params string[] skillIds)
    {
        var skills = skillIds.Select((skillId, index) => new DiscoveredSkillEntry(
            skillId,
            $"{skillId}/SKILL.md",
            $"C:/catalog/{index}/{skillId}/SKILL.md",
            DiscoverySourceKind.Packaged,
            DiscoveryTrustState.Implicit,
            new ParsedSkillDocument(skillId, $"{skillId} description", string.Empty, Array.Empty<SkillReference>(), Array.Empty<ParseDiagnostic>()),
            Array.Empty<ParseDiagnostic>())).ToArray();

        return new DiscoveryResult(skills, Array.Empty<ParseDiagnostic>());
    }
}
