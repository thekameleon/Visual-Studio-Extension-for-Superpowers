using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record ActionAttemptRecord
{
    [JsonConstructor]
    public ActionAttemptRecord(
        int attemptNumber,
        ActionSideEffectKind sideEffectKind,
        bool isIdempotent,
        ActionAttemptState state,
        DateTimeOffset startedAtUtc,
        DateTimeOffset? completedAtUtc = null,
        string? reason = null)
    {
        if (attemptNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptNumber), "Attempt number must be greater than zero.");
        }

        if (!Enum.IsDefined(sideEffectKind))
        {
            throw new ArgumentOutOfRangeException(nameof(sideEffectKind), "Side-effect kind is invalid.");
        }

        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), "Attempt state is invalid.");
        }

        ActionAttemptState = state;
        AttemptNumber = attemptNumber;
        SideEffectKind = sideEffectKind;
        IsIdempotent = isIdempotent;
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
        Reason = reason;
    }

    public int AttemptNumber { get; }

    public ActionSideEffectKind SideEffectKind { get; }

    public bool IsIdempotent { get; }

    public ActionAttemptState ActionAttemptState { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset? CompletedAtUtc { get; }

    public string? Reason { get; }
}
