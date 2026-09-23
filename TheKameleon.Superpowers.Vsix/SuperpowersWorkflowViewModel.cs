using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.UI;
using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Core.Contracts.Context;
using TheKameleon.Superpowers.Core.Contracts.Runs;
using TheKameleon.Superpowers.Core.Contracts.Settings;
using TheKameleon.Superpowers.Skills.Catalog;
using TheKameleon.Superpowers.Skills.Composition;
using TheKameleon.Superpowers.Skills.Execution;
using TheKameleon.Superpowers.Skills.Workflow;
using TheKameleon.Superpowers.Vsix.Context;

namespace TheKameleon.Superpowers.Vsix
{
    /// <summary>
    /// Bindable view model backing the real Plan workflow tool window: skill selection, execution
    /// mode, release/version selection, captured-context diagnostics, prompt preview/edit/copy,
    /// workflow lifecycle controls (pause/cancel/retry) and a simple run history list. All state
    /// transitions delegate to the pure Skills-layer services (WorkflowOrchestrator,
    /// PreviewHandoffService) so no workflow logic is duplicated in the UI layer.
    /// </summary>
    [DataContract]
    internal sealed class SuperpowersWorkflowViewModel : NotifyPropertyChangedObject, IDisposable
    {
        private const string CatalogRelativeRoot = "bundled-catalog/obra.superpowers/2026-09-21";

        private readonly VisualStudioExtensibility extensibility;

        private IReadOnlyList<LoadedCatalogRelease> releases = Array.Empty<LoadedCatalogRelease>();
        private LoadedCatalogRelease? selectedRelease;
        private DiscoveredSkillEntry? selectedSkill;
        private ContextCaptureSnapshot? capturedContext;
        private AdapterRunRecord? currentRun;
        private PreviewHandoffRecord? previewHandoff;
        private readonly List<AcceptedPlanTask> acceptedPlanTasks = new();
        private SkillCompositionRecord? composition;

        private string statusText = "Select a release and a skill, then choose Start.";
        private string promptText = string.Empty;
        private string runStateText = "No run started.";
        private bool canStart = true;
        private bool canControlRun;
        private string? workspaceDirectory;
        private string historySearchText = string.Empty;
        private string handoffStatusText = "No composition available yet.";

        public SuperpowersWorkflowViewModel(VisualStudioExtensibility extensibility)
        {
            this.extensibility = extensibility ?? throw new ArgumentNullException(nameof(extensibility));

            this.ReleaseVersions = new ObservableList<string>();
            this.SkillNames = new ObservableList<string>();
            this.ExecutionModes = new ObservableList<string>(Enum.GetNames<ExecutionMode>());
            this.HistoryEntries = new ObservableList<string>();
            this.ComposedSteps = new ObservableList<string>();
            this.AcceptedTasks = new ObservableList<string>();

            this.SelectedExecutionMode = nameof(ExecutionMode.Guided);

            this.LoadCatalogCommand = new AsyncCommand((parameter, clientContext, cancellationToken) => this.LoadCatalogAsync(cancellationToken));
            this.StartCommand = new AsyncCommand((parameter, clientContext, cancellationToken) => this.StartAsync(clientContext, cancellationToken));
            this.NextCommand = new AsyncCommand((parameter, clientContext, cancellationToken) => this.NextAsync(clientContext, cancellationToken));
            this.PauseCommand = new AsyncCommand((parameter, clientContext, cancellationToken) => this.PauseAsync(cancellationToken));
            this.CancelCommand = new AsyncCommand((parameter, clientContext, cancellationToken) => this.CancelAsync(cancellationToken));
            this.CopyPromptCommand = new AsyncCommand((parameter, clientContext, cancellationToken) => this.CopyPromptAsync(cancellationToken));
            this.RefreshHistoryCommand = new AsyncCommand((parameter, clientContext, cancellationToken) => this.RefreshHistoryAsync(cancellationToken));
            this.ExportHistoryCommand = new AsyncCommand((parameter, clientContext, cancellationToken) => this.ExportHistoryAsync(cancellationToken));
            this.DeleteHistoryCommand = new AsyncCommand((parameter, clientContext, cancellationToken) => this.DeleteHistoryAsync(cancellationToken));
            this.AcceptDesignCommand = new AsyncCommand((parameter, clientContext, cancellationToken) => this.AcceptDesignAsync(cancellationToken));
        }

        /// <summary>
        /// Resolves the workspace directory used for history persistence: the open solution's
        /// directory when one is captured, otherwise a disclosed per-user fallback location so
        /// history is never silently written somewhere unexpected.
        /// </summary>
        private string ResolveWorkspaceDirectory()
        {
            if (this.workspaceDirectory is not null)
            {
                return this.workspaceDirectory;
            }

            var solutionPath = this.capturedContext?.Solution.Path;
            if (!string.IsNullOrWhiteSpace(solutionPath))
            {
                var solutionDirectory = Path.GetDirectoryName(solutionPath);
                if (!string.IsNullOrWhiteSpace(solutionDirectory))
                {
                    this.workspaceDirectory = Path.Combine(solutionDirectory, ".superpowers");
                    return this.workspaceDirectory;
                }
            }

            var fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TheKameleon.Superpowers",
                "history");
            this.workspaceDirectory = fallback;
            return this.workspaceDirectory;
        }

        [DataMember]
        public ObservableList<string> ReleaseVersions { get; }

        [DataMember]
        public ObservableList<string> SkillNames { get; }

        [DataMember]
        public ObservableList<string> ExecutionModes { get; }

        [DataMember]
        public ObservableList<string> HistoryEntries { get; }

        [DataMember]
        public IAsyncCommand LoadCatalogCommand { get; }

        [DataMember]
        public IAsyncCommand StartCommand { get; }

        [DataMember]
        public IAsyncCommand NextCommand { get; }

        [DataMember]
        public IAsyncCommand PauseCommand { get; }

        [DataMember]
        public IAsyncCommand CancelCommand { get; }

        [DataMember]
        public IAsyncCommand CopyPromptCommand { get; }

        [DataMember]
        public IAsyncCommand RefreshHistoryCommand { get; }

        [DataMember]
        public IAsyncCommand ExportHistoryCommand { get; }

        [DataMember]
        public IAsyncCommand DeleteHistoryCommand { get; }

        [DataMember]
        public IAsyncCommand AcceptDesignCommand { get; }

        [DataMember]
        public ObservableList<string> ComposedSteps { get; }

        [DataMember]
        public ObservableList<string> AcceptedTasks { get; }

        [DataMember]
        public string HistorySearchText
        {
            get => this.historySearchText;
            set => this.SetProperty(ref this.historySearchText, value);
        }

        [DataMember]
        public string HandoffStatusText
        {
            get => this.handoffStatusText;
            private set => this.SetProperty(ref this.handoffStatusText, value);
        }

        private string? selectedReleaseVersion;

        [DataMember]
        public string? SelectedReleaseVersion
        {
            get => this.selectedReleaseVersion;
            set
            {
                if (this.SetProperty(ref this.selectedReleaseVersion, value))
                {
                    this.selectedRelease = this.releases.FirstOrDefault(release => release.ReleaseTag == value);
                    this.RefreshSkillNames();
                }
            }
        }

        private string? selectedSkillName;

        [DataMember]
        public string? SelectedSkillName
        {
            get => this.selectedSkillName;
            set
            {
                if (this.SetProperty(ref this.selectedSkillName, value))
                {
                    this.selectedSkill = this.selectedRelease?.Skills.FirstOrDefault(skill => skill.SkillId == value);
                }
            }
        }

        [DataMember]
        public string SelectedExecutionMode { get; set; }

        [DataMember]
        public string StatusText
        {
            get => this.statusText;
            private set => this.SetProperty(ref this.statusText, value);
        }

        [DataMember]
        public string PromptText
        {
            get => this.promptText;
            set => this.SetProperty(ref this.promptText, value);
        }

        [DataMember]
        public string RunStateText
        {
            get => this.runStateText;
            private set => this.SetProperty(ref this.runStateText, value);
        }

        [DataMember]
        public bool CanStart
        {
            get => this.canStart;
            private set => this.SetProperty(ref this.canStart, value);
        }

        [DataMember]
        public bool CanControlRun
        {
            get => this.canControlRun;
            private set => this.SetProperty(ref this.canControlRun, value);
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await this.LoadCatalogAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task LoadCatalogAsync(CancellationToken cancellationToken)
        {
            try
            {
                var catalogRoot = Path.Combine(AppContext.BaseDirectory, CatalogRelativeRoot.Replace('/', Path.DirectorySeparatorChar));
                var result = await Task.Run(() => BundledCatalogLoader.LoadFromDirectory(catalogRoot), cancellationToken).ConfigureAwait(false);

                this.releases = result.Releases;
                this.ReleaseVersions.Clear();
                this.ReleaseVersions.AddRange(this.releases.Select(release => release.ReleaseTag));

                if (result.HasErrors)
                {
                    this.StatusText = $"Bundled catalog loaded with {result.Diagnostics.Count(diagnostic => diagnostic.Severity == ParseDiagnosticSeverity.Error)} error(s). Some releases may be unavailable.";
                }
                else
                {
                    this.StatusText = $"Loaded {this.releases.Count} bundled release(s).";
                }

                this.SelectedReleaseVersion = this.releases.LastOrDefault()?.ReleaseTag;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                this.StatusText = $"Failed to load the bundled catalog: {exception.Message}";
            }
        }

        private void RefreshSkillNames()
        {
            this.SkillNames.Clear();
            if (this.selectedRelease is not null)
            {
                this.SkillNames.AddRange(this.selectedRelease.Skills.Select(skill => skill.SkillId));
            }

            this.SelectedSkillName = this.SkillNames.FirstOrDefault();
        }

        private async Task StartAsync(IClientContext clientContext, CancellationToken cancellationToken)
        {
            if (this.selectedRelease is null || this.selectedSkill is null)
            {
                this.StatusText = "Select a release and a skill before starting a run.";
                return;
            }

            if (!Enum.TryParse<ExecutionMode>(this.SelectedExecutionMode, out var executionMode))
            {
                executionMode = ExecutionMode.Guided;
            }

            this.capturedContext = await VisualStudioContextCollector.CaptureAsync(
                this.extensibility,
                clientContext,
                new SuperpowersSettings(),
                bridgeClient: null,
                cancellationToken).ConfigureAwait(false);

            var runId = Guid.NewGuid().ToString("N");
            var startResult = WorkflowOrchestrator.Start(
                runId,
                this.selectedSkill,
                executionMode,
                this.selectedRelease.ReleaseTag,
                this.selectedRelease.AdapterManifest.SchemaVersion,
                this.capturedContext);

            if (!startResult.Succeeded)
            {
                this.StatusText = "Cannot start this run: " + string.Join(" ", startResult.Diagnostics.Select(diagnostic => diagnostic.Message));
                return;
            }

            this.currentRun = startResult.Run;
            this.PromptText = startResult.Prompt!.Text;
            this.RunStateText = $"Run {runId} ({this.currentRun!.State})";
            this.StatusText = "Run started. Review the prompt, then copy it to Copilot Chat.";
            this.CanControlRun = true;

            this.acceptedPlanTasks.Clear();
            this.ComposePlanIfSupported();
            this.AppendHistoryEntry(AdapterRunState.Running, $"started {this.selectedSkill.SkillId} ({executionMode}) against {this.selectedRelease.ReleaseTag}");
            await this.RefreshHistoryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Composes the Plan sequence (e.g. upstream brainstorming/writing-plans) for the current
        /// release/run when the release declares plan composition metadata, exposing composed
        /// steps and handoff status honestly rather than inventing a generic sequence.
        /// </summary>
        private void ComposePlanIfSupported()
        {
            this.ComposedSteps.Clear();
            this.AcceptedTasks.Clear();

            if (this.selectedRelease is null || this.currentRun is null)
            {
                this.HandoffStatusText = "No composition available yet.";
                return;
            }

            if (this.selectedRelease.PlanMetadata is null)
            {
                this.composition = null;
                this.HandoffStatusText = $"Release '{this.selectedRelease.ReleaseTag}' does not declare Plan composition metadata (brainstorming/writing-plans).";
                return;
            }

            var capabilities = new[]
            {
                new CapabilitySnapshot("copilot-handoff", CapabilityAvailability.ManualOnly, HandoffFallbackKind.PreviewCopy, detail: "Preview/copy manual handoff (see P06)."),
            };

            var result = SkillCompositionCoordinator.ComposePlan(this.selectedRelease, this.currentRun, this.acceptedPlanTasks.ToArray(), capabilities);
            this.currentRun = result.Run;
            this.composition = result.Composition;

            if (result.Composition is not null)
            {
                this.ComposedSteps.AddRange(result.Composition.Steps.Select(step => $"{step.Order}. {step.Purpose} ({step.Skill.SkillId})"));
                this.HandoffStatusText = result.Composition.Handoff is { } handoff
                    ? $"Handoff '{handoff.RequiredCapabilityId}': {handoff.Availability} - {handoff.Reason}"
                    : "Handoff status unavailable.";
            }

            if (result.Diagnostics.Any())
            {
                this.StatusText = "Plan composition: " + string.Join(" ", result.Diagnostics.Select(diagnostic => diagnostic.Message));
            }
        }

        private async Task NextAsync(IClientContext clientContext, CancellationToken cancellationToken)
        {
            if (this.selectedSkill is null)
            {
                this.StatusText = "Start a run before requesting the next prompt.";
                return;
            }

            if (!Enum.TryParse<ExecutionMode>(this.SelectedExecutionMode, out var executionMode))
            {
                executionMode = ExecutionMode.Guided;
            }

            this.capturedContext = await VisualStudioContextCollector.CaptureAsync(
                this.extensibility,
                clientContext,
                new SuperpowersSettings(),
                bridgeClient: null,
                cancellationToken).ConfigureAwait(false);

            var prompt = WorkflowOrchestrator.Next(this.selectedSkill, executionMode, this.capturedContext);
            this.PromptText = prompt.Text;
            this.StatusText = "Prompt refreshed from the latest captured context.";
        }

        private Task PauseAsync(CancellationToken cancellationToken)
        {
            if (this.currentRun is null)
            {
                return Task.CompletedTask;
            }

            var result = WorkflowOrchestrator.Pause(this.currentRun, "Paused from the tool window.");
            this.currentRun = result.Run;
            this.RunStateText = $"Run {this.currentRun.RunId} ({this.currentRun.State})";
            this.StatusText = result.HasErrors
                ? string.Join(" ", result.Diagnostics.Select(diagnostic => diagnostic.Message))
                : "Run paused.";
            this.AppendHistoryEntry(this.currentRun.State, "paused from the tool window");
            return this.RefreshHistoryAsync(cancellationToken);
        }

        private Task CancelAsync(CancellationToken cancellationToken)
        {
            if (this.currentRun is null)
            {
                return Task.CompletedTask;
            }

            var result = WorkflowOrchestrator.Cancel(this.currentRun, "Canceled from the tool window.");
            this.currentRun = result.Run;
            this.RunStateText = $"Run {this.currentRun.RunId} ({this.currentRun.State})";
            this.StatusText = result.HasErrors
                ? string.Join(" ", result.Diagnostics.Select(diagnostic => diagnostic.Message))
                : "Run canceled.";
            this.CanControlRun = false;
            this.AppendHistoryEntry(this.currentRun.State, "canceled from the tool window");
            return this.RefreshHistoryAsync(cancellationToken);
        }

        /// <summary>
        /// Appends a persisted history entry for the current run via the pure
        /// <see cref="WorkflowHistoryService"/>, honoring configured retention/sensitive-content
        /// settings rather than keeping an ephemeral in-memory-only list.
        /// </summary>
        private void AppendHistoryEntry(AdapterRunState state, string note)
        {
            if (this.currentRun is null)
            {
                return;
            }

            var entry = new WorkflowHistoryEntry(
                this.currentRun.RunId,
                this.currentRun.SelectedReleaseTag,
                this.currentRun.ExecutionMode.ToString(),
                state,
                DateTimeOffset.UtcNow,
                this.selectedSkill?.SkillId,
                hadSensitiveContent: false,
                retainedContent: note);

            var mutation = WorkflowHistoryService.Append(this.ResolveWorkspaceDirectory(), entry, new SuperpowersSettings());
            if (mutation.HasErrors)
            {
                this.StatusText = "History could not be persisted: " + string.Join(" ", mutation.Diagnostics.Select(diagnostic => diagnostic.Message));
            }
        }

        /// <summary>
        /// Reloads persisted history from disk, applying the current search text as a run
        /// id/release/skill/state filter.
        /// </summary>
        private Task RefreshHistoryAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var query = BuildHistoryQuery();
            var result = WorkflowHistoryService.Query(this.ResolveWorkspaceDirectory(), query);

            this.HistoryEntries.Clear();
            this.HistoryEntries.AddRange(result.Snapshot.Entries.Select(FormatHistoryEntry));

            this.StatusText = result.HasErrors
                ? "History query failed: " + string.Join(" ", result.Diagnostics.Select(diagnostic => diagnostic.Message))
                : $"Loaded {result.Snapshot.Entries.Count} history entr{(result.Snapshot.Entries.Count == 1 ? "y" : "ies")}.";

            return Task.CompletedTask;
        }

        /// <summary>
        /// Exports the currently filtered history (metadata-only unless sensitive retention is
        /// explicitly enabled) to a timestamped file alongside the persisted history store.
        /// </summary>
        private Task ExportHistoryAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var workspaceDirectory = this.ResolveWorkspaceDirectory();
            var exportPath = Path.Combine(workspaceDirectory, "exports", $"history-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.json");
            var result = WorkflowHistoryService.Export(workspaceDirectory, exportPath, new WorkflowHistoryExportOptions());

            this.StatusText = result.HasErrors
                ? "History export failed: " + string.Join(" ", result.Diagnostics.Select(diagnostic => diagnostic.Message))
                : $"History exported to {result.ExportPath}.";

            return Task.CompletedTask;
        }

        /// <summary>
        /// Deletes the currently filtered history entries from the persisted store.
        /// </summary>
        private async Task DeleteHistoryAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var query = BuildHistoryQuery();
            var result = WorkflowHistoryService.Delete(this.ResolveWorkspaceDirectory(), query);

            this.StatusText = result.HasErrors
                ? "History delete failed: " + string.Join(" ", result.Diagnostics.Select(diagnostic => diagnostic.Message))
                : "Matching history entries were deleted.";

            await this.RefreshHistoryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Records design approval as an accepted plan task and re-composes the Plan sequence so
        /// the next composition step reflects the approved design, without fabricating an accepted
        /// artifact when composition metadata is unavailable.
        /// </summary>
        private Task AcceptDesignAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (this.currentRun is null || this.selectedRelease?.PlanMetadata is null)
            {
                this.StatusText = "Start a run against a release with Plan composition metadata before accepting a design.";
                return Task.CompletedTask;
            }

            var taskId = Guid.NewGuid().ToString("N");
            var order = this.acceptedPlanTasks.Count + 1;
            this.acceptedPlanTasks.Add(new AcceptedPlanTask(taskId, $"Accepted design for {this.selectedSkill?.SkillId ?? "skill"}", order));
            this.AcceptedTasks.Add($"{order}. Accepted design ({taskId})");

            this.ComposePlanIfSupported();
            this.StatusText = "Design accepted; Plan composition advanced with the accepted task.";
            return Task.CompletedTask;
        }

        private WorkflowHistoryQuery BuildHistoryQuery()
        {
            var searchText = this.HistorySearchText?.Trim();
            if (string.IsNullOrEmpty(searchText))
            {
                return new WorkflowHistoryQuery();
            }

            if (Enum.TryParse<AdapterRunState>(searchText, ignoreCase: true, out var state))
            {
                return new WorkflowHistoryQuery(state: state);
            }

            return new WorkflowHistoryQuery(runId: searchText);
        }

        private static string FormatHistoryEntry(WorkflowHistoryEntry entry)
        {
            return $"{entry.CapturedAtUtc:O} - {entry.RunId} ({entry.State}) - {entry.ReleaseTag} - {entry.SelectedSkillId ?? "(no skill)"}";
        }

        private Task CopyPromptAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(this.PromptText))
            {
                this.StatusText = "There is no prompt to copy yet.";
                return Task.CompletedTask;
            }

            if (this.currentRun is not null)
            {
                this.previewHandoff = PreviewHandoffService.Create(
                    this.currentRun.RunId, this.PromptText, HandoffFallbackKind.PreviewCopy, DateTimeOffset.UtcNow);
            }

            try
            {
                Clipboard.SetText(this.PromptText);
                this.StatusText = "Prompt copied to the clipboard. Paste it into Copilot Chat and import the result when it is ready.";
            }
            catch (Exception exception)
            {
                this.StatusText = $"Could not copy the prompt to the clipboard: {exception.Message}";
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
