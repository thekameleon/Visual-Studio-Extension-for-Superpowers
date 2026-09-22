using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace TheKameleon.Superpowers.InProcess;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration("Superpowers Bridge", "In-process capability bridge for Superpowers", "1.0")]
[ProvideMenuResource("SuperpowersBridge.CTMENU", 1)]
[ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
[ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
[Guid(PackageGuidString)]
public sealed class SuperpowersBridgePackage : AsyncPackage
{
    public const string PackageGuidString = "d15ef75c-f455-4074-bec8-a3a3c1338990";

    private static readonly string LogFilePath = Path.Combine(Path.GetTempPath(), "TheKameleon.Superpowers.BridgePackage.log");

    private BridgePipeServer? pipeServer;

    protected override async Task InitializeAsync(
        CancellationToken cancellationToken,
        IProgress<ServiceProgressData> progress)
    {
        Log("InitializeAsync: entered.");
        try
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            Log("InitializeAsync: switched to main thread.");
            await CapabilityProbeCommands.InitializeAsync(this);
            Log("InitializeAsync: CapabilityProbeCommands initialized.");

            var componentModel = await GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
            if (componentModel is null)
            {
                Log("InitializeAsync: SComponentModel service unavailable; bridge pipe server NOT started.");
                return;
            }

            var bridgeHost = new BridgeHost(this, componentModel);
            pipeServer = new BridgePipeServer(bridgeHost);
            pipeServer.Start();
            Log("InitializeAsync: BridgePipeServer started.");
        }
        catch (Exception exception)
        {
            Log($"InitializeAsync: FAILED - {exception}");
            throw;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            pipeServer?.Dispose();
        }

        base.Dispose(disposing);
    }

    private static void Log(string message)
    {
        LogStatic(message);
    }

    internal static void LogStatic(string message)
    {
        try
        {
            var line = string.Format(
                CultureInfo.InvariantCulture,
                "[{0:O}] pid={1} {2}{3}",
                DateTimeOffset.UtcNow,
                System.Diagnostics.Process.GetCurrentProcess().Id,
                message,
                Environment.NewLine);
            File.AppendAllText(LogFilePath, line);
        }
        catch
        {
            // Logging must never fault package initialization.
        }
    }
}
