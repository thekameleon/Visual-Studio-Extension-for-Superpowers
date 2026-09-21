namespace TheKameleon.Superpowers.Bridge.Contracts;

public sealed class SemanticTargetInfo
{
    public string Kind { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? FilePath { get; set; }

    public int StartLine { get; set; }

    public int StartColumn { get; set; }
}
