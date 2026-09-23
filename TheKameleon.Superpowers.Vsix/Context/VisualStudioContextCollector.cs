using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Documents;
using Microsoft.VisualStudio.ProjectSystem.Query;
using TheKameleon.Superpowers.Bridge.Contracts;
using TheKameleon.Superpowers.Core.Contracts.Context;
using TheKameleon.Superpowers.Core.Contracts.Settings;
using TheKameleon.Superpowers.Skills.Context;
using TheKameleon.Superpowers.Vsix.Bridge;

namespace TheKameleon.Superpowers.Vsix.Context;

public static class VisualStudioContextCollector
{
    public static async Task<ContextCaptureSnapshot> CaptureAsync(
        VisualStudioExtensibility extensibility,
        IClientContext clientContext,
        SuperpowersSettings settings,
        IBridgeClient? bridgeClient,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(extensibility);
        ArgumentNullException.ThrowIfNull(clientContext);
        ArgumentNullException.ThrowIfNull(settings);

        bridgeClient ??= UnavailableBridgeClient.Instance;

        var capturedAtUtc = DateTimeOffset.UtcNow;
        var diagnostics = new List<ContextCaptureDiagnostic>();
        var solutionProvenance = new ContextProvenance("VisualStudio.Extensibility", capturedAtUtc);

        var solution = await CaptureSolutionAsync(extensibility, solutionProvenance, diagnostics, cancellationToken).ConfigureAwait(false);
        var projects = await CaptureProjectsAsync(extensibility, solutionProvenance, cancellationToken).ConfigureAwait(false);
        var activeDocument = await CaptureActiveDocumentAsync(clientContext, solutionProvenance, diagnostics, bridgeClient, capturedAtUtc, cancellationToken).ConfigureAwait(false);
        var openDocuments = await CaptureOpenDocumentsAsync(extensibility, solutionProvenance, cancellationToken).ConfigureAwait(false);
        var selection = await CaptureSelectionAsync(clientContext, solutionProvenance, diagnostics, cancellationToken).ConfigureAwait(false);
        var semanticTarget = await CaptureSemanticTargetAsync(clientContext, solutionProvenance, diagnostics, bridgeClient, capturedAtUtc, cancellationToken).ConfigureAwait(false);
        var compilerDiagnostics = await CaptureCompilerDiagnosticsAsync(clientContext, solutionProvenance, diagnostics, bridgeClient, capturedAtUtc, cancellationToken).ConfigureAwait(false);
        var testFailures = await CaptureTestFailuresAsync(solutionProvenance, bridgeClient, cancellationToken).ConfigureAwait(false);
        var gitStatus = await GitStatusCollector.CaptureAsync(
            GetSolutionDirectory(solution), solutionProvenance, cancellationToken).ConfigureAwait(false);

        var raw = ContextCaptureComposer.Compose(
            new ContextProvenance("VisualStudioContextCollector", capturedAtUtc),
            solution,
            projects,
            activeDocument,
            openDocuments,
            selection,
            semanticTarget: semanticTarget,
            compilerDiagnostics: compilerDiagnostics,
            buildSummary: new BuildSummaryContextSnapshot(ContextValueState.Unavailable, solutionProvenance, "Unavailable", new CapturedTextValue(ContextValueState.Unavailable, null, detail: "Build summary capture is not yet wired to a supported collector.")),
            testFailures: testFailures,
            gitStatus: gitStatus,
            diagnostics: diagnostics);

        return ContextPrivacyService.Apply(raw, settings).Snapshot;
    }

    private static string? GetSolutionDirectory(SolutionContextSnapshot solution)
    {
        return string.IsNullOrWhiteSpace(solution.Path) ? null : Path.GetDirectoryName(solution.Path);
    }

    private static async Task<TestFailureContextSnapshot> CaptureTestFailuresAsync(
        ContextProvenance provenance,
        IBridgeClient bridgeClient,
        CancellationToken cancellationToken)
    {
        var detail = "Installed Test Window service/result interfaces are not publicly acquirable through a supported extension contract in the current IDE surface.";
        try
        {
            var bridgeCapabilities = await bridgeClient.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
            var testExplorerCapability = bridgeCapabilities.FirstOrDefault(capability => capability.Capability == BridgeCapability.TestExplorer);
            if (testExplorerCapability is { IsAvailable: false } && !string.IsNullOrWhiteSpace(testExplorerCapability.Detail))
            {
                detail = testExplorerCapability.Detail;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Fall back to the default honest "unavailable" detail below.
        }

        return new TestFailureContextSnapshot(
            ContextValueState.Unavailable,
            provenance,
            "Unavailable",
            0,
            new CapturedTextValue(ContextValueState.Unavailable, null, detail: detail));
    }

    private static async Task<SolutionContextSnapshot> CaptureSolutionAsync(
        VisualStudioExtensibility extensibility,
        ContextProvenance provenance,
        List<ContextCaptureDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        try
        {
            var solutions = await extensibility.Workspaces().QuerySolutionAsync(query => query.With(solution => solution.Path), cancellationToken).ConfigureAwait(false);
            var snapshot = solutions.Cast<ISolutionSnapshot>().FirstOrDefault();
            if (snapshot is null)
            {
                diagnostics.Add(new ContextCaptureDiagnostic("SPCTX701", "No solution is currently loaded.", "Info", "solution"));
                return new SolutionContextSnapshot(ContextValueState.Unavailable, provenance);
            }

            var path = snapshot.Path;
            return new SolutionContextSnapshot(ContextValueState.Available, provenance, path is null ? null : Path.GetFileNameWithoutExtension(path), path);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            diagnostics.Add(new ContextCaptureDiagnostic("SPCTX702", $"Solution capture failed: {exception.Message}", "Warning", "solution"));
            return new SolutionContextSnapshot(ContextValueState.Unavailable, provenance);
        }
    }

    private static async Task<IReadOnlyList<ProjectContextSnapshot>> CaptureProjectsAsync(
        VisualStudioExtensibility extensibility,
        ContextProvenance provenance,
        CancellationToken cancellationToken)
    {
        var results = await extensibility.Workspaces().QueryProjectsAsync(query => query.With(project => project.Path), cancellationToken).ConfigureAwait(false);
        return results.Cast<IProjectSnapshot>()
            .Select(project => new ProjectContextSnapshot(ContextValueState.Available, provenance, project.Path is null ? null : Path.GetFileNameWithoutExtension(project.Path), project.Path))
            .ToArray();
    }

    private static async Task<DocumentContextSnapshot?> CaptureActiveDocumentAsync(
        IClientContext clientContext,
        ContextProvenance provenance,
        List<ContextCaptureDiagnostic> diagnostics,
        IBridgeClient bridgeClient,
        DateTimeOffset capturedAtUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            var view = await clientContext.GetActiveTextViewAsync(cancellationToken).ConfigureAwait(false);
            if (view is null)
            {
                diagnostics.Add(new ContextCaptureDiagnostic("SPCTX703", "No active text view is available.", "Info", "active-document"));
                return new DocumentContextSnapshot(ContextValueState.Unavailable, provenance);
            }

            var fallback = new DocumentContextSnapshot(
                ContextValueState.Available,
                provenance,
                view.Uri?.LocalPath,
                GetDisplayName(view.Uri),
                isOpen: true,
                isDirty: false,
                new CapturedTextValue(ContextValueState.Unavailable, null, detail: "Active document text capture requires the approved in-process bridge."));

            var bridgeCapabilities = await bridgeClient.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
            var capabilityDiagnostic = ActiveDocumentBridgeResolver.EvaluateCapability(bridgeCapabilities);
            if (capabilityDiagnostic is not null)
            {
                diagnostics.Add(capabilityDiagnostic);
                return fallback;
            }

            var bridgeDocument = BridgeContextMapper.MapDocumentText("SuperpowersBridge", await bridgeClient.GetActiveDocumentTextAsync(cancellationToken).ConfigureAwait(false), capturedAtUtc);
            var documentDiagnostic = ActiveDocumentBridgeResolver.EvaluateDocument(bridgeDocument);
            if (documentDiagnostic is not null)
            {
                diagnostics.Add(documentDiagnostic);
                return fallback;
            }

            return new DocumentContextSnapshot(
                bridgeDocument.State,
                bridgeDocument.Provenance,
                bridgeDocument.FilePath ?? view.Uri?.LocalPath,
                bridgeDocument.DisplayName ?? GetDisplayName(view.Uri),
                isOpen: true,
                bridgeDocument.IsDirty,
                bridgeDocument.Content);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            diagnostics.Add(new ContextCaptureDiagnostic("SPCTX704", $"Active document capture failed: {exception.Message}", "Warning", "active-document"));
            return new DocumentContextSnapshot(ContextValueState.Unavailable, provenance);
        }
    }

    private static string? GetDisplayName(Uri? uri)
    {
        var localPath = uri?.IsFile == true ? uri.LocalPath : null;
        return string.IsNullOrWhiteSpace(localPath) ? uri?.ToString() : Path.GetFileName(localPath);
    }

    private static async Task<IReadOnlyList<DocumentContextSnapshot>> CaptureOpenDocumentsAsync(
        VisualStudioExtensibility extensibility,
        ContextProvenance provenance,
        CancellationToken cancellationToken)
    {
        var documents = await extensibility.Documents().GetOpenDocumentsAsync(cancellationToken).ConfigureAwait(false);
        return documents.Select(document => new DocumentContextSnapshot(
            ContextValueState.Available,
            provenance,
            document.Moniker?.ToString(),
            document.Moniker?.ToString(),
            isOpen: true,
            isDirty: false,
            new CapturedTextValue(ContextValueState.Unavailable, null, detail: "Open document text capture is not yet wired to a supported API."))).ToArray();
    }

    private static async Task<SelectionContextSnapshot?> CaptureSelectionAsync(
        IClientContext clientContext,
        ContextProvenance provenance,
        List<ContextCaptureDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        try
        {
            var selectedPath = await clientContext.GetSelectedPathAsync(cancellationToken).ConfigureAwait(false);
            if (selectedPath is null)
            {
                diagnostics.Add(new ContextCaptureDiagnostic("SPCTX705", "No selected path is available.", "Info", "selection"));
                return new SelectionContextSnapshot("Workspace", ContextValueState.Unavailable, provenance);
            }

            return new SelectionContextSnapshot(
                "Workspace",
                ContextValueState.Available,
                provenance,
                kind: SelectionKindResolver.InferKind(selectedPath.LocalPath),
                filePath: selectedPath.LocalPath,
                name: Path.GetFileName(selectedPath.LocalPath));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            diagnostics.Add(new ContextCaptureDiagnostic("SPCTX706", $"Selection capture failed: {exception.Message}", "Warning", "selection"));
            return new SelectionContextSnapshot("Workspace", ContextValueState.Unavailable, provenance);
        }
    }

    private static async Task<SemanticTargetContextSnapshot?> CaptureSemanticTargetAsync(
        IClientContext clientContext,
        ContextProvenance provenance,
        List<ContextCaptureDiagnostic> diagnostics,
        IBridgeClient bridgeClient,
        DateTimeOffset capturedAtUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            var view = await clientContext.GetActiveTextViewAsync(cancellationToken).ConfigureAwait(false);
            if (view is null)
            {
                diagnostics.Add(new ContextCaptureDiagnostic("SPCTX710", "No active text view is available.", "Info", "semantic-target"));
                return new SemanticTargetContextSnapshot(ContextValueState.Unavailable, provenance);
            }

            var filePath = view.Uri?.IsFile == true ? view.Uri.LocalPath : null;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                diagnostics.Add(new ContextCaptureDiagnostic("SPCTX711", "Active document has no supported file identity.", "Info", "semantic-target"));
                return new SemanticTargetContextSnapshot(ContextValueState.Unavailable, provenance);
            }

            var bridgeCapabilities = await bridgeClient.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
            var capabilityDiagnostic = ActiveDocumentBridgeResolver.EvaluateSemanticTargetCapability(bridgeCapabilities);
            if (capabilityDiagnostic is not null)
            {
                diagnostics.Add(capabilityDiagnostic);
                return new SemanticTargetContextSnapshot(ContextValueState.Unavailable, provenance);
            }

            var documentText = view.Document.Text.ToString();
            var position = view.Selection.Extent.Start.Offset;

            var target = await bridgeClient.GetSemanticTargetAsync(filePath, documentText, position, cancellationToken).ConfigureAwait(false);
            var bridgeTarget = BridgeContextMapper.MapSemanticTarget("SuperpowersBridge", target, capturedAtUtc);
            if (bridgeTarget.State == ContextValueState.Unavailable)
            {
                diagnostics.Add(new ContextCaptureDiagnostic("SPCTX712", "No semantic target (class or method) was resolved at the caret position.", "Info", "semantic-target"));
            }

            return bridgeTarget;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            diagnostics.Add(new ContextCaptureDiagnostic("SPCTX713", $"Semantic target capture failed: {exception.Message}", "Warning", "semantic-target"));
            return new SemanticTargetContextSnapshot(ContextValueState.Unavailable, provenance);
        }
    }

    private static async Task<CompilerDiagnosticsContextSnapshot?> CaptureCompilerDiagnosticsAsync(
        IClientContext clientContext,
        ContextProvenance provenance,
        List<ContextCaptureDiagnostic> diagnostics,
        IBridgeClient bridgeClient,
        DateTimeOffset capturedAtUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            var view = await clientContext.GetActiveTextViewAsync(cancellationToken).ConfigureAwait(false);
            if (view is null)
            {
                diagnostics.Add(new ContextCaptureDiagnostic("SPCTX715", "No active text view is available.", "Info", "compiler-diagnostics"));
                return new CompilerDiagnosticsContextSnapshot(ContextValueState.Unavailable, provenance, 0, Array.Empty<CompilerDiagnosticContextItem>());
            }

            var filePath = view.Uri?.IsFile == true ? view.Uri.LocalPath : null;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                diagnostics.Add(new ContextCaptureDiagnostic("SPCTX716", "Active document has no supported file identity.", "Info", "compiler-diagnostics"));
                return new CompilerDiagnosticsContextSnapshot(ContextValueState.Unavailable, provenance, 0, Array.Empty<CompilerDiagnosticContextItem>());
            }

            var bridgeCapabilities = await bridgeClient.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
            var capabilityDiagnostic = ActiveDocumentBridgeResolver.EvaluateCompilerDiagnosticsCapability(bridgeCapabilities);
            if (capabilityDiagnostic is not null)
            {
                diagnostics.Add(capabilityDiagnostic);
                return new CompilerDiagnosticsContextSnapshot(ContextValueState.Unavailable, provenance, 0, Array.Empty<CompilerDiagnosticContextItem>());
            }

            var documentText = view.Document.Text.ToString();
            var result = await bridgeClient.GetCompilerDiagnosticsAsync(filePath, documentText, cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                diagnostics.Add(new ContextCaptureDiagnostic("SPCTX717", "Compiler diagnostics were unavailable from the in-process bridge.", "Info", "compiler-diagnostics"));
                return new CompilerDiagnosticsContextSnapshot(ContextValueState.Unavailable, provenance, 0, Array.Empty<CompilerDiagnosticContextItem>());
            }

            return BridgeContextMapper.MapCompilerDiagnostics("SuperpowersBridge", result.TotalCount, result.Diagnostics, capturedAtUtc);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            diagnostics.Add(new ContextCaptureDiagnostic("SPCTX718", $"Compiler diagnostics capture failed: {exception.Message}", "Warning", "compiler-diagnostics"));
            return new CompilerDiagnosticsContextSnapshot(ContextValueState.Unavailable, provenance, 0, Array.Empty<CompilerDiagnosticContextItem>());
        }
    }
}
