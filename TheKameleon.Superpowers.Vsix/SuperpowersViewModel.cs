using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Shell;
using Microsoft.VisualStudio.Extensibility.UI;
using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Skills.Catalog;
using TheKameleon.Superpowers.Skills.Install;
using TheKameleon.Superpowers.Skills.Setup;
using TheKameleon.Superpowers.Skills.Status;

namespace TheKameleon.Superpowers.Vsix
{
    [DataContract]
    internal sealed class SuperpowersViewModel : NotifyPropertyChangedObject
    {
        private const string CatalogRelativeRoot = "bundled-catalog/obra.superpowers/2026-09-21";

        private readonly VisualStudioExtensibility extensibility;
        private readonly ProfilePaths paths = ProfilePaths.ForCurrentUser();
        private readonly SuperpowersSetup setup;
        private readonly string catalogRoot;
        private IReadOnlyList<LoadedCatalogRelease> releases = Array.Empty<LoadedCatalogRelease>();

        private string? selectedReleaseVersion;
        private string statusText = "Loading Superpowers…";
        private string installedText = "Not installed.";
        private string alwaysOnButtonText = "Turn always-on on";
        private bool alwaysOn;
        private bool isIdle = true;

        public SuperpowersViewModel(VisualStudioExtensibility extensibility)
        {
            this.extensibility = extensibility ?? throw new ArgumentNullException(nameof(extensibility));
            this.setup = new SuperpowersSetup(this.paths);
            this.catalogRoot = Path.Combine(AppContext.BaseDirectory, CatalogRelativeRoot.Replace('/', Path.DirectorySeparatorChar));

            this.InstallCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.InstallAsync, cancellationToken));
            this.RepairCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.RepairAsync, cancellationToken));
            this.RemoveCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.RemoveAsync, cancellationToken));
            this.RefreshCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.RefreshStatusAsync, cancellationToken));
            this.ToggleAlwaysOnCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.ToggleAlwaysOnAsync, cancellationToken));
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
            this.releases = catalog.Releases.Where(release => !release.HasErrors).ToArray();
            this.ReleaseVersions.Clear();
            this.ReleaseVersions.AddRange(this.releases.Select(release => release.IsPrerelease ? $"{release.ReleaseTag} (prerelease)" : release.ReleaseTag));

            var state = this.setup.LoadState().State;
            var installedTag = state.Release?.Tag;
            var preferred = this.releases.FirstOrDefault(release => release.ReleaseTag == installedTag) ?? ReleaseSelection.DefaultRelease(this.releases);
            this.SelectedReleaseVersion = preferred is null ? null : this.Label(preferred);

            if (state.Release is not null)
            {
                var refresh = await Task.Run(() => this.setup.RefreshAgent(), cancellationToken).ConfigureAwait(false);
                this.StatusText = refresh.Messages.FirstOrDefault() ?? "Superpowers is installed.";
            }
            else
            {
                this.StatusText = catalog.HasErrors && this.releases.Count == 0
                    ? "The bundled skill catalog could not be loaded. Reinstall the extension."
                    : "Choose a release and select Install.";
            }

            await this.RefreshStatusAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task InstallAsync(CancellationToken cancellationToken)
        {
            var release = this.SelectedRelease();
            if (release is null)
            {
                this.StatusText = "Choose a release first.";
                return;
            }

            var source = await Task.Run(() => ReleaseSkillLoader.Load(this.catalogRoot, release), cancellationToken).ConfigureAwait(false);
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
                () => this.setup.Install(new InstalledRelease(release.ReleaseTag, release.ResolvedCommit, "bundled"), source, overwrite),
                cancellationToken).ConfigureAwait(false);
            this.Report(result, $"Installed Superpowers {release.ReleaseTag}. Start a new Copilot Chat thread and choose the Superpowers agent.");
            await this.RefreshStatusAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task RepairAsync(CancellationToken cancellationToken)
        {
            var installedTag = this.setup.LoadState().State.Release?.Tag;
            var release = this.releases.FirstOrDefault(candidate => candidate.ReleaseTag == installedTag) ?? this.SelectedRelease();
            if (release is null)
            {
                this.StatusText = "Choose a release first.";
                return;
            }

            var source = await Task.Run(() => ReleaseSkillLoader.Load(this.catalogRoot, release), cancellationToken).ConfigureAwait(false);
            var result = await Task.Run(
                () => this.setup.Repair(new InstalledRelease(release.ReleaseTag, release.ResolvedCommit, "bundled"), source),
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

        private LoadedCatalogRelease? SelectedRelease() =>
            this.releases.FirstOrDefault(release => this.Label(release) == this.SelectedReleaseVersion);

        private string Label(LoadedCatalogRelease release) =>
            release.IsPrerelease ? $"{release.ReleaseTag} (prerelease)" : release.ReleaseTag;

        private static string LevelLabel(StatusLevel level) => level switch
        {
            StatusLevel.Pass => "OK",
            StatusLevel.Warning => "Check",
            StatusLevel.Fail => "Problem",
            _ => "Unknown",
        };
    }
}
