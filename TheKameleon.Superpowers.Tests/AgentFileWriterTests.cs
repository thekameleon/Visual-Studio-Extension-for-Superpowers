using TheKameleon.Superpowers.Skills.Bootstrap;
using TheKameleon.Superpowers.Skills.Install;

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
    [InlineData("Subagents and the Task tool are not available")]
    [InlineData("using-superpowers")]
    public void BootstrapContainsEachTranslationRule(string fragment)
    {
        Assert.Contains(fragment, BootstrapText.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentFileHasFrontMatterAndNoToolsList()
    {
        var content = AgentFileWriter.BuildContent();

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
        Assert.Equal(AgentFileWriter.BuildContent(), File.ReadAllText(profile.Paths.AgentFile));
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

    public void Dispose() => profile.Dispose();
}
