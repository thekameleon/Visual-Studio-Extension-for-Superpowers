using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using System;
using System.Collections.Generic;
using TheKameleon.Superpowers.Bridge.Contracts;

namespace TheKameleon.Superpowers.InProcess;

public sealed class BridgeHost
{
    private readonly VisualStudioWorkspace? workspace;

    public BridgeHost(IComponentModel componentModel)
    {
        if (componentModel is null)
        {
            throw new ArgumentNullException(nameof(componentModel));
        }

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
}
