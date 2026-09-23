using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Settings;

public sealed record CustomCommandAllowlistEntry
{
    [JsonConstructor]
    public CustomCommandAllowlistEntry(
        string commandId,
        string executablePath,
        IReadOnlyList<string>? allowedArguments = null)
    {
        if (string.IsNullOrWhiteSpace(commandId))
        {
            throw new ArgumentException("Command identifier is required.", nameof(commandId));
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path is required.", nameof(executablePath));
        }

        CommandId = commandId;
        ExecutablePath = executablePath;
        AllowedArguments = (allowedArguments ?? Array.Empty<string>()).ToArray();
    }

    public string CommandId { get; }

    public string ExecutablePath { get; }

    public IReadOnlyList<string> AllowedArguments { get; }
}
