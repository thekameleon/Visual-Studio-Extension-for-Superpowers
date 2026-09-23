using TheKameleon.Superpowers.Skills.Bootstrap;
using TheKameleon.Superpowers.Skills.Install;
using TheKameleon.Superpowers.Skills.Models;
using TheKameleon.Superpowers.Skills.Setup;
using TheKameleon.Superpowers.Skills.Status;

namespace TheKameleon.Superpowers.Tests;

public sealed class StatusProbeTests : IDisposable
{
    private readonly TempProfile profile = new();

    private IReadOnlyList<StatusCheck> Run(string? logDirectory = null) => new StatusProbe(profile.Paths).Run(logDirectory);

    private void Install() => new SuperpowersSetup(profile.Paths).Install(
        new InstalledRelease("v1.0.0", "abc", "bundled"),
        new SkillArchiveReadResult(new[] { TestSupport.Skill("alpha") }, Array.Empty<string>()),
        overwriteEdited: false);

    [Fact]
    public void NotInstalledIsAWarning()
    {
        var check = Assert.Single(Run());

        Assert.Equal(StatusLevel.Warning, check.Level);
        Assert.Contains("not installed", check.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HealthyInstallPasses()
    {
        Install();

        var checks = Run();

        Assert.All(checks, check => Assert.Equal(StatusLevel.Pass, check.Level));
        Assert.Contains(checks, check => check.Title == "Skills" && check.Message.Contains("v1.0.0", StringComparison.Ordinal));
        Assert.Contains(checks, check => check.Title == "Superpowers agent");
        Assert.Contains(checks, check => check.Title == "Always-on");
    }

    [Fact]
    public void MissingSkillFailsAndEditedSkillWarns()
    {
        Install();
        File.AppendAllText(Path.Combine(profile.Paths.SkillsRoot, "alpha", "SKILL.md"), "edit");
        Assert.Equal(StatusLevel.Warning, Assert.Single(Run(), check => check.Title == "Skills").Level);

        Directory.Delete(Path.Combine(profile.Paths.SkillsRoot, "alpha"), recursive: true);
        Assert.Equal(StatusLevel.Fail, Assert.Single(Run(), check => check.Title == "Skills").Level);
    }

    [Fact]
    public void MissingAgentFails()
    {
        Install();
        File.Delete(profile.Paths.AgentFile);

        Assert.Equal(StatusLevel.Fail, Assert.Single(Run(), check => check.Title == "Superpowers agent").Level);
    }

    [Fact]
    public void MissingFunctionAgentFileWarnsButHealthyInstallHasNone()
    {
        Install();

        Assert.DoesNotContain(Run(), check => check.Title == "Per-function agents");

        new SuperpowersSetup(profile.Paths).Install(
            new InstalledRelease("v1.0.0", "abc", "bundled"),
            new SkillArchiveReadResult(new[] { TestSupport.Skill("alpha") }, Array.Empty<string>()),
            overwriteEdited: false,
            new ModelPreferences { Preferences = new[] { new ModelPreference(SuperpowersFunction.Review, "OpenAI", "gpt-5") } });

        Assert.DoesNotContain(Run(), check => check.Title == "Per-function agents");

        File.Delete(Path.Combine(profile.Paths.AgentsRoot, "superpowers-review.agent.md"));

        var check = Assert.Single(Run(), check => check.Title == "Per-function agents");
        Assert.Equal(StatusLevel.Warning, check.Level);
        Assert.Contains("Review", check.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateSkillInAnotherPersonalFolderWarns()
    {
        Install();
        Directory.CreateDirectory(Path.Combine(profile.Paths.OtherPersonalSkillRoots[0], "alpha"));

        var duplicate = Assert.Single(Run(), check => check.Title == "Duplicate skills");

        Assert.Equal(StatusLevel.Warning, duplicate.Level);
        Assert.Contains("alpha", duplicate.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CorruptStateFails()
    {
        Install();
        File.WriteAllText(profile.Paths.StateFile, "{ broken");

        Assert.Equal(StatusLevel.Fail, Assert.Single(Run()).Level);
    }

    [Fact]
    public void LogDiagnosticConfirmsDiscoveryFromNewestLog()
    {
        Install();
        var logs = Path.Combine(profile.Root, "logs");
        Directory.CreateDirectory(logs);
        var old = Path.Combine(logs, "1_VSGitHubCopilot.chat.log");
        File.WriteAllText(old, "nothing");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-1));
        File.WriteAllText(Path.Combine(logs, "2_VSGitHubCopilot.chat.log"),
            $"[x] Found 15 skill files in: {profile.Paths.SkillsRoot}\n[y] Registered custom agent: {profile.Paths.AgentFile}\n");

        var check = CopilotLogDiagnostic.Diagnose(logs, profile.Paths);

        Assert.Equal(StatusLevel.Pass, check.Level);
        Assert.Contains(Run(logs), candidate => candidate.Title == CopilotLogDiagnostic.Title);
    }

    [Fact]
    public void LogDiagnosticIsUnknownNeverFail()
    {
        var logs = Path.Combine(profile.Root, "logs");
        Assert.Equal(StatusLevel.Unknown, CopilotLogDiagnostic.Diagnose(logs, profile.Paths).Level);

        Directory.CreateDirectory(logs);
        File.WriteAllText(Path.Combine(logs, "a_VSGitHubCopilot.chat.log"), "unrelated");
        Assert.Equal(StatusLevel.Unknown, CopilotLogDiagnostic.Diagnose(logs, profile.Paths).Level);
    }

    [Fact]
    public void AlwaysOnWarnsWhenBlockIsPresentButRecordSaysOff()
    {
        Install();
        new AlwaysOnInstructionsFile(profile.Paths).Enable(new AlwaysOnState(false, false));

        var check = Assert.Single(Run(), check => check.Title == "Always-on");

        Assert.Equal(StatusLevel.Warning, check.Level);
    }

    [Fact]
    public void SkillsFailsWhenNothingCouldBeInstalledDueToConflicts()
    {
        Directory.CreateDirectory(Path.Combine(profile.Paths.SkillsRoot, "alpha"));
        new SuperpowersSetup(profile.Paths).Install(
            new InstalledRelease("v1.0.0", "abc", "bundled"),
            new SkillArchiveReadResult(new[] { TestSupport.Skill("alpha") }, Array.Empty<string>()),
            overwriteEdited: false);

        var check = Assert.Single(Run(), check => check.Title == "Skills");

        Assert.Equal(StatusLevel.Fail, check.Level);
    }

    public void Dispose() => profile.Dispose();
}
