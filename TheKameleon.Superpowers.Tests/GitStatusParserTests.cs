using System;
using TheKameleon.Superpowers.Skills.Context;

namespace TheKameleon.Superpowers.Tests;

public sealed class GitStatusParserTests
{
    [Theory]
    [InlineData("## main...origin/main", "main")]
    [InlineData("## main...origin/main [ahead 1]", "main")]
    [InlineData("## feature/foo", "feature/foo")]
    [InlineData("## HEAD (no branch)", null)]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("M file.txt", null)]
    public void ParseBranchExtractsBranchNameFromHeaderLine(string? line, string? expected)
    {
        Assert.Equal(expected, GitStatusParser.ParseBranch(line));
    }

    [Fact]
    public void SummarizeStatusReportsCleanWorkingTreeWhenNoEntries()
    {
        Assert.Equal("Working tree clean.", GitStatusParser.SummarizeStatus(Array.Empty<string>()));
    }

    [Fact]
    public void SummarizeStatusCountsModifiedAddedDeletedAndUntrackedEntries()
    {
        var lines = new[]
        {
            " M modified.txt",
            "A  added.txt",
            " D deleted.txt",
            "?? untracked.txt"
        };

        var summary = GitStatusParser.SummarizeStatus(lines);

        Assert.Contains("4 file(s) changed", summary);
        Assert.Contains("1 modified", summary);
        Assert.Contains("1 added", summary);
        Assert.Contains("1 deleted", summary);
        Assert.Contains("1 untracked", summary);
    }

    [Fact]
    public void SummarizeStatusIgnoresBranchHeaderLines()
    {
        var lines = new[] { "## main...origin/main", " M modified.txt" };

        var summary = GitStatusParser.SummarizeStatus(lines);

        Assert.Contains("1 file(s) changed", summary);
    }

    [Fact]
    public void ParseRecentCommitsTrimsEmptyEntriesAndBoundsCount()
    {
        var lines = new[] { "abc123 First commit", "", "def456 Second commit", "ghi789 Third", "jkl012 Fourth", "mno345 Fifth", "pqr678 Sixth" };

        var result = GitStatusParser.ParseRecentCommits(lines, maxCommits: 5);

        Assert.Equal(5, result.Count);
        Assert.Equal("abc123 First commit", result[0]);
    }

    [Fact]
    public void ParseRecentCommitsThrowsForNonPositiveMaxCommits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GitStatusParser.ParseRecentCommits(Array.Empty<string>(), 0));
    }
}
