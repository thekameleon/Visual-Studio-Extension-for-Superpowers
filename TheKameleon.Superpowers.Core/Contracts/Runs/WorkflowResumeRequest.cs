using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record WorkflowResumeRequest
{
    [JsonConstructor]
    public WorkflowResumeRequest(
        string workspaceId,
        string releaseTag,
        string? releaseCommit = null,
        string? selectedSkillId = null,
        string? policyFingerprint = null,
        string? activeDocumentPath = null,
        DateTimeOffset? currentSnapshotCapturedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            throw new ArgumentException("Workspace identifier is required.", nameof(workspaceId));
        }

        if (string.IsNullOrWhiteSpace(releaseTag))
        {
            throw new ArgumentException("Release tag is required.", nameof(releaseTag));
        }

        WorkspaceId = workspaceId;
        ReleaseTag = releaseTag;
        ReleaseCommit = releaseCommit;
        SelectedSkillId = selectedSkillId;
        PolicyFingerprint = policyFingerprint;
        ActiveDocumentPath = activeDocumentPath;
        CurrentSnapshotCapturedAtUtc = currentSnapshotCapturedAtUtc;
    }

    public string WorkspaceId { get; }

    public string ReleaseTag { get; }

    public string? ReleaseCommit { get; }

    public string? SelectedSkillId { get; }

    public string? PolicyFingerprint { get; }

    public string? ActiveDocumentPath { get; }

    public DateTimeOffset? CurrentSnapshotCapturedAtUtc { get; }
}
