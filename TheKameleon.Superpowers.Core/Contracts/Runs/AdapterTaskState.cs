namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public enum AdapterTaskState
{
    Pending = 0,
    InProgress = 1,
    WaitingForEvidence = 2,
    Blocked = 3,
    Completed = 4,
    Failed = 5,
    Canceled = 6
}
