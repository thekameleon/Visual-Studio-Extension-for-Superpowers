using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TheKameleon.Superpowers.Core.Contracts.Runs;
using TheKameleon.Superpowers.Skills.Execution;

namespace TheKameleon.Superpowers.Vsix.Execution;

/// <summary>
/// Scoped edit applier. Applies exactly one caller-supplied text replacement to exactly one file,
/// after validating the edit's preimage still matches the file's current on-disk content, and
/// producing a diff preview for review before/along with the write. Where a conflict is detected
/// (preimage no longer matches), the edit is not applied and a conflict-safe evidence record is
/// returned instead, so callers can re-propose the edit against fresh content.
/// </summary>
public static class ScopedEditApplier
{
    public static async Task<ScopedEditResult> ApplyAsync(
        string evidenceId,
        string filePath,
        int startOffset,
        int length,
        string expectedPreimage,
        string replacementText,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(evidenceId))
        {
            throw new ArgumentException("Evidence identifier is required.", nameof(evidenceId));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        ArgumentNullException.ThrowIfNull(expectedPreimage);
        ArgumentNullException.ThrowIfNull(replacementText);

        if (!File.Exists(filePath))
        {
            return ScopedEditResult.Blocked(
                new AdapterEvidenceRecord(evidenceId, "EditApplication", AdapterEvidenceState.Blocked, DateTimeOffset.UtcNow, detail: $"File '{filePath}' does not exist."),
                diffPreview: null);
        }

        string currentText;
        try
        {
            currentText = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return ScopedEditResult.Blocked(
                new AdapterEvidenceRecord(evidenceId, "EditApplication", AdapterEvidenceState.Blocked, DateTimeOffset.UtcNow, detail: $"Failed to read '{filePath}': {exception.Message}"),
                diffPreview: null);
        }

        var validation = PreimageValidator.Validate(currentText, startOffset, length, expectedPreimage);
        if (!validation.IsValid)
        {
            return ScopedEditResult.Blocked(
                new AdapterEvidenceRecord(evidenceId, "EditApplication", AdapterEvidenceState.Blocked, DateTimeOffset.UtcNow, detail: $"{validation.DiagnosticCode}: {validation.Message}"),
                diffPreview: null);
        }

        var newText = currentText.Substring(0, startOffset) + replacementText + currentText.Substring(startOffset + length);
        var diffPreview = DiffPreviewBuilder.BuildPreview(filePath, expectedPreimage, replacementText);

        try
        {
            // Re-validate immediately before writing to narrow the conflict window between read
            // and write as much as practical for a synchronous, scoped, single-file edit.
            var currentTextBeforeWrite = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(currentTextBeforeWrite, currentText, StringComparison.Ordinal))
            {
                return ScopedEditResult.Blocked(
                    new AdapterEvidenceRecord(evidenceId, "EditApplication", AdapterEvidenceState.Blocked, DateTimeOffset.UtcNow, detail: "The file changed concurrently between preimage validation and write; the edit was not applied."),
                    diffPreview);
            }

            await File.WriteAllTextAsync(filePath, newText, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return ScopedEditResult.Blocked(
                new AdapterEvidenceRecord(evidenceId, "EditApplication", AdapterEvidenceState.Blocked, DateTimeOffset.UtcNow, detail: $"Failed to write '{filePath}': {exception.Message}"),
                diffPreview);
        }

        return ScopedEditResult.Applied(
            new AdapterEvidenceRecord(evidenceId, "EditApplication", AdapterEvidenceState.Observed, DateTimeOffset.UtcNow, detail: $"Applied edit to '{filePath}'."),
            diffPreview);
    }
}

public sealed class ScopedEditResult
{
    private ScopedEditResult(bool wasApplied, AdapterEvidenceRecord evidence, string? diffPreview)
    {
        WasApplied = wasApplied;
        Evidence = evidence;
        DiffPreview = diffPreview;
    }

    public bool WasApplied { get; }

    public AdapterEvidenceRecord Evidence { get; }

    public string? DiffPreview { get; }

    public static ScopedEditResult Applied(AdapterEvidenceRecord evidence, string? diffPreview) => new(true, evidence, diffPreview);

    public static ScopedEditResult Blocked(AdapterEvidenceRecord evidence, string? diffPreview) => new(false, evidence, diffPreview);
}
