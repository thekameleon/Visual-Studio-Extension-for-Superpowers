using TheKameleon.Superpowers.Core.Contracts.Runs;
using TheKameleon.Superpowers.Skills.Execution;

namespace TheKameleon.Superpowers.Tests;

public sealed class ApprovalServiceTests
{
    [Fact]
    public void RequestCreatesPendingApproval()
    {
        var approval = ApprovalService.Request("approval-1", ActionSideEffectKind.BuildExecution, "Build MyProject.csproj", DateTimeOffset.UtcNow);

        Assert.Equal(ApprovalOutcome.Pending, approval.Outcome);
        Assert.False(ApprovalService.IsCurrentlyAuthorized(approval));
    }

    [Fact]
    public void ApprovePendingRequestSucceedsAndAuthorizes()
    {
        var approval = ApprovalService.Request("approval-1", ActionSideEffectKind.BuildExecution, "Build MyProject.csproj", DateTimeOffset.UtcNow);

        var result = ApprovalService.Approve(approval, DateTimeOffset.UtcNow, "user confirmed");

        Assert.True(result.Succeeded);
        Assert.Equal(ApprovalOutcome.Approved, result.Record.Outcome);
        Assert.True(ApprovalService.IsCurrentlyAuthorized(result.Record));
    }

    [Fact]
    public void RejectPendingRequestSucceedsAndDoesNotAuthorize()
    {
        var approval = ApprovalService.Request("approval-1", ActionSideEffectKind.BuildExecution, "Build MyProject.csproj", DateTimeOffset.UtcNow);

        var result = ApprovalService.Reject(approval, DateTimeOffset.UtcNow, "user declined");

        Assert.True(result.Succeeded);
        Assert.Equal(ApprovalOutcome.Rejected, result.Record.Outcome);
        Assert.False(ApprovalService.IsCurrentlyAuthorized(result.Record));
    }

    [Fact]
    public void CannotApproveAlreadyApprovedRequest()
    {
        var approval = ApprovalService.Request("approval-1", ActionSideEffectKind.BuildExecution, "Build MyProject.csproj", DateTimeOffset.UtcNow);
        var approved = ApprovalService.Approve(approval, DateTimeOffset.UtcNow).Record;

        var result = ApprovalService.Approve(approved, DateTimeOffset.UtcNow);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public void RevokeApprovedRequestSucceedsAndRemovesAuthorization()
    {
        var approval = ApprovalService.Request("approval-1", ActionSideEffectKind.BuildExecution, "Build MyProject.csproj", DateTimeOffset.UtcNow);
        var approved = ApprovalService.Approve(approval, DateTimeOffset.UtcNow).Record;

        var result = ApprovalService.Revoke(approved, DateTimeOffset.UtcNow, "trust revoked mid-run");

        Assert.True(result.Succeeded);
        Assert.Equal(ApprovalOutcome.Revoked, result.Record.Outcome);
        Assert.False(ApprovalService.IsCurrentlyAuthorized(result.Record));
    }

    [Fact]
    public void CannotRevokePendingRequest()
    {
        var approval = ApprovalService.Request("approval-1", ActionSideEffectKind.BuildExecution, "Build MyProject.csproj", DateTimeOffset.UtcNow);

        var result = ApprovalService.Revoke(approval, DateTimeOffset.UtcNow);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void CannotRejectAlreadyRejectedRequest()
    {
        var approval = ApprovalService.Request("approval-1", ActionSideEffectKind.BuildExecution, "Build MyProject.csproj", DateTimeOffset.UtcNow);
        var rejected = ApprovalService.Reject(approval, DateTimeOffset.UtcNow).Record;

        var result = ApprovalService.Reject(rejected, DateTimeOffset.UtcNow);

        Assert.False(result.Succeeded);
    }
}
