namespace TheKameleon.Superpowers.Skills.Execution;

/// <summary>
/// Pure helper for validating that a proposed text edit's preimage (the expected "before" text)
/// still matches the actual current document content before applying it. This prevents
/// conflict-unsafe edits from silently overwriting content that changed since the edit was
/// proposed (e.g. because of a concurrent edit or a stale/resumed run).
/// </summary>
public static class PreimageValidator
{
    /// <summary>
    /// Validates that <paramref name="expectedPreimage"/> matches <paramref name="currentText"/>
    /// at the target range described by <paramref name="startOffset"/>/<paramref name="length"/>.
    /// </summary>
    public static PreimageValidationResult Validate(string currentText, int startOffset, int length, string expectedPreimage)
    {
        ArgumentNullException.ThrowIfNull(currentText);
        ArgumentNullException.ThrowIfNull(expectedPreimage);

        if (startOffset < 0)
        {
            return PreimageValidationResult.Failure("SPEDIT301", "Edit start offset cannot be negative.");
        }

        if (length < 0)
        {
            return PreimageValidationResult.Failure("SPEDIT302", "Edit length cannot be negative.");
        }

        if (startOffset > currentText.Length)
        {
            return PreimageValidationResult.Failure("SPEDIT303", "Edit start offset is beyond the end of the current document.");
        }

        var availableLength = currentText.Length - startOffset;
        if (length > availableLength)
        {
            return PreimageValidationResult.Failure("SPEDIT304", "Edit range extends beyond the end of the current document; the document may have changed since the edit was proposed.");
        }

        var actualSlice = currentText.Substring(startOffset, length);
        if (!string.Equals(actualSlice, expectedPreimage, StringComparison.Ordinal))
        {
            return PreimageValidationResult.Failure("SPEDIT305", "The document content at the target range no longer matches the expected preimage; the edit may conflict with a concurrent change.");
        }

        return PreimageValidationResult.Success();
    }
}

/// <summary>
/// Result of a preimage validation check: either the edit is safe to apply, or a diagnostic code
/// and message describing why it is not.
/// </summary>
public sealed class PreimageValidationResult
{
    private PreimageValidationResult(bool isValid, string? diagnosticCode, string? message)
    {
        IsValid = isValid;
        DiagnosticCode = diagnosticCode;
        Message = message;
    }

    public bool IsValid { get; }

    public string? DiagnosticCode { get; }

    public string? Message { get; }

    public static PreimageValidationResult Success() => new(true, null, null);

    public static PreimageValidationResult Failure(string diagnosticCode, string message) => new(false, diagnosticCode, message);
}
