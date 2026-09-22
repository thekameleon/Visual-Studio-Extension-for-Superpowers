using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.ToolWindows;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;

namespace TheKameleon.Superpowers.Vsix
{
    [VisualStudioContribution]
    public sealed class SuperpowersToolWindow : ToolWindow
    {
        internal static ProbeResultsViewModel ProbeResults { get; } = new();

        private SuperpowersToolWindowControl? control;

        public SuperpowersToolWindow()
        {
            this.Title = "Superpowers";
        }

        public override ToolWindowConfiguration ToolWindowConfiguration => new()
        {
            AllowAutoCreation = true,
            Placement = ToolWindowPlacement.DocumentWell,
        };

        public override Task<IRemoteUserControl> GetContentAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.control ??= new SuperpowersToolWindowControl(ProbeResults);
            return Task.FromResult<IRemoteUserControl>(this.control);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.control?.Dispose();
                this.control = null;
            }

            base.Dispose(disposing);
        }
    }
}
