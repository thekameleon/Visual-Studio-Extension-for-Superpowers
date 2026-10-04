using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;

namespace TheKameleon.Superpowers.Vsix
{
    [VisualStudioContribution]
    public sealed class OpenSuperpowersCommand : Command
    {
        [VisualStudioContribution]
        public static ToolbarConfiguration SuperpowersToolbar => new("%Superpowers.Toolbar.DisplayName%")
        {
            Children = [ToolbarChild.Command<OpenSuperpowersCommand>()],
        };

        public override CommandConfiguration CommandConfiguration => new("%Superpowers.Menu.DisplayName%")
        {
            Placements = [CommandPlacement.KnownPlacements.ExtensionsMenu],
            Icon = new(ImageMoniker.Custom("Superpowers"), IconSettings.IconAndText),
            TooltipText = "%Superpowers.Menu.Tooltip%",
        };

        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await this.Extensibility.Shell().ShowToolWindowAsync<SuperpowersToolWindow>(activate: true, cancellationToken);
        }
    }
}
