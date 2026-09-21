using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.ProjectSystem.Query;

namespace TheKameleon.Superpowers.Vsix
{
    [VisualStudioContribution]
    public sealed class BuildProbeCommand : Command
    {
        public override CommandConfiguration CommandConfiguration => new("%Superpowers.BuildProbeCommand.DisplayName%");

        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            Uri? selectedPath;
            try
            {
                selectedPath = await context.GetSelectedPathAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await this.ShowResultAsync(
                    "Build was not invoked.",
                    $"Selected project: unavailable ({exception.GetType().Name}: {exception.Message})",
                    cancellationToken);
                return;
            }

            if (selectedPath is null || !selectedPath.IsFile || !string.Equals(
                System.IO.Path.GetExtension(selectedPath.LocalPath),
                ".csproj",
                StringComparison.OrdinalIgnoreCase))
            {
                await this.ShowResultAsync(
                    "Build was not invoked.",
                    "Select a .csproj node in Solution Explorer before running this probe.",
                    cancellationToken);
                return;
            }

            try
            {
                var queryResults = await this.Extensibility.Workspaces().QueryProjectsAsync(
                    query => query.With(project => project.Path),
                    cancellationToken);
                var selectedProjectPath = System.IO.Path.GetFullPath(selectedPath.LocalPath);
                var project = queryResults.Cast<IProjectSnapshot>().FirstOrDefault(candidate =>
                    candidate.Path is not null && string.Equals(
                        System.IO.Path.GetFullPath(candidate.Path),
                        selectedProjectPath,
                        StringComparison.OrdinalIgnoreCase));
                if (project is null)
                {
                    await this.ShowResultAsync(
                        "Build was not invoked.",
                        $"Selected project was not resolved by the workspace query:{Environment.NewLine}{selectedPath.LocalPath}",
                        cancellationToken);
                    return;
                }

                await project.BuildAsync(cancellationToken);
                await this.ShowResultAsync(
                    "Build invocation task completed.",
                    $"Project: {project.Path}{Environment.NewLine}Outcome: unavailable from this API; inspect normal build evidence separately.",
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                await this.ShowResultAsync(
                    "Build invocation failed.",
                    $"{exception.GetType().Name}: {exception.Message}",
                    cancellationToken);
            }
        }

        private async Task ShowResultAsync(string status, string details, CancellationToken cancellationToken)
        {
            SuperpowersToolWindow.ProbeResults.Update(
                "Project build capability probe",
                status,
                details,
                "This explicit probe can execute project build targets. Task completion is not evidence that the build succeeded.");

            await this.Extensibility.Shell().ShowToolWindowAsync<SuperpowersToolWindow>(activate: true, cancellationToken);
        }
    }
}
