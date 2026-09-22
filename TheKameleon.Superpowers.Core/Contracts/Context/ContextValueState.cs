namespace TheKameleon.Superpowers.Core.Contracts.Context;

public enum ContextValueState
{
    Available = 0,
    Unavailable = 1,
    Redacted = 2,
    Truncated = 3,
    Stale = 4,
    Partial = 5,
    Manual = 6
}
