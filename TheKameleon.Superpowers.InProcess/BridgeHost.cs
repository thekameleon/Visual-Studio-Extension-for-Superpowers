using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;
using TheKameleon.Superpowers.Bridge.Contracts;

namespace TheKameleon.Superpowers.InProcess;

public sealed class BridgeHost
{
    private readonly AsyncPackage package;
    private readonly VisualStudioWorkspace? workspace;
    private readonly IComponentModel componentModel;

    public BridgeHost(AsyncPackage package, IComponentModel componentModel)
    {
        this.package = package ?? throw new ArgumentNullException(nameof(package));
        if (componentModel is null)
        {
            throw new ArgumentNullException(nameof(componentModel));
        }

        this.componentModel = componentModel;
        workspace = componentModel.GetService<VisualStudioWorkspace>();
    }

    public int ProtocolVersion => BridgeProtocol.CurrentVersion;

    public IReadOnlyList<BridgeCapabilityResult> GetCapabilities()
    {
        return BridgeCapabilityCatalog.GetCapabilities(workspace is not null);
    }

    public RoslynCapabilityProbe? GetRoslynProbe()
    {
        return workspace is null ? null : new RoslynCapabilityProbe(workspace);
    }

    public System.Threading.Tasks.Task<DocumentTextInfo?> GetActiveDocumentTextAsync(System.Threading.CancellationToken cancellationToken)
    {
        return DocumentTextProbe.CaptureActiveDocumentAsync(package, componentModel, cancellationToken);
    }
}
