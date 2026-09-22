using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.ComponentModel.Design;
using System.Threading;
using System.Threading.Tasks;

namespace TheKameleon.Superpowers.InProcess;

internal sealed class CapabilityProbeCommands
{
    public const int CodeWindowCommandId = 0x0100;
    public const int ProjectCommandId = 0x0101;
    public const int ItemCommandId = 0x0102;
    public const int SolutionCommandId = 0x0103;
    public const int CompilerDiagnosticsCommandId = 0x0104;
    public static readonly Guid CommandSet = new("3f27dc74-e13b-4e79-a795-d091be92ea24");

    private readonly AsyncPackage package;
    private readonly SelectionIdentityProbe selectionProbe;
    private bool isRunning;

    private CapabilityProbeCommands(AsyncPackage package, OleMenuCommandService commandService, SelectionIdentityProbe selectionProbe)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        this.package = package;
        this.selectionProbe = selectionProbe;
        Add(commandService, CodeWindowCommandId, "Editor");
        Add(commandService, ProjectCommandId, "Project");
        Add(commandService, ItemCommandId, "Item");
        Add(commandService, SolutionCommandId, "Solution");
        Add(commandService, CompilerDiagnosticsCommandId, "Document Compiler Diagnostics");
    }

    public static async Task InitializeAsync(AsyncPackage package)
    {
        var selection = await package.GetServiceAsync(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection;
        var solution = await package.GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
        var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
        if (commandService is not null)
        {
            _ = new CapabilityProbeCommands(package, commandService, new SelectionIdentityProbe(selection, solution));
        }
    }

    private void Add(OleMenuCommandService commandService, int commandId, string scope)
    {
        var command = new OleMenuCommand(
            (_, _) => { _ = package.JoinableTaskFactory.RunAsync(() => ExecuteSafelyAsync(scope)); },
            new CommandID(CommandSet, commandId));
        commandService.AddCommand(command);
    }

    private async Task ExecuteSafelyAsync(string scope)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (isRunning || package.DisposalToken.IsCancellationRequested)
        {
            return;
        }

        isRunning = true;
        try
        {
            if (scope == "Editor" || scope == "Document Compiler Diagnostics")
            {
                await ExecuteAsync(scope);
            }
            else
            {
                var summary = selectionProbe.Capture(scope);
                ShowResult($"Invocation scope: {scope}{Environment.NewLine}{Environment.NewLine}{summary}");
            }
        }
        catch (OperationCanceledException)
        {
            if (!package.DisposalToken.IsCancellationRequested)
            {
                await package.JoinableTaskFactory.SwitchToMainThreadAsync();
                ShowResult("Probe canceled; no result recorded.");
            }
        }
        catch (Exception exception)
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            ShowResult($"Probe failed: {exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            isRunning = false;
        }
    }

    private async Task ExecuteAsync(string scope)
    {
        var componentModel = await package.GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
        await package.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
        var host = componentModel is null ? null : new BridgeHost(package, componentModel);
        var summary = await ProbeEditorAsync(componentModel, host?.GetRoslynProbe(), package.DisposalToken,
            scope == "Document Compiler Diagnostics");

        await package.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
        ShowResult($"Invocation scope: {scope}{Environment.NewLine}{Environment.NewLine}{summary}");
    }

    private async Task<string> ProbeEditorAsync(
        IComponentModel? componentModel,
        RoslynCapabilityProbe? probe,
        CancellationToken cancellationToken,
        bool compilerDiagnostics)
    {
        var manager = await package.GetServiceAsync(typeof(SVsTextManager)) as IVsTextManager;
        await package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var adapters = componentModel?.GetService<IVsEditorAdaptersFactoryService>();
        var documents = componentModel?.GetService<ITextDocumentFactoryService>();
        if (probe is null || manager is null || adapters is null || documents is null)
        {
            return "Unavailable: required Roslyn/editor service is missing.";
        }

        if (ErrorHandler.Failed(manager.GetActiveView(1, null, out var nativeView)) || nativeView is null)
        {
            return "Unavailable: no active text view.";
        }

        var view = adapters.GetWpfTextView(nativeView);
        if (view is null || view.IsClosed ||
            !documents.TryGetTextDocument(view.TextBuffer, out var document))
        {
            return "Unavailable: editor buffer has no supported file context.";
        }

        var snapshot = view.TextSnapshot;
        var caret = view.Caret.Position.VirtualBufferPosition;
        var path = document.FilePath;
        if (caret.IsInVirtualSpace || !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
            snapshot.Length > 1_000_000)
        {
            return "Unavailable: requires a C# file of at most 1,000,000 characters and a non-virtual caret.";
        }

        var capturedText = snapshot.GetText();
        var position = caret.Position.Position;
        string summary;
        if (compilerDiagnostics)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            var diagnostics = await Task.Run(() => probe.GetDocumentCompilerDiagnosticsAsync(
                path, SourceText.From(capturedText), timeout.Token), timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            summary = diagnostics?.Describe(path) ??
                "Unavailable: no unambiguous, current C# document/model. The document may be unmapped, linked, stale, or the workspace changed.";
        }
        else
        {
            var target = await Task.Run(() => probe.GetSemanticTargetAsync(
                path, SourceText.From(capturedText), position, cancellationToken), cancellationToken);
            summary = target is null
                ? "Unavailable: no unambiguous, current C# class/method target. The document may be unmapped, linked, stale, or the caret outside a declaration."
                : $"Kind: {target.Kind}{Environment.NewLine}Signature: {target.DisplayName}" +
                  $"{Environment.NewLine}File: {target.FilePath}" +
                  $"{Environment.NewLine}Declaration: line {target.StartLine + 1}, column {target.StartColumn + 1}" +
                  $"{Environment.NewLine}Captured caret offset: {position}. Read-only probe; no IPC performed.";
        }

        await package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        if (view.IsClosed || !ReferenceEquals(view.TextSnapshot, snapshot) ||
            !view.Caret.Position.VirtualBufferPosition.Equals(caret) ||
            !string.Equals(document.FilePath, path, StringComparison.OrdinalIgnoreCase) ||
            ErrorHandler.Failed(manager.GetActiveView(1, null, out var currentView)) ||
            currentView is null || !ReferenceEquals(adapters.GetWpfTextView(currentView), view))
        {
            return "Unavailable: editor context changed during analysis; invoke again.";
        }

        return summary;
    }

    private void ShowResult(string result)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (package.DisposalToken.IsCancellationRequested)
        {
            return;
        }

        VsShellUtilities.ShowMessageBox(
            package,
            $"{DateTimeOffset.UtcNow:O}{Environment.NewLine}{BuildIdentity.Describe()}{Environment.NewLine}{Environment.NewLine}{result}",
            "Superpowers Bridge Probe",
            Microsoft.VisualStudio.Shell.Interop.OLEMSGICON.OLEMSGICON_INFO,
            Microsoft.VisualStudio.Shell.Interop.OLEMSGBUTTON.OLEMSGBUTTON_OK,
            Microsoft.VisualStudio.Shell.Interop.OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
    }
}