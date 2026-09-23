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
using Microsoft.VisualStudio.Extensibility.UI;
using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Core.Contracts.Settings;
using TheKameleon.Superpowers.Skills.Catalog;
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
        private SuperpowersFunction function;
        private string model = string.Empty;
        private string? lastSuggestion;

        public ModelPreferenceRow(SuperpowersFunction function, Func<SuperpowersFunction, string?>? suggestModel = null, Func<string, string?>? lookupProvider = null)
        {
            this.suggestModel = suggestModel;
            this.lookupProvider = lookupProvider;
            this.function = function;
            this.model = suggestModel?.Invoke(function) ?? string.Empty;
            this.lastSuggestion = string.IsNullOrEmpty(this.model) ? null : this.model;
        }

        [DataMember]
        public SuperpowersFunction Function
        {
            get => this.function;
            set
            {
                if (this.SetProperty(ref this.function, value))
                {
                    this.RaiseNotifyPropertyChangedEvent(nameof(this.FunctionLabel));
                    if (string.IsNullOrWhiteSpace(this.model) || this.model == this.lastSuggestion)
                    {
                        var suggestion = this.suggestModel?.Invoke(value);
                        this.Model = suggestion ?? this.model;
                        this.lastSuggestion = string.IsNullOrEmpty(suggestion) ? null : suggestion;
                    }
                }
            }
        }

        [DataMember]
        public string FunctionLabel => this.Function.ToString();

        [DataMember]
        public string Model
        {
            get => this.model;
            set
            {
                if (this.SetProperty(ref this.model, value))
                {
                    this.RaiseNotifyPropertyChangedEvent(nameof(this.Provider));
                }
            }
        }

        /// <summary>Derived, never stored: looked up from the fetched catalog by the current
        /// Model name. Empty when Model is blank or doesn't match anything in the catalog
        /// (including when the catalog hasn't been fetched yet, or the user typed a model name
        /// the catalog doesn't know about).</summary>
        [DataMember]
        public string Provider => string.IsNullOrWhiteSpace(this.model)
            ? string.Empty
            : this.lookupProvider?.Invoke(this.model) ?? string.Empty;
    }

    [DataContract]
    internal sealed class SuperpowersViewModel : NotifyPropertyChangedObject
    {
        private const string CatalogRelativeRoot = "bundled-catalog/obra.superpowers/2026-09-21";

        private readonly VisualStudioExtensibility extensibility;
        private readonly ProfilePaths paths = ProfilePaths.ForCurrentUser();
        private readonly SuperpowersSetup setup;
        private readonly string catalogRoot;
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
        private IReadOnlyList<AvailableRelease> releases = Array.Empty<AvailableRelease>();
        private readonly CopilotModelCatalogFetcher modelCatalogFetcher = new(Http);
        private readonly ModelCatalogCacheStore modelCatalogCacheStore;
        private readonly ModelPreferencesStore modelPreferencesStore;
        private CopilotModelCatalog? modelCatalog;
        private string modelCatalogStatusText = "Model list not loaded yet.";
        private CopilotPlan selectedPlan = CopilotPlan.Unspecified;

        private string? selectedReleaseVersion;
        private string statusText = "Loading Superpowers…";
        private string installedText = "Not installed.";
        private string alwaysOnButtonText = "Turn always-on on";
        private bool alwaysOn;
        private bool isIdle = true;
        private bool includePrereleases;

        public SuperpowersViewModel(VisualStudioExtensibility extensibility)
        {
            this.extensibility = extensibility ?? throw new ArgumentNullException(nameof(extensibility));
            this.setup = new SuperpowersSetup(this.paths);
            this.catalogRoot = Path.Combine(AppContext.BaseDirectory, CatalogRelativeRoot.Replace('/', Path.DirectorySeparatorChar));
            this.modelCatalogCacheStore = new ModelCatalogCacheStore(this.paths);
            this.modelPreferencesStore = new ModelPreferencesStore(this.paths);
            this.modelCatalog = this.modelCatalogCacheStore.Load();
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
            this.RefreshModelCatalogCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.RefreshModelCatalogAsync, cancellationToken));
            this.AddPreferenceRowCommand = new AsyncCommand((parameter, context, cancellationToken) => { this.FunctionRows.Add(new ModelPreferenceRow(SuperpowersFunction.General, this.SuggestModel, this.LookupProvider)); return Task.CompletedTask; });
            this.RemovePreferenceRowCommand = new AsyncCommand((parameter, context, cancellationToken) => { if (parameter is ModelPreferenceRow row) { this.FunctionRows.Remove(row); } return Task.CompletedTask; });
            this.SaveModelPreferencesCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.SaveModelPreferencesAsync, cancellationToken));
        }

        [DataMember]
        public string Explanation { get; } =
            "Install copies the upstream Superpowers skills into %USERPROFILE%\\.copilot\\skills and adds a Superpowers agent at %USERPROFILE%\\.github\\agents\\superpowers.agent.md. Nothing else is changed unless you turn on always-on.";

        [DataMember]
        public ObservableList<string> ReleaseVersions { get; } = new();

        [DataMember]
        public ObservableList<StatusItem> Checks { get; } = new();

        [DataMember]
        public ObservableList<string> Tips { get; } = new()
        {
            "In Copilot Chat, choose Superpowers in the agent picker, or type @Superpowers.",
            "After installing, start a new chat thread. If Superpowers is not in the agent picker, restart Visual Studio.",
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
        public bool IncludePrereleases
        {
            get => this.includePrereleases;
            set => this.SetProperty(ref this.includePrereleases, value);
        }

        [DataMember]
        public IAsyncCommand CheckForUpdatesCommand { get; }

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

        public Task InitializeAsync(CancellationToken cancellationToken) => this.RunAsync(this.LoadAsync, cancellationToken);

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
            this.ReleaseVersions.Clear();
            this.ReleaseVersions.AddRange(this.releases.Select(this.Label));

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
            this.FunctionRows.Clear();
            this.FunctionRows.AddRange(savedPreferences.Preferences.Select(p => new ModelPreferenceRow(p.Function, lookupProvider: this.LookupProvider) { Model = p.Model }));
        }

        private async Task InstallAsync(CancellationToken cancellationToken)
        {
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
                    "Always-on adds a marked Superpowers block to %USERPROFILE%\\copilot-instructions.md, so every Agent-mode chat uses Superpowers. Your other content in that file is kept. Continue?",
                    PromptOptions.OKCancel,
                    cancellationToken).ConfigureAwait(false);
                if (!confirmed)
                {
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
            this.Checks.AddRange(checks.Select(check => new StatusItem(LevelLabel(check.Level), check.Title, check.Message)));

            var state = this.setup.LoadState().State;
            this.InstalledText = state.Release is null ? "Not installed." : $"Installed: {state.Release.Tag} ({state.Release.Source}).";
            this.alwaysOn = state.AlwaysOn.Enabled;
            this.AlwaysOnButtonText = this.alwaysOn ? "Turn always-on off" : "Turn always-on on";
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

            var known = this.releases.Select(candidate => candidate.Release.ReleaseTag).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var newer = discovery.Releases.Where(release => !known.Contains(release.ReleaseTag)).ToArray();
            if (newer.Length == 0)
            {
                this.StatusText = "You already have every published release.";
                return;
            }

            var newest = newer.OrderByDescending(release => release.PublishedAtUtc).First();
            var confirmed = await this.extensibility.Shell().ShowPromptAsync(
                $"Download Superpowers {newest.ReleaseTag}{(newest.IsPrerelease ? " (prerelease)" : string.Empty)} from github.com/obra/superpowers? It is checked and stored in %LOCALAPPDATA%\\TheKameleon.Superpowers\\downloads; nothing is installed until you select Install.",
                PromptOptions.OKCancel,
                cancellationToken).ConfigureAwait(false);
            if (!confirmed)
            {
                return;
            }

            var target = DownloadedReleases.TargetDirectory(this.paths, newest.ReleaseTag);
            var result = await new ApprovedReleaseDownloadService(Http).DownloadAndActivateAsync(newest, target, approvalGranted: true, cancellationToken).ConfigureAwait(false);
            if (result.HasErrors)
            {
                this.StatusText = $"Download of {newest.ReleaseTag} failed; nothing was changed. " + string.Join(" ", result.Diagnostics.Select(diagnostic => diagnostic.Message));
                return;
            }

            await this.LoadAsync(cancellationToken).ConfigureAwait(false);
            this.SelectedReleaseVersion = this.releases.Where(candidate => candidate.Release.ReleaseTag == newest.ReleaseTag).Select(this.Label).FirstOrDefault();
            this.StatusText = $"Downloaded {newest.ReleaseTag}. Select Install to use it.";
        }

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

        private string? SuggestModel(SuperpowersFunction function) =>
            SuperpowersFunctionModelSuggestion.Suggest(this.modelCatalog, function, this.SelectedPlan);

        private string? LookupProvider(string modelName) =>
            this.modelCatalog?.Models.FirstOrDefault(model => string.Equals(model.Name, modelName, StringComparison.OrdinalIgnoreCase))?.Provider;

        private Task SaveModelPreferencesAsync(CancellationToken cancellationToken)
        {
            var deduped = new Dictionary<SuperpowersFunction, ModelPreference>();
            foreach (var row in this.FunctionRows.Where(row => !string.IsNullOrWhiteSpace(row.Model)))
            {
                deduped[row.Function] = new ModelPreference(row.Function, row.Model.Trim());
            }

            var preferences = new ModelPreferences
            {
                Plan = this.SelectedPlan,
                Preferences = deduped.Values.ToArray(),
            };
            this.modelPreferencesStore.Save(preferences);
            this.StatusText = "Model preferences saved. Select Install or Repair to apply them.";
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

        private static string LevelLabel(StatusLevel level) => level switch
        {
            StatusLevel.Pass => "OK",
            StatusLevel.Warning => "Check",
            StatusLevel.Fail => "Problem",
            _ => "Unknown",
        };
    }
}
