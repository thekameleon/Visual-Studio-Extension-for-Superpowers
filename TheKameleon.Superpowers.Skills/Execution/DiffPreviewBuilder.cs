namespace TheKameleon.Superpowers.Skills.Execution;

/// <summary>
/// Pure helper for producing a minimal, human-reviewable diff preview for a single proposed text
/// edit before it is applied. This is deliberately simple (old/new text side-by-side) rather than
/// a full diff algorithm, matching the scoped nature of edits this product applies.
/// </summary>
public static class DiffPreviewBuilder
{
    public static string BuildPreview(string filePath, string oldText, string newText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);

        return $"--- {filePath} (before){Environment.NewLine}" +
               $"{oldText}{Environment.NewLine}" +
               $"+++ {filePath} (after){Environment.NewLine}" +
               $"{newText}";
    }
}
