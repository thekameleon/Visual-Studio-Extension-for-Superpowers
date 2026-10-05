using TheKameleon.Superpowers.Skills.Bootstrap;
using TheKameleon.Superpowers.Skills.Install;
using TheKameleon.Superpowers.Skills.Models;

namespace TheKameleon.Superpowers.Tests;

public sealed class AgentFileWriterTests : IDisposable
{
    private readonly TempProfile profile = new();

    private AgentFileWriter Writer => new(profile.Paths);

    [Theory]
    [InlineData("get_file")]
    [InlineData("ENTIRE file")]
    [InlineData("keep calling get_file from the next line")]
    [InlineData("Mentioning it is not enough")]
    [InlineData("`superpowers:<name>` means the skill named `<name>`")]
    [InlineData("Do not skip a step")]
    [InlineData("interactive Agent mode (Autopilot off)")]
    [InlineData("say so and do the work sequentially yourself")]
    [InlineData("copilot -p")]
    [InlineData("--allow-all-tools")]
    [InlineData("Verify the subagent's work yourself")]
    [InlineData("using-superpowers")]
    [InlineData("confirm the file exists on disk")]
    [InlineData("Keep each terminal command on one line")]
    [InlineData("\"No test is available\" is not a failing test")]
    [InlineData("No git repository")]
    [InlineData("say the review was not independent")]
    public void BootstrapContainsEachTranslationRule(string fragment)
    {
        Assert.Contains(fragment, BootstrapText.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentFileHasFrontMatterAndNoToolsList()
    {
        var content = AgentFileWriter.BuildContent(SuperpowersFunction.General);

        Assert.StartsWith("---\nname: Superpowers\ndescription: ", content, StringComparison.Ordinal);
        Assert.DoesNotContain("tools:", content, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", content, StringComparison.Ordinal);
        Assert.Contains(BootstrapText.Body.ReplaceLineEndings("\n"), content, StringComparison.Ordinal);
    }

    [Fact]
    public void WritesThenReportsUpToDate()
    {
        var first = Writer.Write(null, overwriteEdited: false);

        Assert.Equal(AgentFileStatus.Written, first.Status);
        Assert.Equal(BootstrapText.Version, first.Agent!.BootstrapVersion);
        Assert.Equal(first.Agent.Sha256, ContentHash.OfFile(profile.Paths.AgentFile));
        Assert.Equal(AgentFileStatus.UpToDate, Writer.Write(first.Agent, false).Status);
    }

    [Fact]
    public void KeepsEditedOrForeignAgentFileUnlessOverwriteIsChosen()
    {
        var first = Writer.Write(null, false);
        File.AppendAllText(profile.Paths.AgentFile, "my edit");

        Assert.True(Writer.IsEdited(first.Agent));
        Assert.Equal(AgentFileStatus.EditedKept, Writer.Write(first.Agent, overwriteEdited: false).Status);
        Assert.Contains("my edit", File.ReadAllText(profile.Paths.AgentFile));

        Assert.Equal(AgentFileStatus.Written, Writer.Write(first.Agent, overwriteEdited: true).Status);
        Assert.DoesNotContain("my edit", File.ReadAllText(profile.Paths.AgentFile));
    }

    [Fact]
    public void RefreshesOutdatedUneditedFile()
    {
        Directory.CreateDirectory(profile.Paths.AgentsRoot);
        File.WriteAllText(profile.Paths.AgentFile, "old bootstrap");
        var recorded = new InstalledAgentFile(ContentHash.OfFile(profile.Paths.AgentFile), 0);

        var outcome = Writer.Write(recorded, overwriteEdited: false);

        Assert.Equal(AgentFileStatus.Written, outcome.Status);
        Assert.Equal(AgentFileWriter.BuildContent(SuperpowersFunction.General), File.ReadAllText(profile.Paths.AgentFile));
    }

    [Fact]
    public void RemoveDeletesOnlyUneditedFile()
    {
        var first = Writer.Write(null, false);
        Assert.Equal(AgentFileStatus.Removed, Writer.Remove(first.Agent).Status);
        Assert.False(File.Exists(profile.Paths.AgentFile));

        var second = Writer.Write(null, false);
        File.AppendAllText(profile.Paths.AgentFile, "edit");
        Assert.Equal(AgentFileStatus.EditedNotRemoved, Writer.Remove(second.Agent).Status);
        Assert.True(File.Exists(profile.Paths.AgentFile));
        using var empty = new TempProfile();
        Assert.Equal(AgentFileStatus.Missing, new AgentFileWriter(empty.Paths).Remove(null).Status);
    }

    [Fact]
    public void BuildContentNeverWritesAModelField()
    {
        var preferences = new ModelPreferences { Preferences = new[] { new ModelPreference(SuperpowersFunction.General, new[] { "Claude Opus 5.5" }) } };

        var content = AgentFileWriter.BuildContent(SuperpowersFunction.General, preferences);

        Assert.DoesNotContain("model:", content);
    }

    [Fact]
    public void BuildContentOmitsSubagentModelListWhenNoneAreSet()
    {
        var content = AgentFileWriter.BuildContent(SuperpowersFunction.General);

        Assert.DoesNotContain("Suggested sub-agent models", content);
    }

    [Fact]
    public void BuildContentRunsSubagentsInVisibleWindowsByDefault()
    {
        var content = AgentFileWriter.BuildContent(SuperpowersFunction.General);

        Assert.Contains("Start-Process pwsh", content);
        Assert.Contains("-Wait", content);
        Assert.Contains("Tee-Object", content);
        Assert.Contains("[Console]::InputEncoding=[Console]::OutputEncoding=[Text.Encoding]::UTF8", content);
    }

    [Fact]
    public void BuildContentRunsSubagentsInTheBackgroundWhenWindowsAreHidden()
    {
        var content = AgentFileWriter.BuildContent(SuperpowersFunction.General, new ModelPreferences { ShowCliWindows = false });

        Assert.DoesNotContain("Start-Process pwsh", content);
        Assert.Contains("Run sub-agents directly in your terminal", content);
    }

    [Fact]
    public void BuildContentListsSubagentModelsPerStep()
    {
        var preferences = new ModelPreferences
        {
            Preferences = new[]
            {
                new ModelPreference(SuperpowersFunction.Review, new[] { "Claude Opus 5.5", "GPT-5.4" }),
                new ModelPreference(SuperpowersFunction.General, new[] { "GPT-5 mini" }),
            },
        };

        var content = AgentFileWriter.BuildContent(SuperpowersFunction.General, preferences);

        Assert.Contains("Suggested sub-agent models", content);
        Assert.Contains("- Review: `Claude Opus 5.5`, `GPT-5.4`", content);
        Assert.Contains("- Any other step: `GPT-5 mini`", content);
    }

    [Fact]
    public void BuildContentGivesEachNonGeneralFunctionADistinctNameAndDescription()
    {
        var general = AgentFileWriter.BuildContent(SuperpowersFunction.General);
        var review = AgentFileWriter.BuildContent(SuperpowersFunction.Review);

        Assert.Contains("name: Superpowers (Review)", review);
        Assert.NotEqual(general, review);
        Assert.DoesNotContain("name: Superpowers\n", review);
    }

    public void Dispose() => profile.Dispose();
}
