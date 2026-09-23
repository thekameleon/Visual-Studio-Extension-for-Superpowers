using System;
using System.Threading;
using System.Threading.Tasks;
using TheKameleon.Superpowers.Core.Contracts.Runs;

namespace TheKameleon.Superpowers.Vsix.Execution;

/// <summary>
/// Scoped build runner. Invokes <c>dotnet build</c> against exactly one caller-supplied project
/// or solution path, with an enforced timeout and cancellation, and reports the outcome as
/// evidence rather than assuming success.
/// </summary>
public static class ScopedBuildRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    public static async Task<AdapterEvidenceRecord> RunAsync(
        string evidenceId,
        string projectOrSolutionPath,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(evidenceId))
        {
            throw new ArgumentException("Evidence identifier is required.", nameof(evidenceId));
        }

        if (string.IsNullOrWhiteSpace(projectOrSolutionPath))
        {
            throw new ArgumentException("Project or solution path is required.", nameof(projectOrSolutionPath));
        }

        var workingDirectory = System.IO.Path.GetDirectoryName(projectOrSolutionPath) ?? System.IO.Directory.GetCurrentDirectory();
        var quotedPath = $"\"{projectOrSolutionPath}\"";

        ScopedProcessResult result;
        try
        {
            result = await ScopedProcessRunner.RunAsync(
                "dotnet",
                $"build {quotedPath} --nologo",
                workingDirectory,
                timeout ?? DefaultTimeout,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new AdapterEvidenceRecord(
                evidenceId,
                "BuildExecution",
                AdapterEvidenceState.Blocked,
                DateTimeOffset.UtcNow,
                detail: "Build was canceled before it completed.");
        }

        return result.Outcome switch
        {
            ScopedProcessOutcome.Completed when result.Succeeded => new AdapterEvidenceRecord(
                evidenceId,
                "BuildExecution",
                AdapterEvidenceState.Observed,
                DateTimeOffset.UtcNow,
                detail: $"Build succeeded (exit code 0). Output:{Environment.NewLine}{result.StandardOutput}"),

            ScopedProcessOutcome.Completed => new AdapterEvidenceRecord(
                evidenceId,
                "BuildExecution",
                AdapterEvidenceState.Observed,
                DateTimeOffset.UtcNow,
                detail: $"Build failed (exit code {result.ExitCode}). Output:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}{result.StandardError}"),

            ScopedProcessOutcome.TimedOut => new AdapterEvidenceRecord(
                evidenceId,
                "BuildExecution",
                AdapterEvidenceState.Stale,
                DateTimeOffset.UtcNow,
                detail: result.FailureDetail),

            _ => new AdapterEvidenceRecord(
                evidenceId,
                "BuildExecution",
                AdapterEvidenceState.Blocked,
                DateTimeOffset.UtcNow,
                detail: result.FailureDetail)
        };
    }
}
