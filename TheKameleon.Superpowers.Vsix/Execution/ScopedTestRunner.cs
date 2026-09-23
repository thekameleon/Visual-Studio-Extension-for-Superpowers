using System;
using System.Threading;
using System.Threading.Tasks;
using TheKameleon.Superpowers.Core.Contracts.Runs;

namespace TheKameleon.Superpowers.Vsix.Execution;

/// <summary>
/// Scoped test runner. Invokes <c>dotnet test</c> against exactly one caller-supplied test
/// project path, with an enforced timeout and cancellation, and reports the outcome as evidence
/// rather than assuming success.
/// </summary>
public static class ScopedTestRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);

    public static async Task<AdapterEvidenceRecord> RunAsync(
        string evidenceId,
        string testProjectPath,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(evidenceId))
        {
            throw new ArgumentException("Evidence identifier is required.", nameof(evidenceId));
        }

        if (string.IsNullOrWhiteSpace(testProjectPath))
        {
            throw new ArgumentException("Test project path is required.", nameof(testProjectPath));
        }

        var workingDirectory = System.IO.Path.GetDirectoryName(testProjectPath) ?? System.IO.Directory.GetCurrentDirectory();
        var quotedPath = $"\"{testProjectPath}\"";

        ScopedProcessResult result;
        try
        {
            result = await ScopedProcessRunner.RunAsync(
                "dotnet",
                $"test {quotedPath} --nologo",
                workingDirectory,
                timeout ?? DefaultTimeout,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new AdapterEvidenceRecord(
                evidenceId,
                "TestExecution",
                AdapterEvidenceState.Blocked,
                DateTimeOffset.UtcNow,
                detail: "Test run was canceled before it completed.");
        }

        return result.Outcome switch
        {
            ScopedProcessOutcome.Completed when result.Succeeded => new AdapterEvidenceRecord(
                evidenceId,
                "TestExecution",
                AdapterEvidenceState.Observed,
                DateTimeOffset.UtcNow,
                detail: $"Tests passed (exit code 0). Output:{Environment.NewLine}{result.StandardOutput}"),

            ScopedProcessOutcome.Completed => new AdapterEvidenceRecord(
                evidenceId,
                "TestExecution",
                AdapterEvidenceState.Observed,
                DateTimeOffset.UtcNow,
                detail: $"Tests failed or run reported a non-zero exit code ({result.ExitCode}). Output:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}{result.StandardError}"),

            ScopedProcessOutcome.TimedOut => new AdapterEvidenceRecord(
                evidenceId,
                "TestExecution",
                AdapterEvidenceState.Stale,
                DateTimeOffset.UtcNow,
                detail: result.FailureDetail),

            _ => new AdapterEvidenceRecord(
                evidenceId,
                "TestExecution",
                AdapterEvidenceState.Blocked,
                DateTimeOffset.UtcNow,
                detail: result.FailureDetail)
        };
    }
}
