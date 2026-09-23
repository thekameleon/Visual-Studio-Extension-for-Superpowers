using System.Text;
using TheKameleon.Superpowers.Skills.Catalog;
using TheKameleon.Superpowers.Skills.Install;
using TheKameleon.Superpowers.Skills.Setup;

namespace TheKameleon.Superpowers.Tests;

public sealed class BundledReleaseInstallTests
{
    public static TheoryData<string> ReleaseTags()
    {
        var data = new TheoryData<string>();
        foreach (var release in BundledCatalogLoader.LoadFromDirectory(TestSupport.BundledCatalogRoot).Releases)
        {
            data.Add(release.ReleaseTag);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ReleaseTags))]
    public void EveryBundledReleaseInstallsAndEveryInstalledSkillIsValid(string tag)
    {
        using var profile = new TempProfile();
        var release = BundledCatalogLoader.LoadFromDirectory(TestSupport.BundledCatalogRoot).Releases.Single(candidate => candidate.ReleaseTag == tag);
        var source = ReleaseSkillLoader.Load(TestSupport.BundledCatalogRoot, release);

        var result = new SuperpowersSetup(profile.Paths).Install(new InstalledRelease(tag, release.ResolvedCommit, "bundled"), source, overwriteEdited: false);

        Assert.NotEqual(SetupStatus.Failed, result.Status);
        Assert.NotEmpty(result.State.Skills);
        foreach (var skill in result.State.Skills)
        {
            var markdown = File.ReadAllBytes(Path.Combine(profile.Paths.SkillsRoot, skill.Name, "SKILL.md"));
            var package = new SkillPackage(skill.Name, new Dictionary<string, byte[]> { ["SKILL.md"] = markdown });
            Assert.Empty(SkillValidator.Validate(package));
        }
    }

    [Fact]
    public void NewestReleaseInstallsAllFifteenSkillsCleanly()
    {
        using var profile = new TempProfile();
        var catalog = BundledCatalogLoader.LoadFromDirectory(TestSupport.BundledCatalogRoot);
        var release = ReleaseSelection.DefaultRelease(catalog.Releases)!;

        var result = new SuperpowersSetup(profile.Paths).Install(
            new InstalledRelease(release.ReleaseTag, release.ResolvedCommit, "bundled"),
            ReleaseSkillLoader.Load(TestSupport.BundledCatalogRoot, release),
            overwriteEdited: false);

        Assert.Equal(SetupStatus.Succeeded, result.Status);
        Assert.Equal(15, result.State.Skills.Count);
    }
}
