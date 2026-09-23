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

        public override async Task<IRemoteUserControl> GetContentAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (this.control is null)
            {
                var viewModel = new SuperpowersViewModel(this.Extensibility);
                this.control = new SuperpowersToolWindowControl(viewModel);
                await viewModel.InitializeAsync(cancellationToken).ConfigureAwait(false);
            }

            return this.control;
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
