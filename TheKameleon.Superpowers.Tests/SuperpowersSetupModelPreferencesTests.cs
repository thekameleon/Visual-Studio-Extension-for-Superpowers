using TheKameleon.Superpowers.Skills.Bootstrap;
using TheKameleon.Superpowers.Skills.Install;
using TheKameleon.Superpowers.Skills.Models;
using TheKameleon.Superpowers.Skills.Setup;

namespace TheKameleon.Superpowers.Tests;

public sealed class SuperpowersSetupModelPreferencesTests
{
    [Fact]
    public void InstallWritesAFunctionAgentFileForEachConfiguredPreference()
    {
        using var profile = new TempProfile();
        var setup = new SuperpowersSetup(profile.Paths);
        var skill = TestSupport.Skill("brainstorming");
        var preferences = ModelPreferences.Empty with
        {
            Preferences = new[] { new ModelPreference(SuperpowersFunction.Review, "Claude Opus 5.5") },
        };

        var result = setup.Install(
            new InstalledRelease("v1.0.0", "commit", "bundled"),
            new SkillArchiveReadResult(new[] { skill }, Array.Empty<string>()),
            overwriteEdited: false,
            preferences);

        Assert.Single(result.State.FunctionAgentFiles);
        Assert.Equal("Review", result.State.FunctionAgentFiles[0].Function);
        var path = AgentFileWriter.FunctionAgentFilePath(profile.Paths, SuperpowersFunction.Review);
        Assert.Contains("model: Claude Opus 5.5", File.ReadAllText(path));
    }
}
