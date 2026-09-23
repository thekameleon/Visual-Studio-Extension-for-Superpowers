using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;

namespace TheKameleon.Superpowers.Vsix
{
    [VisualStudioContribution]
    public sealed class SuperpowersExtension : Extension
    {
        [VisualStudioContribution]
        public static MenuConfiguration SuperpowersMenu => new("%Superpowers.Menu.DisplayName%")
        {
            Placements = [CommandPlacement.KnownPlacements.ExtensionsMenu],
            Children =
            [
                MenuChild.Command<PlanCommand>(),
                MenuChild.Command<ExecuteCommand>(),
                MenuChild.Command<DebugCommand>(),
                MenuChild.Command<ReviewCommand>(),
                MenuChild.Command<VerifyCommand>(),
                MenuChild.Command<TddCommand>(),
                MenuChild.Command<RefactorCommand>(),
                MenuChild.Command<FinishCommand>(),
                MenuChild.Command<ContextProbeCommand>(),
                MenuChild.Command<PublishDiagnosticProbeCommand>(),
                MenuChild.Command<ClearDiagnosticProbeCommand>(),
                MenuChild.Command<BuildProbeCommand>(),
                MenuChild.Command<BridgeProbeCommand>(),
            ],
        };

        public override ExtensionConfiguration ExtensionConfiguration => new()
        {
            RequiresInProcessHosting = false,
            Metadata = new(
                id: "TheKameleon.Superpowers.Vsix.8a7fab37-7cfc-4314-ae3c-946698f3ff5d",
                version: new System.Version(1, 0, GeneratedBuildVersion.Build, GeneratedBuildVersion.Revision),
                publisherName: "TheKameleon",
                displayName: "Superpowers for Visual Studio",
                description: "Superpowers structured AI-assisted development workflows for Visual Studio."),
        };
    }
}
