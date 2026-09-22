namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record WorkflowHistorySnapshot(
    IReadOnlyList<WorkflowHistoryEntry> Entries);
