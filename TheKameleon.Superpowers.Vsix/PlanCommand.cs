using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;

namespace TheKameleon.Superpowers.Vsix
{
    [VisualStudioContribution]
    public sealed class PlanCommand : Command
    {
        public override CommandConfiguration CommandConfiguration => new("%Superpowers.PlanCommand.DisplayName%");

        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await this.Extensibility.Shell().ShowToolWindowAsync<SuperpowersToolWindow>(activate: true, cancellationToken);
        }
    }
}
