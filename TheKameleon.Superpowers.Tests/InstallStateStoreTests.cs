using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Tests;

public sealed class InstallStateStoreTests : IDisposable
{
    private readonly TempProfile profile = new();

    [Fact]
    public void PathsFollowTheVisualStudioLocations()
    {
        var paths = new ProfilePaths(@"C:\Users\me", @"C:\Users\me\AppData\Local");

        Assert.Equal(@"C:\Users\me\.copilot\skills", paths.SkillsRoot);
        Assert.Equal(@"C:\Users\me\.github\agents\superpowers.agent.md", paths.AgentFile);
        Assert.Equal(@"C:\Users\me\copilot-instructions.md", paths.UserInstructionsFile);
        Assert.Equal(@"C:\Users\me\AppData\Local\TheKameleon.Superpowers\install-state.json", paths.StateFile);
        Assert.Equal(new[] { @"C:\Users\me\.claude\skills", @"C:\Users\me\.agents\skills" }, paths.OtherPersonalSkillRoots);
    }

    [Fact]
    public void MissingStateLoadsAsEmpty()
    {
        var load = new InstallStateStore(profile.Paths).Load();

        Assert.Equal(InstallStateStatus.Missing, load.Status);
        Assert.Null(load.State.Release);
        Assert.Empty(load.State.Skills);
    }

    [Fact]
    public void SavedStateRoundTrips()
    {
        var store = new InstallStateStore(profile.Paths);
        var state = InstallState.Empty with
        {
            Release = new InstalledRelease("v6.4.1", "abc", "bundled"),
            Skills = new[] { new InstalledSkill("brainstorming", new Dictionary<string, string> { ["SKILL.md"] = "ff" }) },
            AgentFile = new InstalledAgentFile("aa", 1),
            AlwaysOn = new AlwaysOnState(true, true),
        };

        store.Save(state);
        var load = store.Load();

        Assert.Equal(InstallStateStatus.Loaded, load.Status);
        Assert.Equal(state.Release, load.State.Release);
        Assert.Equal("ff", Assert.Single(load.State.Skills).FileHashes["SKILL.md"]);
        Assert.Equal(state.AgentFile, load.State.AgentFile);
        Assert.Equal(state.AlwaysOn, load.State.AlwaysOn);
        Assert.Empty(Directory.GetFiles(profile.Paths.StateDirectory, "*.tmp"));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{\"schemaVersion\": 99}")]
    [InlineData("{\"schemaVersion\": 1, \"skills\": null}")]
    public void CorruptStateIsReportedNotTreatedAsEmpty(string content)
    {
        Directory.CreateDirectory(profile.Paths.StateDirectory);
        File.WriteAllText(profile.Paths.StateFile, content);

        Assert.Equal(InstallStateStatus.Corrupt, new InstallStateStore(profile.Paths).Load().Status);
    }

    [Fact]
    public void LockIsExclusiveAcrossThreads()
    {
        using (InstallLock.Acquire(TimeSpan.FromSeconds(5)))
        {
            var other = Task.Run(() => Assert.Throws<TimeoutException>(() => InstallLock.Acquire(TimeSpan.FromMilliseconds(100))));
            other.GetAwaiter().GetResult();
        }

        using var again = InstallLock.Acquire(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ContentHashIsLowercaseSha256()
    {
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", ContentHash.Of("abc"u8.ToArray()));
    }

    public void Dispose() => profile.Dispose();
}
