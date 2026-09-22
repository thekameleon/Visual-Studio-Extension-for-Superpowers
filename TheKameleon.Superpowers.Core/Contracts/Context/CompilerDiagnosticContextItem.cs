using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Context;

public sealed record CompilerDiagnosticContextItem
{
    [JsonConstructor]
    public CompilerDiagnosticContextItem(string id, string severity, string message, string? filePath = null, int? startLine = null, int? startColumn = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Diagnostic identifier is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(severity))
        {
            throw new ArgumentException("Diagnostic severity is required.", nameof(severity));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Diagnostic message is required.", nameof(message));
        }

        Id = id;
        Severity = severity;
        Message = message;
        FilePath = filePath;
        StartLine = startLine;
        StartColumn = startColumn;
    }

    public string Id { get; }

    public string Severity { get; }

    public string Message { get; }

    public string? FilePath { get; }

    public int? StartLine { get; }

    public int? StartColumn { get; }
}
