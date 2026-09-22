namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public enum RetryDisposition
{
    Allowed = 0,
    BlockedInvalidState = 1,
    BlockedDuplicateSideEffect = 2
}
