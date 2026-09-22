namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public enum AdapterRunState
{
    Created = 0,
    AwaitingHandoff = 1,
    Running = 2,
    WaitingForEvidence = 3,
    Paused = 4,
    Completed = 5,
    Failed = 6,
    Canceled = 7
}
