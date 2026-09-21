using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TheKameleon.Superpowers.Bridge.Contracts;

namespace TheKameleon.Superpowers.InProcess;

internal sealed class DocumentCompilerDiagnostics
{
    private DocumentCompilerDiagnostics(int totalCount, IReadOnlyList<CompilerDiagnosticInfo> diagnostics)
    {
        TotalCount = totalCount;
        Diagnostics = diagnostics;
    }

    public int TotalCount { get; }
    public IReadOnlyList<CompilerDiagnosticInfo> Diagnostics { get; }

    public static async Task<DocumentCompilerDiagnostics?> ReadAsync(
        Solution solution, string filePath, SourceText capturedText, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ids = solution.GetDocumentIdsWithFilePath(filePath);
        if (ids.Length != 1)
        {
            return null;
        }

        var document = solution.GetDocument(ids[0]);
        if (document is null || document.Project.Language != LanguageNames.CSharp || !document.SupportsSemanticModel)
        {
            return null;
        }

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        if (!text.ContentEquals(capturedText))
        {
            return null;
        }

        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (model is null)
        {
            return null;
        }

        var diagnostics = new List<CompilerDiagnosticInfo>();
        var count = 0;
        foreach (var diagnostic in model.GetDiagnostics(cancellationToken: cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (diagnostic.Severity == DiagnosticSeverity.Hidden || diagnostic.IsSuppressed ||
                !diagnostic.Location.IsInSource || diagnostic.Location.SourceTree != model.SyntaxTree)
            {
                continue;
            }

            count++;
            if (diagnostics.Count == 10)
            {
                continue;
            }

            var span = diagnostic.Location.GetLineSpan();
            var message = diagnostic.GetMessage();
            diagnostics.Add(new CompilerDiagnosticInfo
            {
                Id = diagnostic.Id,
                Severity = diagnostic.Severity.ToString(),
                Message = message.Length <= 300 ? message : message.Substring(0, 288) + " [truncated]",
                FilePath = span.Path,
                StartLine = span.StartLinePosition.Line,
                StartColumn = span.StartLinePosition.Character
            });
        }

        return new DocumentCompilerDiagnostics(count, diagnostics.AsReadOnly());
    }

    public string Describe(string filePath)
    {
        var result = new StringBuilder();
        result.AppendLine($"File: {filePath}");
        result.AppendLine($"Compiler diagnostics: {TotalCount} (this document only; hidden/suppressed excluded).");
        result.AppendLine($"Showing {Diagnostics.Count} of {TotalCount}.");
        foreach (var diagnostic in Diagnostics)
        {
            result.AppendLine($"{diagnostic.Id} [{diagnostic.Severity}] line {diagnostic.StartLine + 1}, column {diagnostic.StartColumn + 1}: {diagnostic.Message}");
        }
        result.Append("Read-only compiler probe; not an Error List snapshot, analyzer report or build result. No IPC performed.");
        return result.ToString();
    }
}
