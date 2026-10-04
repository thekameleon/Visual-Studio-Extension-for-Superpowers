using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;

namespace TheKameleon.Superpowers.Vsix
{
    [VisualStudioContribution]
    public sealed class SuperpowersExtension : Extension
    {
        public override ExtensionConfiguration ExtensionConfiguration => new()
        {
            RequiresInProcessHosting = false,
            Metadata = new(
                id: "TheKameleon.Superpowers",
                version: new System.Version(0, 9, GeneratedBuildVersion.Build, GeneratedBuildVersion.Revision),
                publisherName: "TheKameleon",
                displayName: "Superpowers for Visual Studio",
                description: "Installs the Superpowers skills and a Superpowers agent for GitHub Copilot Chat in Visual Studio. This gives Superpowers functionality to Github Copilot Chat for structured AI workflows, reusable skills, project intelligence.")
            {
                InstallationTargetVersion = "[18.5,)",
                Icon = "Resources\\icon.png",
                PreviewImage = "Resources\\preview.png",
            },
        };
    }
}
