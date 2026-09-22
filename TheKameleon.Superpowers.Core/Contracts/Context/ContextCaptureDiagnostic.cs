using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Context;

public sealed record ContextCaptureDiagnostic
{
    [JsonConstructor]
    public ContextCaptureDiagnostic(string code, string message, string severity, string? scope = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Context diagnostic code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Context diagnostic message is required.", nameof(message));
        }

        if (string.IsNullOrWhiteSpace(severity))
        {
            throw new ArgumentException("Context diagnostic severity is required.", nameof(severity));
        }

        Code = code;
        Message = message;
        Severity = severity;
        Scope = scope;
    }

    public string Code { get; }

    public string Message { get; }

    public string Severity { get; }

    public string? Scope { get; }
}
