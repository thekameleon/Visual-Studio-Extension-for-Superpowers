using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TheKameleon.Superpowers.Bridge.Contracts;

namespace TheKameleon.Superpowers.InProcess;

public sealed class RoslynCapabilityProbe
{
    private readonly Workspace workspace;

    public RoslynCapabilityProbe(Workspace workspace)
    {
        this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    public async Task<IReadOnlyList<CompilerDiagnosticInfo>> GetCompilerDiagnosticsAsync(
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<CompilerDiagnosticInfo>();

        foreach (var project in workspace.CurrentSolution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            if (compilation is null)
            {
                continue;
            }

            foreach (var diagnostic in compilation.GetDiagnostics(cancellationToken))
            {
                if (diagnostic.Severity == DiagnosticSeverity.Hidden)
                {
                    continue;
                }

                var lineSpan = diagnostic.Location.IsInSource
                    ? diagnostic.Location.GetLineSpan()
                    : default;

                diagnostics.Add(new CompilerDiagnosticInfo
                {
                    Id = diagnostic.Id,
                    Message = diagnostic.GetMessage(),
                    Severity = diagnostic.Severity.ToString(),
                    FilePath = lineSpan.Path,
                    StartLine = diagnostic.Location.IsInSource ? lineSpan.StartLinePosition.Line : null,
                    StartColumn = diagnostic.Location.IsInSource ? lineSpan.StartLinePosition.Character : null
                });
            }
        }

        return diagnostics;
    }

    internal async Task<DocumentCompilerDiagnostics?> GetDocumentCompilerDiagnosticsAsync(
        string filePath,
        SourceText capturedText,
        CancellationToken cancellationToken)
    {
        var solution = workspace.CurrentSolution;
        var result = await DocumentCompilerDiagnostics.ReadAsync(
            solution, filePath, capturedText, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return ReferenceEquals(solution, workspace.CurrentSolution) ? result : null;
    }

    public async Task<SemanticTargetInfo?> GetSemanticTargetAsync(
        string filePath,
        SourceText capturedText,
        int position,
        CancellationToken cancellationToken)
    {
        var solution = workspace.CurrentSolution;
        var result = await SemanticTargetResolver.ResolveAsync(
            solution, filePath, capturedText, position, cancellationToken).ConfigureAwait(false);
        return ReferenceEquals(solution, workspace.CurrentSolution) ? result : null;
    }
}