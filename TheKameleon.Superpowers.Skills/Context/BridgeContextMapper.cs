using TheKameleon.Superpowers.Bridge.Contracts;
using TheKameleon.Superpowers.Core.Contracts.Context;

namespace TheKameleon.Superpowers.Skills.Context;

public static class BridgeContextMapper
{
    public static DocumentContextSnapshot MapDocumentText(
        string source,
        DocumentTextInfo? document,
        DateTimeOffset capturedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var provenance = new ContextProvenance(source, capturedAtUtc, isBridgeData: true);
        if (document is null)
        {
            return new DocumentContextSnapshot(
                ContextValueState.Unavailable,
                provenance,
                content: new CapturedTextValue(
                    ContextValueState.Unavailable,
                    null,
                    detail: "Active document text was unavailable from the in-process bridge."));
        }

        var documentState = document.IsPartial ? ContextValueState.Partial : ContextValueState.Available;
        var contentState = document.Text is null
            ? documentState
            : document.IsPartial ? ContextValueState.Partial : ContextValueState.Available;

        return new DocumentContextSnapshot(
            documentState,
            provenance,
            document.FilePath,
            document.DisplayName,
            document.IsOpen,
            document.IsDirty,
            new CapturedTextValue(
                contentState,
                document.Text,
                document.OriginalLength,
                document.CapturedLength,
                document.ContainsSensitiveContent,
                string.IsNullOrWhiteSpace(document.Detail) ? null : document.Detail));
    }

    public static CompilerDiagnosticsContextSnapshot MapCompilerDiagnostics(
        string source,
        int totalCount,
        IReadOnlyList<CompilerDiagnosticInfo> diagnostics,
        DateTimeOffset capturedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var provenance = new ContextProvenance(source, capturedAtUtc, isBridgeData: true);
        var items = diagnostics.Select(diagnostic => new CompilerDiagnosticContextItem(
            diagnostic.Id,
            diagnostic.Severity,
            diagnostic.Message,
            diagnostic.FilePath,
            diagnostic.StartLine,
            diagnostic.StartColumn)).ToArray();

        return new CompilerDiagnosticsContextSnapshot(
            diagnostics.Count == 0 ? ContextValueState.Partial : ContextValueState.Available,
            provenance,
            totalCount,
            items);
    }

    public static SemanticTargetContextSnapshot MapSemanticTarget(
        string source,
        SemanticTargetInfo? target,
        DateTimeOffset capturedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var provenance = new ContextProvenance(source, capturedAtUtc, isBridgeData: true);
        if (target is null)
        {
            return new SemanticTargetContextSnapshot(ContextValueState.Unavailable, provenance);
        }

        return new SemanticTargetContextSnapshot(
            ContextValueState.Available,
            provenance,
            target.Kind,
            target.Name,
            target.DisplayName,
            target.FilePath,
            target.StartLine,
            target.StartColumn);
    }
}
