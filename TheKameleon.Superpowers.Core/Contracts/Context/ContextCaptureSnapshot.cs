using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Context;

public sealed record ContextCaptureSnapshot
{
    public const int CurrentSchemaVersion = 1;

    [JsonConstructor]
    public ContextCaptureSnapshot(
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
        IReadOnlyList<ContextCaptureDiagnostic>? diagnostics = null,
        int schemaVersion = CurrentSchemaVersion)
    {
        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Schema version must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(solution);

        Provenance = provenance;
        Solution = solution;
        Projects = (projects ?? Array.Empty<ProjectContextSnapshot>()).ToArray();
        ActiveDocument = activeDocument;
        OpenDocuments = (openDocuments ?? Array.Empty<DocumentContextSnapshot>()).ToArray();
        Selection = selection;
        SemanticTarget = semanticTarget;
        CompilerDiagnostics = compilerDiagnostics;
        BuildSummary = buildSummary;
        TestFailures = testFailures;
        GitStatus = gitStatus;
        Diagnostics = (diagnostics ?? Array.Empty<ContextCaptureDiagnostic>()).ToArray();
        SchemaVersion = schemaVersion;
    }

    public ContextProvenance Provenance { get; }

    public SolutionContextSnapshot Solution { get; }

    public IReadOnlyList<ProjectContextSnapshot> Projects { get; }

    public DocumentContextSnapshot? ActiveDocument { get; }

    public IReadOnlyList<DocumentContextSnapshot> OpenDocuments { get; }

    public SelectionContextSnapshot? Selection { get; }

    public SemanticTargetContextSnapshot? SemanticTarget { get; }

    public CompilerDiagnosticsContextSnapshot? CompilerDiagnostics { get; }

    public BuildSummaryContextSnapshot? BuildSummary { get; }

    public TestFailureContextSnapshot? TestFailures { get; }

    public GitStatusContextSnapshot? GitStatus { get; }

    public IReadOnlyList<ContextCaptureDiagnostic> Diagnostics { get; }

    public int SchemaVersion { get; }
}
