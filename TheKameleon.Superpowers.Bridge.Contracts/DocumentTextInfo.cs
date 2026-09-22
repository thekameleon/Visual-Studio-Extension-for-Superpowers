namespace TheKameleon.Superpowers.Bridge.Contracts;

public sealed class DocumentTextInfo
{
    public string? FilePath { get; set; }

    public string? DisplayName { get; set; }

    public string? Text { get; set; }

    public int OriginalLength { get; set; }

    public int CapturedLength { get; set; }

    public bool IsOpen { get; set; }

    public bool IsDirty { get; set; }

    public bool IsPartial { get; set; }

    public bool ContainsSensitiveContent { get; set; }

    public string Detail { get; set; } = string.Empty;
}