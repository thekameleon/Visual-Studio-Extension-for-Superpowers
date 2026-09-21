using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.ProjectSystem.Query;

namespace TheKameleon.Superpowers.Vsix
{
    [VisualStudioContribution]
    public sealed class ContextProbeCommand : Command
    {
        public override CommandConfiguration CommandConfiguration => new("%Superpowers.ContextProbeCommand.DisplayName%");

        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            var results = new List<string>();
            var failures = new List<string>();

            await CaptureAsync("Active text view", async () =>
            {
                var textView = await context.GetActiveTextViewAsync(cancellationToken);
                results.Add($"Active text view: {(textView is null ? "not present" : "present")}");
                results.Add($"Active document URI: {textView?.Uri?.ToString() ?? "not available"}");
            }, failures);

            await CaptureAsync("Selected path", async () =>
            {
                var selectedPath = await context.GetSelectedPathAsync(cancellationToken);
                results.Add($"Selected path: {selectedPath?.ToString() ?? "empty"}");
            }, failures);

            await CaptureAsync("Open documents", async () =>
            {
                var openDocuments = await this.Extensibility.Documents().GetOpenDocumentsAsync(cancellationToken);
                results.Add($"Open document count: {openDocuments.Count}");
                results.Add($"First open document moniker: {(openDocuments.Count == 0 ? "not available" : openDocuments[0].Moniker.ToString())}");
            }, failures);

            await CaptureAsync("Workspace projects", async () =>
            {
                var projects = await this.Extensibility.Workspaces().QueryProjectsAsync(
                    query => query.With(project => project.Path),
                    cancellationToken);
                var projectSnapshots = projects.Cast<IProjectSnapshot>().ToArray();
                results.Add("Project query: succeeded");
                results.Add($"Project count: {projectSnapshots.Length}");
            }, failures);

            await CaptureAsync("Workspace solution", async () =>
            {
                var solutions = await this.Extensibility.Workspaces().QuerySolutionAsync(
                    query => query.With(solution => solution.Path),
                    cancellationToken);
                var solutionSnapshots = solutions.Cast<ISolutionSnapshot>().ToArray();
                results.Add("Solution query: succeeded");
                results.Add($"Solution count: {solutionSnapshots.Length}");
                results.Add($"Solution path: {(solutionSnapshots.Length == 0 ? "not available" : solutionSnapshots[0].Path ?? "empty")}");
            }, failures);

            var status = failures.Count == 0
                ? "All public context calls completed."
                : $"Completed with {failures.Count} unavailable or failed call(s).";
            if (failures.Count > 0)
            {
                results.AddRange(failures);
            }

            SuperpowersToolWindow.ProbeResults.Update(
                "Context capability probe",
                status,
                string.Join(Environment.NewLine, results),
                "Verify this output with no solution, no editor, an active editor, and different Solution Explorer selections in each supported IDE.");

            await this.Extensibility.Shell().ShowToolWindowAsync<SuperpowersToolWindow>(activate: true, cancellationToken);
        }

        private static async Task CaptureAsync(string operation, Func<Task> capture, ICollection<string> failures)
        {
            try
            {
                await capture();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                failures.Add($"{operation}: failed ({exception.GetType().Name}: {exception.Message})");
            }
        }
    }
}
