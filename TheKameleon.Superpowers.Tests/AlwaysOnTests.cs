using System.Text;
using TheKameleon.Superpowers.Skills.Bootstrap;
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Tests;

public sealed class AlwaysOnTests : IDisposable
{
    private const string Body = "Line one\nLine two";
    private readonly TempProfile profile = new();

    [Fact]
    public void AppliesToEmptyText()
    {
        var result = AlwaysOnBlockEditor.Apply(string.Empty, Body, "\n");

        Assert.Equal(BlockEditStatus.Changed, result.Status);
        Assert.Equal(AlwaysOnBlockEditor.BeginMarker + "\nLine one\nLine two\n" + AlwaysOnBlockEditor.EndMarker + "\n", result.Text);
    }

    [Fact]
    public void AppendsAfterExistingContentAndRemovesCleanly()
    {
        const string original = "# My preferences\nUse tabs.\n";

        var applied = AlwaysOnBlockEditor.Apply(original, Body, "\n");
        var removed = AlwaysOnBlockEditor.Remove(applied.Text);

        Assert.StartsWith(original, applied.Text, StringComparison.Ordinal);
        Assert.Equal(BlockEditStatus.Changed, removed.Status);
        Assert.Equal(original, removed.Text);
    }

    [Fact]
    public void FileWithoutTrailingNewlineGainsOneAfterRoundTrip()
    {
        var applied = AlwaysOnBlockEditor.Apply("A", Body, "\n");

        Assert.Equal("A\n", AlwaysOnBlockEditor.Remove(applied.Text).Text);
    }

    [Fact]
    public void ApplyIsIdempotentAndReplacesChangedBody()
    {
        var once = AlwaysOnBlockEditor.Apply("x\n", Body, "\n").Text;

        Assert.Equal(BlockEditStatus.Unchanged, AlwaysOnBlockEditor.Apply(once, Body, "\n").Status);
        var replaced = AlwaysOnBlockEditor.Apply(once, "New body", "\n");
        Assert.Equal(BlockEditStatus.Changed, replaced.Status);
        Assert.Contains("New body", replaced.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Line one", replaced.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void PreservesCrLfLineEndings()
    {
        var result = AlwaysOnBlockEditor.Apply("A\r\n", Body, "\r\n");

        Assert.DoesNotContain("\n", result.Text.Replace("\r\n", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<!-- superpowers:begin x -->\n<!-- superpowers:begin y -->\n<!-- superpowers:end -->\n")]
    [InlineData("<!-- superpowers:end -->\n<!-- superpowers:begin x -->\n")]
    [InlineData("<!-- superpowers:begin x -->\nno end\n")]
    public void RefusesMalformedMarkers(string text)
    {
        Assert.Equal(BlockEditStatus.MalformedMarkers, AlwaysOnBlockEditor.Apply(text, Body, "\n").Status);
        Assert.Equal(BlockEditStatus.MalformedMarkers, AlwaysOnBlockEditor.Remove(text).Status);
    }

    [Fact]
    public void EnableCreatesFileAndDisableDeletesItWhenCreated()
    {
        var file = new AlwaysOnInstructionsFile(profile.Paths);

        var enabled = file.Enable(new AlwaysOnState(false, false));

        Assert.Equal(AlwaysOnStatus.Enabled, enabled.Status);
        Assert.Equal(new AlwaysOnState(true, true), enabled.State);
        Assert.True(file.IsBlockPresent());

        var disabled = file.Disable(enabled.State);

        Assert.Equal(AlwaysOnStatus.Disabled, disabled.Status);
        Assert.False(File.Exists(profile.Paths.UserInstructionsFile));
    }

    [Theory]
    [InlineData("utf-8-bom")]
    [InlineData("utf-16")]
    public void PreservesEncodingAndExistingContent(string encodingName)
    {
        Encoding encoding = encodingName == "utf-16" ? Encoding.Unicode : new UTF8Encoding(true);
        Directory.CreateDirectory(profile.Paths.UserProfile);
        File.WriteAllText(profile.Paths.UserInstructionsFile, "Keep me.\r\n", encoding);
        var original = File.ReadAllBytes(profile.Paths.UserInstructionsFile);
        var file = new AlwaysOnInstructionsFile(profile.Paths);

        var enabled = file.Enable(new AlwaysOnState(false, false));
        var afterEnable = File.ReadAllBytes(profile.Paths.UserInstructionsFile);
        file.Disable(enabled.State);

        Assert.Equal(new AlwaysOnState(true, false), enabled.State);
        Assert.Equal(encoding.GetPreamble(), afterEnable.Take(encoding.GetPreamble().Length).ToArray());
        Assert.Equal(original, File.ReadAllBytes(profile.Paths.UserInstructionsFile));
    }

    [Fact]
    public void EnableReportsMalformedFileWithoutChangingIt()
    {
        Directory.CreateDirectory(profile.Paths.UserProfile);
        File.WriteAllText(profile.Paths.UserInstructionsFile, "<!-- superpowers:end -->\n");

        var outcome = new AlwaysOnInstructionsFile(profile.Paths).Enable(new AlwaysOnState(false, false));

        Assert.Equal(AlwaysOnStatus.MalformedMarkers, outcome.Status);
        Assert.Equal("<!-- superpowers:end -->\n", File.ReadAllText(profile.Paths.UserInstructionsFile));
    }

    public void Dispose() => profile.Dispose();
}
