namespace TheKameleon.Superpowers.Bridge.Contracts;

public sealed class CompilerDiagnosticInfo
{
    public string Id { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string? FilePath { get; set; }

    public int? StartLine { get; set; }

    public int? StartColumn { get; set; }
}
