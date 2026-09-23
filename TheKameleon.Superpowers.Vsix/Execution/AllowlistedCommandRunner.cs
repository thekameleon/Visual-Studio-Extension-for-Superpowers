using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TheKameleon.Superpowers.Core.Contracts.Runs;
using TheKameleon.Superpowers.Core.Contracts.Settings;
using TheKameleon.Superpowers.Skills.Execution;

namespace TheKameleon.Superpowers.Vsix.Execution;

/// <summary>
/// Executes explicitly allowlisted custom commands. Every invocation is re-validated against the
/// caller-supplied allowlist immediately before execution (denying anything not present), runs
/// through the bounded/timeout-enforced <see cref="ScopedProcessRunner"/>, and reports truthful
/// evidence rather than assuming success.
/// </summary>
public static class AllowlistedCommandRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

    public static async Task<AdapterEvidenceRecord> RunAsync(
        string evidenceId,
        IReadOnlyList<CustomCommandAllowlistEntry> allowlist,
        string commandId,
        string executablePath,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(evidenceId))
        {
            throw new ArgumentException("Evidence identifier is required.", nameof(evidenceId));
        }

        var validation = CommandAllowlistValidator.Validate(allowlist, commandId, executablePath, arguments);
        if (!validation.IsAllowed)
        {
            return new AdapterEvidenceRecord(
                evidenceId,
                "CustomCommandExecution",
                AdapterEvidenceState.Blocked,
                DateTimeOffset.UtcNow,
                detail: $"{validation.DiagnosticCode}: {validation.Message}");
        }

        var argumentLine = string.Join(" ", arguments.Select(argument => $"\"{argument}\""));

        ScopedProcessResult result;
        try
        {
            result = await ScopedProcessRunner.RunAsync(
                executablePath,
                argumentLine,
                workingDirectory,
                timeout ?? DefaultTimeout,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new AdapterEvidenceRecord(
                evidenceId,
                "CustomCommandExecution",
                AdapterEvidenceState.Blocked,
                DateTimeOffset.UtcNow,
                detail: $"Command '{commandId}' was canceled before it completed.");
        }

        return result.Outcome switch
        {
            ScopedProcessOutcome.Completed => new AdapterEvidenceRecord(
                evidenceId,
                "CustomCommandExecution",
                AdapterEvidenceState.Observed,
                DateTimeOffset.UtcNow,
                detail: $"Command '{commandId}' exited with code {result.ExitCode}. Output:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}{result.StandardError}"),

            ScopedProcessOutcome.TimedOut => new AdapterEvidenceRecord(
                evidenceId,
                "CustomCommandExecution",
                AdapterEvidenceState.Stale,
                DateTimeOffset.UtcNow,
                detail: result.FailureDetail),

            _ => new AdapterEvidenceRecord(
                evidenceId,
                "CustomCommandExecution",
                AdapterEvidenceState.Blocked,
                DateTimeOffset.UtcNow,
                detail: result.FailureDetail)
        };
    }
}
