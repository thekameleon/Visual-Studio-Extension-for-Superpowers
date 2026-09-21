using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Documents;
using Microsoft.VisualStudio.Extensibility.Languages;

namespace TheKameleon.Superpowers.Vsix
{
    internal static class DiagnosticsProbe
    {
        private static DiagnosticsReporter? reporter;

        public const string ReporterId = "TheKameleon.Superpowers.DiagnosticsProbe";
        public const string DiagnosticCode = "TKSPROBE001";
        public const string DiagnosticMessage = "Synthetic Superpowers capability probe diagnostic. Safe to clear.";

        public static DiagnosticsReporter GetReporter(VisualStudioExtensibility extensibility)
        {
            return reporter ??= extensibility.Languages().GetDiagnosticsReporter(ReporterId);
        }

        public static async Task ShowResultAsync(
            VisualStudioExtensibility extensibility,
            string status,
            string details,
            CancellationToken cancellationToken)
        {
            SuperpowersToolWindow.ProbeResults.Update(
                "Diagnostics reporter probe",
                status,
                details,
                "Confirm the synthetic TKSPROBE001 entry appears or is removed in Error List. This probe does not read host diagnostics.");

            await extensibility.Shell().ShowToolWindowAsync<SuperpowersToolWindow>(activate: true, cancellationToken);
        }
    }

    [VisualStudioContribution]
    public sealed class PublishDiagnosticProbeCommand : Command
    {
        public override CommandConfiguration CommandConfiguration => new("%Superpowers.PublishDiagnosticProbeCommand.DisplayName%");

        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            var textView = await context.GetActiveTextViewAsync(cancellationToken);
            if (textView is null)
            {
                await DiagnosticsProbe.ShowResultAsync(
                    this.Extensibility,
                    "Diagnostic was not published.",
                    "Active text document: not available",
                    cancellationToken);
                return;
            }

            var reporter = DiagnosticsProbe.GetReporter(this.Extensibility);
            var diagnostic = new DocumentDiagnostic(textView.Selection.Extent, DiagnosticsProbe.DiagnosticMessage)
            {
                ErrorCode = DiagnosticsProbe.DiagnosticCode,
                ProviderName = "Superpowers capability probe",
            };

            await reporter.ReportDiagnosticAsync(diagnostic, cancellationToken);
            await DiagnosticsProbe.ShowResultAsync(
                this.Extensibility,
                "Synthetic diagnostic report call succeeded.",
                $"Diagnostic code: {DiagnosticsProbe.DiagnosticCode}{Environment.NewLine}Document URI: {textView.Uri}",
                cancellationToken);
        }
    }

    [VisualStudioContribution]
    public sealed class ClearDiagnosticProbeCommand : Command
    {
        public override CommandConfiguration CommandConfiguration => new("%Superpowers.ClearDiagnosticProbeCommand.DisplayName%");

        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            var textView = await context.GetActiveTextViewAsync(cancellationToken);
            if (textView is null)
            {
                await DiagnosticsProbe.ShowResultAsync(
                    this.Extensibility,
                    "Diagnostics were not cleared.",
                    "Active text document: not available",
                    cancellationToken);
                return;
            }

            var reporter = DiagnosticsProbe.GetReporter(this.Extensibility);
            await reporter.ClearDiagnosticsAsync(textView.Document, cancellationToken);
            await DiagnosticsProbe.ShowResultAsync(
                this.Extensibility,
                "Synthetic diagnostic clear call succeeded.",
                $"Diagnostic code: {DiagnosticsProbe.DiagnosticCode}{Environment.NewLine}Document URI: {textView.Uri}",
                cancellationToken);
        }
    }
}
