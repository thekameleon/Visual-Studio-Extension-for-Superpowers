using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record ApprovalRecord
{
    [JsonConstructor]
    public ApprovalRecord(
        string approvalId,
        ActionSideEffectKind sideEffectKind,
        string exactActionDescription,
        ApprovalOutcome outcome,
        DateTimeOffset requestedAtUtc,
        DateTimeOffset? decidedAtUtc = null,
        string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(approvalId))
        {
            throw new ArgumentException("Approval identifier is required.", nameof(approvalId));
        }

        if (!Enum.IsDefined(sideEffectKind))
        {
            throw new ArgumentOutOfRangeException(nameof(sideEffectKind), "Side-effect kind is invalid.");
        }

        if (string.IsNullOrWhiteSpace(exactActionDescription))
        {
            throw new ArgumentException("Exact action description is required.", nameof(exactActionDescription));
        }

        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), "Approval outcome is invalid.");
        }

        ApprovalId = approvalId;
        SideEffectKind = sideEffectKind;
        ExactActionDescription = exactActionDescription;
        Outcome = outcome;
        RequestedAtUtc = requestedAtUtc;
        DecidedAtUtc = decidedAtUtc;
        Reason = reason;
    }

    public string ApprovalId { get; }

    public ActionSideEffectKind SideEffectKind { get; }

    public string ExactActionDescription { get; }

    public ApprovalOutcome Outcome { get; }

    public DateTimeOffset RequestedAtUtc { get; }

    public DateTimeOffset? DecidedAtUtc { get; }

    public string? Reason { get; }
}
