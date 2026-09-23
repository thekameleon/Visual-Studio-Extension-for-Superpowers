using System;
using System.Collections.Generic;
using System.Linq;

namespace TheKameleon.Superpowers.Skills.Context;

/// <summary>
/// Pure parsing helpers for bounded, read-only Git status/branch/recent-commit output.
/// Extracted so the parsing logic can be unit tested without invoking the git CLI or a real repository.
/// </summary>
public static class GitStatusParser
{
    /// <summary>
    /// Parses the first line of <c>git status --porcelain=v1 --branch</c> output
    /// (e.g. <c>## main...origin/main [ahead 1]</c>) to extract the current branch name.
    /// Returns <see langword="null"/> when no branch header line is present (e.g. detached HEAD
    /// without upstream, or empty output).
    /// </summary>
    public static string? ParseBranch(string? branchStatusLine)
    {
        if (string.IsNullOrWhiteSpace(branchStatusLine) || !branchStatusLine.StartsWith("##", StringComparison.Ordinal))
        {
            return null;
        }

        var header = branchStatusLine[2..].Trim();
        if (header.StartsWith("HEAD (no branch)", StringComparison.Ordinal) || header.Length == 0)
        {
            return null;
        }

        var separatorIndex = header.IndexOf("...", StringComparison.Ordinal);
        var branch = separatorIndex >= 0 ? header[..separatorIndex] : header;
        var spaceIndex = branch.IndexOf(' ');
        return spaceIndex >= 0 ? branch[..spaceIndex] : branch;
    }

    /// <summary>
    /// Summarizes bounded porcelain status output into a short, human-readable line
    /// (e.g. "3 files changed (2 modified, 1 untracked)"), without including full diff content.
    /// </summary>
    public static string SummarizeStatus(IReadOnlyList<string> statusLines)
    {
        ArgumentNullException.ThrowIfNull(statusLines);

        var entries = statusLines
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("##", StringComparison.Ordinal))
            .ToArray();

        if (entries.Length == 0)
        {
            return "Working tree clean.";
        }

        var modified = entries.Count(line => line.Length > 0 && (line[0] == 'M' || (line.Length > 1 && line[1] == 'M')));
        var added = entries.Count(line => line.Length > 0 && (line[0] == 'A' || (line.Length > 1 && line[1] == 'A')));
        var deleted = entries.Count(line => line.Length > 0 && (line[0] == 'D' || (line.Length > 1 && line[1] == 'D')));
        var untracked = entries.Count(line => line.StartsWith("??", StringComparison.Ordinal));
        var other = entries.Length - modified - added - deleted - untracked;

        var parts = new List<string>();
        if (modified > 0)
        {
            parts.Add($"{modified} modified");
        }

        if (added > 0)
        {
            parts.Add($"{added} added");
        }

        if (deleted > 0)
        {
            parts.Add($"{deleted} deleted");
        }

        if (untracked > 0)
        {
            parts.Add($"{untracked} untracked");
        }

        if (other > 0)
        {
            parts.Add($"{other} other");
        }

        return $"{entries.Length} file(s) changed ({string.Join(", ", parts)}).";
    }

    /// <summary>
    /// Trims a bounded list of "%h %s" formatted recent-commit lines, removing empty entries
    /// and enforcing a maximum count so history capture stays bounded.
    /// </summary>
    public static IReadOnlyList<string> ParseRecentCommits(IReadOnlyList<string> commitLines, int maxCommits = 5)
    {
        ArgumentNullException.ThrowIfNull(commitLines);
        if (maxCommits <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCommits), "Maximum commit count must be greater than zero.");
        }

        return commitLines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim())
            .Take(maxCommits)
            .ToArray();
    }
}
