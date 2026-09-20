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
        private readonly SuperpowersToolWindowControl control = new();

        public SuperpowersToolWindow()
        {
            this.Title = "Superpowers";
        }

        public override ToolWindowConfiguration ToolWindowConfiguration => new()
        {
            Placement = ToolWindowPlacement.DocumentWell,
        };

        public override Task<IRemoteUserControl> GetContentAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IRemoteUserControl>(this.control);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.control.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
