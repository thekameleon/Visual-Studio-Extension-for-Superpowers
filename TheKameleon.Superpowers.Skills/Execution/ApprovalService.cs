using TheKameleon.Superpowers.Core.Contracts.Runs;

namespace TheKameleon.Superpowers.Skills.Execution;

/// <summary>
/// Pure, in-memory-state-transition service for exact-action approval, rejection and revocation.
/// Every approval is scoped to one exact action description (e.g. a specific build target, a
/// specific file edit, a specific allowlisted command invocation with its resolved arguments) so
/// that an approval for one action can never be silently reused to authorize a different one.
/// </summary>
public static class ApprovalService
{
    /// <summary>
    /// Creates a new pending approval request for an exact action.
    /// </summary>
    public static ApprovalRecord Request(string approvalId, ActionSideEffectKind sideEffectKind, string exactActionDescription, DateTimeOffset requestedAtUtc)
    {
        return new ApprovalRecord(approvalId, sideEffectKind, exactActionDescription, ApprovalOutcome.Pending, requestedAtUtc);
    }

    /// <summary>
    /// Approves a pending request. Fails if the request is not currently pending.
    /// </summary>
    public static ApprovalTransitionResult Approve(ApprovalRecord approval, DateTimeOffset decidedAtUtc, string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(approval);

        if (approval.Outcome != ApprovalOutcome.Pending)
        {
            return ApprovalTransitionResult.Failure(approval, $"Approval '{approval.ApprovalId}' cannot be approved from state '{approval.Outcome}'.");
        }

        return ApprovalTransitionResult.Success(new ApprovalRecord(
            approval.ApprovalId,
            approval.SideEffectKind,
            approval.ExactActionDescription,
            ApprovalOutcome.Approved,
            approval.RequestedAtUtc,
            decidedAtUtc,
            reason));
    }

    /// <summary>
    /// Rejects a pending request. Fails if the request is not currently pending.
    /// </summary>
    public static ApprovalTransitionResult Reject(ApprovalRecord approval, DateTimeOffset decidedAtUtc, string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(approval);

        if (approval.Outcome != ApprovalOutcome.Pending)
        {
            return ApprovalTransitionResult.Failure(approval, $"Approval '{approval.ApprovalId}' cannot be rejected from state '{approval.Outcome}'.");
        }

        return ApprovalTransitionResult.Success(new ApprovalRecord(
            approval.ApprovalId,
            approval.SideEffectKind,
            approval.ExactActionDescription,
            ApprovalOutcome.Rejected,
            approval.RequestedAtUtc,
            decidedAtUtc,
            reason));
    }

    /// <summary>
    /// Revokes a previously approved request, for example when the underlying trust basis
    /// changes mid-run. Fails if the request was not previously approved.
    /// </summary>
    public static ApprovalTransitionResult Revoke(ApprovalRecord approval, DateTimeOffset decidedAtUtc, string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(approval);

        if (approval.Outcome != ApprovalOutcome.Approved)
        {
            return ApprovalTransitionResult.Failure(approval, $"Approval '{approval.ApprovalId}' cannot be revoked from state '{approval.Outcome}'.");
        }

        return ApprovalTransitionResult.Success(new ApprovalRecord(
            approval.ApprovalId,
            approval.SideEffectKind,
            approval.ExactActionDescription,
            ApprovalOutcome.Revoked,
            approval.RequestedAtUtc,
            decidedAtUtc,
            reason));
    }

    /// <summary>
    /// Determines whether an approval currently authorizes proceeding with its exact action.
    /// An approval only authorizes execution while it is Approved; Pending, Rejected and Revoked
    /// never authorize execution.
    /// </summary>
    public static bool IsCurrentlyAuthorized(ApprovalRecord approval)
    {
        ArgumentNullException.ThrowIfNull(approval);
        return approval.Outcome == ApprovalOutcome.Approved;
    }
}

/// <summary>
/// Result of attempting an approval state transition: either the updated record, or a failure
/// reason describing why the transition was rejected.
/// </summary>
public sealed class ApprovalTransitionResult
{
    private ApprovalTransitionResult(bool succeeded, ApprovalRecord record, string? failureReason)
    {
        Succeeded = succeeded;
        Record = record;
        FailureReason = failureReason;
    }

    public bool Succeeded { get; }

    public ApprovalRecord Record { get; }

    public string? FailureReason { get; }

    public static ApprovalTransitionResult Success(ApprovalRecord record) => new(true, record, null);

    public static ApprovalTransitionResult Failure(ApprovalRecord record, string reason) => new(false, record, reason);
}
