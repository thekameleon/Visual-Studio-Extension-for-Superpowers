using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Shell;
using Microsoft.VisualStudio.RpcContracts.Notifications;
using Microsoft.VisualStudio.Extensibility.UI;
using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Core.Contracts.Settings;
using TheKameleon.Superpowers.Skills.Catalog;
using TheKameleon.Superpowers.Skills.Cli;
using TheKameleon.Superpowers.Skills.Install;
using TheKameleon.Superpowers.Skills.Models;
using TheKameleon.Superpowers.Skills.Setup;
using TheKameleon.Superpowers.Skills.Status;

namespace TheKameleon.Superpowers.Vsix
{
    [DataContract]
    internal sealed class ModelPreferenceRow : NotifyPropertyChangedObject
    {
        private readonly Func<SuperpowersFunction, string?>? suggestModel;
        private readonly Func<string, string?>? lookupProvider;
        private readonly Func<SuperpowersFunction, ModelPreferenceRow, bool>? isFunctionAvailable;
        private readonly Action<SuperpowersFunction>? reportFunctionConflict;
        private SuperpowersFunction function;
        private string newModel = string.Empty;
        private string? lastSuggestion;

        public ModelPreferenceRow(
            SuperpowersFunction function,
            Func<SuperpowersFunction, string?>? suggestModel = null,
            Func<string, string?>? lookupProvider = null,
            Func<SuperpowersFunction, ModelPreferenceRow, bool>? isFunctionAvailable = null,
            Action<SuperpowersFunction>? reportFunctionConflict = null,
            IEnumerable<string>? models = null,
            Func<ModelPreferenceRow, CancellationToken, Task<string?>>? pickModel = null)
        {
            this.suggestModel = suggestModel;
            this.lookupProvider = lookupProvider;
            this.isFunctionAvailable = isFunctionAvailable;
            this.reportFunctionConflict = reportFunctionConflict;
            this.function = function;
            if (models is not null)
            {
                this.Models.AddRange(models.Select(this.Describe));
            }
            else
            {
                var suggestion = suggestModel?.Invoke(function);
                if (!string.IsNullOrWhiteSpace(suggestion))
                {
                    this.Models.Add(this.Describe(suggestion));
                    this.lastSuggestion = suggestion;
                }
            }

            this.AddModelCommand = new AsyncCommand(async (parameter, context, cancellationToken) =>
            {
                if (pickModel is null)
                {
                    this.AddModel(this.NewModel);
                    this.NewModel = string.Empty;
                    return;
                }

                this.AddModel(await pickModel(this, cancellationToken).ConfigureAwait(false));
            });
            this.RemoveModelCommand = new AsyncCommand((parameter, context, cancellationToken) =>
            {
                if (parameter is SuggestedModel model)
                {
                    this.Models.Remove(model);
                }

                return Task.CompletedTask;
            });
        }

        [DataMember]
        public SuperpowersFunction Function
        {
            get => this.function;
            set
            {
                if (EqualityComparer<SuperpowersFunction>.Default.Equals(this.function, value))
                {
                    return;
                }

                if (this.isFunctionAvailable is not null && !this.isFunctionAvailable(value, this))
                {
                    this.reportFunctionConflict?.Invoke(value);
                    // WPF's ComboBox shows the clicked item as selected immediately, regardless of
                    // whether the bound setter accepts it. Since we're declining without changing
                    // the backing field, we still have to raise PropertyChanged for Function so the
                    // binding re-pulls the (unchanged) value from the getter and the ComboBox's
                    // visible selection snaps back to what it actually is.
                    this.RaiseNotifyPropertyChangedEvent(nameof(this.Function));
                    return;
                }

                this.function = value;
                this.RaiseNotifyPropertyChangedEvent(nameof(this.Function));
                this.RaiseNotifyPropertyChangedEvent(nameof(this.FunctionLabel));
                var onlySuggestion = this.Models.Count == 0
                    || (this.Models.Count == 1 && this.Models[0].Name == this.lastSuggestion);
                if (onlySuggestion)
                {
                    var suggestion = this.suggestModel?.Invoke(value);
                    if (!string.IsNullOrWhiteSpace(suggestion))
                    {
                        this.Models.Clear();
                        this.Models.Add(this.Describe(suggestion));
                    }

                    this.lastSuggestion = string.IsNullOrEmpty(suggestion) ? this.lastSuggestion : suggestion;
                }
            }
        }

        [DataMember]
        public string FunctionLabel => this.Function == SuperpowersFunction.General ? "Any other step" : this.Function.ToString();

        /// <summary>Models suggested to Copilot Chat for sub-agents dispatched during this step.</summary>
        [DataMember]
        public ObservableList<SuggestedModel> Models { get; } = new();

        [DataMember]
        public string NewModel
        {
            get => this.newModel;
            set => this.SetProperty(ref this.newModel, value);
        }

        [DataMember]
        public IAsyncCommand AddModelCommand { get; }

        [DataMember]
        public IAsyncCommand RemoveModelCommand { get; }

        public IReadOnlyList<string> ModelNames => this.Models.Select(model => model.Name).ToArray();

        public void AddModel(string? name)
        {
            var trimmed = name?.Trim();
            if (string.IsNullOrEmpty(trimmed) || this.Models.Any(model => string.Equals(model.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            this.Models.Add(this.Describe(trimmed));
        }

        private SuggestedModel Describe(string name) => new(name.Trim(), this.lookupProvider?.Invoke(name.Trim()) ?? string.Empty);
    }

    [DataContract]
    internal sealed class SuggestedModel
    {
        public SuggestedModel(string name, string provider)
        {
            this.Name = name;
            this.Provider = provider;
        }

        [DataMember]
        public string Name { get; }

        /// <summary>Looked up from the fetched catalog; empty when the catalog doesn't know the model.</summary>
        [DataMember]
        public string Provider { get; }

        [DataMember]
        public string Label => string.IsNullOrEmpty(this.Provider) ? this.Name : $"{this.Name} ({this.Provider})";
    }

    [DataContract]
    internal sealed class SuperpowersViewModel : NotifyPropertyChangedObject
    {
        private const string CatalogRelativeRoot = "bundled-catalog/obra.superpowers/2026-09-21";

        private readonly VisualStudioExtensibility extensibility;
        private readonly ProfilePaths paths = ProfilePaths.ForCurrentUser();
        private readonly SuperpowersSetup setup;
        private readonly CopilotCliSetup copilotCli = new(new CmdProcessRunner());
        private CopilotCliVerification? cliVerification;
        private readonly string catalogRoot;
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
        private IReadOnlyList<AvailableRelease> releases = Array.Empty<AvailableRelease>();
        private IReadOnlyList<DiscoveredRemoteRelease> remoteReleases = Array.Empty<DiscoveredRemoteRelease>();
        private readonly CopilotModelCatalogFetcher modelCatalogFetcher = new(Http);
        private readonly ModelCatalogCacheStore modelCatalogCacheStore;
        private readonly ModelPreferencesStore modelPreferencesStore;
        private CopilotModelCatalog? modelCatalog;
        private string modelCatalogStatusText = "Model list not loaded yet.";
        private CopilotPlan selectedPlan = CopilotPlan.Unspecified;
        private bool showCliWindows = true;

        private string? selectedReleaseVersion;
        private string statusText = "Loading Superpowers…";
        private string installedText = "Not installed.";
        private string alwaysOnButtonText = "Turn always-on on";
        private bool alwaysOn;
        private bool isIdle = true;
        private bool includePrereleases = true;
        private bool showCopilotCli = true;

        public SuperpowersViewModel(VisualStudioExtensibility extensibility)
        {
            this.extensibility = extensibility ?? throw new ArgumentNullException(nameof(extensibility));
            this.setup = new SuperpowersSetup(this.paths);
            this.catalogRoot = Path.Combine(
                Path.GetDirectoryName(typeof(SuperpowersViewModel).Assembly.Location) ?? AppContext.BaseDirectory,
                CatalogRelativeRoot.Replace('/', Path.DirectorySeparatorChar));
            this.modelCatalogCacheStore = new ModelCatalogCacheStore(this.paths);
            this.modelPreferencesStore = new ModelPreferencesStore(this.paths);
            this.modelCatalog = this.modelCatalogCacheStore.Load();
            this.showCopilotCli = !File.Exists(this.HideCopilotCliMarker);
            this.RefreshModelNameOptions();
            this.modelCatalogStatusText = this.modelCatalog is null
                ? "Model list not loaded yet. Type a model name manually, or select Refresh model list."
                : $"Model list last refreshed {this.modelCatalog.FetchedAtUtc:yyyy-MM-dd HH:mm} UTC.";

            this.InstallCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.InstallAsync, cancellationToken));
            this.RepairCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.RepairAsync, cancellationToken));
            this.RemoveCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.RemoveAsync, cancellationToken));
            this.RefreshCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.RefreshStatusAsync, cancellationToken));
            this.ToggleAlwaysOnCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.ToggleAlwaysOnAsync, cancellationToken));
            this.CheckForUpdatesCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.CheckForUpdatesAsync, cancellationToken));
            this.RemoveDownloadCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.RemoveDownloadAsync, cancellationToken));
            this.RefreshModelCatalogCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.RefreshModelCatalogAsync, cancellationToken));
            this.AddPreferenceRowCommand = new AsyncCommand((parameter, context, cancellationToken) =>
            {
                var nextFunction = this.NextAvailableFunction();
                if (nextFunction is null)
                {
                    this.StatusText = "Every function already has a model preference row.";
                    return Task.CompletedTask;
                }

                this.FunctionRows.Add(new ModelPreferenceRow(nextFunction.Value, this.SuggestModel, this.LookupProvider, this.IsFunctionAvailable, this.ReportFunctionConflict, pickModel: this.PickModelAsync));
                return Task.CompletedTask;
            });
            this.RemovePreferenceRowCommand = new AsyncCommand((parameter, context, cancellationToken) => { if (parameter is ModelPreferenceRow row) { this.FunctionRows.Remove(row); } return Task.CompletedTask; });
            this.SaveModelPreferencesCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.SaveModelPreferencesAsync, cancellationToken));
            this.InstallCopilotCliCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.InstallCopilotCliAsync, cancellationToken));
            this.SignInCopilotCliCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.SignInCopilotCliAsync, cancellationToken));
            this.VerifyCopilotCliCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.VerifyCopilotCliAsync, cancellationToken));
            this.InstallPowerShellCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.InstallPowerShellAsync, cancellationToken));
        }

        [DataMember]
        public IAsyncCommand InstallCopilotCliCommand { get; }

        [DataMember]
        public IAsyncCommand InstallPowerShellCommand { get; }

        [DataMember]
        public IAsyncCommand SignInCopilotCliCommand { get; }

        [DataMember]
        public IAsyncCommand VerifyCopilotCliCommand { get; }

        [DataMember]
        public string Explanation { get; } =
            "Superpowers (github.com/obra/superpowers) is a set of skills that give a coding agent a disciplined workflow: brainstorm, write a plan, execute it with test-driven development, debug systematically, request review and verify before finishing. This extension installs those skills, unchanged, where GitHub Copilot Chat finds them natively, adds a Superpowers agent and keeps them up to date. Copilot Chat then does the work with its own tools; nothing else is changed unless you turn on always-on.";

        [DataMember]
        public string Limitations { get; } =
            "Copilot Chat in Visual Studio has no Task tool, so it cannot start the independent sub-agents that several Superpowers skills rely on (parallel work, fresh-context implementers and independent reviewers), and a chat uses one model throughout. To mimic sub-agents, the Superpowers agent runs the GitHub Copilot CLI from the terminal (copilot -p \"<task>\") as a separate agent with its own context, optionally with a different model per step, then checks its work itself. Your chat keeps the model you selected. Without the CLI, Copilot does the work sequentially in the chat and says when a review was not independent.";

        [DataMember]
        public ObservableList<string> ReleaseVersions { get; } = new();

        [DataMember]
        public ObservableList<StatusItem> Checks { get; } = new();

        [DataMember]
        public ObservableList<string> Tips { get; } = new()
        {
            "In Copilot Chat, choose Superpowers in the agent picker, or type @Superpowers.",
            "After installing or enabling Superpowers, start a new chat thread. If Superpowers is not in the agent picker, restart Visual Studio.",
            "Turn off Autopilot for brainstorming and planning; those skills ask you questions.",
            "Try: \"I want to add a feature that …\" (brainstorming).",
            "Try: \"This test is failing. Fix it.\" (systematic debugging).",
            "Try: \"Write an implementation plan for …\" (writing plans).",
            "Remove Superpowers here before uninstalling the extension; uninstalling does not delete these files.",
        };

        [DataMember]
        public string? SelectedReleaseVersion
        {
            get => this.selectedReleaseVersion;
            set => this.SetProperty(ref this.selectedReleaseVersion, value);
        }

        [DataMember]
        public string StatusText
        {
            get => this.statusText;
            set => this.SetProperty(ref this.statusText, value);
        }

        [DataMember]
        public string InstalledText
        {
            get => this.installedText;
            set => this.SetProperty(ref this.installedText, value);
        }

        [DataMember]
        public bool ShowCopilotCli
        {
            get => this.showCopilotCli;
            set
            {
                if (this.SetProperty(ref this.showCopilotCli, value))
                {
                    try
                    {
                        if (value)
                        {
                            File.Delete(this.HideCopilotCliMarker);
                        }
                        else
                        {
                            Directory.CreateDirectory(this.paths.StateDirectory);
                            File.WriteAllText(this.HideCopilotCliMarker, string.Empty);
                        }
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
        }

        private string HideCopilotCliMarker => Path.Combine(this.paths.StateDirectory, "hide-copilot-cli");

        [DataMember]
        public bool AlwaysOn
        {
            get => this.alwaysOn;
            set => this.SetProperty(ref this.alwaysOn, value);
        }

        [DataMember]
        public string AlwaysOnButtonText
        {
            get => this.alwaysOnButtonText;
            set => this.SetProperty(ref this.alwaysOnButtonText, value);
        }

        [DataMember]
        public bool IsIdle
        {
            get => this.isIdle;
            set => this.SetProperty(ref this.isIdle, value);
        }

        [DataMember]
        public IAsyncCommand InstallCommand { get; }

        [DataMember]
        public IAsyncCommand RepairCommand { get; }

        [DataMember]
        public IAsyncCommand RemoveCommand { get; }

        [DataMember]
        public IAsyncCommand RefreshCommand { get; }

        [DataMember]
        public IAsyncCommand ToggleAlwaysOnCommand { get; }

        [DataMember]
        public bool ShowCliWindows
        {
            get => this.showCliWindows;
            set
            {
                if (this.SetProperty(ref this.showCliWindows, value))
                {
                    var preferences = this.modelPreferencesStore.Load() with { ShowCliWindows = value };
                    this.modelPreferencesStore.Save(preferences);
                    this.setup.RefreshAgent(preferences);
                    this.StatusText = value ? "Copilot CLI sub-agents will run in visible windows that close when they finish." : "Copilot CLI sub-agents will run in the background.";
                }
            }
        }

        [DataMember]
        public bool IncludePrereleases
        {
            get => this.includePrereleases;
            set
            {
                if (this.SetProperty(ref this.includePrereleases, value))
                {
                    var previous = this.SelectedReleaseVersion;
                    this.RebuildReleaseVersions();
                    this.SelectedReleaseVersion = this.ReleaseVersions.Contains(previous!) ? previous : this.ReleaseVersions.FirstOrDefault();
                }
            }
        }

        [DataMember]
        public IAsyncCommand CheckForUpdatesCommand { get; }

        [DataMember]
        public IAsyncCommand RemoveDownloadCommand { get; }

        [DataMember]
        public string ModelCatalogStatusText
        {
            get => this.modelCatalogStatusText;
            set => this.SetProperty(ref this.modelCatalogStatusText, value);
        }

        [DataMember]
        public ObservableList<CopilotPlan> PlanOptions { get; } = new(Enum.GetValues<CopilotPlan>());

        [DataMember]
        public CopilotPlan SelectedPlan
        {
            get => this.selectedPlan;
            set => this.SetProperty(ref this.selectedPlan, value);
        }

        [DataMember]
        public ObservableList<ModelPreferenceRow> FunctionRows { get; } = new();

        [DataMember]
        public ObservableList<SuperpowersFunction> FunctionOptions { get; } = new(Enum.GetValues<SuperpowersFunction>());

        [DataMember]
        public ObservableList<string> ModelNameOptions { get; } = new();

        [DataMember]
        public IAsyncCommand RefreshModelCatalogCommand { get; }

        [DataMember]
        public IAsyncCommand AddPreferenceRowCommand { get; }

        [DataMember]
        public IAsyncCommand RemovePreferenceRowCommand { get; }

        [DataMember]
        public IAsyncCommand SaveModelPreferencesCommand { get; }

        public Task InitializeAsync(CancellationToken cancellationToken) => this.RunAsync(this.StartupAsync, cancellationToken);

        private async Task StartupAsync(CancellationToken cancellationToken)
        {
            await this.LoadAsync(cancellationToken).ConfigureAwait(false);
            var status = this.StatusText;
            try
            {
                await this.RefreshModelCatalogAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                this.ModelCatalogStatusText = "Couldn't refresh the model list: " + exception.Message;
            }

            try
            {
                var discovery = await new ApprovedReleaseDiscoveryService(Http).DiscoverAsync(ReleaseChannelFilter.IncludePrerelease, cancellationToken).ConfigureAwait(false);
                if (!discovery.HasErrors)
                {
                    var previous = this.SelectedReleaseVersion;
                    this.remoteReleases = discovery.Releases;
                    this.RebuildReleaseVersions();
                    this.SelectedReleaseVersion = previous;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
            }

            this.StatusText = status;
        }

        private async Task RunAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
        {
            if (!this.IsIdle)
            {
                return;
            }

            this.IsIdle = false;
            try
            {
                await operation(cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException exception)
            {
                this.StatusText = exception.Message;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                this.StatusText = $"Something went wrong: {exception.Message}";
            }
            finally
            {
                this.IsIdle = true;
            }
        }

        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            var catalog = await Task.Run(() => BundledCatalogLoader.LoadFromDirectory(this.catalogRoot), cancellationToken).ConfigureAwait(false);
            var bundled = catalog.Releases.Where(release => !release.HasErrors).Select(release => new AvailableRelease(this.catalogRoot, release, "bundled"));
            var downloaded = await Task.Run(() => DownloadedReleases.Load(this.paths), cancellationToken).ConfigureAwait(false);
            this.releases = downloaded
                .Concat(bundled.Where(candidate => downloaded.All(download => download.Release.ReleaseTag != candidate.Release.ReleaseTag)))
                .OrderByDescending(candidate => candidate.Release.PublishedAtUtc ?? DateTimeOffset.MinValue)
                .ToArray();
            this.RebuildReleaseVersions();

            var state = this.setup.LoadState().State;
            var installedTag = state.Release?.Tag;
            var preferred = this.releases.FirstOrDefault(candidate => candidate.Release.ReleaseTag == installedTag)
                ?? this.releases.FirstOrDefault(candidate => candidate.Release == ReleaseSelection.DefaultRelease(this.releases.Select(r => r.Release)));
            this.SelectedReleaseVersion = preferred is null ? null : this.Label(preferred);

            var savedPreferences = this.modelPreferencesStore.Load();

            if (state.Release is not null)
            {
                var refresh = await Task.Run(() => this.setup.RefreshAgent(savedPreferences), cancellationToken).ConfigureAwait(false);
                this.StatusText = refresh.Messages.FirstOrDefault() ?? "Superpowers is installed.";
            }
            else
            {
                this.StatusText = catalog.HasErrors && this.releases.Count == 0
                    ? "The bundled skill catalog could not be loaded. Reinstall the extension."
                    : "Choose a release and select Install.";
            }

            await this.RefreshStatusAsync(cancellationToken).ConfigureAwait(false);

            this.SelectedPlan = savedPreferences.Plan;
            this.showCliWindows = savedPreferences.ShowCliWindows;
            this.RaiseNotifyPropertyChangedEvent(nameof(this.ShowCliWindows));
            this.FunctionRows.Clear();
            this.FunctionRows.AddRange(savedPreferences.Preferences.Select(p => new ModelPreferenceRow(
                p.Function,
                lookupProvider: this.LookupProvider,
                isFunctionAvailable: this.IsFunctionAvailable,
                reportFunctionConflict: this.ReportFunctionConflict,
                models: p.Models,
                pickModel: this.PickModelAsync)));
        }

        private async Task InstallAsync(CancellationToken cancellationToken)
        {
            var remote = this.SelectedRemoteRelease();
            if (remote is not null)
            {
                if (!await this.DownloadAsync(remote, cancellationToken).ConfigureAwait(false))
                {
                    return;
                }
            }

            var selected = this.SelectedRelease();
            if (selected is null)
            {
                this.StatusText = "Choose a release first.";
                return;
            }

            var release = selected.Release;
            var source = await Task.Run(() => ReleaseSkillLoader.Load(selected.CatalogRoot, release), cancellationToken).ConfigureAwait(false);
            var preview = this.setup.Preview(source.Skills);
            var overwrite = false;
            if (preview.EditedSkills.Count > 0 || preview.AgentFileEdited)
            {
                var edited = preview.EditedSkills.Concat(preview.AgentFileEdited ? new[] { "superpowers.agent.md" } : Array.Empty<string>());
                overwrite = await this.extensibility.Shell().ShowPromptAsync(
                    $"You have edited: {string.Join(", ", edited)}.\n\nOK replaces your edits with {release.ReleaseTag}. Cancel keeps your versions.",
                    PromptOptions.OKCancel,
                    cancellationToken).ConfigureAwait(false);
            }

            var result = await Task.Run(
                () => this.setup.Install(new InstalledRelease(release.ReleaseTag, release.ResolvedCommit, selected.Source), source, overwrite, this.modelPreferencesStore.Load()),
                cancellationToken).ConfigureAwait(false);
            this.Report(result, $"Installed Superpowers {release.ReleaseTag}. Start a new Copilot Chat thread and choose the Superpowers agent.");
            await this.RefreshStatusAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task RepairAsync(CancellationToken cancellationToken)
        {
            var installedTag = this.setup.LoadState().State.Release?.Tag;
            var selected = this.releases.FirstOrDefault(candidate => candidate.Release.ReleaseTag == installedTag) ?? this.SelectedRelease();
            if (selected is null)
            {
                this.StatusText = "Choose a release first.";
                return;
            }

            var release = selected.Release;
            var source = await Task.Run(() => ReleaseSkillLoader.Load(selected.CatalogRoot, release), cancellationToken).ConfigureAwait(false);
            var result = await Task.Run(
                () => this.setup.Repair(new InstalledRelease(release.ReleaseTag, release.ResolvedCommit, selected.Source), source, this.modelPreferencesStore.Load()),
                cancellationToken).ConfigureAwait(false);
            this.Report(result, $"Repaired Superpowers {release.ReleaseTag}.");
            await this.RefreshStatusAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task RemoveAsync(CancellationToken cancellationToken)
        {
            var confirmed = await this.extensibility.Shell().ShowPromptAsync(
                "Remove the Superpowers skills, the Superpowers agent and the always-on block from your profile? Files you edited are kept.",
                PromptOptions.OKCancel,
                cancellationToken).ConfigureAwait(false);
            if (!confirmed)
            {
                return;
            }

            var result = await Task.Run(() => this.setup.Remove(), cancellationToken).ConfigureAwait(false);
            this.Report(result, "Superpowers was removed from your profile.");
            await this.RefreshStatusAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task ToggleAlwaysOnAsync(CancellationToken cancellationToken)
        {
            var enable = !this.alwaysOn;
            if (enable)
            {
                var confirmed = await this.extensibility.Shell().ShowPromptAsync(
                    "Always-on adds a marked Superpowers block to the copilot-instructions.md file, so every Agent-mode chat uses Superpowers. Your other content in that file is kept. Continue?",
                    PromptOptions.OKCancel,
                    cancellationToken).ConfigureAwait(false);
                if (!confirmed)
                {
                    this.RaiseNotifyPropertyChangedEvent(nameof(this.AlwaysOn));
                    return;
                }
            }

            var result = await Task.Run(() => this.setup.SetAlwaysOn(enable), cancellationToken).ConfigureAwait(false);
            this.Report(result, enable ? "Always-on is on." : "Always-on is off.");
            await this.RefreshStatusAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task RefreshStatusAsync(CancellationToken cancellationToken)
        {
            var checks = await Task.Run(() => new StatusProbe(this.paths).Run(CopilotLogDiagnostic.DefaultLogDirectory), cancellationToken).ConfigureAwait(false);
            this.Checks.Clear();
            this.Checks.AddRange(checks.Select(ToStatusItem));
            var cliStatus = await Task.Run(this.copilotCli.Detect, cancellationToken).ConfigureAwait(false);
            this.Checks.Add(ToStatusItem(CopilotCliSetup.ToInstalledCheck(cliStatus)));
            this.Checks.Add(ToStatusItem(CopilotCliSetup.ToSignInCheck(cliStatus, this.cliVerification)));
            var pwshVersion = await Task.Run(this.copilotCli.DetectPowerShellVersion, cancellationToken).ConfigureAwait(false);
            this.Checks.Add(ToStatusItem(CopilotCliSetup.ToPowerShellCheck(pwshVersion)));

            var state = this.setup.LoadState().State;
            this.InstalledText = state.Release is null ? "Not installed." : $"Installed: {state.Release.Tag} ({state.Release.Source}).";
            this.AlwaysOn = state.AlwaysOn.Enabled;
            this.RaiseNotifyPropertyChangedEvent(nameof(this.AlwaysOn));
            this.AlwaysOnButtonText = this.alwaysOn ? "Turn always-on off" : "Turn always-on on";
        }

        private async Task InstallPowerShellAsync(CancellationToken cancellationToken)
        {
            var version = await Task.Run(this.copilotCli.DetectPowerShellVersion, cancellationToken).ConfigureAwait(false);
            if (CopilotCliSetup.ToPowerShellCheck(version).Level == StatusLevel.Pass)
            {
                this.StatusText = $"PowerShell {version} is already installed.";
                return;
            }

            var confirmed = await this.extensibility.Shell().ShowPromptAsync(
                $"Install PowerShell 7 by running:\n\n{CopilotCliSetup.PowerShellInstallCommand}\n\nYou can also download it from {CopilotCliSetup.PowerShellDownloadUrl}\n\nThis installs software outside your Superpowers files. Remove will not uninstall it.",
                PromptOptions.OKCancel,
                cancellationToken).ConfigureAwait(false);
            if (!confirmed)
            {
                this.StatusText = "PowerShell 7 installation was not started.";
                return;
            }

            this.StatusText = "Installing PowerShell 7 with winget...";
            var result = await Task.Run(this.copilotCli.InstallPowerShell, cancellationToken).ConfigureAwait(false);
            this.StatusText = result.Message;
            await this.RefreshStatusAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task InstallCopilotCliAsync(CancellationToken cancellationToken)
        {
            var status = await Task.Run(this.copilotCli.Detect, cancellationToken).ConfigureAwait(false);
            if (status.IsInstalled)
            {
                this.StatusText = $"GitHub Copilot CLI {status.Version} is already installed.";
                return;
            }

            var command = await Task.Run(this.copilotCli.ChooseInstallCommand, cancellationToken).ConfigureAwait(false);
            if (command is null)
            {
                this.StatusText = "Neither winget nor npm was found, so the GitHub Copilot CLI cannot be installed from here. Install winget (App Installer) or Node.js 22+, then try again.";
                return;
            }

            var confirmed = await this.extensibility.Shell().ShowPromptAsync(
                $"Install the GitHub Copilot CLI by running:\n\n{command}\n\nThis installs software outside your Superpowers files. Remove will not uninstall it.",
                PromptOptions.OKCancel,
                cancellationToken).ConfigureAwait(false);
            if (!confirmed)
            {
                this.StatusText = "Copilot CLI installation was not started.";
                return;
            }

            this.StatusText = $"Installing the GitHub Copilot CLI ({command})…";
            var result = await Task.Run(() => this.copilotCli.Install(command), cancellationToken).ConfigureAwait(false);
            this.StatusText = result.Message;
            await this.RefreshStatusAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task VerifyCopilotCliAsync(CancellationToken cancellationToken)
        {
            var status = await Task.Run(this.copilotCli.Detect, cancellationToken).ConfigureAwait(false);
            if (!status.IsInstalled)
            {
                this.StatusText = "Install the GitHub Copilot CLI first.";
                return;
            }

            this.StatusText = "Verifying the Copilot CLI sign-in with a test prompt (uses one Copilot request)...";
            this.cliVerification = await Task.Run(this.copilotCli.Verify, cancellationToken).ConfigureAwait(false);
            this.StatusText = this.cliVerification.Message;
            await this.RefreshStatusAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task SignInCopilotCliAsync(CancellationToken cancellationToken)
        {
            var status = await Task.Run(this.copilotCli.Detect, cancellationToken).ConfigureAwait(false);
            if (!status.IsInstalled)
            {
                this.StatusText = "Install the GitHub Copilot CLI first.";
                return;
            }

            using var login = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/d /c copilot login") { UseShellExecute = true });
            if (login is null)
            {
                this.StatusText = "Could not start the Copilot CLI sign-in.";
                return;
            }

            this.StatusText = "Signing in to the Copilot CLI. Approve the request in your browser; the sign-in window closes when it finishes.";
            await login.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            this.cliVerification = null;
            this.StatusText = login.ExitCode == 0
                ? "Signed in to the GitHub Copilot CLI."
                : $"Copilot CLI sign-in did not complete (exit code {login.ExitCode}).";
            await this.RefreshStatusAsync(cancellationToken).ConfigureAwait(false);
        }

        private void Report(SetupResult result, string successMessage)
        {
            this.StatusText = result.Status switch
            {
                SetupStatus.Succeeded => successMessage,
                SetupStatus.Partial => successMessage + " Notes: " + string.Join(" ", result.Messages),
                _ => "Nothing was changed. " + string.Join(" ", result.Messages),
            };
        }

        private async Task CheckForUpdatesAsync(CancellationToken cancellationToken)
        {
            var filter = this.IncludePrereleases ? ReleaseChannelFilter.IncludePrerelease : ReleaseChannelFilter.StableOnly;
            var discovery = await new ApprovedReleaseDiscoveryService(Http).DiscoverAsync(filter, cancellationToken).ConfigureAwait(false);
            if (discovery.HasErrors || discovery.Releases.Count == 0)
            {
                this.StatusText = "Could not check GitHub for releases. " + string.Join(" ", discovery.Diagnostics.Select(diagnostic => diagnostic.Message));
                return;
            }

            var previous = this.SelectedReleaseVersion;
            this.remoteReleases = discovery.Releases;
            this.RebuildReleaseVersions();
            this.SelectedReleaseVersion = this.ReleaseVersions.Contains(previous!) ? previous : this.ReleaseVersions.FirstOrDefault();
            var notLocal = this.RemoteOnly().Count();
            this.StatusText = notLocal == 0
                ? "You already have every published release."
                : $"Found {notLocal} release(s) on GitHub that are not downloaded yet, marked (GitHub). Selecting one and choosing Install downloads it first.";
        }

        private async Task<bool> DownloadAsync(DiscoveredRemoteRelease remote, CancellationToken cancellationToken)
        {
            var confirmed = await this.extensibility.Shell().ShowPromptAsync(
                $"Download Superpowers {remote.ReleaseTag}{(remote.IsPrerelease ? " (prerelease)" : string.Empty)} from github.com/obra/superpowers? It is checked and stored in %LOCALAPPDATA%\\TheKameleon.Superpowers\\downloads.",
                PromptOptions.OKCancel,
                cancellationToken).ConfigureAwait(false);
            if (!confirmed)
            {
                return false;
            }

            var target = DownloadedReleases.TargetDirectory(this.paths, remote.ReleaseTag);
            var result = await new ApprovedReleaseDownloadService(Http).DownloadAndActivateAsync(remote, target, approvalGranted: true, cancellationToken).ConfigureAwait(false);
            if (result.HasErrors)
            {
                this.StatusText = $"Download of {remote.ReleaseTag} failed; nothing was changed. " + string.Join(" ", result.Diagnostics.Select(diagnostic => diagnostic.Message));
                return false;
            }

            await this.LoadAsync(cancellationToken).ConfigureAwait(false);
            this.SelectedReleaseVersion = this.releases.Where(candidate => candidate.Release.ReleaseTag == remote.ReleaseTag).Select(this.Label).FirstOrDefault();
            return true;
        }

        private async Task RemoveDownloadAsync(CancellationToken cancellationToken)
        {
            var selected = this.SelectedRelease();
            if (selected is null || selected.Source != "download")
            {
                this.StatusText = "Select a release marked (downloaded) to remove it.";
                return;
            }

            var tag = selected.Release.ReleaseTag;
            if (string.Equals(this.setup.LoadState().State.Release?.Tag, tag, StringComparison.OrdinalIgnoreCase))
            {
                this.StatusText = $"{tag} is currently installed. Install a different release before removing its download.";
                return;
            }

            var confirmed = await this.extensibility.Shell().ShowPromptAsync(
                $"Delete the downloaded copy of Superpowers {tag}? You can download it again later.",
                PromptOptions.OKCancel,
                cancellationToken).ConfigureAwait(false);
            if (!confirmed)
            {
                return;
            }

            var target = DownloadedReleases.TargetDirectory(this.paths, tag);
            await Task.Run(() => { if (Directory.Exists(target)) { Directory.Delete(target, recursive: true); } }, cancellationToken).ConfigureAwait(false);
            await this.LoadAsync(cancellationToken).ConfigureAwait(false);
            this.StatusText = $"Removed the downloaded copy of {tag}.";
        }

        private IEnumerable<DiscoveredRemoteRelease> RemoteOnly()
        {
            var known = this.releases.Select(candidate => candidate.Release.ReleaseTag).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return this.remoteReleases.Where(remote => !known.Contains(remote.ReleaseTag) && (this.IncludePrereleases || !remote.IsPrerelease));
        }

        private void RebuildReleaseVersions()
        {
            var entries = this.releases.Select(candidate => (Date: candidate.Release.PublishedAtUtc ?? DateTimeOffset.MinValue, Label: this.Label(candidate)))
                .Concat(this.RemoteOnly().Select(remote => ((DateTimeOffset)remote.PublishedAtUtc, RemoteLabel(remote))))
                .OrderByDescending(entry => entry.Item1)
                .Select(entry => entry.Item2)
                .ToArray();
            this.ReleaseVersions.Clear();
            this.ReleaseVersions.AddRange(entries);
        }

        private DiscoveredRemoteRelease? SelectedRemoteRelease() =>
            this.RemoteOnly().FirstOrDefault(remote => RemoteLabel(remote) == this.SelectedReleaseVersion);

        private static string RemoteLabel(DiscoveredRemoteRelease remote) =>
            remote.ReleaseTag + (remote.IsPrerelease ? " (prerelease)" : string.Empty) + " (GitHub)";

        private async Task RefreshModelCatalogAsync(CancellationToken cancellationToken)
        {
            var result = await this.modelCatalogFetcher.FetchAsync(cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                this.ModelCatalogStatusText = "Couldn't refresh the model list. " + string.Join(" ", result.Problems)
                    + (this.modelCatalog is null ? " Type a model name manually." : " Keeping the last known list.");
                return;
            }

            var catalogToUse = result.Catalog!.Categories.Count == 0 && this.modelCatalog?.Categories.Count > 0
                ? result.Catalog! with { Categories = this.modelCatalog.Categories }
                : result.Catalog!;

            this.modelCatalog = catalogToUse;
            this.modelCatalogCacheStore.Save(catalogToUse);
            this.RefreshModelNameOptions();
            this.ModelCatalogStatusText = $"Model list refreshed {catalogToUse.FetchedAtUtc:yyyy-MM-dd HH:mm} UTC ({catalogToUse.Models.Count} models)."
                + (result.Problems.Count > 0 ? " " + string.Join(" ", result.Problems) : string.Empty);
        }

        private void RefreshModelNameOptions()
        {
            this.ModelNameOptions.Clear();
            if (this.modelCatalog is not null)
            {
                this.ModelNameOptions.AddRange(this.modelCatalog.Models.Select(model => model.Name));
            }
        }

        private async Task<string?> PickModelAsync(ModelPreferenceRow row, CancellationToken cancellationToken)
        {
            var existing = row.ModelNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var dialogViewModel = new AddModelDialogViewModel(row.FunctionLabel, this.ModelNameOptions.Where(name => !existing.Contains(name)));
            using var dialog = new AddModelDialogControl(dialogViewModel);
            var result = await this.extensibility.Shell().ShowDialogAsync(
                dialog,
                "Add sub-agent model",
                new DialogOption(DialogButton.OKCancel, DialogResult.OK),
                cancellationToken).ConfigureAwait(false);
            return result == DialogResult.OK ? dialogViewModel.SelectedModel : null;
        }

        private string? SuggestModel(SuperpowersFunction function) =>
            SuperpowersFunctionModelSuggestion.Suggest(this.modelCatalog, function, this.SelectedPlan);

        private string? LookupProvider(string modelName) =>
            this.modelCatalog?.Models.FirstOrDefault(model => string.Equals(model.Name, modelName, StringComparison.OrdinalIgnoreCase))?.Provider;

        private bool IsFunctionAvailable(SuperpowersFunction function, ModelPreferenceRow row) =>
            !this.FunctionRows.Any(other => !ReferenceEquals(other, row) && other.Function == function);

        private void ReportFunctionConflict(SuperpowersFunction function) =>
            this.StatusText = $"{function} already has a sub-agent model row. Remove or change that row first.";

        private SuperpowersFunction? NextAvailableFunction()
        {
            var used = this.FunctionRows.Select(row => row.Function).ToHashSet();
            foreach (var candidate in Enum.GetValues<SuperpowersFunction>())
            {
                if (!used.Contains(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private Task SaveModelPreferencesAsync(CancellationToken cancellationToken)
        {
            var deduped = new Dictionary<SuperpowersFunction, ModelPreference>();
            foreach (var row in this.FunctionRows)
            {
                row.AddModel(row.NewModel);
                row.NewModel = string.Empty;
                if (row.Models.Count > 0)
                {
                    deduped[row.Function] = new ModelPreference(row.Function, row.ModelNames);
                }
            }

            var preferences = new ModelPreferences
            {
                Plan = this.SelectedPlan,
                ShowCliWindows = this.ShowCliWindows,
                Preferences = deduped.Values.ToArray(),
            };
            this.modelPreferencesStore.Save(preferences);
            this.StatusText = "Sub-agent model suggestions saved. Select Install or Repair to apply them.";
            return Task.CompletedTask;
        }

        private AvailableRelease? SelectedRelease() =>
            this.releases.FirstOrDefault(candidate => this.Label(candidate) == this.SelectedReleaseVersion);

        private string Label(AvailableRelease candidate)
        {
            var label = candidate.Release.ReleaseTag;
            if (candidate.Release.IsPrerelease)
            {
                label += " (prerelease)";
            }

            return candidate.Source == "download" ? label + " (downloaded)" : label;
        }

        private static StatusItem ToStatusItem(StatusCheck check) => check.Level switch
        {
            StatusLevel.Pass => new StatusItem(LevelLabel(check.Level), check.Title, check.Message, "\u2713", "#FF2E9E44"),
            StatusLevel.Warning => new StatusItem(LevelLabel(check.Level), check.Title, check.Message, "!", "#FFD69E00"),
            StatusLevel.Fail => new StatusItem(LevelLabel(check.Level), check.Title, check.Message, "\u2715", "#FFD13438"),
            _ => new StatusItem(LevelLabel(check.Level), check.Title, check.Message, "!", "#FFD69E00"),
        };

        private static string LevelLabel(StatusLevel level) => level switch
        {
            StatusLevel.Pass => "OK",
            StatusLevel.Warning => "Check",
            StatusLevel.Fail => "Problem",
            _ => "Unknown",
        };
    }
}
