using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Tests;

public sealed class SkillInstallerTests : IDisposable
{
    private readonly TempProfile profile = new();

    private SkillInstaller Installer => new(profile.Paths);

    private string SkillFile(string name, string relative = "SKILL.md") => Path.Combine(profile.Paths.SkillsRoot, name, relative);

    [Fact]
    public void InstallsFreshSkillsWithSupportingFiles()
    {
        var outcome = Installer.Install(new[] { TestSupport.Skill("alpha", ("scripts/run.sh", "echo")), TestSupport.Skill("beta") }, Array.Empty<InstalledSkill>(), overwriteEdited: false);

        Assert.True(outcome.Succeeded);
        Assert.Empty(outcome.Issues);
        Assert.Equal(new[] { "alpha", "beta" }, outcome.Installed.Select(skill => skill.Name));
        Assert.Equal("echo", File.ReadAllText(SkillFile("alpha", Path.Combine("scripts", "run.sh"))));
        Assert.Equal(2, outcome.Installed[0].FileHashes.Count);
        Assert.False(Directory.Exists(profile.Paths.StagingRoot) && Directory.EnumerateFileSystemEntries(profile.Paths.StagingRoot).Any());
    }

    [Fact]
    public void ReplacesOwnedUnmodifiedSkill()
    {
        var first = Installer.Install(new[] { TestSupport.Skill("alpha") }, Array.Empty<InstalledSkill>(), false);
        var updated = TestSupport.SkillWithMarkdown("alpha", "---\nname: alpha\ndescription: new\n---\nv2\n");

        var second = Installer.Install(new[] { updated }, first.Installed, false);

        Assert.True(second.Succeeded);
        Assert.Contains("v2", File.ReadAllText(SkillFile("alpha")));
        Assert.Empty(Directory.GetDirectories(profile.Paths.SkillsRoot, "*.superpowers-backup-*"));
    }

    [Fact]
    public void LeavesSkillsItDidNotInstallUntouched()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SkillFile("alpha"))!);
        File.WriteAllText(SkillFile("alpha"), "mine");

        var outcome = Installer.Install(new[] { TestSupport.Skill("alpha") }, Array.Empty<InstalledSkill>(), overwriteEdited: true);

        Assert.True(outcome.Succeeded);
        Assert.Empty(outcome.Installed);
        Assert.Equal(SkillIssueKind.Conflict, Assert.Single(outcome.Issues).Kind);
        Assert.Equal("mine", File.ReadAllText(SkillFile("alpha")));
    }

    [Fact]
    public void KeepsEditedOwnedSkillUnlessOverwriteIsChosen()
    {
        var first = Installer.Install(new[] { TestSupport.Skill("alpha") }, Array.Empty<InstalledSkill>(), false);
        File.AppendAllText(SkillFile("alpha"), "my edit");
        var update = TestSupport.SkillWithMarkdown("alpha", "---\nname: alpha\ndescription: new\n---\nv2\n");

        var kept = Installer.Install(new[] { update }, first.Installed, overwriteEdited: false);

        Assert.Equal(SkillIssueKind.EditedKept, Assert.Single(kept.Issues).Kind);
        Assert.Contains("my edit", File.ReadAllText(SkillFile("alpha")));
        Assert.Same(first.Installed[0], Assert.Single(kept.Installed));

        var overwritten = Installer.Install(new[] { update }, first.Installed, overwriteEdited: true);

        Assert.Equal(SkillIssueKind.EditedOverwritten, Assert.Single(overwritten.Issues).Kind);
        Assert.Contains("v2", File.ReadAllText(SkillFile("alpha")));
    }

    [Fact]
    public void RemovesOwnedSkillsAbsentFromTheNewRelease()
    {
        var first = Installer.Install(new[] { TestSupport.Skill("alpha"), TestSupport.Skill("beta") }, Array.Empty<InstalledSkill>(), false);

        var second = Installer.Install(new[] { TestSupport.Skill("alpha") }, first.Installed, false);

        Assert.True(second.Succeeded);
        Assert.False(Directory.Exists(Path.Combine(profile.Paths.SkillsRoot, "beta")));
    }

    [Fact]
    public void KeepsEditedSkillAbsentFromTheNewRelease()
    {
        var first = Installer.Install(new[] { TestSupport.Skill("alpha"), TestSupport.Skill("beta") }, Array.Empty<InstalledSkill>(), false);
        File.AppendAllText(SkillFile("beta"), "edit");

        var second = Installer.Install(new[] { TestSupport.Skill("alpha") }, first.Installed, false);

        Assert.Equal(SkillIssueKind.RemovedEditedKept, Assert.Single(second.Issues).Kind);
        Assert.True(File.Exists(SkillFile("beta")));
    }

    [Fact]
    public void SkipsInvalidSkill()
    {
        var invalid = TestSupport.SkillWithMarkdown("alpha", "---\nname: wrong\ndescription: x\n---\n");

        var outcome = Installer.Install(new[] { invalid, TestSupport.Skill("beta") }, Array.Empty<InstalledSkill>(), false);

        Assert.Equal(SkillIssueKind.Invalid, Assert.Single(outcome.Issues).Kind);
        Assert.Equal("beta", Assert.Single(outcome.Installed).Name);
        Assert.False(Directory.Exists(Path.Combine(profile.Paths.SkillsRoot, "alpha")));
    }

    [Fact]
    public void RollsBackEverySwapWhenOneFails()
    {
        var first = Installer.Install(new[] { TestSupport.Skill("alpha"), TestSupport.Skill("beta") }, Array.Empty<InstalledSkill>(), false);
        var alphaBefore = File.ReadAllText(SkillFile("alpha"));
        var updates = new[]
        {
            TestSupport.SkillWithMarkdown("alpha", "---\nname: alpha\ndescription: new\n---\nv2\n"),
            TestSupport.SkillWithMarkdown("beta", "---\nname: beta\ndescription: new\n---\nv2\n"),
        };

        SkillInstallOutcome outcome;
        using (new FileStream(SkillFile("beta"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            outcome = Installer.Install(updates, first.Installed, false);
        }

        Assert.False(outcome.Succeeded);
        Assert.NotNull(outcome.FailureReason);
        Assert.Equal(alphaBefore, File.ReadAllText(SkillFile("alpha")));
        Assert.Same(first.Installed, outcome.Installed);
        Assert.Empty(Directory.GetDirectories(profile.Paths.SkillsRoot, "*.superpowers-backup-*"));
    }

    [Fact]
    public void RemoveDeletesUneditedAndReportsEdited()
    {
        var first = Installer.Install(new[] { TestSupport.Skill("alpha"), TestSupport.Skill("beta") }, Array.Empty<InstalledSkill>(), false);
        File.AppendAllText(SkillFile("beta"), "edit");

        var issues = Installer.Remove(first.Installed);

        Assert.False(Directory.Exists(Path.Combine(profile.Paths.SkillsRoot, "alpha")));
        Assert.True(File.Exists(SkillFile("beta")));
        Assert.Equal("beta", Assert.Single(issues).SkillName);
    }

    [Fact]
    public void MatchesPackageDetectsIdenticalContent()
    {
        var package = TestSupport.Skill("alpha");
        Installer.Install(new[] { package }, Array.Empty<InstalledSkill>(), false);

        Assert.True(Installer.MatchesPackage(package));
        File.AppendAllText(SkillFile("alpha"), "edit");
        Assert.False(Installer.MatchesPackage(package));
    }

    public void Dispose() => profile.Dispose();
}
