using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Skills.Discovery;

namespace TheKameleon.Superpowers.Tests;

public sealed class SkillDiscoveryServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "SuperpowersTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void DiscoversPackagedSkill()
    {
        var packaged = CreateRoot("packaged");
        WriteSkill(packaged, "plan/SKILL.md", "Plan", "Packaged plan");

        var result = SkillDiscoveryService.Discover(packaged, null, null);

        var skill = Assert.Single(result.Skills);
        Assert.Equal(DiscoverySourceKind.Packaged, skill.SourceKind);
        Assert.Equal(DiscoveryTrustState.Implicit, skill.TrustState);
        Assert.Equal("Plan", skill.SkillId);
    }

    [Fact]
    public void HigherPrecedenceUserSkillOverridesPackagedSkill()
    {
        var packaged = CreateRoot("packaged");
        var user = CreateRoot("user");
        WriteSkill(packaged, "plan/SKILL.md", "Plan", "Packaged plan");
        WriteSkill(user, "plan/SKILL.md", "Plan", "User plan");

        var result = SkillDiscoveryService.Discover(packaged, user, null);

        var skill = Assert.Single(result.Skills);
        Assert.Equal(DiscoverySourceKind.User, skill.SourceKind);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT205");
    }

    [Fact]
    public void SolutionSkillOverridesUserAndPackagedSkill()
    {
        var packaged = CreateRoot("packaged");
        var user = CreateRoot("user");
        var solution = CreateRoot("solution");
        WriteSkill(packaged, "plan/SKILL.md", "Plan", "Packaged plan");
        WriteSkill(user, "plan/SKILL.md", "Plan", "User plan");
        WriteSkill(solution, "plan/SKILL.md", "Plan", "Solution plan");

        var result = SkillDiscoveryService.Discover(packaged, user, solution);

        var skill = Assert.Single(result.Skills);
        Assert.Equal(DiscoverySourceKind.Solution, skill.SourceKind);
        Assert.Equal(DiscoveryTrustState.RequiresApproval, skill.TrustState);
        Assert.Equal("Solution plan\n", skill.Document.Body);
    }

    [Fact]
    public void MissingDiscoveryRootReportsWarning()
    {
        var result = SkillDiscoveryService.Discover(Path.Combine(root, "missing"), null, null);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT202");
    }

    [Fact]
    public void InvalidRootReportsError()
    {
        var result = SkillDiscoveryService.Discover("\0", null, null);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT201");
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private string CreateRoot(string name)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteSkill(string root, string relativePath, string name, string description)
    {
        var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, $"---\nname: {name}\ndescription: {description}\n---\n{description}\n");
    }
}
