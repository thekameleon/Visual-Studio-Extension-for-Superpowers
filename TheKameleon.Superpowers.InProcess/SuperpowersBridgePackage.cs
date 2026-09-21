using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace TheKameleon.Superpowers.InProcess;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration("Superpowers Bridge", "In-process capability bridge for Superpowers", "1.0")]
[ProvideMenuResource("SuperpowersBridge.CTMENU", 1)]
[Guid(PackageGuidString)]
public sealed class SuperpowersBridgePackage : AsyncPackage
{
    public const string PackageGuidString = "d15ef75c-f455-4074-bec8-a3a3c1338990";

    protected override async Task InitializeAsync(
        CancellationToken cancellationToken,
        IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        await CapabilityProbeCommands.InitializeAsync(this);
    }
}