using TheKameleon.Superpowers.Core.Contracts.Context;

namespace TheKameleon.Superpowers.Skills.Context;

public static class ContextCaptureComposer
{
    public static ContextCaptureSnapshot Compose(
        ContextProvenance provenance,
        SolutionContextSnapshot solution,
        IReadOnlyList<ProjectContextSnapshot>? projects = null,
        DocumentContextSnapshot? activeDocument = null,
        IReadOnlyList<DocumentContextSnapshot>? openDocuments = null,
        SelectionContextSnapshot? selection = null,
        SemanticTargetContextSnapshot? semanticTarget = null,
        CompilerDiagnosticsContextSnapshot? compilerDiagnostics = null,
        BuildSummaryContextSnapshot? buildSummary = null,
        TestFailureContextSnapshot? testFailures = null,
        GitStatusContextSnapshot? gitStatus = null,
        IReadOnlyList<ContextCaptureDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(solution);

        return new ContextCaptureSnapshot(
            provenance,
            solution,
            projects,
            activeDocument,
            openDocuments,
            selection,
            semanticTarget,
            compilerDiagnostics,
            buildSummary,
            testFailures,
            gitStatus,
            diagnostics);
    }
}
