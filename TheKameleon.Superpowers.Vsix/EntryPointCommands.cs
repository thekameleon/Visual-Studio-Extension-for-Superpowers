using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;

namespace TheKameleon.Superpowers.Vsix
{
    /// <summary>
    /// Shared base for every entry-point command (Execute, Debug, TDD, Review, Verify, Refactor,
    /// Finish) beside the existing Plan command. Each command only differs by the tool window it
    /// activates; all entry points route through the same workflow/mode/policy path rather than
    /// duplicating prompts.
    /// </summary>
    public abstract class SuperpowersEntryPointCommand<TToolWindow> : Command
        where TToolWindow : SuperpowersEntryPointToolWindow
    {
        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await this.Extensibility.Shell().ShowToolWindowAsync<TToolWindow>(activate: true, cancellationToken);
        }
    }

    [VisualStudioContribution]
    public sealed class ExecuteCommand : SuperpowersEntryPointCommand<ExecuteToolWindow>
    {
        public override CommandConfiguration CommandConfiguration => new("%Superpowers.ExecuteCommand.DisplayName%");
    }

    [VisualStudioContribution]
    public sealed class DebugCommand : SuperpowersEntryPointCommand<DebugToolWindow>
    {
        public override CommandConfiguration CommandConfiguration => new("%Superpowers.DebugCommand.DisplayName%");
    }

    [VisualStudioContribution]
    public sealed class TddCommand : SuperpowersEntryPointCommand<TddToolWindow>
    {
        public override CommandConfiguration CommandConfiguration => new("%Superpowers.TddCommand.DisplayName%");
    }

    [VisualStudioContribution]
    public sealed class ReviewCommand : SuperpowersEntryPointCommand<ReviewToolWindow>
    {
        public override CommandConfiguration CommandConfiguration => new("%Superpowers.ReviewCommand.DisplayName%");
    }

    [VisualStudioContribution]
    public sealed class VerifyCommand : SuperpowersEntryPointCommand<VerifyToolWindow>
    {
        public override CommandConfiguration CommandConfiguration => new("%Superpowers.VerifyCommand.DisplayName%");
    }

    [VisualStudioContribution]
    public sealed class RefactorCommand : SuperpowersEntryPointCommand<RefactorToolWindow>
    {
        public override CommandConfiguration CommandConfiguration => new("%Superpowers.RefactorCommand.DisplayName%");
    }

    [VisualStudioContribution]
    public sealed class FinishCommand : SuperpowersEntryPointCommand<FinishToolWindow>
    {
        public override CommandConfiguration CommandConfiguration => new("%Superpowers.FinishCommand.DisplayName%");
    }
}
