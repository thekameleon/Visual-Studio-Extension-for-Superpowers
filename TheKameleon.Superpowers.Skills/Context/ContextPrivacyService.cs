using System.Text.RegularExpressions;
using TheKameleon.Superpowers.Core.Contracts.Context;
using TheKameleon.Superpowers.Core.Contracts.Settings;

namespace TheKameleon.Superpowers.Skills.Context;

public static class ContextPrivacyService
{
    private static readonly Regex SecretPattern = new("(?i)(api[_-]?key|token|password|secret)\\s*[:=]\\s*[^\\s]+", RegexOptions.Compiled);

    public static PrivacyTransformResult Apply(ContextCaptureSnapshot snapshot, SuperpowersSettings settings)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(settings);

        var diagnostics = new List<ContextCaptureDiagnostic>();
        var options = new ContextPrivacyOptions(settings.MaxContextCharacters, settings.Exclusions.Select(rule => rule.Pattern).ToArray(), redactSecrets: true);

        var activeDocument = TransformDocument(snapshot.ActiveDocument, options, diagnostics, "active-document");
        var openDocuments = snapshot.OpenDocuments.Select(document => TransformDocument(document, options, diagnostics, "open-document")).ToArray();
        var compilerDiagnostics = TransformCompilerDiagnostics(snapshot.CompilerDiagnostics, options, diagnostics);
        var buildSummary = TransformBuildSummary(snapshot.BuildSummary, options, diagnostics);
        var testFailures = TransformTestFailures(snapshot.TestFailures, options, diagnostics);
        var gitStatus = TransformGitStatus(snapshot.GitStatus, options, diagnostics);

        var transformed = new ContextCaptureSnapshot(
            snapshot.Provenance,
            snapshot.Solution,
            snapshot.Projects,
            activeDocument,
            openDocuments,
            snapshot.Selection,
            snapshot.SemanticTarget,
            compilerDiagnostics,
            buildSummary,
            testFailures,
            gitStatus,
            snapshot.Diagnostics.Concat(diagnostics).ToArray(),
            snapshot.SchemaVersion);

        return new PrivacyTransformResult(transformed, diagnostics);
    }

    private static DocumentContextSnapshot? TransformDocument(DocumentContextSnapshot? document, ContextPrivacyOptions options, List<ContextCaptureDiagnostic> diagnostics, string scope)
    {
        if (document is null)
        {
            return null;
        }

        if (IsExcluded(document.FilePath, options))
        {
            diagnostics.Add(new ContextCaptureDiagnostic("SPCTX601", $"Excluded '{document.FilePath}'.", "Info", scope));
            return new DocumentContextSnapshot(ContextValueState.Redacted, document.Provenance, document.FilePath, document.DisplayName, document.IsOpen, document.IsDirty,
                new CapturedTextValue(ContextValueState.Redacted, null, detail: "Excluded by privacy rules."));
        }

        var content = TransformText(document.Content, options, diagnostics, scope, document.FilePath);
        return new DocumentContextSnapshot(content?.State ?? document.State, document.Provenance, document.FilePath, document.DisplayName, document.IsOpen, document.IsDirty, content);
    }

    private static CompilerDiagnosticsContextSnapshot? TransformCompilerDiagnostics(CompilerDiagnosticsContextSnapshot? snapshot, ContextPrivacyOptions options, List<ContextCaptureDiagnostic> diagnostics)
    {
        if (snapshot is null)
        {
            return null;
        }

        var items = new List<CompilerDiagnosticContextItem>();
        foreach (var item in snapshot.Diagnostics)
        {
            if (IsExcluded(item.FilePath, options))
            {
                diagnostics.Add(new ContextCaptureDiagnostic("SPCTX604", $"Excluded compiler diagnostic from '{item.FilePath}'.", "Info", "compiler-diagnostics"));
                continue;
            }

            var message = item.Message;
            if (options.RedactSecrets && SecretPattern.IsMatch(message))
            {
                message = SecretPattern.Replace(message, "[redacted-secret]");
                diagnostics.Add(new ContextCaptureDiagnostic("SPCTX602", $"Sensitive content was redacted from a compiler diagnostic message{(item.FilePath is null ? string.Empty : $" in '{item.FilePath}'")}.", "Warning", "compiler-diagnostics"));
            }

            items.Add(new CompilerDiagnosticContextItem(item.Id, item.Severity, message, item.FilePath, item.StartLine, item.StartColumn));
        }

        return new CompilerDiagnosticsContextSnapshot(snapshot.State, snapshot.Provenance, snapshot.TotalCount, items);
    }

    private static BuildSummaryContextSnapshot? TransformBuildSummary(BuildSummaryContextSnapshot? snapshot, ContextPrivacyOptions options, List<ContextCaptureDiagnostic> diagnostics)
    {
        if (snapshot is null)
        {
            return null;
        }

        return new BuildSummaryContextSnapshot(snapshot.State, snapshot.Provenance, snapshot.Status, TransformText(snapshot.Summary, options, diagnostics, "build-summary", null));
    }

    private static TestFailureContextSnapshot? TransformTestFailures(TestFailureContextSnapshot? snapshot, ContextPrivacyOptions options, List<ContextCaptureDiagnostic> diagnostics)
    {
        if (snapshot is null)
        {
            return null;
        }

        return new TestFailureContextSnapshot(snapshot.State, snapshot.Provenance, snapshot.Status, snapshot.FailedCount, TransformText(snapshot.Summary, options, diagnostics, "test-failures", null));
    }

    private static GitStatusContextSnapshot? TransformGitStatus(GitStatusContextSnapshot? snapshot, ContextPrivacyOptions options, List<ContextCaptureDiagnostic> diagnostics)
    {
        if (snapshot is null)
        {
            return null;
        }

        return new GitStatusContextSnapshot(snapshot.State, snapshot.Provenance, snapshot.Branch, TransformText(snapshot.StatusSummary, options, diagnostics, "git-status", null), snapshot.RecentCommits);
    }

    private static CapturedTextValue? TransformText(CapturedTextValue? value, ContextPrivacyOptions options, List<ContextCaptureDiagnostic> diagnostics, string scope, string? filePath)
    {
        if (value is null || value.Value is null)
        {
            return value;
        }

        var text = value.Value;
        var containsSensitiveContent = value.ContainsSensitiveContent;
        if (options.RedactSecrets && SecretPattern.IsMatch(text))
        {
            text = SecretPattern.Replace(text, "[redacted-secret]");
            containsSensitiveContent = false;
            diagnostics.Add(new ContextCaptureDiagnostic("SPCTX602", $"Sensitive content was redacted{(filePath is null ? string.Empty : $" from '{filePath}'") }.", "Warning", scope));
        }

        if (text.Length > options.MaxCharacters)
        {
            diagnostics.Add(new ContextCaptureDiagnostic("SPCTX603", $"Context content exceeded the {options.MaxCharacters} character budget and was truncated.", "Info", scope));
            text = text[..options.MaxCharacters];
            return new CapturedTextValue(ContextValueState.Truncated, text, value.OriginalLength == 0 ? value.Value.Length : value.OriginalLength, text.Length, containsSensitiveContent, value.Detail);
        }

        return new CapturedTextValue(value.State, text, value.OriginalLength == 0 ? value.Value.Length : value.OriginalLength, text.Length, containsSensitiveContent, value.Detail);
    }

    private static bool IsExcluded(string? filePath, ContextPrivacyOptions options)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        return options.ExclusionPatterns.Any(pattern => filePath.Contains(pattern.Replace("**/", string.Empty).Replace("*", string.Empty), StringComparison.OrdinalIgnoreCase));
    }
}
