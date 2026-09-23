using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TheKameleon.Superpowers.Vsix.Execution;

/// <summary>
/// Bounded, scoped process runner shared by build/test/custom-command execution paths. Enforces a
/// caller-supplied timeout in addition to caller cancellation, bounds captured output size, and
/// never throws for expected failure modes (missing executable, timeout, non-zero exit) so that
/// callers can produce honest evidence rather than unhandled exceptions.
/// </summary>
public static class ScopedProcessRunner
{
    public const int DefaultMaxOutputCharacters = 20000;

    public static async Task<ScopedProcessResult> RunAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        int maxOutputCharacters = DefaultMaxOutputCharacters)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required.", nameof(fileName));
        }

        ProcessStartInfo startInfo;
        try
        {
            startInfo = new ProcessStartInfo(fileName, arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
        }
        catch (Exception exception)
        {
            return ScopedProcessResult.Failed($"Process could not be configured: {exception.Message}");
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                return ScopedProcessResult.Failed("Process failed to start.");
            }
        }
        catch (Exception exception)
        {
            return ScopedProcessResult.Failed($"Process could not be started: {exception.Message}");
        }

        var readOutputTask = process.StandardOutput.ReadToEndAsync();
        var readErrorTask = process.StandardError.ReadToEndAsync();

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw;
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return ScopedProcessResult.TimedOut(timeout);
        }

        var stdOut = Bound(await readOutputTask.ConfigureAwait(false), maxOutputCharacters);
        var stdErr = Bound(await readErrorTask.ConfigureAwait(false), maxOutputCharacters);

        return ScopedProcessResult.Completed(process.ExitCode, stdOut, stdErr);
    }

    private static string Bound(string text, int maxOutputCharacters)
    {
        if (text.Length <= maxOutputCharacters)
        {
            return text;
        }

        return text.Substring(0, maxOutputCharacters) + $"{Environment.NewLine}... [truncated at {maxOutputCharacters} characters]";
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

/// <summary>
/// Outcome of a bounded scoped process invocation: either it completed (with an exit code and
/// captured output), timed out, or could not be started/failed to run at all.
/// </summary>
public sealed class ScopedProcessResult
{
    private ScopedProcessResult(
        ScopedProcessOutcome outcome,
        int? exitCode,
        string? standardOutput,
        string? standardError,
        string? failureDetail,
        TimeSpan? timeout)
    {
        Outcome = outcome;
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
        FailureDetail = failureDetail;
        Timeout = timeout;
    }

    public ScopedProcessOutcome Outcome { get; }

    public int? ExitCode { get; }

    public string? StandardOutput { get; }

    public string? StandardError { get; }

    public string? FailureDetail { get; }

    public TimeSpan? Timeout { get; }

    public bool Succeeded => Outcome == ScopedProcessOutcome.Completed && ExitCode == 0;

    public static ScopedProcessResult Completed(int exitCode, string standardOutput, string standardError) =>
        new(ScopedProcessOutcome.Completed, exitCode, standardOutput, standardError, null, null);

    public static ScopedProcessResult TimedOut(TimeSpan timeout) =>
        new(ScopedProcessOutcome.TimedOut, null, null, null, $"Process exceeded the {timeout.TotalSeconds:0}s timeout and was terminated.", timeout);

    public static ScopedProcessResult Failed(string detail) =>
        new(ScopedProcessOutcome.Failed, null, null, null, detail, null);
}

public enum ScopedProcessOutcome
{
    Completed = 0,
    TimedOut = 1,
    Failed = 2
}
