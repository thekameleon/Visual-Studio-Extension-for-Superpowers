using TheKameleon.Superpowers.Skills.Bootstrap;
using TheKameleon.Superpowers.Skills.Install;
using TheKameleon.Superpowers.Skills.Setup;

namespace TheKameleon.Superpowers.Tests;

public sealed class SuperpowersSetupTests : IDisposable
{
    private static readonly InstalledRelease Release = new("v1.0.0", "abc", "bundled");
    private readonly TempProfile profile = new();

    private SuperpowersSetup Setup => new(profile.Paths);

    private static SkillArchiveReadResult Source(params SkillPackage[] skills) => new(skills, Array.Empty<string>());

    [Fact]
    public void InstallWritesSkillsAgentAndState()
    {
        var result = Setup.Install(Release, Source(TestSupport.Skill("alpha"), TestSupport.Skill("beta")), overwriteEdited: false);

        Assert.Equal(SetupStatus.Succeeded, result.Status);
        Assert.True(File.Exists(Path.Combine(profile.Paths.SkillsRoot, "beta", "SKILL.md")));
        Assert.True(File.Exists(profile.Paths.AgentFile));
        var state = Setup.LoadState();
        Assert.Equal(InstallStateStatus.Loaded, state.Status);
        Assert.Equal(Release, state.State.Release);
        Assert.Equal(2, state.State.Skills.Count);
        Assert.Equal(BootstrapText.Version, state.State.AgentFile!.BootstrapVersion);
    }

    [Fact]
    public void PreviewReportsConflictsAndEdits()
    {
        Setup.Install(Release, Source(TestSupport.Skill("alpha")), false);
        File.AppendAllText(Path.Combine(profile.Paths.SkillsRoot, "alpha", "SKILL.md"), "edit");
        Directory.CreateDirectory(Path.Combine(profile.Paths.SkillsRoot, "beta"));

        var preview = Setup.Preview(new[] { TestSupport.Skill("alpha"), TestSupport.Skill("beta") });

        Assert.Equal(new[] { "beta" }, preview.Conflicts);
        Assert.Equal(new[] { "alpha" }, preview.EditedSkills);
        Assert.False(preview.AgentFileEdited);
    }

    [Fact]
    public void PartialWhenSomethingWasSkipped()
    {
        Directory.CreateDirectory(Path.Combine(profile.Paths.SkillsRoot, "beta"));

        var result = Setup.Install(Release, Source(TestSupport.Skill("alpha"), TestSupport.Skill("beta")), false);

        Assert.Equal(SetupStatus.Partial, result.Status);
        Assert.Contains(result.Messages, message => message.Contains("beta", StringComparison.Ordinal));
    }

    [Fact]
    public void InstallRefusesCorruptStateAndRepairRecovers()
    {
        var source = Source(TestSupport.Skill("alpha"));
        Setup.Install(Release, source, false);
        File.WriteAllText(profile.Paths.StateFile, "{ broken");

        Assert.Equal(SetupStatus.Failed, Setup.Install(Release, source, false).Status);

        var repaired = Setup.Repair(Release, source);

        Assert.Equal(SetupStatus.Succeeded, repaired.Status);
        Assert.Equal("alpha", Assert.Single(Setup.LoadState().State.Skills).Name);
    }

    [Fact]
    public void RepairDoesNotAdoptFoldersWithDifferentContent()
    {
        Directory.CreateDirectory(Path.Combine(profile.Paths.SkillsRoot, "alpha"));
        File.WriteAllText(Path.Combine(profile.Paths.SkillsRoot, "alpha", "SKILL.md"), "someone else's skill");

        var repaired = Setup.Repair(Release, Source(TestSupport.Skill("alpha")));

        Assert.Equal(SetupStatus.Partial, repaired.Status);
        Assert.Equal("someone else's skill", File.ReadAllText(Path.Combine(profile.Paths.SkillsRoot, "alpha", "SKILL.md")));
    }

    [Fact]
    public void RemoveDeletesEverythingOwned()
    {
        Setup.Install(Release, Source(TestSupport.Skill("alpha")), false);
        Setup.SetAlwaysOn(true);

        var result = Setup.Remove();

        Assert.Equal(SetupStatus.Succeeded, result.Status);
        Assert.False(Directory.Exists(Path.Combine(profile.Paths.SkillsRoot, "alpha")));
        Assert.False(File.Exists(profile.Paths.AgentFile));
        Assert.False(File.Exists(profile.Paths.UserInstructionsFile));
        Assert.Equal(InstallStateStatus.Missing, Setup.LoadState().Status);
    }

    [Fact]
    public void SetAlwaysOnPersistsTheSetting()
    {
        Setup.Install(Release, Source(TestSupport.Skill("alpha")), false);

        Assert.Equal(SetupStatus.Succeeded, Setup.SetAlwaysOn(true).Status);
        Assert.True(Setup.LoadState().State.AlwaysOn.Enabled);
        Assert.Equal(SetupStatus.Succeeded, Setup.SetAlwaysOn(false).Status);
        Assert.False(Setup.LoadState().State.AlwaysOn.Enabled);
    }

    [Fact]
    public void RefreshAgentRewritesOutdatedUneditedAgentFile()
    {
        Setup.Install(Release, Source(TestSupport.Skill("alpha")), false);
        File.WriteAllText(profile.Paths.AgentFile, "old");
        var store = new InstallStateStore(profile.Paths);
        var state = store.Load().State;
        store.Save(state with { AgentFile = new InstalledAgentFile(ContentHash.OfFile(profile.Paths.AgentFile), 0) });

        var result = Setup.RefreshAgent();

        Assert.Equal(SetupStatus.Succeeded, result.Status);
        Assert.Equal(AgentFileWriter.BuildContent(), File.ReadAllText(profile.Paths.AgentFile));
    }

    public void Dispose() => profile.Dispose();
}
