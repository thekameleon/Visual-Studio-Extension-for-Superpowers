using System.Text.Json.Serialization;
using TheKameleon.Superpowers.Core.Contracts.Catalog;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record AdapterTaskRecord
{
    [JsonConstructor]
    public AdapterTaskRecord(
        string taskId,
        string title,
        AdapterTaskState state,
        IReadOnlyList<AdapterTaskEvidenceRequirement>? requiredEvidence = null,
        IReadOnlyList<AdapterEvidenceRecord>? evidence = null,
        IReadOnlyList<ParseDiagnostic>? diagnostics = null,
        IReadOnlyList<ActionAttemptRecord>? attempts = null)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new ArgumentException("Task identifier is required.", nameof(taskId));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Task title is required.", nameof(title));
        }

        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), "Task state is invalid.");
        }

        TaskId = taskId;
        Title = title;
        State = state;
        RequiredEvidence = (requiredEvidence ?? Array.Empty<AdapterTaskEvidenceRequirement>()).ToArray();
        Evidence = (evidence ?? Array.Empty<AdapterEvidenceRecord>()).ToArray();
        Diagnostics = (diagnostics ?? Array.Empty<ParseDiagnostic>()).ToArray();
        Attempts = (attempts ?? Array.Empty<ActionAttemptRecord>()).OrderBy(attempt => attempt.AttemptNumber).ToArray();
    }

    public string TaskId { get; }

    public string Title { get; }

    public AdapterTaskState State { get; }

    public IReadOnlyList<AdapterTaskEvidenceRequirement> RequiredEvidence { get; }

    public IReadOnlyList<AdapterEvidenceRecord> Evidence { get; }

    public IReadOnlyList<ParseDiagnostic> Diagnostics { get; }

    public IReadOnlyList<ActionAttemptRecord> Attempts { get; }

    public bool IsEvidenceGateSatisfied => AdapterEvidenceGateEvaluator.Evaluate(this).IsSatisfied;
}
