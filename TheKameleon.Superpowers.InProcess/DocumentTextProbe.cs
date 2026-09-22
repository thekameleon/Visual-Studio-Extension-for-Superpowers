using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TheKameleon.Superpowers.Bridge.Contracts;

namespace TheKameleon.Superpowers.InProcess;

internal static class DocumentTextProbe
{
    private const int MaxDocumentCharacters = 1_000_000;

    public static async Task<DocumentTextInfo?> CaptureActiveDocumentAsync(
        AsyncPackage package,
        IComponentModel componentModel,
        CancellationToken cancellationToken)
    {
        if (package is null)
        {
            throw new ArgumentNullException(nameof(package));
        }

        if (componentModel is null)
        {
            throw new ArgumentNullException(nameof(componentModel));
        }

        var manager = await package.GetServiceAsync(typeof(SVsTextManager)) as IVsTextManager;
        await package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var adapters = componentModel.GetService<IVsEditorAdaptersFactoryService>();
        var documents = componentModel.GetService<ITextDocumentFactoryService>();
        if (manager is null || adapters is null || documents is null)
        {
            return null;
        }

        if (ErrorHandler.Failed(manager.GetActiveView(1, null, out var nativeView)) || nativeView is null)
        {
            return null;
        }

        var view = adapters.GetWpfTextView(nativeView);
        if (view is null || view.IsClosed || !documents.TryGetTextDocument(view.TextBuffer, out var document))
        {
            return null;
        }

        var snapshot = view.TextSnapshot;
        var filePath = document.FilePath;
        if (string.IsNullOrWhiteSpace(filePath) || snapshot.Length > MaxDocumentCharacters)
        {
            return new DocumentTextInfo
            {
                FilePath = filePath,
                DisplayName = string.IsNullOrWhiteSpace(filePath) ? null : Path.GetFileName(filePath),
                IsOpen = true,
                IsDirty = document.IsDirty,
                IsPartial = true,
                Detail = string.IsNullOrWhiteSpace(filePath)
                    ? "Active document has no supported file identity."
                    : $"Active document exceeds the {MaxDocumentCharacters} character bridge limit."
            };
        }

        var text = snapshot.GetText();

        await package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        if (view.IsClosed || !ReferenceEquals(view.TextSnapshot, snapshot) ||
            !string.Equals(document.FilePath, filePath, StringComparison.OrdinalIgnoreCase) ||
            ErrorHandler.Failed(manager.GetActiveView(1, null, out var currentView)) ||
            currentView is null || !ReferenceEquals(adapters.GetWpfTextView(currentView), view))
        {
            return new DocumentTextInfo
            {
                FilePath = filePath,
                DisplayName = Path.GetFileName(filePath),
                IsOpen = true,
                IsDirty = document.IsDirty,
                IsPartial = true,
                Detail = "Editor context changed during active-document text capture; invoke again."
            };
        }

        return new DocumentTextInfo
        {
            FilePath = filePath,
            DisplayName = Path.GetFileName(filePath),
            Text = text,
            OriginalLength = snapshot.Length,
            CapturedLength = text.Length,
            IsOpen = true,
            IsDirty = document.IsDirty,
            IsPartial = false,
            ContainsSensitiveContent = false,
            Detail = "Captured from the active editor snapshot through the approved in-process bridge."
        };
    }
}