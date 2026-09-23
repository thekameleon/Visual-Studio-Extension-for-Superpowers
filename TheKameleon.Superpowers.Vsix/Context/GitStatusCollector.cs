using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TheKameleon.Superpowers.Core.Contracts.Context;
using TheKameleon.Superpowers.Skills.Context;

namespace TheKameleon.Superpowers.Vsix.Context;

/// <summary>
/// Bounded, read-only Git status/branch/recent-commit collector. Invokes the public <c>git</c> CLI
/// (no network access, no repository mutation) with a short timeout and truthfully reports
/// "unavailable" when git is not installed, the workspace is not a repository, or the process
/// fails/times out.
/// </summary>
public static class GitStatusCollector
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(5);
    private const int MaxRecentCommits = 5;

    public static async Task<GitStatusContextSnapshot> CaptureAsync(
        string? workingDirectory,
        ContextProvenance provenance,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provenance);

        if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
        {
            return Unavailable(provenance, "No solution directory is available for Git status capture.");
        }

        try
        {
            var statusOutput = await RunGitAsync(workingDirectory, "status --porcelain=v1 --branch", cancellationToken).ConfigureAwait(false);
            if (statusOutput is null)
            {
                return Unavailable(provenance, "Git is not installed, the workspace is not a repository, or the status command timed out.");
            }

            var statusLines = statusOutput.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
            var branch = GitStatusParser.ParseBranch(statusLines.FirstOrDefault());
            var statusSummaryText = GitStatusParser.SummarizeStatus(statusLines.Skip(1).ToArray());

            var logOutput = await RunGitAsync(workingDirectory, $"log -n {MaxRecentCommits} --pretty=%h %s", cancellationToken).ConfigureAwait(false);
            var recentCommits = logOutput is null
                ? Array.Empty<string>()
                : GitStatusParser.ParseRecentCommits(logOutput.Split('\n'), MaxRecentCommits);

            return new GitStatusContextSnapshot(
                ContextValueState.Available,
                provenance,
                branch,
                new CapturedTextValue(ContextValueState.Available, statusSummaryText),
                recentCommits);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Unavailable(provenance, $"Git status capture failed: {exception.Message}");
        }
    }

    private static GitStatusContextSnapshot Unavailable(ContextProvenance provenance, string detail)
    {
        return new GitStatusContextSnapshot(
            ContextValueState.Unavailable,
            provenance,
            null,
            new CapturedTextValue(ContextValueState.Unavailable, null, detail: detail));
    }

    private static async Task<string?> RunGitAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo;
        try
        {
            startInfo = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };
        }
        catch (Exception)
        {
            return null;
        }

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();

        try
        {
            if (!process.Start())
            {
                return null;
            }
        }
        catch (Exception)
        {
            // git is not installed or not on PATH; report as unavailable rather than throwing.
            return null;
        }

        var readOutputTask = process.StandardOutput.ReadToEndAsync();

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(CommandTimeout);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return null;
        }

        output.Append(await readOutputTask.ConfigureAwait(false));

        return process.ExitCode == 0 ? output.ToString() : null;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // Best-effort cleanup only; a failure here must not surface as an unhandled exception.
        }
    }
}
