using TheKameleon.Superpowers.Skills.Bootstrap;
using TheKameleon.Superpowers.Skills.Install;
using TheKameleon.Superpowers.Skills.Models;
using TheKameleon.Superpowers.Skills.Setup;

namespace TheKameleon.Superpowers.Tests;

public sealed class SuperpowersSetupModelPreferencesTests
{
    [Fact]
    public void InstallWritesSubagentModelsIntoTheMainAgentOnly()
    {
        using var profile = new TempProfile();
        var setup = new SuperpowersSetup(profile.Paths);
        var skill = TestSupport.Skill("brainstorming");
        var preferences = ModelPreferences.Empty with
        {
            Preferences = new[] { new ModelPreference(SuperpowersFunction.Review, new[] { "Claude Opus 5.5", "GPT-5.4" }) },
        };

        var result = setup.Install(
            new InstalledRelease("v1.0.0", "commit", "bundled"),
            new SkillArchiveReadResult(new[] { skill }, Array.Empty<string>()),
            overwriteEdited: false,
            preferences);

        Assert.Empty(result.State.FunctionAgentFiles);
        Assert.False(File.Exists(AgentFileWriter.FunctionAgentFilePath(profile.Paths, SuperpowersFunction.Review)));
        var agent = File.ReadAllText(profile.Paths.AgentFile);
        Assert.DoesNotContain("model:", agent);
        Assert.Contains("- Review: `Claude Opus 5.5`, `GPT-5.4`", agent);
    }
}
