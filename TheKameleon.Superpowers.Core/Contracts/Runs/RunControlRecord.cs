using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record RunControlRecord
{
    [JsonConstructor]
    public RunControlRecord(RunControlAction action, DateTimeOffset requestedAtUtc, string? reason = null)
    {
        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action), "Run control action is invalid.");
        }

        Action = action;
        RequestedAtUtc = requestedAtUtc;
        Reason = reason;
    }

    public RunControlAction Action { get; }

    public DateTimeOffset RequestedAtUtc { get; }

    public string? Reason { get; }
}
