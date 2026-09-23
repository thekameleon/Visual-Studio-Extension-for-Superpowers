using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.ToolWindows;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;

namespace TheKameleon.Superpowers.Vsix
{
    /// <summary>
    /// Shared base for every entry-point tool window (Plan, Execute, Debug, TDD, Review, Verify,
    /// Refactor, Finish). All entry points route through the same workflow/mode/policy path in
    /// <see cref="SuperpowersWorkflowViewModel"/>; only the entry-point id and window title differ.
    /// </summary>
    public abstract class SuperpowersEntryPointToolWindow : ToolWindow
    {
        private readonly string entryPointId;
        private SuperpowersWorkflowViewModel? workflow;
        private SuperpowersToolWindowControl? control;

        protected SuperpowersEntryPointToolWindow(string entryPointId, string title)
        {
            this.entryPointId = entryPointId;
            this.Title = title;
        }

        public override async Task<IRemoteUserControl> GetContentAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (this.control is null)
            {
                this.workflow = new SuperpowersWorkflowViewModel(this.Extensibility, this.entryPointId);
                this.control = new SuperpowersToolWindowControl(this.workflow);
                await this.workflow.InitializeAsync(cancellationToken).ConfigureAwait(false);
            }

            return this.control;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.control?.Dispose();
                this.control = null;
                this.workflow?.Dispose();
                this.workflow = null;
            }

            base.Dispose(disposing);
        }
    }

    [VisualStudioContribution]
    public sealed class SuperpowersToolWindow : SuperpowersEntryPointToolWindow
    {
        internal static ProbeResultsViewModel ProbeResults { get; } = new();

        public SuperpowersToolWindow()
            : base("Plan", "Superpowers")
        {
        }

        public override ToolWindowConfiguration ToolWindowConfiguration => new()
        {
            AllowAutoCreation = true,
            Placement = ToolWindowPlacement.DocumentWell,
        };
    }

    [VisualStudioContribution]
    public sealed class ExecuteToolWindow : SuperpowersEntryPointToolWindow
    {
        public ExecuteToolWindow()
            : base("Execute", "Superpowers - Execute")
        {
        }

        public override ToolWindowConfiguration ToolWindowConfiguration => new()
        {
            AllowAutoCreation = true,
            Placement = ToolWindowPlacement.DocumentWell,
        };
    }

    [VisualStudioContribution]
    public sealed class DebugToolWindow : SuperpowersEntryPointToolWindow
    {
        public DebugToolWindow()
            : base("Debug", "Superpowers - Debug")
        {
        }

        public override ToolWindowConfiguration ToolWindowConfiguration => new()
        {
            AllowAutoCreation = true,
            Placement = ToolWindowPlacement.DocumentWell,
        };
    }

    [VisualStudioContribution]
    public sealed class TddToolWindow : SuperpowersEntryPointToolWindow
    {
        public TddToolWindow()
            : base("TDD", "Superpowers - TDD")
        {
        }

        public override ToolWindowConfiguration ToolWindowConfiguration => new()
        {
            AllowAutoCreation = true,
            Placement = ToolWindowPlacement.DocumentWell,
        };
    }

    [VisualStudioContribution]
    public sealed class ReviewToolWindow : SuperpowersEntryPointToolWindow
    {
        public ReviewToolWindow()
            : base("Review", "Superpowers - Review")
        {
        }

        public override ToolWindowConfiguration ToolWindowConfiguration => new()
        {
            AllowAutoCreation = true,
            Placement = ToolWindowPlacement.DocumentWell,
        };
    }

    [VisualStudioContribution]
    public sealed class VerifyToolWindow : SuperpowersEntryPointToolWindow
    {
        public VerifyToolWindow()
            : base("Verify", "Superpowers - Verify")
        {
        }

        public override ToolWindowConfiguration ToolWindowConfiguration => new()
        {
            AllowAutoCreation = true,
            Placement = ToolWindowPlacement.DocumentWell,
        };
    }

    [VisualStudioContribution]
    public sealed class RefactorToolWindow : SuperpowersEntryPointToolWindow
    {
        public RefactorToolWindow()
            : base("Refactor", "Superpowers - Refactor")
        {
        }

        public override ToolWindowConfiguration ToolWindowConfiguration => new()
        {
            AllowAutoCreation = true,
            Placement = ToolWindowPlacement.DocumentWell,
        };
    }

    [VisualStudioContribution]
    public sealed class FinishToolWindow : SuperpowersEntryPointToolWindow
    {
        public FinishToolWindow()
            : base("Finish", "Superpowers - Finish")
        {
        }

        public override ToolWindowConfiguration ToolWindowConfiguration => new()
        {
            AllowAutoCreation = true,
            Placement = ToolWindowPlacement.DocumentWell,
        };
    }
}

