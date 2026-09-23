using TheKameleon.Superpowers.Core.Contracts.Settings;

namespace TheKameleon.Superpowers.Skills.Execution;

/// <summary>
/// Pure validator for custom-command execution requests against the user's explicit allowlist.
/// A command may only run if its command identifier matches an allowlist entry, its resolved
/// executable path matches that entry's executable path exactly, and every requested argument is
/// present in that entry's allowed-argument set. This is an allowlist, not a sandbox: it narrows
/// what can run, but does not itself contain what runs.
/// </summary>
public static class CommandAllowlistValidator
{
    public static CommandAllowlistValidationResult Validate(
        IReadOnlyList<CustomCommandAllowlistEntry> allowlist,
        string commandId,
        string executablePath,
        IReadOnlyList<string> requestedArguments)
    {
        ArgumentNullException.ThrowIfNull(allowlist);
        ArgumentNullException.ThrowIfNull(requestedArguments);

        if (string.IsNullOrWhiteSpace(commandId))
        {
            return CommandAllowlistValidationResult.Denied("SPCMD401", "Command identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return CommandAllowlistValidationResult.Denied("SPCMD402", "Executable path is required.");
        }

        var entry = allowlist.FirstOrDefault(candidate => string.Equals(candidate.CommandId, commandId, StringComparison.Ordinal));
        if (entry is null)
        {
            return CommandAllowlistValidationResult.Denied("SPCMD403", $"Command '{commandId}' is not present in the allowlist.");
        }

        if (!string.Equals(entry.ExecutablePath, executablePath, StringComparison.OrdinalIgnoreCase))
        {
            return CommandAllowlistValidationResult.Denied("SPCMD404", $"Command '{commandId}' resolved to executable '{executablePath}', which does not match the allowlisted executable '{entry.ExecutablePath}'.");
        }

        foreach (var argument in requestedArguments)
        {
            if (!entry.AllowedArguments.Contains(argument, StringComparer.Ordinal))
            {
                return CommandAllowlistValidationResult.Denied("SPCMD405", $"Argument '{argument}' is not present in the allowlisted argument set for command '{commandId}'.");
            }
        }

        return CommandAllowlistValidationResult.Allowed();
    }
}

public sealed class CommandAllowlistValidationResult
{
    private CommandAllowlistValidationResult(bool isAllowed, string? diagnosticCode, string? message)
    {
        IsAllowed = isAllowed;
        DiagnosticCode = diagnosticCode;
        Message = message;
    }

    public bool IsAllowed { get; }

    public string? DiagnosticCode { get; }

    public string? Message { get; }

    public static CommandAllowlistValidationResult Allowed() => new(true, null, null);

    public static CommandAllowlistValidationResult Denied(string diagnosticCode, string message) => new(false, diagnosticCode, message);
}
