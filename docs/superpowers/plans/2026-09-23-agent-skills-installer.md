# Agent Skills Installer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the extension as an installer, version manager and status panel that puts unmodified upstream Superpowers skills plus a *Superpowers* custom agent into the user's profile, so VS 2026 Copilot Chat uses them natively.

**Architecture:** Portable, unit-tested install logic lives in `TheKameleon.Superpowers.Skills/Install`, `/Bootstrap` and `/Status`, all driven through one facade, `SuperpowersSetup`, which is rooted at an injectable `ProfilePaths` so tests never touch the real profile. The VSIX is a thin Remote UI tool window over that facade. The in-process bridge and the prompt/workflow/execution subsystems are deleted. Phase 1 ships bundled releases only; Phase 2 hardens and re-enables GitHub downloads.

**Tech Stack:** C# 12, .NET 8 (`net8.0` libraries/tests, `net8.0-windows8.0` VSIX and integration tests), Microsoft.VisualStudio.Extensibility SDK 17.14.40608 (Remote UI), xUnit v3, System.Text.Json, System.IO.Compression.

**Spec:** `docs/superpowers/specs/2026-09-23-agent-skills-installer-design.md`

## Global Constraints

- Supported IDE: Visual Studio 2026 **18.5 or later** only (`InstallationTargetVersion = "[18.5,)"`), architectures amd64 and arm64.
- Personal skills folder: `%USERPROFILE%\.copilot\skills\`. Agent file: `%USERPROFILE%\.github\agents\superpowers.agent.md`. Always-on file: `%USERPROFILE%\copilot-instructions.md`. State: `%LOCALAPPDATA%\TheKameleon.Superpowers\install-state.json`.
- Upstream `SKILL.md` files and their supporting files are installed **byte-for-byte unchanged**.
- The extension changes only files recorded as its own, plus its delimited block. It never overwrites or deletes anything else.
- Nothing is written to the user profile until the user clicks **Install**.
- Always-on is **off by default**. Block markers: `<!-- superpowers:begin (managed by Superpowers for Visual Studio; edit outside this block) -->` and `<!-- superpowers:end -->`.
- Skill front-matter rules: `name` is 1–64 characters of lowercase letters, digits and hyphens, and equals the folder name; `description` is present and at most 1,024 characters.
- Agent picker name `Superpowers`; menu label `Superpowers`; VSIX display name `Superpowers for Visual Studio`.
- Icon: original artwork only. No upstream Superpowers branding and no superhero trademark imagery.
- Tests must use a temporary `ProfilePaths`, never the real user profile.
- No other AI provider, no private or unsupported Copilot API, no Copilot log content displayed or stored.

## Deviations from the spec (decided while planning)

| Spec | Plan | Reason |
|---|---|---|
| §7 check 1 (VS version ≥ 18.5 at runtime) | Enforced by the manifest `InstallationTargetVersion`; no runtime check | The VSIX cannot install on older versions, so a runtime check can never fail. |
| §5 `agentFile.path`, `alwaysOn.blockSha256` | Omitted | The path is derived from `ProfilePaths`; the block is rewritten whole, so a block hash adds nothing. |
| §8 bootstrap as an embedded resource | A C# raw string constant (`BootstrapText.Body`) | Same effect with no resource plumbing, and it is directly testable. |
| §9 SVG master | XAML drawing master `art/superpowers-icon.xaml`, rendered to PNG by `build/Render-Icons.ps1` | WPF renders XAML natively on Windows with no extra tools. The vector `.xaml` moniker variant is deferred; PNGs at 16/20/32 px are shipped. |
| §12 keep `Core/Contracts/Settings` (trimmed) | Keep only `ReleaseChannelFilter`; release and always-on choices live in `install-state.json` | One source of truth for the installed state. |
| §12 delete list | Also deletes `Skills/Discovery/*` | User/solution skill discovery is Copilot's job now. |

## File Structure

**Created (Skills library, `TheKameleon.Superpowers.Skills`):**

| File | Responsibility |
|---|---|
| `Catalog/ReleaseSelection.cs` | Pick the default release (newest stable without errors). |
| `Install/ProfilePaths.cs` | Every profile/state path, rooted at an injectable user profile and local app data. |
| `Install/ContentHash.cs` | SHA-256 hex helpers. |
| `Install/SkillPackage.cs` | A skill folder's files in memory. |
| `Install/SkillFrontMatter.cs` | Minimal YAML front-matter reader (`name`, `description`). |
| `Install/SkillValidator.cs` | VS Agent Skills rules. |
| `Install/SkillArchiveReader.cs` | Safely read `*/skills/<name>/**` from an upstream zipball. |
| `Install/ReleaseSkillLoader.cs` | Load skill packages for a validated catalog release. |
| `Install/InstallState.cs` | State records. |
| `Install/InstallStateStore.cs` | Atomic load/save of `install-state.json`. |
| `Install/InstallLock.cs` | Cross-process named mutex. |
| `Install/SkillInstaller.cs` | Stage, validate, swap with rollback, remove. |
| `Bootstrap/BootstrapText.cs` | Versioned bootstrap body. |
| `Bootstrap/AgentFileWriter.cs` | Write/refresh/remove `superpowers.agent.md`. |
| `Bootstrap/AlwaysOnBlockEditor.cs` | Pure text insert/replace/remove of the delimited block. |
| `Bootstrap/AlwaysOnInstructionsFile.cs` | Apply the editor to the real file, preserving encoding. |
| `Setup/SuperpowersSetup.cs` | Facade: preview, install, remove, repair, always-on, agent refresh. |
| `Status/StatusProbe.cs` | Status check list. |
| `Status/CopilotLogDiagnostic.cs` | Best-effort Copilot log scan. |
| `Catalog/DownloadedReleases.cs` (Phase 2) | Downloaded catalog roots under the state directory. |

**Created (VSIX):** `OpenSuperpowersCommand.cs`, `SuperpowersViewModel.cs`, `StatusItem.cs`, `Images/Superpowers.16.16.png`, `Images/Superpowers.20.20.png`, `Resources/icon.png`, `Resources/preview.png`. **Created elsewhere:** `art/superpowers-icon.xaml`, `build/Render-Icons.ps1`, `docs/superpowers/acceptance/agent-skills-acceptance.md`.

**Modified:** `.gitignore`, `BundledCatalogLoader.cs`, `LoadedCatalogRelease.cs`, `ApprovedReleaseDownloadService.cs` (Phase 2), `SuperpowersExtension.cs`, `SuperpowersToolWindow.cs`, `SuperpowersToolWindowControl.cs/.xaml`, `string-resources.json`, all five `.csproj` files, `TheKameleon.Superpowers.slnx`, `build/SuperpowersBuildVersion.targets`, integration tests, `README.md`, `docs/superpowers/plans/implementation-plan.md`.

**Deleted:** see Task 2 (Phase 1) and Task 17 (Phase 2).

## How to run things

- Build everything: `dotnet build TheKameleon.Superpowers.slnx` (needs a `.slnx`-capable SDK, .NET SDK 9.0.200 or later).
- Unit tests: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj`
- One unit test class: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~<ClassName>"`
- Integration tests (they build the VSIX first): `dotnet test TheKameleon.Superpowers.IntegrationTests/TheKameleon.Superpowers.IntegrationTests.csproj`

---

# Phase 1 — Bundled-release installer

### Task 1: Commit the bundled release archives

The default Visual Studio `.gitignore` rule `[Rr]eleases/` hides `bundled-catalog/**/releases/`, so clean clones and CI build a VSIX whose catalog points at missing archives.

**Files:**
- Modify: `.gitignore` (after the `[Rr]eleases/` line, line 24)
- Add: `bundled-catalog/obra.superpowers/2026-09-21/releases/**` (13 release folders, about 6.7 MB)
- Test: `TheKameleon.Superpowers.Tests/BundledCatalogLoaderTests.cs` (existing `LoadsBundledCatalogFromRealDirectory`)

**Interfaces:** Consumes nothing. Produces the committed archives that every later catalog test relies on.

- [ ] **Step 1: Run the existing real-catalog test to see it fail in a clean checkout**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~BundledCatalogLoaderTests.LoadsBundledCatalogFromRealDirectory"`
Expected: FAIL with diagnostics such as `SPCAT415 ... missing its bundled source archive`.

- [ ] **Step 2: Re-include the releases folder in `.gitignore`**

Directly after the line `[Rr]eleases/` add:

```gitignore
# Bundled upstream Superpowers releases are product content, not build output.
!bundled-catalog/**/releases/
```

- [ ] **Step 3: Copy the archives from the maintainer's local catalog**

The only existing copy is in the main checkout. Copy it (PowerShell, from the repository root):

```powershell
Copy-Item -Recurse -Force "C:\Users\kamel\source\repos\TheKameleon.Superpowers.Vsix\bundled-catalog\obra.superpowers\2026-09-21\releases" "bundled-catalog\obra.superpowers\2026-09-21\"
```

If that folder is unavailable, regenerate with `build/Generate-BundledCatalog.ps1` instead and commit the regenerated `catalog.json` too.

- [ ] **Step 4: Confirm git now sees the files and the test passes**

Run: `git check-ignore -v bundled-catalog/obra.superpowers/2026-09-21/releases/v6.4.1/source.zip`
Expected: no output (not ignored).

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~BundledCatalogLoaderTests.LoadsBundledCatalogFromRealDirectory"`
Expected: PASS. If it fails with `SPCAT418`/`SPCAT421`, the copied archives do not match the committed `catalog.json`; stop and report rather than editing hashes.

- [ ] **Step 5: Commit**

```bash
git add .gitignore bundled-catalog/obra.superpowers/2026-09-21/releases
git commit -m "fix: commit bundled Superpowers release archives excluded by .gitignore"
```

---

### Task 2: Retire the legacy subsystems

Delete the bridge, context capture, prompt/workflow and execution code (spec §12, N02). This leaves a compiling solution with a placeholder tool window that Task 13 replaces.

**Files:**
- Delete projects: `TheKameleon.Superpowers.InProcess/`, `TheKameleon.Superpowers.Bridge.Contracts/`
- Delete: `build/BridgeDebugDeployment.targets`, `scripts/Prepare-BridgeDebug.ps1`
- Delete (VSIX): `TheKameleon.Superpowers.Vsix/Bridge/`, `Context/`, `Execution/`, `BridgeProbeCommand.cs`, `BuildProbeCommand.cs`, `ContextProbeCommand.cs`, `DiagnosticsProbeCommands.cs`, `ProbeResultsViewModel.cs`, `SuperpowersWorkflowViewModel.cs`
- Delete (Skills): `TheKameleon.Superpowers.Skills/Composition/`, `Context/`, `Discovery/`, `Execution/`, `Workflow/`
- Delete (Core): `Contracts/Context/`, `Contracts/Runs/`, `Contracts/Catalog/CatalogReloadResult.cs`, `Contracts/Catalog/DiscoveryResult.cs`, `Contracts/Settings/CriticalWarningPolicy.cs`, `CustomCommandAllowlistEntry.cs`, `ExclusionRule.cs`, `ExecutionMode.cs`, `SuperpowersSettings.cs`
- Delete (unit tests): `ActionPolicyEvaluatorTests.cs`, `ActiveDocumentBridgeResolverTests.cs`, `AdapterRunStateTests.cs`, `ApprovalServiceTests.cs`, `BridgeCapabilityCatalogTests.cs`, `BridgePipeTransportIntegrationTests.cs`, `CatalogReloadServiceTests.cs`, `CommandAllowlistValidatorTests.cs`, `ContextPrivacyServiceTests.cs`, `CopilotHandoffCoordinatorTests.cs`, `DocumentCompilerDiagnosticsTests.cs`, `GitStatusParserTests.cs`, `PreimageValidatorTests.cs`, `PreviewHandoffServiceTests.cs`, `PromptComposerTests.cs`, `RunLifecycleCoordinatorTests.cs`, `SelectionIdentityTests.cs`, `SelectionKindResolverTests.cs`, `SemanticTargetResolverTests.cs`, `SkillCompositionCoordinatorTests.cs`, `SkillDiscoveryServiceTests.cs`, `SuperpowersSettingsTests.cs`, `UnavailableBridgeClientTests.cs`, `UnitTest1.cs`, `UnsupportedCopilotBridgeTests.cs`, `WorkflowOrchestratorTests.cs`, `WorkflowStoreTests.cs`
- Delete (integration tests): `BridgeDebugDeploymentTests.cs`, `InProcessBridgePackageTests.cs`, `WorkflowArtifactsPackageTests.cs`
- Modify: `TheKameleon.Superpowers.slnx`, all remaining `.csproj` files, `build/SuperpowersBuildVersion.targets`, `SuperpowersExtension.cs`, `SuperpowersToolWindow.cs`, `SuperpowersToolWindowControl.cs`, `SuperpowersToolWindowControl.xaml`, `.vsextension/string-resources.json`, `BuildIdentityPackageTests.cs`, `BuildVersionTests.cs`, `ExtensionPackageTests.cs`, `PlanToolWindowPackageTests.cs`
- Create: `TheKameleon.Superpowers.Vsix/SuperpowersViewModel.cs` (placeholder)

**Interfaces:** Consumes nothing. Produces `internal sealed class SuperpowersViewModel` (placeholder with `[DataMember] string StatusText`), replaced in Task 13. The VSIX becomes the build-version **Owner**.

- [ ] **Step 1: Delete the files listed above**

```bash
git rm -r -q TheKameleon.Superpowers.InProcess TheKameleon.Superpowers.Bridge.Contracts build/BridgeDebugDeployment.targets scripts/Prepare-BridgeDebug.ps1
cd TheKameleon.Superpowers.Vsix && git rm -r -q Bridge Context Execution BridgeProbeCommand.cs BuildProbeCommand.cs ContextProbeCommand.cs DiagnosticsProbeCommands.cs ProbeResultsViewModel.cs SuperpowersWorkflowViewModel.cs && cd ..
cd TheKameleon.Superpowers.Skills && git rm -r -q Composition Context Discovery Execution Workflow && cd ..
cd TheKameleon.Superpowers.Core/Contracts && git rm -r -q Context Runs Catalog/CatalogReloadResult.cs Catalog/DiscoveryResult.cs Settings/CriticalWarningPolicy.cs Settings/CustomCommandAllowlistEntry.cs Settings/ExclusionRule.cs Settings/ExecutionMode.cs Settings/SuperpowersSettings.cs && cd ../..
cd TheKameleon.Superpowers.Tests && git rm -q ActionPolicyEvaluatorTests.cs ActiveDocumentBridgeResolverTests.cs AdapterRunStateTests.cs ApprovalServiceTests.cs BridgeCapabilityCatalogTests.cs BridgePipeTransportIntegrationTests.cs CatalogReloadServiceTests.cs CommandAllowlistValidatorTests.cs ContextPrivacyServiceTests.cs CopilotHandoffCoordinatorTests.cs DocumentCompilerDiagnosticsTests.cs GitStatusParserTests.cs PreimageValidatorTests.cs PreviewHandoffServiceTests.cs PromptComposerTests.cs RunLifecycleCoordinatorTests.cs SelectionIdentityTests.cs SelectionKindResolverTests.cs SemanticTargetResolverTests.cs SkillCompositionCoordinatorTests.cs SkillDiscoveryServiceTests.cs SuperpowersSettingsTests.cs UnavailableBridgeClientTests.cs UnitTest1.cs UnsupportedCopilotBridgeTests.cs WorkflowOrchestratorTests.cs WorkflowStoreTests.cs && cd ..
cd TheKameleon.Superpowers.IntegrationTests && git rm -q BridgeDebugDeploymentTests.cs InProcessBridgePackageTests.cs WorkflowArtifactsPackageTests.cs && cd ..
```

- [ ] **Step 2: Update the solution file**

Replace the whole content of `TheKameleon.Superpowers.slnx` with:

```xml
<Solution>
  <Project Path="TheKameleon.Superpowers.Core/TheKameleon.Superpowers.Core.csproj" />
  <Project Path="TheKameleon.Superpowers.IntegrationTests/TheKameleon.Superpowers.IntegrationTests.csproj" />
  <Project Path="TheKameleon.Superpowers.Skills/TheKameleon.Superpowers.Skills.csproj" />
  <Project Path="TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj" />
  <Project Path="TheKameleon.Superpowers.Vsix/TheKameleon.Superpowers.Vsix.csproj">
    <Deploy />
  </Project>
</Solution>
```

- [ ] **Step 3: Update the project files**

In `TheKameleon.Superpowers.Skills.csproj` delete the line:

```xml
    <ProjectReference Include="..\TheKameleon.Superpowers.Bridge.Contracts\TheKameleon.Superpowers.Bridge.Contracts.csproj" />
```

In `TheKameleon.Superpowers.Tests.csproj` delete the `Microsoft.CodeAnalysis.CSharp.Workspaces` `PackageReference`, the `Bridge.Contracts` `ProjectReference`, and all nine `<Compile Include="..\TheKameleon.Superpowers.InProcess\..." />` / `<Compile Include="..\TheKameleon.Superpowers.Vsix\Bridge\..." />` lines. The final project-reference group is:

```xml
  <ItemGroup>
    <ProjectReference Include="..\TheKameleon.Superpowers.Core\TheKameleon.Superpowers.Core.csproj" />
    <ProjectReference Include="..\TheKameleon.Superpowers.Skills\TheKameleon.Superpowers.Skills.csproj" />
  </ItemGroup>
```

In `TheKameleon.Superpowers.IntegrationTests.csproj` replace the second `ItemGroup` of references (the one that starts with the Skills `ProjectReference`) with:

```xml
  <ItemGroup>
    <ProjectReference Include="..\TheKameleon.Superpowers.Skills\TheKameleon.Superpowers.Skills.csproj" />
    <ProjectReference Include="..\TheKameleon.Superpowers.Vsix\TheKameleon.Superpowers.Vsix.csproj" ReferenceOutputAssembly="false" />
    <None Include="..\TheKameleon.Superpowers.Vsix\bin\$(Configuration)\net8.0-windows8.0\TheKameleon.Superpowers.Vsix.vsix" Link="TheKameleon.Superpowers.Vsix.vsix" CopyToOutputDirectory="Always" />
  </ItemGroup>
```

In `TheKameleon.Superpowers.Vsix.csproj`:
- change `<SuperpowersBuildRole>Consumer</SuperpowersBuildRole>` to `<SuperpowersBuildRole>Owner</SuperpowersBuildRole>`;
- delete the `MessagePack` `PackageReference` (no remaining usages);
- delete the `TheKameleon.Superpowers.InProcess` `ProjectReference`;
- delete `<Import Project="..\build\BridgeDebugDeployment.targets" />`.

- [ ] **Step 4: Make the build-version targets work with the VSIX as owner**

In `build/SuperpowersBuildVersion.targets`:

1. Replace the `SuperpowersBridgeVersionFile` property line with:

```xml
	<SuperpowersBuildVersionFile Condition="'$(SuperpowersBuildVersionFile)' == ''">$(MSBuildProjectDirectory)\obj\$(Configuration)\superpowers-build-version.txt</SuperpowersBuildVersionFile>
```

2. Replace every other occurrence of `$(SuperpowersBridgeVersionFile)` with `$(SuperpowersBuildVersionFile)`.
3. Change the `Consumer` error text to `Text="Build the version owner before this project; the shared build version is missing."`.
4. Change the `GenerateSuperpowersMetadataVersion` target condition from `'$(SuperpowersBuildRole)' == 'Consumer'` to `'$(SuperpowersBuildRole)' != ''`, so the owning VSIX still generates `GeneratedBuildVersion`.
5. Delete the whole `StampSuperpowersBridgeManifest` target (VSSDK-only).

In `TheKameleon.Superpowers.IntegrationTests/BuildVersionTests.cs` replace both occurrences of `SuperpowersBridgeVersionFile` (lines 24 and 133) with `SuperpowersBuildVersionFile`. If `ConsumerFailsWhenDependencyStampIsMissing` asserts the old error text (`Build the bridge dependency…`), update the expected text to `Build the version owner before this project`.

- [ ] **Step 5: Replace the tool window with a placeholder**

Create `TheKameleon.Superpowers.Vsix/SuperpowersViewModel.cs`:

```csharp
using System.Runtime.Serialization;
using Microsoft.VisualStudio.Extensibility.UI;

namespace TheKameleon.Superpowers.Vsix
{
    [DataContract]
    internal sealed class SuperpowersViewModel : NotifyPropertyChangedObject
    {
        private string statusText = "Superpowers is being rebuilt as a skills installer.";

        [DataMember]
        public string StatusText
        {
            get => this.statusText;
            set => this.SetProperty(ref this.statusText, value);
        }
    }
}
```

Replace `TheKameleon.Superpowers.Vsix/SuperpowersToolWindowControl.cs` with:

```csharp
using Microsoft.VisualStudio.Extensibility.UI;

namespace TheKameleon.Superpowers.Vsix
{
    internal sealed class SuperpowersToolWindowControl : RemoteUserControl
    {
        public SuperpowersToolWindowControl(SuperpowersViewModel dataContext)
            : base(dataContext)
        {
        }
    }
}
```

Replace `TheKameleon.Superpowers.Vsix/SuperpowersToolWindowControl.xaml` with:

```xml
<DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
              xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
              xmlns:colors="clr-namespace:Microsoft.VisualStudio.PlatformUI;assembly=Microsoft.VisualStudio.Shell.15.0">
    <Border Padding="12"
            Background="{DynamicResource {x:Static colors:EnvironmentColors.ToolWindowBackgroundBrushKey}}"
            TextElement.Foreground="{DynamicResource {x:Static colors:EnvironmentColors.ToolWindowTextBrushKey}}">
        <TextBlock Text="{Binding StatusText}" TextWrapping="Wrap" />
    </Border>
</DataTemplate>
```

Replace `TheKameleon.Superpowers.Vsix/SuperpowersToolWindow.cs` with:

```csharp
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.ToolWindows;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;

namespace TheKameleon.Superpowers.Vsix
{
    [VisualStudioContribution]
    public sealed class SuperpowersToolWindow : ToolWindow
    {
        private SuperpowersToolWindowControl? control;

        public SuperpowersToolWindow()
        {
            this.Title = "Superpowers";
        }

        public override ToolWindowConfiguration ToolWindowConfiguration => new()
        {
            AllowAutoCreation = true,
            Placement = ToolWindowPlacement.DocumentWell,
        };

        public override Task<IRemoteUserControl> GetContentAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.control ??= new SuperpowersToolWindowControl(new SuperpowersViewModel());
            return Task.FromResult<IRemoteUserControl>(this.control);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.control?.Dispose();
                this.control = null;
            }

            base.Dispose(disposing);
        }
    }
}
```

In `SuperpowersExtension.cs` reduce the menu children to `MenuChild.Command<PlanCommand>(),` only. In `.vsextension/string-resources.json` keep only:

```json
{
  "Superpowers.PlanCommand.DisplayName": "Plan",
  "Superpowers.Menu.DisplayName": "Superpowers"
}
```

- [ ] **Step 6: Update the integration tests that referenced removed code**

Replace the body of `BuildIdentityPackageTests.BothPackagesAndTheirDllsShareOneBuildVersion` (rename the method to `PackageAndDllShareOneBuildVersion`) with:

```csharp
        var modern = ReadIdentity("TheKameleon.Superpowers.Vsix");

        Assert.Equal(modern.PackageVersion, modern.FileVersion);
        Assert.Equal(modern.FileVersion, modern.InformationalVersion);
        Assert.True(Version.Parse(modern.PackageVersion).Revision > 0);
        Assert.Equal(new Version(1, 0, 1, 0), modern.AssemblyVersion);
```

In `ExtensionPackageTests.cs` delete the `BuildDependencyDoesNotBundleBridgeRuntime` test method.

In `PlanToolWindowPackageTests.cs` delete these test methods: `PackageRegistersCapabilityProbeCommand`, `PackageEmbedsRemoteViewWithWorkflowBindings`, `PackageEmbedsHistorySearchAndPlanCompositionControlsWithAccessibleNames`, `WorkflowViewModelDeclaresRemoteUiSerializationAttributes`. Keep the private helpers.

- [ ] **Step 7: Build and run all tests**

Run: `dotnet build TheKameleon.Superpowers.slnx`
Expected: Build succeeded, 0 errors. If a kept file fails to compile because it references a deleted type, the delete list is wrong: stop and report the type and file rather than deleting more.

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj`
Expected: PASS (the remaining catalog/parser tests).

Run: `dotnet test TheKameleon.Superpowers.IntegrationTests/TheKameleon.Superpowers.IntegrationTests.csproj`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "refactor: retire bridge, context capture, prompt and execution subsystems"
```

---

### Task 3: Expose archive path, prerelease flag and default release selection

**Files:**
- Modify: `TheKameleon.Superpowers.Core/Contracts/Catalog/LoadedCatalogRelease.cs`
- Modify: `TheKameleon.Superpowers.Skills/Catalog/BundledCatalogLoader.cs` (`LoadRelease` return at lines 135–143; `CatalogReleaseModel` class)
- Create: `TheKameleon.Superpowers.Skills/Catalog/ReleaseSelection.cs`
- Test: `TheKameleon.Superpowers.Tests/ReleaseSelectionTests.cs`, `TheKameleon.Superpowers.Tests/BundledCatalogLoaderTests.cs`
- Create: `TheKameleon.Superpowers.Tests/TestSupport.cs`

**Interfaces:**
- Produces on `LoadedCatalogRelease`: `string ArchivePath { get; init; }`, `bool IsPrerelease { get; init; }`, `DateTimeOffset? PublishedAtUtc { get; init; }`.
- Produces: `static LoadedCatalogRelease? ReleaseSelection.DefaultRelease(IEnumerable<LoadedCatalogRelease> releases)`.
- Produces test helpers `TestSupport.RepositoryPath(params string[])`, `TestSupport.Release(string tag, bool prerelease, DateTimeOffset published, bool hasError = false)`.

- [ ] **Step 1: Write the test helpers**

Create `TheKameleon.Superpowers.Tests/TestSupport.cs`:

```csharp
using TheKameleon.Superpowers.Core.Contracts.Catalog;

namespace TheKameleon.Superpowers.Tests;

internal static class TestSupport
{
    public static string RepositoryPath(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TheKameleon.Superpowers.slnx")))
            {
                return Path.Combine(new[] { current.FullName }.Concat(segments).ToArray());
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from the test output directory.");
    }

    public static string BundledCatalogRoot => RepositoryPath("bundled-catalog", "obra.superpowers", "2026-09-21");

    public static LoadedCatalogRelease Release(string tag, bool prerelease, DateTimeOffset published, bool hasError = false)
    {
        var diagnostics = hasError
            ? new[] { new ParseDiagnostic(ParseDiagnosticSeverity.Error, "TEST", "broken") }
            : Array.Empty<ParseDiagnostic>();
        return new LoadedCatalogRelease(
            tag,
            "commit-" + tag,
            "MIT",
            new AdapterManifest(1, Array.Empty<AdapterManifestAction>(), Array.Empty<ParseDiagnostic>()),
            null,
            Array.Empty<DiscoveredSkillEntry>(),
            Array.Empty<string>(),
            diagnostics)
        {
            ArchivePath = $"releases/{tag}/source.zip",
            IsPrerelease = prerelease,
            PublishedAtUtc = published,
        };
    }
}
```

- [ ] **Step 2: Write the failing tests**

Create `TheKameleon.Superpowers.Tests/ReleaseSelectionTests.cs`:

```csharp
using TheKameleon.Superpowers.Skills.Catalog;

namespace TheKameleon.Superpowers.Tests;

public sealed class ReleaseSelectionTests
{
    [Fact]
    public void PicksNewestStableRelease()
    {
        var releases = new[]
        {
            TestSupport.Release("v1.0.0", prerelease: false, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            TestSupport.Release("v2.0.0", prerelease: false, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)),
            TestSupport.Release("v1.5.0", prerelease: false, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)),
        };

        Assert.Equal("v2.0.0", ReleaseSelection.DefaultRelease(releases)!.ReleaseTag);
    }

    [Fact]
    public void IgnoresPrereleasesAndReleasesWithErrors()
    {
        var releases = new[]
        {
            TestSupport.Release("v1.0.0", prerelease: false, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            TestSupport.Release("v3.0.0-beta", prerelease: true, new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)),
            TestSupport.Release("v2.0.0", prerelease: false, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), hasError: true),
        };

        Assert.Equal("v1.0.0", ReleaseSelection.DefaultRelease(releases)!.ReleaseTag);
    }

    [Fact]
    public void ReturnsNullWhenNothingIsEligible()
    {
        Assert.Null(ReleaseSelection.DefaultRelease(Array.Empty<Core.Contracts.Catalog.LoadedCatalogRelease>()));
    }
}
```

Add to `BundledCatalogLoaderTests.cs`:

```csharp
    [Fact]
    public void ExposesArchivePathPrereleaseFlagAndPublishDate()
    {
        var result = BundledCatalogLoader.LoadFromDirectory(TestSupport.BundledCatalogRoot);

        var latest = Assert.Single(result.Releases, release => release.ReleaseTag == "v6.4.1");
        Assert.Equal("releases/v6.4.1/source.zip", latest.ArchivePath);
        Assert.False(latest.IsPrerelease);
        Assert.Equal(new DateTimeOffset(2026, 9, 19, 0, 32, 44, TimeSpan.Zero), latest.PublishedAtUtc);
        Assert.Equal("v6.4.1", ReleaseSelection.DefaultRelease(result.Releases)!.ReleaseTag);
    }
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~ReleaseSelectionTests|FullyQualifiedName~ExposesArchivePath"`
Expected: build FAIL — `ArchivePath`, `IsPrerelease`, `PublishedAtUtc` and `ReleaseSelection` do not exist.

- [ ] **Step 4: Implement**

Replace `LoadedCatalogRelease.cs` body so the record keeps its constructor and gains three init properties:

```csharp
namespace TheKameleon.Superpowers.Core.Contracts.Catalog;

public sealed record LoadedCatalogRelease(
    string ReleaseTag,
    string ResolvedCommit,
    string LicenseText,
    AdapterManifest AdapterManifest,
    PlanEntryPointMetadata? PlanMetadata,
    IReadOnlyList<DiscoveredSkillEntry> Skills,
    IReadOnlyList<string> Assets,
    IReadOnlyList<ParseDiagnostic> Diagnostics)
{
    public string ArchivePath { get; init; } = string.Empty;

    public bool IsPrerelease { get; init; }

    public DateTimeOffset? PublishedAtUtc { get; init; }

    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == ParseDiagnosticSeverity.Error)
        || AdapterManifest.HasErrors
        || (PlanMetadata?.SchemaVersion ?? PlanEntryPointMetadata.CurrentSchemaVersion) <= 0
        || Skills.Any(skill => skill.Diagnostics.Any(diagnostic => diagnostic.Severity == ParseDiagnosticSeverity.Error));
}
```

In `BundledCatalogLoader.cs`, add `using System.Globalization;` at the top, add two properties to `CatalogReleaseModel`:

```csharp
        public bool Prerelease { get; init; }

        public string? PublishedAtUtc { get; init; }
```

and replace the `return new LoadedCatalogRelease(...);` statement at the end of `LoadRelease` with:

```csharp
        DateTimeOffset? publishedAtUtc = DateTimeOffset.TryParse(
            release.PublishedAtUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var published)
            ? published
            : null;

        return new LoadedCatalogRelease(
            releaseTag,
            provenance.ResolvedCommit ?? release.ResolvedCommit ?? string.Empty,
            licenseText,
            adapterManifest,
            planMetadata,
            skills,
            assets.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray(),
            diagnostics)
        {
            ArchivePath = release.ArchivePath ?? string.Empty,
            IsPrerelease = release.Prerelease,
            PublishedAtUtc = publishedAtUtc,
        };
```

Create `TheKameleon.Superpowers.Skills/Catalog/ReleaseSelection.cs`:

```csharp
using TheKameleon.Superpowers.Core.Contracts.Catalog;

namespace TheKameleon.Superpowers.Skills.Catalog;

public static class ReleaseSelection
{
    public static LoadedCatalogRelease? DefaultRelease(IEnumerable<LoadedCatalogRelease> releases)
    {
        ArgumentNullException.ThrowIfNull(releases);
        return releases
            .Where(release => !release.HasErrors && !release.IsPrerelease)
            .OrderByDescending(release => release.PublishedAtUtc ?? DateTimeOffset.MinValue)
            .FirstOrDefault();
    }
}
```

In `BundledCatalogLoaderTests.cs` replace the private `GetRepositoryRelativePath(...)` calls with `TestSupport.RepositoryPath(...)` and delete the private helper.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: expose release archive path and prerelease data; select newest stable by default"
```

---

### Task 4: Skill front-matter reader and validator

The existing `SkillDocumentParser` rejects `../` and `https://` links that some upstream skills contain, so installation uses a separate, minimal validator for exactly the VS rules.

**Files:**
- Create: `TheKameleon.Superpowers.Skills/Install/SkillPackage.cs`, `Install/SkillFrontMatter.cs`, `Install/SkillValidator.cs`
- Test: `TheKameleon.Superpowers.Tests/SkillValidatorTests.cs`
- Modify: `TheKameleon.Superpowers.Tests/TestSupport.cs` (add `Skill` helpers)

**Interfaces:**
- Produces: `public sealed record SkillPackage(string Name, IReadOnlyDictionary<string, byte[]> Files)`. File keys are relative paths with `/` separators, for example `SKILL.md`, `scripts/run.sh`.
- Produces: `public sealed record SkillFrontMatter(string? Name, string? Description)` with `static SkillFrontMatter? Read(string text)`.
- Produces: `static IReadOnlyList<string> SkillValidator.Validate(SkillPackage package)`; an empty list means valid.
- Produces test helpers `TestSupport.Skill(string name, params (string Path, string Content)[] extraFiles)` and `TestSupport.SkillWithMarkdown(string folderName, string skillMarkdown)`.

- [ ] **Step 1: Add test helpers**

Append to `TestSupport` (add `using System.Text;` and `using TheKameleon.Superpowers.Skills.Install;`):

```csharp
    public static SkillPackage Skill(string name, params (string Path, string Content)[] extraFiles)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["SKILL.md"] = Encoding.UTF8.GetBytes($"---\nname: {name}\ndescription: Use when testing {name}.\n---\n\nBody of {name}.\n"),
        };
        foreach (var (path, content) in extraFiles)
        {
            files[path] = Encoding.UTF8.GetBytes(content);
        }

        return new SkillPackage(name, files);
    }

    public static SkillPackage SkillWithMarkdown(string folderName, string skillMarkdown)
    {
        return new SkillPackage(folderName, new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["SKILL.md"] = Encoding.UTF8.GetBytes(skillMarkdown),
        });
    }
```

- [ ] **Step 2: Write the failing tests**

Create `TheKameleon.Superpowers.Tests/SkillValidatorTests.cs`:

```csharp
using System.Text;
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Tests;

public sealed class SkillValidatorTests
{
    [Fact]
    public void AcceptsUpstreamStyleSkill()
    {
        var package = TestSupport.SkillWithMarkdown(
            "brainstorming",
            "---\nname: brainstorming\ndescription: \"You MUST use this before any creative work - creating features.\"\n---\n\n# Brainstorming\n");

        Assert.Empty(SkillValidator.Validate(package));
    }

    [Fact]
    public void AcceptsFoldedMultilineDescriptionAndBom()
    {
        var markdown = "\uFEFF---\r\nname: aspire\r\ndescription: >-\r\n  First line\r\n  second line.\r\nmetadata:\r\n  owner: team\r\n---\r\nBody\r\n";
        var package = new SkillPackage("aspire", new Dictionary<string, byte[]> { ["SKILL.md"] = Encoding.UTF8.GetBytes(markdown) });

        Assert.Empty(SkillValidator.Validate(package));
        Assert.Equal("First line second line.", SkillFrontMatter.Read(markdown)!.Description);
    }

    [Theory]
    [InlineData("---\nname: Brainstorming\ndescription: x\n---\n", "brainstorming", "lowercase")]
    [InlineData("---\nname: other\ndescription: x\n---\n", "brainstorming", "does not match folder")]
    [InlineData("---\ndescription: x\n---\n", "brainstorming", "'name' is missing")]
    [InlineData("---\nname: brainstorming\n---\n", "brainstorming", "'description' is missing")]
    [InlineData("# no front matter\n", "brainstorming", "no YAML front matter")]
    public void RejectsInvalidFrontMatter(string markdown, string folder, string expectedFragment)
    {
        var problems = SkillValidator.Validate(TestSupport.SkillWithMarkdown(folder, markdown));

        Assert.Contains(problems, problem => problem.Contains(expectedFragment, StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsDescriptionLongerThan1024Characters()
    {
        var markdown = $"---\nname: long\ndescription: {new string('a', 1025)}\n---\n";

        Assert.Contains(SkillValidator.Validate(TestSupport.SkillWithMarkdown("long", markdown)), problem => problem.Contains("1024", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsMissingSkillFile()
    {
        var package = new SkillPackage("empty", new Dictionary<string, byte[]> { ["README.md"] = Array.Empty<byte>() });

        Assert.Contains(SkillValidator.Validate(package), problem => problem.Contains("SKILL.md is missing", StringComparison.Ordinal));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~SkillValidatorTests"`
Expected: build FAIL — `SkillPackage`, `SkillValidator`, `SkillFrontMatter` do not exist.

- [ ] **Step 4: Implement**

`TheKameleon.Superpowers.Skills/Install/SkillPackage.cs`:

```csharp
namespace TheKameleon.Superpowers.Skills.Install;

public sealed record SkillPackage(string Name, IReadOnlyDictionary<string, byte[]> Files);
```

`TheKameleon.Superpowers.Skills/Install/SkillFrontMatter.cs`:

```csharp
namespace TheKameleon.Superpowers.Skills.Install;

/// <summary>Reads the top-level scalar keys of a SKILL.md YAML front-matter block.</summary>
public sealed record SkillFrontMatter(string? Name, string? Description)
{
    public static SkillFrontMatter? Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.TrimStart('\uFEFF').Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length == 0 || lines[0].TrimEnd() != "---")
        {
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        string? blockKey = null;
        var folded = false;
        var blockLines = new List<string>();

        for (var index = 1; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.TrimEnd() == "---")
            {
                FlushBlock();
                return new SkillFrontMatter(Get("name"), Get("description"));
            }

            if (blockKey is not null)
            {
                if (line.Length == 0 || char.IsWhiteSpace(line[0]))
                {
                    blockLines.Add(line.Trim());
                    continue;
                }

                FlushBlock();
            }

            if (line.Length == 0 || char.IsWhiteSpace(line[0]) || line.StartsWith('#'))
            {
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (value is ">" or ">-" or ">+" or "|" or "|-" or "|+")
            {
                blockKey = key;
                folded = value[0] == '>';
                blockLines.Clear();
                continue;
            }

            values[key] = Unquote(value);
        }

        return null;

        void FlushBlock()
        {
            if (blockKey is null)
            {
                return;
            }

            values[blockKey] = folded
                ? string.Join(" ", blockLines.Where(part => part.Length > 0))
                : string.Join("\n", blockLines).Trim();
            blockKey = null;
        }

        string? Get(string key) => values.TryGetValue(key, out var found) ? found : null;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
        }

        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        return value;
    }
}
```

`TheKameleon.Superpowers.Skills/Install/SkillValidator.cs`:

```csharp
using System.Text;
using System.Text.RegularExpressions;

namespace TheKameleon.Superpowers.Skills.Install;

/// <summary>Visual Studio Agent Skills front-matter rules.</summary>
public static class SkillValidator
{
    public const int MaxNameLength = 64;
    public const int MaxDescriptionLength = 1024;

    private static readonly Regex NamePattern = new("^[a-z0-9-]+$", RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> Validate(SkillPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var problems = new List<string>();
        if (!package.Files.TryGetValue("SKILL.md", out var bytes))
        {
            problems.Add("SKILL.md is missing.");
            return problems;
        }

        var frontMatter = SkillFrontMatter.Read(Encoding.UTF8.GetString(bytes));
        if (frontMatter is null)
        {
            problems.Add("SKILL.md has no YAML front matter.");
            return problems;
        }

        if (string.IsNullOrEmpty(frontMatter.Name))
        {
            problems.Add("Front matter 'name' is missing.");
        }
        else
        {
            if (frontMatter.Name.Length > MaxNameLength || !NamePattern.IsMatch(frontMatter.Name))
            {
                problems.Add($"Name '{frontMatter.Name}' must be 1-64 lowercase letters, digits or hyphens.");
            }

            if (!string.Equals(frontMatter.Name, package.Name, StringComparison.Ordinal))
            {
                problems.Add($"Name '{frontMatter.Name}' does not match folder '{package.Name}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(frontMatter.Description))
        {
            problems.Add("Front matter 'description' is missing.");
        }
        else if (frontMatter.Description.Length > MaxDescriptionLength)
        {
            problems.Add($"Description is {frontMatter.Description.Length} characters; the limit is 1024.");
        }

        return problems;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~SkillValidatorTests"`
Expected: PASS (all cases).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: validate skills against Visual Studio Agent Skills front-matter rules"
```

---

### Task 5: Read skills safely from a release archive

**Files:**
- Create: `TheKameleon.Superpowers.Skills/Install/SkillArchiveReader.cs`, `Install/ReleaseSkillLoader.cs`
- Test: `TheKameleon.Superpowers.Tests/SkillArchiveReaderTests.cs`

**Interfaces:**
- Consumes: `SkillPackage` (Task 4), `LoadedCatalogRelease.ArchivePath` (Task 3).
- Produces: `public sealed record SkillArchiveLimits(int MaxEntries, long MaxTotalBytes)` with `static SkillArchiveLimits Default` (5,000 entries, 50 MB).
- Produces: `public sealed record SkillArchiveReadResult(IReadOnlyList<SkillPackage> Skills, IReadOnlyList<string> Problems)`.
- Produces: `static SkillArchiveReadResult SkillArchiveReader.Read(Stream archiveStream, SkillArchiveLimits? limits = null)`.
- Produces: `static SkillArchiveReadResult ReleaseSkillLoader.Load(string catalogRoot, LoadedCatalogRelease release)`.

- [ ] **Step 1: Write the failing tests**

Create `TheKameleon.Superpowers.Tests/SkillArchiveReaderTests.cs`:

```csharp
using System.IO.Compression;
using System.Text;
using TheKameleon.Superpowers.Skills.Catalog;
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Tests;

public sealed class SkillArchiveReaderTests
{
    [Fact]
    public void ReadsEachSkillFolderWithSupportingFiles()
    {
        using var archive = BuildArchive(
            ("root/README.md", "ignored"),
            ("root/skills/alpha/SKILL.md", "alpha"),
            ("root/skills/alpha/scripts/run.sh", "echo hi"),
            ("root/skills/beta/SKILL.md", "beta"));

        var result = SkillArchiveReader.Read(archive);

        Assert.Empty(result.Problems);
        Assert.Equal(new[] { "alpha", "beta" }, result.Skills.Select(skill => skill.Name));
        var alpha = result.Skills[0];
        Assert.Equal(new[] { "SKILL.md", "scripts/run.sh" }, alpha.Files.Keys.OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal("echo hi", Encoding.UTF8.GetString(alpha.Files["scripts/run.sh"]));
    }

    [Theory]
    [InlineData("root/skills/../evil/SKILL.md")]
    [InlineData("root/skills/alpha/../../evil.txt")]
    [InlineData("root/skills/alpha/C:/evil.txt")]
    public void SkipsUnsafePaths(string unsafePath)
    {
        using var archive = BuildArchive(("root/skills/alpha/SKILL.md", "alpha"), (unsafePath, "bad"));

        var result = SkillArchiveReader.Read(archive);

        Assert.Contains(result.Problems, problem => problem.Contains("Unsafe archive path", StringComparison.Ordinal));
        var alpha = Assert.Single(result.Skills);
        Assert.Equal(new[] { "SKILL.md" }, alpha.Files.Keys);
    }

    [Fact]
    public void RejectsArchiveOverEntryLimit()
    {
        using var archive = BuildArchive(("root/skills/a/SKILL.md", "a"), ("root/skills/b/SKILL.md", "b"), ("root/skills/c/SKILL.md", "c"));

        var result = SkillArchiveReader.Read(archive, new SkillArchiveLimits(MaxEntries: 2, MaxTotalBytes: 1_000_000));

        Assert.Empty(result.Skills);
        Assert.Contains(result.Problems, problem => problem.Contains("entries", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsArchiveOverSizeLimit()
    {
        using var archive = BuildArchive(("root/skills/a/SKILL.md", new string('x', 2_000)));

        var result = SkillArchiveReader.Read(archive, new SkillArchiveLimits(MaxEntries: 100, MaxTotalBytes: 1_000));

        Assert.Empty(result.Skills);
        Assert.Contains(result.Problems, problem => problem.Contains("uncompressed", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadsAllSkillsFromTheNewestBundledRelease()
    {
        var catalog = BundledCatalogLoader.LoadFromDirectory(TestSupport.BundledCatalogRoot);
        var release = Assert.Single(catalog.Releases, candidate => candidate.ReleaseTag == "v6.4.1");

        var result = ReleaseSkillLoader.Load(TestSupport.BundledCatalogRoot, release);

        Assert.Empty(result.Problems);
        Assert.Equal(15, result.Skills.Count);
        Assert.All(result.Skills, skill => Assert.Empty(SkillValidator.Validate(skill)));
        Assert.Contains(result.Skills, skill => skill.Name == "using-superpowers");
    }

    [Fact]
    public void RefusesReleaseWithCatalogErrors()
    {
        var broken = TestSupport.Release("v9.9.9", prerelease: false, DateTimeOffset.UtcNow, hasError: true);

        var result = ReleaseSkillLoader.Load(TestSupport.BundledCatalogRoot, broken);

        Assert.Empty(result.Skills);
        Assert.Contains(result.Problems, problem => problem.Contains("failed catalog validation", StringComparison.Ordinal));
    }

    private static MemoryStream BuildArchive(params (string Path, string Content)[] entries)
    {
        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in entries)
            {
                using var writer = new StreamWriter(archive.CreateEntry(path).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        memory.Position = 0;
        return memory;
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~SkillArchiveReaderTests"`
Expected: build FAIL — `SkillArchiveReader`, `SkillArchiveLimits`, `ReleaseSkillLoader` do not exist.

- [ ] **Step 3: Implement**

`TheKameleon.Superpowers.Skills/Install/SkillArchiveReader.cs`:

```csharp
using System.IO.Compression;

namespace TheKameleon.Superpowers.Skills.Install;

public sealed record SkillArchiveLimits(int MaxEntries, long MaxTotalBytes)
{
    public static SkillArchiveLimits Default { get; } = new(5_000, 50L * 1024 * 1024);
}

public sealed record SkillArchiveReadResult(IReadOnlyList<SkillPackage> Skills, IReadOnlyList<string> Problems);

/// <summary>Reads <c>&lt;root&gt;/skills/&lt;name&gt;/**</c> from an upstream GitHub zipball without extracting to disk.</summary>
public static class SkillArchiveReader
{
    public static SkillArchiveReadResult Read(Stream archiveStream, SkillArchiveLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(archiveStream);
        limits ??= SkillArchiveLimits.Default;
        var problems = new List<string>();
        var skills = new SortedDictionary<string, Dictionary<string, byte[]>>(StringComparer.Ordinal);

        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Count > limits.MaxEntries)
        {
            problems.Add($"The archive has {archive.Entries.Count} entries; the limit is {limits.MaxEntries}.");
            return new SkillArchiveReadResult(Array.Empty<SkillPackage>(), problems);
        }

        long totalBytes = 0;
        foreach (var entry in archive.Entries)
        {
            var segments = entry.FullName.Replace('\\', '/').Split('/');
            if (segments.Length < 4 || segments[1] != "skills" || segments[^1].Length == 0)
            {
                continue;
            }

            var name = segments[2];
            var rest = segments[3..];
            if (!IsSafeSegment(name) || rest.Any(segment => !IsSafeSegment(segment)))
            {
                problems.Add($"Unsafe archive path '{entry.FullName}' was skipped.");
                continue;
            }

            totalBytes += entry.Length;
            if (totalBytes > limits.MaxTotalBytes)
            {
                problems.Add($"Skill files exceed {limits.MaxTotalBytes} uncompressed bytes; the archive was rejected.");
                return new SkillArchiveReadResult(Array.Empty<SkillPackage>(), problems);
            }

            using var source = entry.Open();
            using var memory = new MemoryStream();
            source.CopyTo(memory);

            if (!skills.TryGetValue(name, out var files))
            {
                files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                skills[name] = files;
            }

            files[string.Join('/', rest)] = memory.ToArray();
        }

        var packages = skills.Select(pair => new SkillPackage(pair.Key, pair.Value)).ToArray();
        return new SkillArchiveReadResult(packages, problems);
    }

    private static bool IsSafeSegment(string segment)
    {
        return segment.Length > 0
            && segment is not "." and not ".."
            && segment.IndexOfAny(new[] { ':', '\0' }) < 0;
    }
}
```

`TheKameleon.Superpowers.Skills/Install/ReleaseSkillLoader.cs`:

```csharp
using TheKameleon.Superpowers.Core.Contracts.Catalog;

namespace TheKameleon.Superpowers.Skills.Install;

public static class ReleaseSkillLoader
{
    /// <summary>Loads skill packages for a release the catalog loader has already hash-validated.</summary>
    public static SkillArchiveReadResult Load(string catalogRoot, LoadedCatalogRelease release)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogRoot);
        ArgumentNullException.ThrowIfNull(release);

        if (release.HasErrors)
        {
            return Failure($"Release '{release.ReleaseTag}' failed catalog validation and cannot be installed.");
        }

        if (string.IsNullOrWhiteSpace(release.ArchivePath))
        {
            return Failure($"Release '{release.ReleaseTag}' has no archive path.");
        }

        var root = Path.GetFullPath(catalogRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var archivePath = Path.GetFullPath(Path.Combine(root, release.ArchivePath));
        if (!archivePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return Failure($"Release '{release.ReleaseTag}' archive path points outside the catalog.");
        }

        if (!File.Exists(archivePath))
        {
            return Failure($"Release '{release.ReleaseTag}' archive is missing.");
        }

        using var stream = File.OpenRead(archivePath);
        return SkillArchiveReader.Read(stream);
    }

    private static SkillArchiveReadResult Failure(string problem)
    {
        return new SkillArchiveReadResult(Array.Empty<SkillPackage>(), new[] { problem });
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~SkillArchiveReaderTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: read upstream skill folders safely from release archives"
```

---

### Task 6: Profile paths, hashing, install state and lock

**Files:**
- Create: `TheKameleon.Superpowers.Skills/Install/ProfilePaths.cs`, `Install/ContentHash.cs`, `Install/InstallState.cs`, `Install/InstallStateStore.cs`, `Install/InstallLock.cs`
- Create: `TheKameleon.Superpowers.Tests/TempProfile.cs`
- Test: `TheKameleon.Superpowers.Tests/InstallStateStoreTests.cs`

**Interfaces:**
- Produces: `public sealed record ProfilePaths(string UserProfile, string LocalAppData)` with `static ProfilePaths ForCurrentUser()` and string properties `SkillsRoot`, `StagingRoot`, `AgentsRoot`, `AgentFile`, `UserInstructionsFile`, `StateDirectory`, `StateFile`, `DownloadsRoot`, plus `IReadOnlyList<string> OtherPersonalSkillRoots`.
- Produces: `static string ContentHash.Of(byte[] content)`, `static string ContentHash.OfFile(string path)` (lowercase hex SHA-256).
- Produces records `InstallState`, `InstalledRelease(string Tag, string Commit, string Source)`, `InstalledSkill(string Name, IReadOnlyDictionary<string, string> FileHashes)`, `InstalledAgentFile(string Sha256, int BootstrapVersion)`, `AlwaysOnState(bool Enabled, bool CreatedFile)`.
- Produces: `enum InstallStateStatus { Missing, Loaded, Corrupt }`, `record InstallStateLoad(InstallStateStatus Status, InstallState State)`, `class InstallStateStore(ProfilePaths paths)` with `Load()`, `Save(InstallState)`, `Delete()`.
- Produces: `static IDisposable InstallLock.Acquire(TimeSpan timeout)` (throws `TimeoutException`).
- Produces test helper `TempProfile : IDisposable` with `Paths`.

- [ ] **Step 1: Write the test helper and failing tests**

Create `TheKameleon.Superpowers.Tests/TempProfile.cs`:

```csharp
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Tests;

internal sealed class TempProfile : IDisposable
{
    public TempProfile()
    {
        Root = Path.Combine(Path.GetTempPath(), "SuperpowersTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Paths = new ProfilePaths(Path.Combine(Root, "profile"), Path.Combine(Root, "localappdata"));
    }

    public string Root { get; }

    public ProfilePaths Paths { get; }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
```

Create `TheKameleon.Superpowers.Tests/InstallStateStoreTests.cs`:

```csharp
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Tests;

public sealed class InstallStateStoreTests : IDisposable
{
    private readonly TempProfile profile = new();

    [Fact]
    public void PathsFollowTheVisualStudioLocations()
    {
        var paths = new ProfilePaths(@"C:\Users\me", @"C:\Users\me\AppData\Local");

        Assert.Equal(@"C:\Users\me\.copilot\skills", paths.SkillsRoot);
        Assert.Equal(@"C:\Users\me\.github\agents\superpowers.agent.md", paths.AgentFile);
        Assert.Equal(@"C:\Users\me\copilot-instructions.md", paths.UserInstructionsFile);
        Assert.Equal(@"C:\Users\me\AppData\Local\TheKameleon.Superpowers\install-state.json", paths.StateFile);
        Assert.Equal(new[] { @"C:\Users\me\.claude\skills", @"C:\Users\me\.agents\skills" }, paths.OtherPersonalSkillRoots);
    }

    [Fact]
    public void MissingStateLoadsAsEmpty()
    {
        var load = new InstallStateStore(profile.Paths).Load();

        Assert.Equal(InstallStateStatus.Missing, load.Status);
        Assert.Null(load.State.Release);
        Assert.Empty(load.State.Skills);
    }

    [Fact]
    public void SavedStateRoundTrips()
    {
        var store = new InstallStateStore(profile.Paths);
        var state = InstallState.Empty with
        {
            Release = new InstalledRelease("v6.4.1", "abc", "bundled"),
            Skills = new[] { new InstalledSkill("brainstorming", new Dictionary<string, string> { ["SKILL.md"] = "ff" }) },
            AgentFile = new InstalledAgentFile("aa", 1),
            AlwaysOn = new AlwaysOnState(true, true),
        };

        store.Save(state);
        var load = store.Load();

        Assert.Equal(InstallStateStatus.Loaded, load.Status);
        Assert.Equal(state.Release, load.State.Release);
        Assert.Equal("ff", Assert.Single(load.State.Skills).FileHashes["SKILL.md"]);
        Assert.Equal(state.AgentFile, load.State.AgentFile);
        Assert.Equal(state.AlwaysOn, load.State.AlwaysOn);
        Assert.Empty(Directory.GetFiles(profile.Paths.StateDirectory, "*.tmp"));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{\"schemaVersion\": 99}")]
    [InlineData("{\"schemaVersion\": 1, \"skills\": null}")]
    public void CorruptStateIsReportedNotTreatedAsEmpty(string content)
    {
        Directory.CreateDirectory(profile.Paths.StateDirectory);
        File.WriteAllText(profile.Paths.StateFile, content);

        Assert.Equal(InstallStateStatus.Corrupt, new InstallStateStore(profile.Paths).Load().Status);
    }

    [Fact]
    public void LockIsExclusiveAcrossThreads()
    {
        using (InstallLock.Acquire(TimeSpan.FromSeconds(5)))
        {
            var other = Task.Run(() => Assert.Throws<TimeoutException>(() => InstallLock.Acquire(TimeSpan.FromMilliseconds(100))));
            other.GetAwaiter().GetResult();
        }

        using var again = InstallLock.Acquire(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ContentHashIsLowercaseSha256()
    {
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", ContentHash.Of("abc"u8.ToArray()));
    }

    public void Dispose() => profile.Dispose();
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~InstallStateStoreTests"`
Expected: build FAIL — types do not exist.

- [ ] **Step 3: Implement**

`Install/ProfilePaths.cs`:

```csharp
namespace TheKameleon.Superpowers.Skills.Install;

/// <summary>Every location the extension reads or writes, rooted so tests can use a temporary profile.</summary>
public sealed record ProfilePaths(string UserProfile, string LocalAppData)
{
    public static ProfilePaths ForCurrentUser() => new(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    public string SkillsRoot => Path.Combine(UserProfile, ".copilot", "skills");

    public string StagingRoot => Path.Combine(UserProfile, ".copilot", ".superpowers-staging");

    public string AgentsRoot => Path.Combine(UserProfile, ".github", "agents");

    public string AgentFile => Path.Combine(AgentsRoot, "superpowers.agent.md");

    public string UserInstructionsFile => Path.Combine(UserProfile, "copilot-instructions.md");

    public string StateDirectory => Path.Combine(LocalAppData, "TheKameleon.Superpowers");

    public string StateFile => Path.Combine(StateDirectory, "install-state.json");

    public string DownloadsRoot => Path.Combine(StateDirectory, "downloads");

    public IReadOnlyList<string> OtherPersonalSkillRoots => new[]
    {
        Path.Combine(UserProfile, ".claude", "skills"),
        Path.Combine(UserProfile, ".agents", "skills"),
    };
}
```

`Install/ContentHash.cs`:

```csharp
using System.Security.Cryptography;

namespace TheKameleon.Superpowers.Skills.Install;

public static class ContentHash
{
    public static string Of(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    public static string OfFile(string path) => Of(File.ReadAllBytes(path));
}
```

`Install/InstallState.cs`:

```csharp
namespace TheKameleon.Superpowers.Skills.Install;

public sealed record InstallState
{
    public const int CurrentSchemaVersion = 1;

    public static InstallState Empty { get; } = new();

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public InstalledRelease? Release { get; init; }

    public IReadOnlyList<InstalledSkill> Skills { get; init; } = Array.Empty<InstalledSkill>();

    public InstalledAgentFile? AgentFile { get; init; }

    public AlwaysOnState AlwaysOn { get; init; } = new(false, false);
}

public sealed record InstalledRelease(string Tag, string Commit, string Source);

public sealed record InstalledSkill(string Name, IReadOnlyDictionary<string, string> FileHashes);

public sealed record InstalledAgentFile(string Sha256, int BootstrapVersion);

public sealed record AlwaysOnState(bool Enabled, bool CreatedFile);
```

`Install/InstallStateStore.cs`:

```csharp
using System.Text.Json;

namespace TheKameleon.Superpowers.Skills.Install;

public enum InstallStateStatus
{
    Missing,
    Loaded,
    Corrupt,
}

public sealed record InstallStateLoad(InstallStateStatus Status, InstallState State);

public sealed class InstallStateStore(ProfilePaths paths)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public InstallStateLoad Load()
    {
        if (!File.Exists(paths.StateFile))
        {
            return new InstallStateLoad(InstallStateStatus.Missing, InstallState.Empty);
        }

        try
        {
            var state = JsonSerializer.Deserialize<InstallState>(File.ReadAllText(paths.StateFile), Options);
            if (state is null
                || state.SchemaVersion != InstallState.CurrentSchemaVersion
                || state.Skills is null
                || state.AlwaysOn is null
                || state.Skills.Any(skill => skill?.Name is null || skill.FileHashes is null))
            {
                return new InstallStateLoad(InstallStateStatus.Corrupt, InstallState.Empty);
            }

            return new InstallStateLoad(InstallStateStatus.Loaded, state);
        }
        catch (JsonException)
        {
            return new InstallStateLoad(InstallStateStatus.Corrupt, InstallState.Empty);
        }
    }

    public void Save(InstallState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Directory.CreateDirectory(paths.StateDirectory);
        var temporary = paths.StateFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state, Options));
        File.Move(temporary, paths.StateFile, overwrite: true);
    }

    public void Delete()
    {
        if (File.Exists(paths.StateFile))
        {
            File.Delete(paths.StateFile);
        }
    }
}
```

`Install/InstallLock.cs`:

```csharp
namespace TheKameleon.Superpowers.Skills.Install;

/// <summary>Serializes install changes across Visual Studio instances. Acquire and release on the same thread.</summary>
public static class InstallLock
{
    public const string MutexName = @"Local\TheKameleon.Superpowers.Install";

    public static IDisposable Acquire(TimeSpan timeout)
    {
        var mutex = new Mutex(initiallyOwned: false, MutexName);
        try
        {
            if (!mutex.WaitOne(timeout))
            {
                mutex.Dispose();
                throw new TimeoutException("Another Visual Studio window is changing the Superpowers installation. Try again in a moment.");
            }
        }
        catch (AbandonedMutexException)
        {
            // The previous owner exited without releasing; this thread now owns the mutex.
        }

        return new Releaser(mutex);
    }

    private sealed class Releaser(Mutex mutex) : IDisposable
    {
        public void Dispose()
        {
            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~InstallStateStoreTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add profile paths, install state store and cross-process install lock"
```

---

### Task 7: Skill installer

**Files:**
- Create: `TheKameleon.Superpowers.Skills/Install/SkillInstaller.cs`
- Test: `TheKameleon.Superpowers.Tests/SkillInstallerTests.cs`

**Interfaces:**
- Consumes: `ProfilePaths`, `ContentHash`, `InstalledSkill` (Task 6); `SkillPackage`, `SkillValidator` (Task 4).
- Produces: `enum SkillIssueKind { Conflict, Invalid, EditedKept, EditedOverwritten, RemovedEditedKept }`, `record SkillIssue(string SkillName, SkillIssueKind Kind, string Message)`, `record SkillInstallOutcome(bool Succeeded, IReadOnlyList<InstalledSkill> Installed, IReadOnlyList<SkillIssue> Issues, string? FailureReason)`.
- Produces on `SkillInstaller(ProfilePaths paths)`:
  - `SkillInstallOutcome Install(IReadOnlyList<SkillPackage> packages, IReadOnlyList<InstalledSkill> owned, bool overwriteEdited)`
  - `IReadOnlyList<SkillIssue> Remove(IReadOnlyList<InstalledSkill> owned)`
  - `bool IsEdited(InstalledSkill skill)`
  - `bool MatchesPackage(SkillPackage package)`
  - `IReadOnlyList<string> FindConflicts(IEnumerable<SkillPackage> packages, IReadOnlyList<InstalledSkill> owned)`
  - `static InstalledSkill Describe(SkillPackage package)`

- [ ] **Step 1: Write the failing tests**

Create `TheKameleon.Superpowers.Tests/SkillInstallerTests.cs`:

```csharp
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Tests;

public sealed class SkillInstallerTests : IDisposable
{
    private readonly TempProfile profile = new();

    private SkillInstaller Installer => new(profile.Paths);

    private string SkillFile(string name, string relative = "SKILL.md") => Path.Combine(profile.Paths.SkillsRoot, name, relative);

    [Fact]
    public void InstallsFreshSkillsWithSupportingFiles()
    {
        var outcome = Installer.Install(new[] { TestSupport.Skill("alpha", ("scripts/run.sh", "echo")), TestSupport.Skill("beta") }, Array.Empty<InstalledSkill>(), overwriteEdited: false);

        Assert.True(outcome.Succeeded);
        Assert.Empty(outcome.Issues);
        Assert.Equal(new[] { "alpha", "beta" }, outcome.Installed.Select(skill => skill.Name));
        Assert.Equal("echo", File.ReadAllText(SkillFile("alpha", Path.Combine("scripts", "run.sh"))));
        Assert.Equal(2, outcome.Installed[0].FileHashes.Count);
        Assert.False(Directory.Exists(profile.Paths.StagingRoot) && Directory.EnumerateFileSystemEntries(profile.Paths.StagingRoot).Any());
    }

    [Fact]
    public void ReplacesOwnedUnmodifiedSkill()
    {
        var first = Installer.Install(new[] { TestSupport.Skill("alpha") }, Array.Empty<InstalledSkill>(), false);
        var updated = TestSupport.SkillWithMarkdown("alpha", "---\nname: alpha\ndescription: new\n---\nv2\n");

        var second = Installer.Install(new[] { updated }, first.Installed, false);

        Assert.True(second.Succeeded);
        Assert.Contains("v2", File.ReadAllText(SkillFile("alpha")));
        Assert.Empty(Directory.GetDirectories(profile.Paths.SkillsRoot, "*.superpowers-backup-*"));
    }

    [Fact]
    public void LeavesSkillsItDidNotInstallUntouched()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SkillFile("alpha"))!);
        File.WriteAllText(SkillFile("alpha"), "mine");

        var outcome = Installer.Install(new[] { TestSupport.Skill("alpha") }, Array.Empty<InstalledSkill>(), overwriteEdited: true);

        Assert.True(outcome.Succeeded);
        Assert.Empty(outcome.Installed);
        Assert.Equal(SkillIssueKind.Conflict, Assert.Single(outcome.Issues).Kind);
        Assert.Equal("mine", File.ReadAllText(SkillFile("alpha")));
    }

    [Fact]
    public void KeepsEditedOwnedSkillUnlessOverwriteIsChosen()
    {
        var first = Installer.Install(new[] { TestSupport.Skill("alpha") }, Array.Empty<InstalledSkill>(), false);
        File.AppendAllText(SkillFile("alpha"), "my edit");
        var update = TestSupport.SkillWithMarkdown("alpha", "---\nname: alpha\ndescription: new\n---\nv2\n");

        var kept = Installer.Install(new[] { update }, first.Installed, overwriteEdited: false);

        Assert.Equal(SkillIssueKind.EditedKept, Assert.Single(kept.Issues).Kind);
        Assert.Contains("my edit", File.ReadAllText(SkillFile("alpha")));
        Assert.Same(first.Installed[0], Assert.Single(kept.Installed));

        var overwritten = Installer.Install(new[] { update }, first.Installed, overwriteEdited: true);

        Assert.Equal(SkillIssueKind.EditedOverwritten, Assert.Single(overwritten.Issues).Kind);
        Assert.Contains("v2", File.ReadAllText(SkillFile("alpha")));
    }

    [Fact]
    public void RemovesOwnedSkillsAbsentFromTheNewRelease()
    {
        var first = Installer.Install(new[] { TestSupport.Skill("alpha"), TestSupport.Skill("beta") }, Array.Empty<InstalledSkill>(), false);

        var second = Installer.Install(new[] { TestSupport.Skill("alpha") }, first.Installed, false);

        Assert.True(second.Succeeded);
        Assert.False(Directory.Exists(Path.Combine(profile.Paths.SkillsRoot, "beta")));
    }

    [Fact]
    public void KeepsEditedSkillAbsentFromTheNewRelease()
    {
        var first = Installer.Install(new[] { TestSupport.Skill("alpha"), TestSupport.Skill("beta") }, Array.Empty<InstalledSkill>(), false);
        File.AppendAllText(SkillFile("beta"), "edit");

        var second = Installer.Install(new[] { TestSupport.Skill("alpha") }, first.Installed, false);

        Assert.Equal(SkillIssueKind.RemovedEditedKept, Assert.Single(second.Issues).Kind);
        Assert.True(File.Exists(SkillFile("beta")));
    }

    [Fact]
    public void SkipsInvalidSkill()
    {
        var invalid = TestSupport.SkillWithMarkdown("alpha", "---\nname: wrong\ndescription: x\n---\n");

        var outcome = Installer.Install(new[] { invalid, TestSupport.Skill("beta") }, Array.Empty<InstalledSkill>(), false);

        Assert.Equal(SkillIssueKind.Invalid, Assert.Single(outcome.Issues).Kind);
        Assert.Equal("beta", Assert.Single(outcome.Installed).Name);
        Assert.False(Directory.Exists(Path.Combine(profile.Paths.SkillsRoot, "alpha")));
    }

    [Fact]
    public void RollsBackEverySwapWhenOneFails()
    {
        var first = Installer.Install(new[] { TestSupport.Skill("alpha"), TestSupport.Skill("beta") }, Array.Empty<InstalledSkill>(), false);
        var alphaBefore = File.ReadAllText(SkillFile("alpha"));
        var updates = new[]
        {
            TestSupport.SkillWithMarkdown("alpha", "---\nname: alpha\ndescription: new\n---\nv2\n"),
            TestSupport.SkillWithMarkdown("beta", "---\nname: beta\ndescription: new\n---\nv2\n"),
        };

        SkillInstallOutcome outcome;
        using (new FileStream(SkillFile("beta"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            outcome = Installer.Install(updates, first.Installed, false);
        }

        Assert.False(outcome.Succeeded);
        Assert.NotNull(outcome.FailureReason);
        Assert.Equal(alphaBefore, File.ReadAllText(SkillFile("alpha")));
        Assert.Same(first.Installed, outcome.Installed);
        Assert.Empty(Directory.GetDirectories(profile.Paths.SkillsRoot, "*.superpowers-backup-*"));
    }

    [Fact]
    public void RemoveDeletesUneditedAndReportsEdited()
    {
        var first = Installer.Install(new[] { TestSupport.Skill("alpha"), TestSupport.Skill("beta") }, Array.Empty<InstalledSkill>(), false);
        File.AppendAllText(SkillFile("beta"), "edit");

        var issues = Installer.Remove(first.Installed);

        Assert.False(Directory.Exists(Path.Combine(profile.Paths.SkillsRoot, "alpha")));
        Assert.True(File.Exists(SkillFile("beta")));
        Assert.Equal("beta", Assert.Single(issues).SkillName);
    }

    [Fact]
    public void MatchesPackageDetectsIdenticalContent()
    {
        var package = TestSupport.Skill("alpha");
        Installer.Install(new[] { package }, Array.Empty<InstalledSkill>(), false);

        Assert.True(Installer.MatchesPackage(package));
        File.AppendAllText(SkillFile("alpha"), "edit");
        Assert.False(Installer.MatchesPackage(package));
    }

    public void Dispose() => profile.Dispose();
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~SkillInstallerTests"`
Expected: build FAIL — `SkillInstaller` does not exist.

- [ ] **Step 3: Implement**

`TheKameleon.Superpowers.Skills/Install/SkillInstaller.cs`:

```csharp
namespace TheKameleon.Superpowers.Skills.Install;

public enum SkillIssueKind
{
    Conflict,
    Invalid,
    EditedKept,
    EditedOverwritten,
    RemovedEditedKept,
}

public sealed record SkillIssue(string SkillName, SkillIssueKind Kind, string Message);

public sealed record SkillInstallOutcome(
    bool Succeeded,
    IReadOnlyList<InstalledSkill> Installed,
    IReadOnlyList<SkillIssue> Issues,
    string? FailureReason);

public sealed class SkillInstaller(ProfilePaths paths)
{
    public static InstalledSkill Describe(SkillPackage package)
    {
        return new InstalledSkill(
            package.Name,
            package.Files.ToDictionary(file => file.Key, file => ContentHash.Of(file.Value), StringComparer.Ordinal));
    }

    public IReadOnlyList<string> FindConflicts(IEnumerable<SkillPackage> packages, IReadOnlyList<InstalledSkill> owned)
    {
        var ownedNames = owned.Select(skill => skill.Name).ToHashSet(StringComparer.Ordinal);
        return packages
            .Where(package => !ownedNames.Contains(package.Name) && Directory.Exists(SkillDirectory(package.Name)))
            .Select(package => package.Name)
            .ToArray();
    }

    public bool IsEdited(InstalledSkill skill)
    {
        var onDisk = ReadHashes(skill.Name);
        return onDisk is not null && !SameHashes(onDisk, skill.FileHashes);
    }

    public bool MatchesPackage(SkillPackage package)
    {
        var onDisk = ReadHashes(package.Name);
        return onDisk is not null && SameHashes(onDisk, Describe(package).FileHashes);
    }

    public SkillInstallOutcome Install(IReadOnlyList<SkillPackage> packages, IReadOnlyList<InstalledSkill> owned, bool overwriteEdited)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(owned);

        var issues = new List<SkillIssue>();
        var installed = new List<InstalledSkill>();
        var ownedByName = owned.ToDictionary(skill => skill.Name, StringComparer.Ordinal);
        var swaps = new List<(string Target, string? Backup)>();
        var stagingRoot = Path.Combine(paths.StagingRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(paths.SkillsRoot);

        try
        {
            foreach (var package in packages)
            {
                ownedByName.TryGetValue(package.Name, out var previous);
                var problems = SkillValidator.Validate(package);
                if (problems.Count > 0)
                {
                    issues.Add(new SkillIssue(package.Name, SkillIssueKind.Invalid, $"Skipped '{package.Name}': {string.Join(" ", problems)}"));
                    if (previous is not null)
                    {
                        installed.Add(previous);
                    }

                    continue;
                }

                var target = SkillDirectory(package.Name);
                if (previous is null && Directory.Exists(target))
                {
                    issues.Add(new SkillIssue(package.Name, SkillIssueKind.Conflict, $"A '{package.Name}' skill that Superpowers did not install already exists; it was left unchanged."));
                    continue;
                }

                if (previous is not null && IsEdited(previous))
                {
                    if (!overwriteEdited)
                    {
                        issues.Add(new SkillIssue(package.Name, SkillIssueKind.EditedKept, $"Your edited '{package.Name}' skill was kept."));
                        installed.Add(previous);
                        continue;
                    }

                    issues.Add(new SkillIssue(package.Name, SkillIssueKind.EditedOverwritten, $"Your edits to '{package.Name}' were replaced."));
                }

                var staged = Path.Combine(stagingRoot, package.Name);
                WriteFiles(staged, package.Files);

                string? backup = null;
                if (Directory.Exists(target))
                {
                    backup = target + ".superpowers-backup-" + Guid.NewGuid().ToString("N");
                    Directory.Move(target, backup);
                }

                swaps.Add((target, backup));
                Directory.Move(staged, target);
                installed.Add(Describe(package));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            RollBack(swaps);
            return new SkillInstallOutcome(false, owned, issues, $"Installation failed and was rolled back: {exception.Message}");
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
        }

        foreach (var (_, backup) in swaps)
        {
            if (backup is not null)
            {
                TryDeleteDirectory(backup);
            }
        }

        var newNames = packages.Select(package => package.Name).ToHashSet(StringComparer.Ordinal);
        issues.AddRange(Remove(owned.Where(skill => !newNames.Contains(skill.Name)).ToArray()));
        return new SkillInstallOutcome(true, installed, issues, null);
    }

    public IReadOnlyList<SkillIssue> Remove(IReadOnlyList<InstalledSkill> owned)
    {
        var issues = new List<SkillIssue>();
        foreach (var skill in owned)
        {
            var directory = SkillDirectory(skill.Name);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            if (IsEdited(skill))
            {
                issues.Add(new SkillIssue(skill.Name, SkillIssueKind.RemovedEditedKept, $"Your edited '{skill.Name}' skill was left in place and is no longer managed by Superpowers."));
                continue;
            }

            Directory.Delete(directory, recursive: true);
        }

        return issues;
    }

    private string SkillDirectory(string name) => Path.Combine(paths.SkillsRoot, name);

    private Dictionary<string, string>? ReadHashes(string name)
    {
        var directory = SkillDirectory(name);
        if (!Directory.Exists(directory))
        {
            return null;
        }

        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).ToDictionary(
            file => Path.GetRelativePath(directory, file).Replace('\\', '/'),
            ContentHash.OfFile,
            StringComparer.Ordinal);
    }

    private static bool SameHashes(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right)
    {
        return left.Count == right.Count
            && left.All(pair => right.TryGetValue(pair.Key, out var hash) && string.Equals(hash, pair.Value, StringComparison.OrdinalIgnoreCase));
    }

    private static void WriteFiles(string directory, IReadOnlyDictionary<string, byte[]> files)
    {
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var (relativePath, content) in files)
        {
            var destination = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException($"Skill file path '{relativePath}' escapes its folder.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllBytes(destination, content);
        }
    }

    private static void RollBack(List<(string Target, string? Backup)> swaps)
    {
        for (var index = swaps.Count - 1; index >= 0; index--)
        {
            var (target, backup) = swaps[index];
            TryDeleteDirectory(target);
            if (backup is not null && Directory.Exists(backup))
            {
                Directory.Move(backup, target);
            }
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort: leftover staging or backup folders are harmless and retried next time.
        }
    }
}
```

Note: `RollBack` runs only for swaps that were recorded. In `RollsBackEverySwapWhenOneFails` the failure is `Directory.Move(beta, backup)` for beta, which throws before beta's swap is recorded, so rollback restores alpha only and beta is untouched.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~SkillInstallerTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: install skills with ownership tracking, conflict detection and rollback"
```

---

### Task 8: Bootstrap text and agent file

**Files:**
- Create: `TheKameleon.Superpowers.Skills/Bootstrap/BootstrapText.cs`, `Bootstrap/AgentFileWriter.cs`
- Test: `TheKameleon.Superpowers.Tests/AgentFileWriterTests.cs`

**Interfaces:**
- Consumes: `ProfilePaths`, `ContentHash`, `InstalledAgentFile` (Task 6).
- Produces: `static class BootstrapText` with `const int Version = 1` and `const string Body`.
- Produces: `enum AgentFileStatus { Written, UpToDate, EditedKept, Removed, EditedNotRemoved, Missing }`, `record AgentFileOutcome(AgentFileStatus Status, InstalledAgentFile? Agent)`.
- Produces on `AgentFileWriter(ProfilePaths paths)`: `static string BuildContent()`, `bool IsEdited(InstalledAgentFile? recorded)`, `AgentFileOutcome Write(InstalledAgentFile? recorded, bool overwriteEdited)`, `AgentFileOutcome Remove(InstalledAgentFile? recorded)`.

- [ ] **Step 1: Write the failing tests**

Create `TheKameleon.Superpowers.Tests/AgentFileWriterTests.cs`:

```csharp
using TheKameleon.Superpowers.Skills.Bootstrap;
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Tests;

public sealed class AgentFileWriterTests : IDisposable
{
    private readonly TempProfile profile = new();

    private AgentFileWriter Writer => new(profile.Paths);

    [Theory]
    [InlineData("get_file")]
    [InlineData("ENTIRE file")]
    [InlineData("keep calling get_file from the next line")]
    [InlineData("Mentioning it is not enough")]
    [InlineData("`superpowers:<name>` means the skill named `<name>`")]
    [InlineData("Do not skip a step")]
    [InlineData("interactive Agent mode (Autopilot off)")]
    [InlineData("Subagents and the Task tool are not available")]
    [InlineData("using-superpowers")]
    public void BootstrapContainsEachTranslationRule(string fragment)
    {
        Assert.Contains(fragment, BootstrapText.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentFileHasFrontMatterAndNoToolsList()
    {
        var content = AgentFileWriter.BuildContent();

        Assert.StartsWith("---\nname: Superpowers\ndescription: ", content, StringComparison.Ordinal);
        Assert.DoesNotContain("tools:", content, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", content, StringComparison.Ordinal);
        Assert.Contains(BootstrapText.Body.ReplaceLineEndings("\n"), content, StringComparison.Ordinal);
    }

    [Fact]
    public void WritesThenReportsUpToDate()
    {
        var first = Writer.Write(null, overwriteEdited: false);

        Assert.Equal(AgentFileStatus.Written, first.Status);
        Assert.Equal(BootstrapText.Version, first.Agent!.BootstrapVersion);
        Assert.Equal(first.Agent.Sha256, ContentHash.OfFile(profile.Paths.AgentFile));
        Assert.Equal(AgentFileStatus.UpToDate, Writer.Write(first.Agent, false).Status);
    }

    [Fact]
    public void KeepsEditedOrForeignAgentFileUnlessOverwriteIsChosen()
    {
        var first = Writer.Write(null, false);
        File.AppendAllText(profile.Paths.AgentFile, "my edit");

        Assert.True(Writer.IsEdited(first.Agent));
        Assert.Equal(AgentFileStatus.EditedKept, Writer.Write(first.Agent, overwriteEdited: false).Status);
        Assert.Contains("my edit", File.ReadAllText(profile.Paths.AgentFile));

        Assert.Equal(AgentFileStatus.Written, Writer.Write(first.Agent, overwriteEdited: true).Status);
        Assert.DoesNotContain("my edit", File.ReadAllText(profile.Paths.AgentFile));
    }

    [Fact]
    public void RefreshesOutdatedUneditedFile()
    {
        Directory.CreateDirectory(profile.Paths.AgentsRoot);
        File.WriteAllText(profile.Paths.AgentFile, "old bootstrap");
        var recorded = new InstalledAgentFile(ContentHash.OfFile(profile.Paths.AgentFile), 0);

        var outcome = Writer.Write(recorded, overwriteEdited: false);

        Assert.Equal(AgentFileStatus.Written, outcome.Status);
        Assert.Equal(AgentFileWriter.BuildContent(), File.ReadAllText(profile.Paths.AgentFile));
    }

    [Fact]
    public void RemoveDeletesOnlyUneditedFile()
    {
        var first = Writer.Write(null, false);
        Assert.Equal(AgentFileStatus.Removed, Writer.Remove(first.Agent).Status);
        Assert.False(File.Exists(profile.Paths.AgentFile));

        var second = Writer.Write(null, false);
        File.AppendAllText(profile.Paths.AgentFile, "edit");
        Assert.Equal(AgentFileStatus.EditedNotRemoved, Writer.Remove(second.Agent).Status);
        Assert.True(File.Exists(profile.Paths.AgentFile));
        using var empty = new TempProfile();
        Assert.Equal(AgentFileStatus.Missing, new AgentFileWriter(empty.Paths).Remove(null).Status);
    }

    public void Dispose() => profile.Dispose();
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~AgentFileWriterTests"`
Expected: build FAIL — types do not exist.

- [ ] **Step 3: Implement**

`TheKameleon.Superpowers.Skills/Bootstrap/BootstrapText.cs` (text copied verbatim from spec §8):

```csharp
namespace TheKameleon.Superpowers.Skills.Bootstrap;

/// <summary>Adapts upstream Superpowers wording to Visual Studio Copilot. Contains no methodology of its own.</summary>
public static class BootstrapText
{
    public const int Version = 1;

    public const string Body = """
        You have Superpowers skills. They are listed in the "Available Skills" section, each with a file path.

        At the start of every conversation, read the `using-superpowers` skill. Before responding to ANY request, including clarifying questions, check whether another skill applies. If there is even a small chance one applies, read it and follow it exactly. Announce it first: "Using <skill> to <purpose>".

        Reading skills:
        - Read skills with get_file and always read the ENTIRE file. get_file may return fewer lines than requested: check the last line number you received and keep calling get_file from the next line until you reach the end. Never act on a partially read skill.
        - When a skill names another skill (as `superpowers:<name>` or `<name>`), read that skill with get_file before carrying out that step. Mentioning it is not enough.
        - When a skill references a supporting file in its own folder, resolve it relative to that folder and read it when the skill says to.
        - Follow a skill's steps in order. Do not skip a step because the answer seems obvious.

        Translating skill wording to this environment:
        - "Skill tool" / "invoke the skill" means: read that skill's SKILL.md with get_file.
        - `superpowers:<name>` means the skill named `<name>` in the Available Skills list.
        - "TodoWrite" means: keep a visible checklist in your reply and update it as you go.
        - "your human partner" means the user. When a skill says to ask the user something, use ask_question, one question at a time.
        - If ask_question reports that the user is unavailable, stop and tell the user this skill needs interactive Agent mode (Autopilot off). Do not invent answers.
        - Subagents and the Task tool are not available. When a skill requires them, say so and do the work sequentially yourself.
        """;
}
```

`TheKameleon.Superpowers.Skills/Bootstrap/AgentFileWriter.cs`:

```csharp
using System.Text;
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Skills.Bootstrap;

public enum AgentFileStatus
{
    Written,
    UpToDate,
    EditedKept,
    Removed,
    EditedNotRemoved,
    Missing,
}

public sealed record AgentFileOutcome(AgentFileStatus Status, InstalledAgentFile? Agent);

public sealed class AgentFileWriter(ProfilePaths paths)
{
    private const string Description = "Agent mode with Superpowers skills — brainstorming, planning, TDD, systematic debugging, code review and verification.";

    public static string BuildContent()
    {
        return "---\nname: Superpowers\ndescription: " + Description + "\n---\n\n" + BootstrapText.Body.ReplaceLineEndings("\n") + "\n";
    }

    public bool IsEdited(InstalledAgentFile? recorded)
    {
        return File.Exists(paths.AgentFile)
            && (recorded is null || !string.Equals(ContentHash.OfFile(paths.AgentFile), recorded.Sha256, StringComparison.OrdinalIgnoreCase));
    }

    public AgentFileOutcome Write(InstalledAgentFile? recorded, bool overwriteEdited)
    {
        var content = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(BuildContent());
        var hash = ContentHash.Of(content);
        var current = new InstalledAgentFile(hash, BootstrapText.Version);

        if (File.Exists(paths.AgentFile))
        {
            var onDisk = ContentHash.OfFile(paths.AgentFile);
            if (string.Equals(onDisk, hash, StringComparison.OrdinalIgnoreCase))
            {
                return new AgentFileOutcome(AgentFileStatus.UpToDate, current);
            }

            if (IsEdited(recorded) && !overwriteEdited)
            {
                return new AgentFileOutcome(AgentFileStatus.EditedKept, recorded);
            }
        }

        Directory.CreateDirectory(paths.AgentsRoot);
        File.WriteAllBytes(paths.AgentFile, content);
        return new AgentFileOutcome(AgentFileStatus.Written, current);
    }

    public AgentFileOutcome Remove(InstalledAgentFile? recorded)
    {
        if (!File.Exists(paths.AgentFile))
        {
            return new AgentFileOutcome(AgentFileStatus.Missing, null);
        }

        if (IsEdited(recorded))
        {
            return new AgentFileOutcome(AgentFileStatus.EditedNotRemoved, recorded);
        }

        File.Delete(paths.AgentFile);
        return new AgentFileOutcome(AgentFileStatus.Removed, null);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~AgentFileWriterTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add versioned Copilot bootstrap and Superpowers agent file writer"
```

---

### Task 9: Always-on block editor and instructions file

**Files:**
- Create: `TheKameleon.Superpowers.Skills/Bootstrap/AlwaysOnBlockEditor.cs`, `Bootstrap/AlwaysOnInstructionsFile.cs`
- Test: `TheKameleon.Superpowers.Tests/AlwaysOnTests.cs`

**Interfaces:**
- Consumes: `BootstrapText.Body` (Task 8), `ProfilePaths`, `AlwaysOnState` (Task 6).
- Produces: `enum BlockEditStatus { Changed, Unchanged, MalformedMarkers }`, `record BlockEditResult(BlockEditStatus Status, string Text)`, `static class AlwaysOnBlockEditor` with `BeginMarker`, `EndMarker`, `Apply(string text, string body, string newline)`, `Remove(string text)`, `IsPresent(string text)`.
- Produces: `enum AlwaysOnStatus { Enabled, Disabled, Unchanged, MalformedMarkers }`, `record AlwaysOnOutcome(AlwaysOnStatus Status, AlwaysOnState State)`, `class AlwaysOnInstructionsFile(ProfilePaths paths)` with `Enable(AlwaysOnState current)`, `Disable(AlwaysOnState current)`, `bool IsBlockPresent()`.

- [ ] **Step 1: Write the failing tests**

Create `TheKameleon.Superpowers.Tests/AlwaysOnTests.cs`:

```csharp
using System.Text;
using TheKameleon.Superpowers.Skills.Bootstrap;
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Tests;

public sealed class AlwaysOnTests : IDisposable
{
    private const string Body = "Line one\nLine two";
    private readonly TempProfile profile = new();

    [Fact]
    public void AppliesToEmptyText()
    {
        var result = AlwaysOnBlockEditor.Apply(string.Empty, Body, "\n");

        Assert.Equal(BlockEditStatus.Changed, result.Status);
        Assert.Equal(AlwaysOnBlockEditor.BeginMarker + "\nLine one\nLine two\n" + AlwaysOnBlockEditor.EndMarker + "\n", result.Text);
    }

    [Fact]
    public void AppendsAfterExistingContentAndRemovesCleanly()
    {
        const string original = "# My preferences\nUse tabs.\n";

        var applied = AlwaysOnBlockEditor.Apply(original, Body, "\n");
        var removed = AlwaysOnBlockEditor.Remove(applied.Text);

        Assert.StartsWith(original, applied.Text, StringComparison.Ordinal);
        Assert.Equal(BlockEditStatus.Changed, removed.Status);
        Assert.Equal(original, removed.Text);
    }

    [Fact]
    public void FileWithoutTrailingNewlineGainsOneAfterRoundTrip()
    {
        var applied = AlwaysOnBlockEditor.Apply("A", Body, "\n");

        Assert.Equal("A\n", AlwaysOnBlockEditor.Remove(applied.Text).Text);
    }

    [Fact]
    public void ApplyIsIdempotentAndReplacesChangedBody()
    {
        var once = AlwaysOnBlockEditor.Apply("x\n", Body, "\n").Text;

        Assert.Equal(BlockEditStatus.Unchanged, AlwaysOnBlockEditor.Apply(once, Body, "\n").Status);
        var replaced = AlwaysOnBlockEditor.Apply(once, "New body", "\n");
        Assert.Equal(BlockEditStatus.Changed, replaced.Status);
        Assert.Contains("New body", replaced.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Line one", replaced.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void PreservesCrLfLineEndings()
    {
        var result = AlwaysOnBlockEditor.Apply("A\r\n", Body, "\r\n");

        Assert.DoesNotContain("\n", result.Text.Replace("\r\n", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<!-- superpowers:begin x -->\n<!-- superpowers:begin y -->\n<!-- superpowers:end -->\n")]
    [InlineData("<!-- superpowers:end -->\n<!-- superpowers:begin x -->\n")]
    [InlineData("<!-- superpowers:begin x -->\nno end\n")]
    public void RefusesMalformedMarkers(string text)
    {
        Assert.Equal(BlockEditStatus.MalformedMarkers, AlwaysOnBlockEditor.Apply(text, Body, "\n").Status);
        Assert.Equal(BlockEditStatus.MalformedMarkers, AlwaysOnBlockEditor.Remove(text).Status);
    }

    [Fact]
    public void EnableCreatesFileAndDisableDeletesItWhenCreated()
    {
        var file = new AlwaysOnInstructionsFile(profile.Paths);

        var enabled = file.Enable(new AlwaysOnState(false, false));

        Assert.Equal(AlwaysOnStatus.Enabled, enabled.Status);
        Assert.Equal(new AlwaysOnState(true, true), enabled.State);
        Assert.True(file.IsBlockPresent());

        var disabled = file.Disable(enabled.State);

        Assert.Equal(AlwaysOnStatus.Disabled, disabled.Status);
        Assert.False(File.Exists(profile.Paths.UserInstructionsFile));
    }

    [Theory]
    [InlineData("utf-8-bom")]
    [InlineData("utf-16")]
    public void PreservesEncodingAndExistingContent(string encodingName)
    {
        Encoding encoding = encodingName == "utf-16" ? Encoding.Unicode : new UTF8Encoding(true);
        Directory.CreateDirectory(profile.Paths.UserProfile);
        File.WriteAllText(profile.Paths.UserInstructionsFile, "Keep me.\r\n", encoding);
        var original = File.ReadAllBytes(profile.Paths.UserInstructionsFile);
        var file = new AlwaysOnInstructionsFile(profile.Paths);

        var enabled = file.Enable(new AlwaysOnState(false, false));
        var afterEnable = File.ReadAllBytes(profile.Paths.UserInstructionsFile);
        file.Disable(enabled.State);

        Assert.Equal(new AlwaysOnState(true, false), enabled.State);
        Assert.Equal(encoding.GetPreamble(), afterEnable.Take(encoding.GetPreamble().Length).ToArray());
        Assert.Equal(original, File.ReadAllBytes(profile.Paths.UserInstructionsFile));
    }

    [Fact]
    public void EnableReportsMalformedFileWithoutChangingIt()
    {
        Directory.CreateDirectory(profile.Paths.UserProfile);
        File.WriteAllText(profile.Paths.UserInstructionsFile, "<!-- superpowers:end -->\n");

        var outcome = new AlwaysOnInstructionsFile(profile.Paths).Enable(new AlwaysOnState(false, false));

        Assert.Equal(AlwaysOnStatus.MalformedMarkers, outcome.Status);
        Assert.Equal("<!-- superpowers:end -->\n", File.ReadAllText(profile.Paths.UserInstructionsFile));
    }

    public void Dispose() => profile.Dispose();
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~AlwaysOnTests"`
Expected: build FAIL — types do not exist.

- [ ] **Step 3: Implement**

`TheKameleon.Superpowers.Skills/Bootstrap/AlwaysOnBlockEditor.cs`:

```csharp
namespace TheKameleon.Superpowers.Skills.Bootstrap;

public enum BlockEditStatus
{
    Changed,
    Unchanged,
    MalformedMarkers,
}

public sealed record BlockEditResult(BlockEditStatus Status, string Text);

/// <summary>Pure text edits of the one managed block; everything outside the markers is preserved.</summary>
public static class AlwaysOnBlockEditor
{
    public const string BeginMarker = "<!-- superpowers:begin (managed by Superpowers for Visual Studio; edit outside this block) -->";
    public const string EndMarker = "<!-- superpowers:end -->";
    private const string BeginPrefix = "<!-- superpowers:begin";

    public static bool IsPresent(string text) => TryFind(text, out var start, out _) && start >= 0;

    public static BlockEditResult Apply(string text, string body, string newline)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!TryFind(text, out var start, out var end))
        {
            return new BlockEditResult(BlockEditStatus.MalformedMarkers, text);
        }

        var block = BeginMarker + newline + body.ReplaceLineEndings(newline).TrimEnd() + newline + EndMarker;
        string result;
        if (start < 0)
        {
            var separator = text.Length == 0 || text.EndsWith('\n') ? string.Empty : newline;
            result = text + separator + block + newline;
        }
        else
        {
            result = text[..start] + block + text[end..];
        }

        return string.Equals(result, text, StringComparison.Ordinal)
            ? new BlockEditResult(BlockEditStatus.Unchanged, text)
            : new BlockEditResult(BlockEditStatus.Changed, result);
    }

    public static BlockEditResult Remove(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!TryFind(text, out var start, out var end))
        {
            return new BlockEditResult(BlockEditStatus.MalformedMarkers, text);
        }

        if (start < 0)
        {
            return new BlockEditResult(BlockEditStatus.Unchanged, text);
        }

        var after = end;
        if (after < text.Length && text[after] == '\r')
        {
            after++;
        }

        if (after < text.Length && text[after] == '\n')
        {
            after++;
        }

        return new BlockEditResult(BlockEditStatus.Changed, text[..start] + text[after..]);
    }

    private static bool TryFind(string text, out int start, out int end)
    {
        start = -1;
        end = -1;
        var beginCount = CountOccurrences(text, BeginPrefix);
        var endCount = CountOccurrences(text, EndMarker);
        if (beginCount == 0 && endCount == 0)
        {
            return true;
        }

        if (beginCount != 1 || endCount != 1)
        {
            return false;
        }

        start = text.IndexOf(BeginPrefix, StringComparison.Ordinal);
        var endIndex = text.IndexOf(EndMarker, StringComparison.Ordinal);
        if (endIndex < start)
        {
            start = -1;
            return false;
        }

        end = endIndex + EndMarker.Length;
        return true;
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
```

`TheKameleon.Superpowers.Skills/Bootstrap/AlwaysOnInstructionsFile.cs`:

```csharp
using System.Text;
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Skills.Bootstrap;

public enum AlwaysOnStatus
{
    Enabled,
    Disabled,
    Unchanged,
    MalformedMarkers,
}

public sealed record AlwaysOnOutcome(AlwaysOnStatus Status, AlwaysOnState State);

/// <summary>Applies <see cref="AlwaysOnBlockEditor"/> to %USERPROFILE%\copilot-instructions.md, preserving its encoding.</summary>
public sealed class AlwaysOnInstructionsFile(ProfilePaths paths)
{
    public bool IsBlockPresent()
    {
        return File.Exists(paths.UserInstructionsFile) && AlwaysOnBlockEditor.IsPresent(Read().Text);
    }

    public AlwaysOnOutcome Enable(AlwaysOnState current)
    {
        var existed = File.Exists(paths.UserInstructionsFile);
        var (text, encoding) = existed ? Read() : (string.Empty, new UTF8Encoding(false));
        var newline = text.Contains("\r\n", StringComparison.Ordinal) || !existed ? "\r\n" : "\n";

        var result = AlwaysOnBlockEditor.Apply(text, BootstrapText.Body, newline);
        if (result.Status == BlockEditStatus.MalformedMarkers)
        {
            return new AlwaysOnOutcome(AlwaysOnStatus.MalformedMarkers, current);
        }

        var state = new AlwaysOnState(true, current.CreatedFile || !existed);
        if (result.Status == BlockEditStatus.Unchanged)
        {
            return new AlwaysOnOutcome(AlwaysOnStatus.Unchanged, state);
        }

        Directory.CreateDirectory(paths.UserProfile);
        Write(result.Text, encoding);
        return new AlwaysOnOutcome(AlwaysOnStatus.Enabled, state);
    }

    public AlwaysOnOutcome Disable(AlwaysOnState current)
    {
        var disabled = new AlwaysOnState(false, false);
        if (!File.Exists(paths.UserInstructionsFile))
        {
            return new AlwaysOnOutcome(AlwaysOnStatus.Unchanged, disabled);
        }

        var (text, encoding) = Read();
        var result = AlwaysOnBlockEditor.Remove(text);
        if (result.Status == BlockEditStatus.MalformedMarkers)
        {
            return new AlwaysOnOutcome(AlwaysOnStatus.MalformedMarkers, current);
        }

        if (result.Status == BlockEditStatus.Unchanged)
        {
            return new AlwaysOnOutcome(AlwaysOnStatus.Unchanged, disabled);
        }

        if (current.CreatedFile && string.IsNullOrWhiteSpace(result.Text))
        {
            File.Delete(paths.UserInstructionsFile);
        }
        else
        {
            Write(result.Text, encoding);
        }

        return new AlwaysOnOutcome(AlwaysOnStatus.Disabled, disabled);
    }

    private (string Text, Encoding Encoding) Read()
    {
        var bytes = File.ReadAllBytes(paths.UserInstructionsFile);
        Encoding encoding = bytes switch
        {
            [0xEF, 0xBB, 0xBF, ..] => new UTF8Encoding(true),
            [0xFF, 0xFE, ..] => new UnicodeEncoding(bigEndian: false, byteOrderMark: true),
            [0xFE, 0xFF, ..] => new UnicodeEncoding(bigEndian: true, byteOrderMark: true),
            _ => new UTF8Encoding(false),
        };
        var preamble = encoding.GetPreamble().Length;
        return (encoding.GetString(bytes, preamble, bytes.Length - preamble), encoding);
    }

    private void Write(string text, Encoding encoding)
    {
        var temporary = paths.UserInstructionsFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllBytes(temporary, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
        File.Move(temporary, paths.UserInstructionsFile, overwrite: true);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~AlwaysOnTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: manage the optional always-on bootstrap block in user Copilot instructions"
```

---

### Task 10: `SuperpowersSetup` facade

**Files:**
- Create: `TheKameleon.Superpowers.Skills/Setup/SuperpowersSetup.cs`
- Test: `TheKameleon.Superpowers.Tests/SuperpowersSetupTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 6–9 and `SkillArchiveReadResult` (Task 5).
- Produces: `record InstallPreview(IReadOnlyList<string> Conflicts, IReadOnlyList<string> EditedSkills, bool AgentFileEdited)`, `enum SetupStatus { Succeeded, Partial, Failed }`, `record SetupResult(SetupStatus Status, IReadOnlyList<string> Messages, InstallState State)`.
- Produces on `SuperpowersSetup(ProfilePaths paths)`:
  - `InstallStateLoad LoadState()`
  - `InstallPreview Preview(IReadOnlyList<SkillPackage> packages)`
  - `SetupResult Install(InstalledRelease release, SkillArchiveReadResult source, bool overwriteEdited)`
  - `SetupResult Repair(InstalledRelease release, SkillArchiveReadResult source)`
  - `SetupResult Remove()`
  - `SetupResult SetAlwaysOn(bool enabled)`
  - `SetupResult RefreshAgent()`

- [ ] **Step 1: Write the failing tests**

Create `TheKameleon.Superpowers.Tests/SuperpowersSetupTests.cs`:

```csharp
using TheKameleon.Superpowers.Skills.Bootstrap;
using TheKameleon.Superpowers.Skills.Install;
using TheKameleon.Superpowers.Skills.Setup;

namespace TheKameleon.Superpowers.Tests;

public sealed class SuperpowersSetupTests : IDisposable
{
    private static readonly InstalledRelease Release = new("v1.0.0", "abc", "bundled");
    private readonly TempProfile profile = new();

    private SuperpowersSetup Setup => new(profile.Paths);

    private static SkillArchiveReadResult Source(params SkillPackage[] skills) => new(skills, Array.Empty<string>());

    [Fact]
    public void InstallWritesSkillsAgentAndState()
    {
        var result = Setup.Install(Release, Source(TestSupport.Skill("alpha"), TestSupport.Skill("beta")), overwriteEdited: false);

        Assert.Equal(SetupStatus.Succeeded, result.Status);
        Assert.True(File.Exists(Path.Combine(profile.Paths.SkillsRoot, "beta", "SKILL.md")));
        Assert.True(File.Exists(profile.Paths.AgentFile));
        var state = Setup.LoadState();
        Assert.Equal(InstallStateStatus.Loaded, state.Status);
        Assert.Equal(Release, state.State.Release);
        Assert.Equal(2, state.State.Skills.Count);
        Assert.Equal(BootstrapText.Version, state.State.AgentFile!.BootstrapVersion);
    }

    [Fact]
    public void PreviewReportsConflictsAndEdits()
    {
        Setup.Install(Release, Source(TestSupport.Skill("alpha")), false);
        File.AppendAllText(Path.Combine(profile.Paths.SkillsRoot, "alpha", "SKILL.md"), "edit");
        Directory.CreateDirectory(Path.Combine(profile.Paths.SkillsRoot, "beta"));

        var preview = Setup.Preview(new[] { TestSupport.Skill("alpha"), TestSupport.Skill("beta") });

        Assert.Equal(new[] { "beta" }, preview.Conflicts);
        Assert.Equal(new[] { "alpha" }, preview.EditedSkills);
        Assert.False(preview.AgentFileEdited);
    }

    [Fact]
    public void PartialWhenSomethingWasSkipped()
    {
        Directory.CreateDirectory(Path.Combine(profile.Paths.SkillsRoot, "beta"));

        var result = Setup.Install(Release, Source(TestSupport.Skill("alpha"), TestSupport.Skill("beta")), false);

        Assert.Equal(SetupStatus.Partial, result.Status);
        Assert.Contains(result.Messages, message => message.Contains("beta", StringComparison.Ordinal));
    }

    [Fact]
    public void InstallRefusesCorruptStateAndRepairRecovers()
    {
        var source = Source(TestSupport.Skill("alpha"));
        Setup.Install(Release, source, false);
        File.WriteAllText(profile.Paths.StateFile, "{ broken");

        Assert.Equal(SetupStatus.Failed, Setup.Install(Release, source, false).Status);

        var repaired = Setup.Repair(Release, source);

        Assert.Equal(SetupStatus.Succeeded, repaired.Status);
        Assert.Equal("alpha", Assert.Single(Setup.LoadState().State.Skills).Name);
    }

    [Fact]
    public void RepairDoesNotAdoptFoldersWithDifferentContent()
    {
        Directory.CreateDirectory(Path.Combine(profile.Paths.SkillsRoot, "alpha"));
        File.WriteAllText(Path.Combine(profile.Paths.SkillsRoot, "alpha", "SKILL.md"), "someone else's skill");

        var repaired = Setup.Repair(Release, Source(TestSupport.Skill("alpha")));

        Assert.Equal(SetupStatus.Partial, repaired.Status);
        Assert.Equal("someone else's skill", File.ReadAllText(Path.Combine(profile.Paths.SkillsRoot, "alpha", "SKILL.md")));
    }

    [Fact]
    public void RemoveDeletesEverythingOwned()
    {
        Setup.Install(Release, Source(TestSupport.Skill("alpha")), false);
        Setup.SetAlwaysOn(true);

        var result = Setup.Remove();

        Assert.Equal(SetupStatus.Succeeded, result.Status);
        Assert.False(Directory.Exists(Path.Combine(profile.Paths.SkillsRoot, "alpha")));
        Assert.False(File.Exists(profile.Paths.AgentFile));
        Assert.False(File.Exists(profile.Paths.UserInstructionsFile));
        Assert.Equal(InstallStateStatus.Missing, Setup.LoadState().Status);
    }

    [Fact]
    public void SetAlwaysOnPersistsTheSetting()
    {
        Setup.Install(Release, Source(TestSupport.Skill("alpha")), false);

        Assert.Equal(SetupStatus.Succeeded, Setup.SetAlwaysOn(true).Status);
        Assert.True(Setup.LoadState().State.AlwaysOn.Enabled);
        Assert.Equal(SetupStatus.Succeeded, Setup.SetAlwaysOn(false).Status);
        Assert.False(Setup.LoadState().State.AlwaysOn.Enabled);
    }

    [Fact]
    public void RefreshAgentRewritesOutdatedUneditedAgentFile()
    {
        Setup.Install(Release, Source(TestSupport.Skill("alpha")), false);
        File.WriteAllText(profile.Paths.AgentFile, "old");
        var store = new InstallStateStore(profile.Paths);
        var state = store.Load().State;
        store.Save(state with { AgentFile = new InstalledAgentFile(ContentHash.OfFile(profile.Paths.AgentFile), 0) });

        var result = Setup.RefreshAgent();

        Assert.Equal(SetupStatus.Succeeded, result.Status);
        Assert.Equal(AgentFileWriter.BuildContent(), File.ReadAllText(profile.Paths.AgentFile));
    }

    public void Dispose() => profile.Dispose();
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~SuperpowersSetupTests"`
Expected: build FAIL — `SuperpowersSetup` does not exist.

- [ ] **Step 3: Implement**

`TheKameleon.Superpowers.Skills/Setup/SuperpowersSetup.cs`:

```csharp
using TheKameleon.Superpowers.Skills.Bootstrap;
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Skills.Setup;

public sealed record InstallPreview(IReadOnlyList<string> Conflicts, IReadOnlyList<string> EditedSkills, bool AgentFileEdited);

public enum SetupStatus
{
    Succeeded,
    Partial,
    Failed,
}

public sealed record SetupResult(SetupStatus Status, IReadOnlyList<string> Messages, InstallState State);

/// <summary>The one entry point the UI uses. Every mutating call holds the cross-process install lock.</summary>
public sealed class SuperpowersSetup(ProfilePaths paths)
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(30);
    private const string CorruptMessage = "The Superpowers install record is unreadable. Use Repair.";

    private readonly InstallStateStore store = new(paths);
    private readonly SkillInstaller skills = new(paths);
    private readonly AgentFileWriter agent = new(paths);
    private readonly AlwaysOnInstructionsFile alwaysOn = new(paths);

    public InstallStateLoad LoadState() => store.Load();

    public InstallPreview Preview(IReadOnlyList<SkillPackage> packages)
    {
        var state = store.Load().State;
        return new InstallPreview(
            skills.FindConflicts(packages, state.Skills),
            state.Skills.Where(skills.IsEdited).Select(skill => skill.Name).ToArray(),
            state.AgentFile is not null && agent.IsEdited(state.AgentFile));
    }

    public SetupResult Install(InstalledRelease release, SkillArchiveReadResult source, bool overwriteEdited)
    {
        using var _ = InstallLock.Acquire(LockTimeout);
        var load = store.Load();
        if (load.Status == InstallStateStatus.Corrupt)
        {
            return new SetupResult(SetupStatus.Failed, new[] { CorruptMessage }, load.State);
        }

        return InstallCore(load.State, release, source, overwriteEdited);
    }

    public SetupResult Repair(InstalledRelease release, SkillArchiveReadResult source)
    {
        using var _ = InstallLock.Acquire(LockTimeout);
        var load = store.Load();
        var known = load.Status == InstallStateStatus.Loaded ? load.State : InstallState.Empty;
        var knownNames = known.Skills.Select(skill => skill.Name).ToHashSet(StringComparer.Ordinal);
        var adopted = source.Skills
            .Where(package => !knownNames.Contains(package.Name) && skills.MatchesPackage(package))
            .Select(SkillInstaller.Describe);
        var seed = known with { Skills = known.Skills.Concat(adopted).ToArray() };
        if (seed.AgentFile is null && File.Exists(paths.AgentFile)
            && string.Equals(ContentHash.OfFile(paths.AgentFile), ContentHash.Of(System.Text.Encoding.UTF8.GetBytes(AgentFileWriter.BuildContent())), StringComparison.OrdinalIgnoreCase))
        {
            seed = seed with { AgentFile = new InstalledAgentFile(ContentHash.OfFile(paths.AgentFile), BootstrapText.Version) };
        }

        return InstallCore(seed, release, source, overwriteEdited: false);
    }

    public SetupResult Remove()
    {
        using var _ = InstallLock.Acquire(LockTimeout);
        var load = store.Load();
        if (load.Status == InstallStateStatus.Corrupt)
        {
            return new SetupResult(SetupStatus.Failed, new[] { CorruptMessage }, load.State);
        }

        var messages = skills.Remove(load.State.Skills).Select(issue => issue.Message).ToList();
        if (agent.Remove(load.State.AgentFile).Status == AgentFileStatus.EditedNotRemoved)
        {
            messages.Add("Your edited superpowers.agent.md was left in place.");
        }

        if (load.State.AlwaysOn.Enabled && alwaysOn.Disable(load.State.AlwaysOn).Status == AlwaysOnStatus.MalformedMarkers)
        {
            messages.Add("The always-on block in copilot-instructions.md has damaged markers; remove it by hand.");
        }

        store.Delete();
        return new SetupResult(messages.Count == 0 ? SetupStatus.Succeeded : SetupStatus.Partial, messages, InstallState.Empty);
    }

    public SetupResult SetAlwaysOn(bool enabled)
    {
        using var _ = InstallLock.Acquire(LockTimeout);
        var load = store.Load();
        if (load.Status == InstallStateStatus.Corrupt)
        {
            return new SetupResult(SetupStatus.Failed, new[] { CorruptMessage }, load.State);
        }

        var outcome = enabled ? alwaysOn.Enable(load.State.AlwaysOn) : alwaysOn.Disable(load.State.AlwaysOn);
        if (outcome.Status == AlwaysOnStatus.MalformedMarkers)
        {
            return new SetupResult(SetupStatus.Failed, new[] { "copilot-instructions.md has damaged Superpowers markers; fix or remove them by hand, then try again." }, load.State);
        }

        var state = load.State with { AlwaysOn = outcome.State };
        store.Save(state);
        return new SetupResult(SetupStatus.Succeeded, Array.Empty<string>(), state);
    }

    public SetupResult RefreshAgent()
    {
        using var _ = InstallLock.Acquire(LockTimeout);
        var load = store.Load();
        if (load.Status != InstallStateStatus.Loaded || load.State.AgentFile is null)
        {
            return new SetupResult(SetupStatus.Succeeded, Array.Empty<string>(), load.State);
        }

        var outcome = agent.Write(load.State.AgentFile, overwriteEdited: false);
        if (outcome.Status == AgentFileStatus.EditedKept)
        {
            return new SetupResult(SetupStatus.Partial, new[] { "A newer Superpowers agent is available, but your edited superpowers.agent.md was kept. Use Install to replace it." }, load.State);
        }

        var state = load.State with { AgentFile = outcome.Agent };
        store.Save(state);
        return new SetupResult(SetupStatus.Succeeded, Array.Empty<string>(), state);
    }

    private SetupResult InstallCore(InstallState seed, InstalledRelease release, SkillArchiveReadResult source, bool overwriteEdited)
    {
        var messages = source.Problems.ToList();
        var outcome = skills.Install(source.Skills, seed.Skills, overwriteEdited);
        messages.AddRange(outcome.Issues.Select(issue => issue.Message));
        if (!outcome.Succeeded)
        {
            messages.Add(outcome.FailureReason!);
            return new SetupResult(SetupStatus.Failed, messages, seed);
        }

        var agentOutcome = agent.Write(seed.AgentFile, overwriteEdited);
        if (agentOutcome.Status == AgentFileStatus.EditedKept)
        {
            messages.Add("Your edited superpowers.agent.md was kept.");
        }

        var state = seed with { Release = release, Skills = outcome.Installed, AgentFile = agentOutcome.Agent };
        store.Save(state);
        return new SetupResult(messages.Count == 0 ? SetupStatus.Succeeded : SetupStatus.Partial, messages, state);
    }
}
```

`Repair` compares the agent file with the bytes `AgentFileWriter.Write` produces. `Write` encodes with UTF-8 without BOM, and `Encoding.UTF8.GetBytes` also emits no BOM, so the hashes match.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~SuperpowersSetupTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add SuperpowersSetup facade for install, repair, remove and always-on"
```

---

### Task 11: Status probe and Copilot log diagnostic

**Files:**
- Create: `TheKameleon.Superpowers.Skills/Status/StatusProbe.cs`, `Status/CopilotLogDiagnostic.cs`
- Test: `TheKameleon.Superpowers.Tests/StatusProbeTests.cs`

**Interfaces:**
- Consumes: `SuperpowersSetup` (Task 10), `SkillInstaller.IsEdited`, `AgentFileWriter.IsEdited`, `AlwaysOnInstructionsFile.IsBlockPresent`.
- Produces: `enum StatusLevel { Pass, Warning, Fail, Unknown }`, `record StatusCheck(string Title, StatusLevel Level, string Message)`.
- Produces: `StatusProbe(ProfilePaths paths).Run(string? copilotLogDirectory) -> IReadOnlyList<StatusCheck>`.
- Produces: `static string CopilotLogDiagnostic.DefaultLogDirectory`, `static StatusCheck CopilotLogDiagnostic.Diagnose(string logDirectory, ProfilePaths paths)`.

- [ ] **Step 1: Write the failing tests**

Create `TheKameleon.Superpowers.Tests/StatusProbeTests.cs`:

```csharp
using TheKameleon.Superpowers.Skills.Install;
using TheKameleon.Superpowers.Skills.Setup;
using TheKameleon.Superpowers.Skills.Status;

namespace TheKameleon.Superpowers.Tests;

public sealed class StatusProbeTests : IDisposable
{
    private readonly TempProfile profile = new();

    private IReadOnlyList<StatusCheck> Run(string? logDirectory = null) => new StatusProbe(profile.Paths).Run(logDirectory);

    private void Install() => new SuperpowersSetup(profile.Paths).Install(
        new InstalledRelease("v1.0.0", "abc", "bundled"),
        new SkillArchiveReadResult(new[] { TestSupport.Skill("alpha") }, Array.Empty<string>()),
        overwriteEdited: false);

    [Fact]
    public void NotInstalledIsAWarning()
    {
        var check = Assert.Single(Run());

        Assert.Equal(StatusLevel.Warning, check.Level);
        Assert.Contains("not installed", check.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HealthyInstallPasses()
    {
        Install();

        var checks = Run();

        Assert.All(checks, check => Assert.Equal(StatusLevel.Pass, check.Level));
        Assert.Contains(checks, check => check.Title == "Skills" && check.Message.Contains("v1.0.0", StringComparison.Ordinal));
        Assert.Contains(checks, check => check.Title == "Superpowers agent");
        Assert.Contains(checks, check => check.Title == "Always-on");
    }

    [Fact]
    public void MissingSkillFailsAndEditedSkillWarns()
    {
        Install();
        File.AppendAllText(Path.Combine(profile.Paths.SkillsRoot, "alpha", "SKILL.md"), "edit");
        Assert.Equal(StatusLevel.Warning, Assert.Single(Run(), check => check.Title == "Skills").Level);

        Directory.Delete(Path.Combine(profile.Paths.SkillsRoot, "alpha"), recursive: true);
        Assert.Equal(StatusLevel.Fail, Assert.Single(Run(), check => check.Title == "Skills").Level);
    }

    [Fact]
    public void MissingAgentFails()
    {
        Install();
        File.Delete(profile.Paths.AgentFile);

        Assert.Equal(StatusLevel.Fail, Assert.Single(Run(), check => check.Title == "Superpowers agent").Level);
    }

    [Fact]
    public void DuplicateSkillInAnotherPersonalFolderWarns()
    {
        Install();
        Directory.CreateDirectory(Path.Combine(profile.Paths.OtherPersonalSkillRoots[0], "alpha"));

        var duplicate = Assert.Single(Run(), check => check.Title == "Duplicate skills");

        Assert.Equal(StatusLevel.Warning, duplicate.Level);
        Assert.Contains("alpha", duplicate.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CorruptStateFails()
    {
        Install();
        File.WriteAllText(profile.Paths.StateFile, "{ broken");

        Assert.Equal(StatusLevel.Fail, Assert.Single(Run()).Level);
    }

    [Fact]
    public void LogDiagnosticConfirmsDiscoveryFromNewestLog()
    {
        Install();
        var logs = Path.Combine(profile.Root, "logs");
        Directory.CreateDirectory(logs);
        var old = Path.Combine(logs, "1_VSGitHubCopilot.chat.log");
        File.WriteAllText(old, "nothing");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-1));
        File.WriteAllText(Path.Combine(logs, "2_VSGitHubCopilot.chat.log"),
            $"[x] Found 15 skill files in: {profile.Paths.SkillsRoot}\n[y] Registered custom agent: {profile.Paths.AgentFile}\n");

        var check = CopilotLogDiagnostic.Diagnose(logs, profile.Paths);

        Assert.Equal(StatusLevel.Pass, check.Level);
        Assert.Contains(Run(logs), candidate => candidate.Title == CopilotLogDiagnostic.Title);
    }

    [Fact]
    public void LogDiagnosticIsUnknownNeverFail()
    {
        var logs = Path.Combine(profile.Root, "logs");
        Assert.Equal(StatusLevel.Unknown, CopilotLogDiagnostic.Diagnose(logs, profile.Paths).Level);

        Directory.CreateDirectory(logs);
        File.WriteAllText(Path.Combine(logs, "a_VSGitHubCopilot.chat.log"), "unrelated");
        Assert.Equal(StatusLevel.Unknown, CopilotLogDiagnostic.Diagnose(logs, profile.Paths).Level);
    }

    public void Dispose() => profile.Dispose();
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~StatusProbeTests"`
Expected: build FAIL — types do not exist.

- [ ] **Step 3: Implement**

`TheKameleon.Superpowers.Skills/Status/CopilotLogDiagnostic.cs`:

```csharp
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Skills.Status;

public enum StatusLevel
{
    Pass,
    Warning,
    Fail,
    Unknown,
}

public sealed record StatusCheck(string Title, StatusLevel Level, string Message);

/// <summary>Best-effort read of Copilot's undocumented chat log. Never reports Fail; log content is never returned.</summary>
public static class CopilotLogDiagnostic
{
    public const string Title = "Copilot discovery (diagnostic)";

    public static string DefaultLogDirectory => Path.Combine(Path.GetTempPath(), "VSGitHubCopilotLogs");

    public static StatusCheck Diagnose(string logDirectory, ProfilePaths paths)
    {
        try
        {
            var newest = Directory.Exists(logDirectory)
                ? new DirectoryInfo(logDirectory).EnumerateFiles("*.chat.log").OrderByDescending(file => file.LastWriteTimeUtc).FirstOrDefault()
                : null;
            if (newest is null)
            {
                return new StatusCheck(Title, StatusLevel.Unknown, "No Copilot chat log was found. Open Copilot Chat, then select Refresh.");
            }

            var skillsNeedle = "skill files in: " + paths.SkillsRoot;
            var agentNeedle = "Registered custom agent: " + paths.AgentFile;
            bool foundSkills = false, foundAgent = false;
            using var stream = new FileStream(newest.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line && !(foundSkills && foundAgent))
            {
                foundSkills |= line.Contains("Found ", StringComparison.Ordinal) && line.Contains(skillsNeedle, StringComparison.OrdinalIgnoreCase);
                foundAgent |= line.Contains(agentNeedle, StringComparison.OrdinalIgnoreCase);
            }

            return foundSkills && foundAgent
                ? new StatusCheck(Title, StatusLevel.Pass, "Copilot's log shows it found the skills and the Superpowers agent.")
                : new StatusCheck(Title, StatusLevel.Unknown, "Copilot's log does not confirm discovery yet. Start a new chat thread or restart Visual Studio, then select Refresh.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new StatusCheck(Title, StatusLevel.Unknown, "Copilot's log could not be read.");
        }
    }
}
```

`TheKameleon.Superpowers.Skills/Status/StatusProbe.cs`:

```csharp
using TheKameleon.Superpowers.Skills.Bootstrap;
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Skills.Status;

public sealed class StatusProbe(ProfilePaths paths)
{
    public IReadOnlyList<StatusCheck> Run(string? copilotLogDirectory)
    {
        var load = new InstallStateStore(paths).Load();
        if (load.Status == InstallStateStatus.Corrupt)
        {
            return new[] { new StatusCheck("Install record", StatusLevel.Fail, "The Superpowers install record is unreadable. Use Repair.") };
        }

        var state = load.State;
        if (state.Release is null)
        {
            return new[] { new StatusCheck("Skills", StatusLevel.Warning, "Superpowers is not installed yet. Choose a release and select Install.") };
        }

        var checks = new List<StatusCheck> { SkillsCheck(state) };

        var duplicates = state.Skills
            .Select(skill => skill.Name)
            .Where(name => paths.OtherPersonalSkillRoots.Any(root => Directory.Exists(Path.Combine(root, name))))
            .ToArray();
        if (duplicates.Length > 0)
        {
            checks.Add(new StatusCheck("Duplicate skills", StatusLevel.Warning,
                $"These skills also exist in ~/.claude/skills or ~/.agents/skills, so Copilot may see two copies: {string.Join(", ", duplicates)}."));
        }

        checks.Add(AgentCheck(state));
        checks.Add(AlwaysOnCheck(state));

        if (copilotLogDirectory is not null)
        {
            checks.Add(CopilotLogDiagnostic.Diagnose(copilotLogDirectory, paths));
        }

        return checks;
    }

    private StatusCheck SkillsCheck(InstallState state)
    {
        var installer = new SkillInstaller(paths);
        var missing = state.Skills.Where(skill => !Directory.Exists(Path.Combine(paths.SkillsRoot, skill.Name))).Select(skill => skill.Name).ToArray();
        var edited = state.Skills.Where(installer.IsEdited).Select(skill => skill.Name).ToArray();
        var summary = $"{state.Skills.Count} skills from {state.Release!.Tag} are installed in {paths.SkillsRoot}.";
        if (missing.Length > 0)
        {
            return new StatusCheck("Skills", StatusLevel.Fail, $"{summary} Missing: {string.Join(", ", missing)}. Use Repair.");
        }

        return edited.Length > 0
            ? new StatusCheck("Skills", StatusLevel.Warning, $"{summary} Edited by you: {string.Join(", ", edited)}.")
            : new StatusCheck("Skills", StatusLevel.Pass, summary);
    }

    private StatusCheck AgentCheck(InstallState state)
    {
        const string title = "Superpowers agent";
        if (state.AgentFile is null || !File.Exists(paths.AgentFile))
        {
            return new StatusCheck(title, StatusLevel.Fail, "The Superpowers agent file is missing. Use Repair.");
        }

        return new AgentFileWriter(paths).IsEdited(state.AgentFile)
            ? new StatusCheck(title, StatusLevel.Warning, "You have edited superpowers.agent.md; updates will not replace it automatically.")
            : new StatusCheck(title, StatusLevel.Pass, $"Select Superpowers in the Copilot agent picker (bootstrap version {state.AgentFile.BootstrapVersion}).");
    }

    private StatusCheck AlwaysOnCheck(InstallState state)
    {
        const string title = "Always-on";
        if (!state.AlwaysOn.Enabled)
        {
            return new StatusCheck(title, StatusLevel.Pass, "Off. Superpowers applies only when you select the Superpowers agent.");
        }

        return new AlwaysOnInstructionsFile(paths).IsBlockPresent()
            ? new StatusCheck(title, StatusLevel.Pass, "On. Every Agent-mode chat receives the Superpowers bootstrap.")
            : new StatusCheck(title, StatusLevel.Fail, "On, but the block is missing from copilot-instructions.md. Turn always-on off and on again.");
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~StatusProbeTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add install status checks and best-effort Copilot discovery diagnostic"
```

---

### Task 12: Install every bundled release into a temporary profile

**Files:**
- Test: `TheKameleon.Superpowers.Tests/BundledReleaseInstallTests.cs`

**Interfaces:** Consumes `BundledCatalogLoader`, `ReleaseSkillLoader`, `SuperpowersSetup`, `SkillFrontMatter`, `SkillValidator`. Produces nothing new.

- [ ] **Step 1: Write the test**

Create `TheKameleon.Superpowers.Tests/BundledReleaseInstallTests.cs`:

```csharp
using System.Text;
using TheKameleon.Superpowers.Skills.Catalog;
using TheKameleon.Superpowers.Skills.Install;
using TheKameleon.Superpowers.Skills.Setup;

namespace TheKameleon.Superpowers.Tests;

public sealed class BundledReleaseInstallTests
{
    public static TheoryData<string> ReleaseTags()
    {
        var data = new TheoryData<string>();
        foreach (var release in BundledCatalogLoader.LoadFromDirectory(TestSupport.BundledCatalogRoot).Releases)
        {
            data.Add(release.ReleaseTag);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ReleaseTags))]
    public void EveryBundledReleaseInstallsAndEveryInstalledSkillIsValid(string tag)
    {
        using var profile = new TempProfile();
        var release = BundledCatalogLoader.LoadFromDirectory(TestSupport.BundledCatalogRoot).Releases.Single(candidate => candidate.ReleaseTag == tag);
        var source = ReleaseSkillLoader.Load(TestSupport.BundledCatalogRoot, release);

        var result = new SuperpowersSetup(profile.Paths).Install(new InstalledRelease(tag, release.ResolvedCommit, "bundled"), source, overwriteEdited: false);

        Assert.NotEqual(SetupStatus.Failed, result.Status);
        Assert.NotEmpty(result.State.Skills);
        foreach (var skill in result.State.Skills)
        {
            var markdown = File.ReadAllBytes(Path.Combine(profile.Paths.SkillsRoot, skill.Name, "SKILL.md"));
            var package = new SkillPackage(skill.Name, new Dictionary<string, byte[]> { ["SKILL.md"] = markdown });
            Assert.Empty(SkillValidator.Validate(package));
        }
    }

    [Fact]
    public void NewestReleaseInstallsAllFifteenSkillsCleanly()
    {
        using var profile = new TempProfile();
        var catalog = BundledCatalogLoader.LoadFromDirectory(TestSupport.BundledCatalogRoot);
        var release = ReleaseSelection.DefaultRelease(catalog.Releases)!;

        var result = new SuperpowersSetup(profile.Paths).Install(
            new InstalledRelease(release.ReleaseTag, release.ResolvedCommit, "bundled"),
            ReleaseSkillLoader.Load(TestSupport.BundledCatalogRoot, release),
            overwriteEdited: false);

        Assert.Equal(SetupStatus.Succeeded, result.Status);
        Assert.Equal(15, result.State.Skills.Count);
    }
}
```

- [ ] **Step 2: Run the test**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~BundledReleaseInstallTests"`
Expected: PASS for all 13 releases plus the newest-release fact. If an older release reports `Partial`, that is allowed; read the messages. If any release is `Failed`, stop and report the messages; do not loosen the assertion.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "test: install every bundled Superpowers release into a temporary profile"
```

---

### Task 13: The Superpowers tool window

**Files:**
- Create: `TheKameleon.Superpowers.Vsix/OpenSuperpowersCommand.cs`, `StatusItem.cs`
- Delete: `TheKameleon.Superpowers.Vsix/PlanCommand.cs`
- Replace: `SuperpowersViewModel.cs`, `SuperpowersToolWindowControl.xaml`
- Modify: `SuperpowersToolWindow.cs`, `SuperpowersExtension.cs`, `.vsextension/string-resources.json`
- Rename and modify: `TheKameleon.Superpowers.IntegrationTests/PlanToolWindowPackageTests.cs` → `ToolWindowPackageTests.cs`; modify `ExtensionPackageTests.cs`

**Interfaces:** Consumes `SuperpowersSetup`, `ReleaseSkillLoader`, `ReleaseSelection`, `BundledCatalogLoader`, `StatusProbe`, `CopilotLogDiagnostic`, `ProfilePaths.ForCurrentUser()`. Produces the VSIX UI. Later tasks bind `HeaderIcon` (Task 14) and a download command (Task 18).

- [ ] **Step 1: Write the failing package tests**

Rename the file and class: `git mv TheKameleon.Superpowers.IntegrationTests/PlanToolWindowPackageTests.cs TheKameleon.Superpowers.IntegrationTests/ToolWindowPackageTests.cs`, then rename `public class PlanToolWindowPackageTests` to `public class ToolWindowPackageTests`.

In `ToolWindowPackageTests.cs`:
- In `PackageRegistersPlanCommand` rename the method to `PackageRegistersOpenCommand`, and change the command name to `"TheKameleon.Superpowers.Vsix.OpenSuperpowersCommand"` and the resource to `AssertLocalizedDisplayName(package, command, "Superpowers.OpenCommand.DisplayName", "Open")`.
- In `PlanCommandIsPlacedUnderSuperpowersInExtensionsMenu` rename the method to `OpenCommandIsPlacedUnderSuperpowersInExtensionsMenu` and change `"TheKameleon.Superpowers.Vsix.PlanCommand"` to `"TheKameleon.Superpowers.Vsix.OpenSuperpowersCommand"`.
- Add these tests above the private helpers:

```csharp
        [Fact]
        public void PackageRegistersOnlyTheOpenCommand()
        {
            using var package = OpenPackage();
            using var stream = OpenRequiredEntry(package, ".vsextension/extension.json");
            using var registration = JsonDocument.Parse(stream);
            var names = registration.RootElement.GetProperty("commandSets").EnumerateArray()
                .SelectMany(commandSet => commandSet.GetProperty("commands").EnumerateArray())
                .Select(command => command.GetProperty("name").GetString())
                .ToArray();

            Assert.Equal(new[] { "TheKameleon.Superpowers.Vsix.OpenSuperpowersCommand" }, names);
        }

        [Fact]
        public void PackageEmbedsInstallerViewWithAccessibleCommands()
        {
            var view = LoadEmbeddedToolWindowXaml();
            XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

            foreach (var command in new[] { "InstallCommand", "RepairCommand", "RemoveCommand", "RefreshCommand", "ToggleAlwaysOnCommand" })
            {
                var button = Assert.Single(view.Descendants(presentation + "Button"), element =>
                    (string?)element.Attribute("Command") == $"{{Binding {command}}}");
                Assert.False(string.IsNullOrWhiteSpace((string?)button.Attribute("AutomationProperties.Name")),
                    $"Button bound to {command} must expose an accessible name.");
            }

            var releasePicker = Assert.Single(view.Descendants(presentation + "ComboBox"));
            Assert.Equal("{Binding ReleaseVersions}", (string?)releasePicker.Attribute("ItemsSource"));
            Assert.False(string.IsNullOrWhiteSpace((string?)releasePicker.Attribute("AutomationProperties.Name")));
            Assert.Single(view.Descendants(presentation + "ItemsControl"), element => (string?)element.Attribute("ItemsSource") == "{Binding Checks}");
            Assert.Single(view.Descendants(presentation + "ItemsControl"), element => (string?)element.Attribute("ItemsSource") == "{Binding Tips}");
        }

        [Fact]
        public void ViewModelDeclaresRemoteUiSerializationAttributes()
        {
            using var package = OpenPackage();
            using var assemblyStream = OpenRequiredEntry(package, "TheKameleon.Superpowers.Vsix.dll");
            using var assembly = new MemoryStream();
            assemblyStream.CopyTo(assembly);
            assembly.Position = 0;
            using var peReader = new PEReader(assembly);
            var metadata = peReader.GetMetadataReader();
            var type = Assert.Single(metadata.TypeDefinitions.Select(metadata.GetTypeDefinition), candidate =>
                metadata.GetString(candidate.Name) == "SuperpowersViewModel");

            Assert.Contains(type.GetCustomAttributes(), attribute => GetAttributeTypeName(metadata, attribute) == "DataContractAttribute");
            foreach (var propertyName in new[] { "StatusText", "InstalledText", "SelectedReleaseVersion", "AlwaysOnButtonText", "IsIdle", "Checks", "Tips" })
            {
                var property = Assert.Single(type.GetProperties().Select(metadata.GetPropertyDefinition), candidate =>
                    metadata.GetString(candidate.Name) == propertyName);
                Assert.Contains(property.GetCustomAttributes(), attribute => GetAttributeTypeName(metadata, attribute) == "DataMemberAttribute");
            }
        }
```

In `ExtensionPackageTests.cs` change `Assert.Equal("[17.14,)", (string?)target.Attribute("Version"));` to `Assert.Equal("[18.5,)", (string?)target.Attribute("Version"));`.

- [ ] **Step 2: Run the package tests to verify they fail**

Run: `dotnet test TheKameleon.Superpowers.IntegrationTests/TheKameleon.Superpowers.IntegrationTests.csproj --filter "FullyQualifiedName~ToolWindowPackageTests|FullyQualifiedName~ManifestDeclaresSupportedVisualStudioArchitecture"`
Expected: FAIL — `OpenSuperpowersCommand` is not registered, the XAML has none of the bindings, and the version range is `[17.14,)`.

- [ ] **Step 3: Implement the command, extension metadata and resources**

Delete `PlanCommand.cs` (`git rm TheKameleon.Superpowers.Vsix/PlanCommand.cs`). Create `TheKameleon.Superpowers.Vsix/OpenSuperpowersCommand.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;

namespace TheKameleon.Superpowers.Vsix
{
    [VisualStudioContribution]
    public sealed class OpenSuperpowersCommand : Command
    {
        public override CommandConfiguration CommandConfiguration => new("%Superpowers.OpenCommand.DisplayName%");

        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await this.Extensibility.Shell().ShowToolWindowAsync<SuperpowersToolWindow>(activate: true, cancellationToken);
        }
    }
}
```

In `SuperpowersExtension.cs` change the menu child to `MenuChild.Command<OpenSuperpowersCommand>(),` and add `InstallationTargetVersion = "[18.5,)",` to the metadata object:

```csharp
            Metadata = new(
                id: "TheKameleon.Superpowers.Vsix.8a7fab37-7cfc-4314-ae3c-946698f3ff5d",
                version: new System.Version(1, 0, GeneratedBuildVersion.Build, GeneratedBuildVersion.Revision),
                publisherName: "TheKameleon",
                displayName: "Superpowers for Visual Studio",
                description: "Installs the Superpowers skills and a Superpowers agent for GitHub Copilot Chat in Visual Studio 2026.")
            {
                InstallationTargetVersion = "[18.5,)",
            },
```

Replace `.vsextension/string-resources.json` with:

```json
{
  "Superpowers.OpenCommand.DisplayName": "Open",
  "Superpowers.Menu.DisplayName": "Superpowers"
}
```

- [ ] **Step 4: Implement the view model**

Create `TheKameleon.Superpowers.Vsix/StatusItem.cs`:

```csharp
using System.Runtime.Serialization;

namespace TheKameleon.Superpowers.Vsix
{
    [DataContract]
    internal sealed class StatusItem
    {
        public StatusItem(string level, string title, string message)
        {
            this.Level = level;
            this.Title = title;
            this.Message = message;
        }

        [DataMember]
        public string Level { get; }

        [DataMember]
        public string Title { get; }

        [DataMember]
        public string Message { get; }
    }
}
```

Replace `TheKameleon.Superpowers.Vsix/SuperpowersViewModel.cs`:

```csharp
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
```

`ShowPromptAsync(string, PromptOptions<bool>, CancellationToken)` and `PromptOptions.OKCancel` come from `Microsoft.VisualStudio.Extensibility.Shell` (see <https://learn.microsoft.com/visualstudio/extensibility/visualstudio.extensibility/dialog-prompts/prompts>). If the compiler reports a different signature for SDK 17.14.40608, adapt only the call shape and keep the same message text and OK/Cancel semantics.

In `SuperpowersToolWindow.cs` replace `GetContentAsync` with:

```csharp
        public override async Task<IRemoteUserControl> GetContentAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (this.control is null)
            {
                var viewModel = new SuperpowersViewModel(this.Extensibility);
                this.control = new SuperpowersToolWindowControl(viewModel);
                await viewModel.InitializeAsync(cancellationToken).ConfigureAwait(false);
            }

            return this.control;
        }
```

- [ ] **Step 5: Implement the view**

Replace `TheKameleon.Superpowers.Vsix/SuperpowersToolWindowControl.xaml`:

```xml
<DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
              xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
              xmlns:colors="clr-namespace:Microsoft.VisualStudio.PlatformUI;assembly=Microsoft.VisualStudio.Shell.15.0">
    <Border Padding="12"
            Background="{DynamicResource {x:Static colors:EnvironmentColors.ToolWindowBackgroundBrushKey}}"
            TextElement.Foreground="{DynamicResource {x:Static colors:EnvironmentColors.ToolWindowTextBrushKey}}">
        <ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled">
            <StackPanel IsEnabled="{Binding IsIdle}">
                <TextBlock Text="Superpowers for GitHub Copilot" FontWeight="SemiBold" FontSize="16" />
                <TextBlock Text="{Binding Explanation}" TextWrapping="Wrap" Margin="0,6,0,0" />

                <Grid Margin="0,12,0,0">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto" />
                        <ColumnDefinition Width="*" />
                    </Grid.ColumnDefinitions>
                    <TextBlock Grid.Column="0" Text="Release:" Margin="0,0,8,0" VerticalAlignment="Center" />
                    <ComboBox Grid.Column="1"
                              ItemsSource="{Binding ReleaseVersions}"
                              SelectedItem="{Binding SelectedReleaseVersion, Mode=TwoWay}"
                              AutomationProperties.Name="Superpowers release" />
                </Grid>

                <WrapPanel Margin="0,8,0,0">
                    <Button Command="{Binding InstallCommand}" Content="Install" Margin="0,0,8,4" Padding="12,2" AutomationProperties.Name="Install the selected Superpowers release" />
                    <Button Command="{Binding RepairCommand}" Content="Repair" Margin="0,0,8,4" Padding="12,2" AutomationProperties.Name="Repair the Superpowers installation" />
                    <Button Command="{Binding RemoveCommand}" Content="Remove from my profile" Margin="0,0,8,4" Padding="12,2" AutomationProperties.Name="Remove Superpowers from your profile" />
                    <Button Command="{Binding ToggleAlwaysOnCommand}" Content="{Binding AlwaysOnButtonText}" Margin="0,0,8,4" Padding="12,2" AutomationProperties.Name="Toggle always-on Superpowers" />
                </WrapPanel>

                <TextBlock Text="{Binding InstalledText}" FontWeight="SemiBold" Margin="0,8,0,0" TextWrapping="Wrap" />
                <TextBlock Text="{Binding StatusText}" Margin="0,4,0,0" TextWrapping="Wrap" />

                <Grid Margin="0,16,0,4">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="Auto" />
                    </Grid.ColumnDefinitions>
                    <TextBlock Grid.Column="0" Text="Status" FontWeight="SemiBold" VerticalAlignment="Center" />
                    <Button Grid.Column="1" Command="{Binding RefreshCommand}" Content="Refresh" Padding="12,2" AutomationProperties.Name="Refresh Superpowers status" />
                </Grid>
                <ItemsControl ItemsSource="{Binding Checks}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <StackPanel Margin="0,4,0,0">
                                <TextBlock TextWrapping="Wrap">
                                    <Run Text="{Binding Level, Mode=OneWay}" FontWeight="SemiBold" />
                                    <Run Text=" · " />
                                    <Run Text="{Binding Title, Mode=OneWay}" FontWeight="SemiBold" />
                                </TextBlock>
                                <TextBlock Text="{Binding Message}" TextWrapping="Wrap" Margin="12,0,0,0" />
                            </StackPanel>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>

                <TextBlock Text="Using Superpowers" FontWeight="SemiBold" Margin="0,16,0,4" />
                <ItemsControl ItemsSource="{Binding Tips}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <TextBlock Text="{Binding}" TextWrapping="Wrap" Margin="0,2,0,0" />
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </StackPanel>
        </ScrollViewer>
    </Border>
</DataTemplate>
```

- [ ] **Step 6: Run all tests**

Run: `dotnet build TheKameleon.Superpowers.slnx`
Expected: Build succeeded.

Run: `dotnet test TheKameleon.Superpowers.IntegrationTests/TheKameleon.Superpowers.IntegrationTests.csproj`
Expected: PASS.

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj`
Expected: PASS.

- [ ] **Step 7: Smoke test in VS 2026**

Press F5 on `TheKameleon.Superpowers.Vsix` (Exp instance). Open **Extensions ▸ Superpowers ▸ Open**. Expected: v6.4.1 preselected, status "not installed", the Install button enabled. **Do not click Install in the Exp instance unless you intend to change your real profile**; the Exp instance uses your real `%USERPROFILE%`. Close the Exp instance.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: replace the Plan window with the Superpowers installer tool window"
```

---

### Task 14: Superpowers icon

**Files:**
- Create: `art/superpowers-icon.xaml`, `build/Render-Icons.ps1`
- Create (generated, committed): `TheKameleon.Superpowers.Vsix/Images/Superpowers.16.16.png`, `Images/Superpowers.20.20.png`, `Images/Superpowers.32.32.png`, `TheKameleon.Superpowers.Vsix/Resources/icon.png` (128×128), `Resources/preview.png` (200×200)
- Modify: `TheKameleon.Superpowers.Vsix/TheKameleon.Superpowers.Vsix.csproj`, `SuperpowersExtension.cs`, `OpenSuperpowersCommand.cs`, `SuperpowersToolWindowControl.xaml`
- Test: `TheKameleon.Superpowers.IntegrationTests/ExtensionPackageTests.cs`

**Interfaces:** Consumes the Task 13 UI. Produces the custom image moniker `Superpowers`.

- [ ] **Step 1: Write the failing package tests**

Add to `ExtensionPackageTests.cs` (add `using System.IO.Compression;` and `using System.Xml.Linq;` if missing):

```csharp
        [Fact]
        public void ManifestDeclaresIconAndPreviewImageThatArePackaged()
        {
            using var package = ZipFile.OpenRead(Path.Combine(AppContext.BaseDirectory, PackageName));
            var manifest = ReadManifest(package);
            var metadata = Assert.Single(manifest.Descendants(ManifestNamespace + "Metadata"));

            foreach (var element in new[] { "Icon", "PreviewImage" })
            {
                var path = (string?)metadata.Element(ManifestNamespace + element);
                Assert.False(string.IsNullOrWhiteSpace(path), $"Manifest must declare <{element}>.");
                Assert.NotNull(package.GetEntry(path!.Replace('\\', '/')));
            }
        }

        [Fact]
        public void PackageContainsCommandIconImages()
        {
            using var package = ZipFile.OpenRead(Path.Combine(AppContext.BaseDirectory, PackageName));

            Assert.Contains(package.Entries, entry => entry.FullName.EndsWith("Superpowers.16.16.png", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(package.Entries, entry => entry.FullName.EndsWith("Superpowers.20.20.png", StringComparison.OrdinalIgnoreCase));
        }
```

If `ReadManifest(package)` in this file takes a different parameter type, use the same call pattern as `ManifestDeclaresSupportedVisualStudioArchitecture`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test TheKameleon.Superpowers.IntegrationTests/TheKameleon.Superpowers.IntegrationTests.csproj --filter "FullyQualifiedName~ManifestDeclaresIconAndPreviewImageThatArePackaged|FullyQualifiedName~PackageContainsCommandIconImages"`
Expected: FAIL — no `<Icon>`, no images.

- [ ] **Step 3: Create the art master and render script**

`art/superpowers-icon.xaml`, an original design (a lightning bolt on a rounded square), drawn on a 32×32 canvas:

```xml
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Width="32" Height="32">
    <Rectangle Canvas.Left="1" Canvas.Top="1" Width="30" Height="30" RadiusX="7" RadiusY="7">
        <Rectangle.Fill>
            <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                <GradientStop Color="#6D3BEA" Offset="0" />
                <GradientStop Color="#3A1C9C" Offset="1" />
            </LinearGradientBrush>
        </Rectangle.Fill>
    </Rectangle>
    <Path Data="M18.5,3.5 L8,18 L14.5,18 L12.5,28.5 L24,13 L17.5,13 Z"
          Fill="#FFD43B" Stroke="#1B1037" StrokeThickness="1.2" StrokeLineJoin="Round" />
</Canvas>
```

`build/Render-Icons.ps1`:

```powershell
<#
.SYNOPSIS
Renders art/superpowers-icon.xaml to the PNG sizes the VSIX ships. Run with Windows PowerShell (STA).
#>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase

$root = Split-Path -Parent $PSScriptRoot
$xaml = Get-Content -Raw (Join-Path $root 'art\superpowers-icon.xaml')
$vsix = Join-Path $root 'TheKameleon.Superpowers.Vsix'

$targets = @(
    @{ Size = 16;  Path = 'Images\Superpowers.16.16.png' },
    @{ Size = 20;  Path = 'Images\Superpowers.20.20.png' },
    @{ Size = 32;  Path = 'Images\Superpowers.32.32.png' },
    @{ Size = 128; Path = 'Resources\icon.png' },
    @{ Size = 200; Path = 'Resources\preview.png' }
)

foreach ($target in $targets) {
    $size = $target.Size
    $viewbox = New-Object System.Windows.Controls.Viewbox
    $viewbox.Child = [System.Windows.Markup.XamlReader]::Parse($xaml)
    $viewbox.Width = $size
    $viewbox.Height = $size
    $viewbox.Measure((New-Object System.Windows.Size($size, $size)))
    $viewbox.Arrange((New-Object System.Windows.Rect(0, 0, $size, $size)))
    $viewbox.UpdateLayout()

    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($viewbox)
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))

    $output = Join-Path $vsix $target.Path
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $output) | Out-Null
    $stream = [System.IO.File]::Create($output)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
    Write-Host "Rendered $($target.Path) ($size x $size)"
}
```

Run: `powershell.exe -NoProfile -STA -File build/Render-Icons.ps1`
Expected: five "Rendered …" lines and the five PNG files exist.

- [ ] **Step 4: Wire the images into the extension**

In `TheKameleon.Superpowers.Vsix.csproj`, in the `ItemGroup` with the catalog `Content`, add:

```xml
    <Content Include="Resources\icon.png" IncludeInVSIX="true" />
    <Content Include="Resources\preview.png" IncludeInVSIX="true" />
```

In `SuperpowersExtension.cs` extend the metadata initializer:

```csharp
            {
                InstallationTargetVersion = "[18.5,)",
                Icon = "Resources\\icon.png",
                PreviewImage = "Resources\\preview.png",
            },
```

In `OpenSuperpowersCommand.cs` give the command the icon (add `using Microsoft.VisualStudio.Extensibility;` if missing):

```csharp
        public override CommandConfiguration CommandConfiguration => new("%Superpowers.OpenCommand.DisplayName%")
        {
            Icon = new(ImageMoniker.Custom("Superpowers"), IconSettings.IconAndText),
        };
```

In `SuperpowersToolWindowControl.xaml` add the namespace `xmlns:vs="http://schemas.microsoft.com/visualstudio/extensibility/2022/xaml"` to the `DataTemplate` element and replace the title `TextBlock` with:

```xml
                <StackPanel Orientation="Horizontal">
                    <vs:Image Source="Superpowers" Width="32" Height="32" Margin="0,0,8,0" AutomationProperties.Name="Superpowers" />
                    <TextBlock Text="Superpowers for GitHub Copilot" FontWeight="SemiBold" FontSize="16" VerticalAlignment="Center" />
                </StackPanel>
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test TheKameleon.Superpowers.IntegrationTests/TheKameleon.Superpowers.IntegrationTests.csproj`
Expected: PASS. If `PackageContainsCommandIconImages` still fails, the SDK did not pick up `Images\` automatically: add `<Content Include="Images\*.png" IncludeInVSIX="true" />` to the csproj and rerun.

- [ ] **Step 6: Check it visually in VS 2026**

F5 into Exp. Check that the Superpowers icon shows next to **Extensions ▸ Superpowers ▸ Open**, in the tool window header, and in **Manage Extensions** for the extension. Check light, dark and high-contrast themes. Record the result in the acceptance checklist (Task 15).

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: add original Superpowers icon to the menu, tool window and extension listing"
```

---

### Task 15: Documentation and acceptance checklist

**Files:**
- Replace: `README.md`
- Create: `docs/superpowers/acceptance/agent-skills-acceptance.md`
- Modify: `docs/superpowers/plans/implementation-plan.md` (decision register §2 and a new record at the end)

**Interfaces:** Consumes the finished Phase 1 behavior. Produces user and contributor documentation.

- [ ] **Step 1: Write the README**

Replace `README.md` with:

````markdown
# Superpowers for Visual Studio

Brings the [Superpowers](https://github.com/obra/superpowers) skills (brainstorming, writing plans, test-driven development, systematic debugging, code review and verification) to GitHub Copilot Chat in **Visual Studio 2026 18.5 or later**.

Copilot in Visual Studio natively discovers Agent Skills. This extension installs the unmodified upstream skills where Copilot finds them, and adds a **Superpowers** agent that makes Copilot use them.

## Use it

1. Open **Extensions ▸ Superpowers ▸ Open**.
2. Pick a release (the newest stable one is preselected) and select **Install**.
3. In Copilot Chat, choose **Superpowers** in the agent picker (or type `@Superpowers`) and start a new thread.
4. Turn **Autopilot off** for brainstorming and planning; those skills ask you questions.

## What it changes on your machine

| Location | What |
|---|---|
| `%USERPROFILE%\.copilot\skills\<skill>\` | The upstream skill folders, unchanged |
| `%USERPROFILE%\.github\agents\superpowers.agent.md` | The Superpowers agent |
| `%USERPROFILE%\copilot-instructions.md` | Only if you turn on **always-on**: one marked block; the rest of the file is untouched |
| `%LOCALAPPDATA%\TheKameleon.Superpowers\install-state.json` | What the extension installed, so it never touches anything else |

The extension never overwrites a skill it did not install, and asks before replacing files you edited.

**Before uninstalling the extension, open the tool window and select Remove from my profile.** Visual Studio gives extensions no uninstall hook, so the files stay otherwise.

## Status panel

The tool window checks that the skills and agent are present and unmodified, and warns if the same skills also exist in `~/.claude/skills` or `~/.agents/skills` (Copilot would see duplicates). A diagnostic line reads Copilot's own log to confirm it discovered them; it reports *Unknown* rather than failing when the log cannot confirm.

## Build and test

Requires Windows, Visual Studio 2026 with the extension-development workload, and a `.slnx`-capable .NET SDK (9.0.200 or later). All projects target .NET 8.

```powershell
dotnet build TheKameleon.Superpowers.slnx
dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj
dotnet test TheKameleon.Superpowers.IntegrationTests/TheKameleon.Superpowers.IntegrationTests.csproj
```

Unit tests install into temporary folders and never touch your profile. The F5 experimental instance uses your **real** profile, so Install there changes your real `%USERPROFILE%`.

Manual acceptance on VS 2026: [docs/superpowers/acceptance/agent-skills-acceptance.md](docs/superpowers/acceptance/agent-skills-acceptance.md). Design: [docs/superpowers/specs/2026-09-23-agent-skills-installer-design.md](docs/superpowers/specs/2026-09-23-agent-skills-installer-design.md).

## Licenses

Superpowers skills are © Jesse Vincent and contributors, MIT licensed; each bundled release includes its `LICENSE.txt`. The icon is original artwork for this extension.
````

- [ ] **Step 2: Write the acceptance checklist**

Create `docs/superpowers/acceptance/agent-skills-acceptance.md`:

```markdown
# Agent Skills acceptance checklist (VS 2026)

Run on a clean profile after installing the VSIX. Use the **Superpowers** agent with **Autopilot off** unless a row says otherwise, and a new chat thread per row. Evidence comes from the newest `%TEMP%\VSGitHubCopilotLogs\*.chat.log`: search for `get_file(` lines and the assistant text.

Test solution: a small C# class library with `PriceCalculator.ApplyDiscount(decimal price, int percent)` that rounds with `MidpointRounding.ToZero`, and an xUnit test expecting `ApplyDiscount(11.25m, 10) == 10.13m` (fails until `AwayFromZero` is used).

| # | Prompt | Pass criteria | VS version | Result | Date |
|---|---|---|---|---|---|
| A1 | "I want to add a loyalty discount that stacks with the percentage discount." | `brainstorming` read; one question at a time; no code written | | | |
| A2 | "The RoundsHalfAwayFromZero test is failing. Fix it." | `systematic-debugging` read to its last line; test run **before** the edit; root cause stated; tests rerun before claiming success | | | |
| A3 | "Write an implementation plan for adding sales tax to PriceCalculator." | `writing-plans` read | | | |
| A4 | "I think the fix is done — is this ready to merge?" | `verification-before-completion` read; tests run before any success claim | | | |
| A5 | Any flow above that names another skill | the referenced skill is read with `get_file` | | | |
| A6 | "Help me write a new skill." | `writing-skills` (681 lines) read to its last line | | | |
| A7 | A1 with Autopilot on | agent stops and says interactive Agent mode is required | | | |
| A8 | Always-on enabled, default Agent (not Superpowers) | `using-superpowers` read without selecting the agent | | | |
| U1 | Install, Repair, Remove, always-on on/off from the tool window | each reports success; files appear/disappear as the README table says | | | |
| U2 | Status panel after Install and a new chat thread | all checks OK; the diagnostic line shows OK | | | |
| U3 | Icon in menu, tool window, Manage Extensions; light, dark, high contrast | legible in every theme | | | |

A5 gates the release: if referenced skills are still not loaded, record the evidence and decide on the index-skill fallback (spec §13) before publishing.
```

- [ ] **Step 3: Record the decisions in the canonical plan**

In `docs/superpowers/plans/implementation-plan.md`, append these rows to the decision table in §2:

```markdown
| N01 | First release targets Visual Studio 2026 18.5+ only; amends D07. | Approved 2026-09-23 |
| N02 | Retire the in-process bridge, context capture, prompt composition, handoff, workflow and execution subsystems; supersedes D02/D19 and P05–P07 product scope. | Approved 2026-09-23 |
| N03 | Activation through a user-level *Superpowers* custom agent, plus opt-in always-on block in `%USERPROFILE%\copilot-instructions.md`; amends the no-instruction-file-edit constraint. | Approved 2026-09-23 |
| N04–N07 | Unmodified upstream skills; ownership-only file changes; nothing written before Install; original icon. See the Agent Skills installer spec. | Approved 2026-09-23 |
```

and append at the end of the file:

```markdown
| Agent Skills redirection | P08–P11 are replaced by [the Agent Skills installer plan](2026-09-23-agent-skills-installer.md), implementing [the design spec](../specs/2026-09-23-agent-skills-installer-design.md). The 2026-09-23 spike showed VS 2026 Copilot discovers `~/.copilot/skills` and user-level custom agents, and follows upstream skills once bootstrapped. |
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "docs: rewrite README and add Agent Skills acceptance checklist"
```

- [ ] **Step 5: Phase gate**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj` and `dotnet test TheKameleon.Superpowers.IntegrationTests/TheKameleon.Superpowers.IntegrationTests.csproj`
Expected: both PASS. The repository rule requires the full unit and integration suites to pass before starting Phase 2. Then run the acceptance checklist rows A1–A8 and U1–U3 on VS 2026 and record the results.

---

# Phase 2 — Downloading newer releases

### Task 16: Harden the download service

**Files:**
- Modify: `TheKameleon.Superpowers.Skills/Catalog/ApprovedReleaseDownloadService.cs`
- Test: `TheKameleon.Superpowers.Tests/ApprovedReleaseDownloadServiceTests.cs`

**Interfaces:** Keeps `DownloadAndActivateAsync(DiscoveredRemoteRelease, string targetDirectory, bool approvalGranted, CancellationToken)`. Adds `public const int MaxArchiveEntries = 10_000;` and `public const long MaxExtractedBytes = 100L * 1024 * 1024;`. New diagnostic code `SPCAT612` (archive too large when extracted).

- [ ] **Step 1: Write the failing tests**

Add to `ApprovedReleaseDownloadServiceTests.cs`:

```csharp
    [Fact]
    public async Task RejectsPlainHttpUrls()
    {
        using var client = CreateClient(_ => CreateZipResponse(CreateValidArchive()));
        var release = CreateApprovedRelease() with { ZipballUrl = "http://api.github.com/repos/obra/superpowers/zipball/v1.0.0" };

        var result = await new ApprovedReleaseDownloadService(client).DownloadAndActivateAsync(release, Path.Combine(tempRoot, "t"), true, CancellationToken.None);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT602");
    }

    [Fact]
    public async Task StopsReadingOversizedDownloadsWhileStreaming()
    {
        var oversized = new byte[ApprovedReleaseDownloadService.MaxDownloadBytes + 1];
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream(oversized)) });

        var result = await new ApprovedReleaseDownloadService(client).DownloadAndActivateAsync(CreateApprovedRelease(), Path.Combine(tempRoot, "t"), true, CancellationToken.None);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT610");
    }

    [Fact]
    public async Task RejectsTraversalIntoASiblingFolderWithTheSamePrefix()
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "release-root/LICENSE", "MIT License");
            WriteEntry(archive, "../expanded-evil/owned.txt", "nope");
        }

        using var client = CreateClient(_ => CreateZipResponse(memory.ToArray()));

        var result = await new ApprovedReleaseDownloadService(client).DownloadAndActivateAsync(CreateApprovedRelease(), Path.Combine(tempRoot, "t"), true, CancellationToken.None);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT606");
    }

    [Fact]
    public async Task KeepsTheActiveReleaseWhenItCannotBeMovedAside()
    {
        var target = Path.Combine(tempRoot, "active-catalog");
        Directory.CreateDirectory(target);
        var sentinel = Path.Combine(target, "sentinel.txt");
        await File.WriteAllTextAsync(sentinel, "keep");
        using var client = CreateClient(_ => CreateZipResponse(CreateValidArchive()));

        DownloadActivationResult result;
        using (new FileStream(sentinel, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            result = await new ApprovedReleaseDownloadService(client).DownloadAndActivateAsync(CreateApprovedRelease(), target, true, CancellationToken.None);
        }

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT611");
        Assert.Equal("keep", await File.ReadAllTextAsync(sentinel));
    }
```

Rename the existing `PreservesExistingActiveDirectoryWhenActivationFails` to `PreservesExistingActiveDirectoryWhenLicenseIsMissing` (it fails on the missing license, not on activation).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~ApprovedReleaseDownloadServiceTests"`
Expected: the four new tests FAIL. `KeepsTheActiveReleaseWhenItCannotBeMovedAside` fails because the catch block deletes the target.

- [ ] **Step 3: Implement the fixes**

1. In `IsApprovedRelease` add scheme checks as the first two conditions:

```csharp
        return Uri.TryCreate(release.DetailsUrl, UriKind.Absolute, out var detailsUri)
            && Uri.TryCreate(release.ZipballUrl, UriKind.Absolute, out var zipballUri)
            && detailsUri.Scheme == Uri.UriSchemeHttps
            && zipballUri.Scheme == Uri.UriSchemeHttps
            && string.Equals(release.SourceRepositoryUrl, ApprovedReleaseDiscoveryService.ApprovedRepositoryUrl, StringComparison.OrdinalIgnoreCase)
            && string.Equals(detailsUri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            && detailsUri.AbsolutePath.StartsWith("/obra/superpowers/releases/", StringComparison.OrdinalIgnoreCase)
            && string.Equals(zipballUri.Host, "api.github.com", StringComparison.OrdinalIgnoreCase)
            && zipballUri.AbsolutePath.StartsWith("/repos/obra/superpowers/zipball/", StringComparison.OrdinalIgnoreCase);
```

2. Stage next to the target (same volume, so `Directory.Move` works). Replace the `stagingRoot` line in `DownloadAndActivateAsync` with:

```csharp
        var stagingRoot = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(targetDirectory))!, ".staging", Guid.NewGuid().ToString("N"));
```

3. In `DownloadArchiveAsync` replace the body of `using (response)` after the status check with a bounded copy:

```csharp
            if (response.Content.Headers.ContentLength > MaxDownloadBytes)
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT610", $"Approved release download exceeds the supported size of {MaxDownloadBytes} bytes."));
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var memory = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (memory.Length + read > MaxDownloadBytes)
                {
                    diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT610", $"Approved release download exceeds the supported size of {MaxDownloadBytes} bytes."));
                    return null;
                }

                memory.Write(buffer, 0, read);
            }

            return memory.ToArray();
```

4. Replace `ExtractArchiveSafely` with:

```csharp
    private static void ExtractArchiveSafely(byte[] archiveBytes, string destinationRoot, List<ParseDiagnostic> diagnostics)
    {
        using var archiveStream = new MemoryStream(archiveBytes, writable: false);
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false);
        var root = Path.GetFullPath(destinationRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (archive.Entries.Count > MaxArchiveEntries || archive.Entries.Sum(entry => entry.Length) > MaxExtractedBytes)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT612", $"Downloaded release archive is too large when extracted (limit {MaxArchiveEntries} entries, {MaxExtractedBytes} bytes)."));
            return;
        }

        foreach (var entry in archive.Entries)
        {
            var destinationPath = Path.GetFullPath(Path.Combine(root, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
            if (!destinationPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT606", $"Downloaded release archive contains an unsafe path '{entry.FullName}'."));
                return;
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            using var source = entry.Open();
            using var destination = File.Create(destinationPath);
            source.CopyTo(destination);
        }
    }
```

and add the two constants under `MaxDownloadBytes`:

```csharp
    public const int MaxArchiveEntries = 10_000;
    public const long MaxExtractedBytes = 100L * 1024 * 1024;
```

5. Replace `ActivateAtomically` with:

```csharp
    private static string ActivateAtomically(string stagingRoot, string targetDirectory, List<ParseDiagnostic> diagnostics)
    {
        var targetFullPath = Path.GetFullPath(targetDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(targetFullPath)!);
        var backupPath = targetFullPath + ".backup-" + Guid.NewGuid().ToString("N");
        var movedOriginal = false;

        try
        {
            if (Directory.Exists(targetFullPath))
            {
                Directory.Move(targetFullPath, backupPath);
                movedOriginal = true;
            }

            Directory.Move(stagingRoot, targetFullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (movedOriginal && !Directory.Exists(targetFullPath))
            {
                Directory.Move(backupPath, targetFullPath);
            }

            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT611", $"Approved release activation failed and the previous selection was preserved: {exception.Message}"));
            return string.Empty;
        }

        try
        {
            if (Directory.Exists(backupPath))
            {
                Directory.Delete(backupPath, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Warning, "SPCAT613", $"The previous release could not be cleaned up: {exception.Message}"));
        }

        return targetFullPath;
    }
```

6. In `DownloadAndActivateAsync` the `finally` block deletes `stagingRoot` when activation did not happen. The staging folder now sits under `<target parent>\.staging\`. Keep that behavior; after it, also delete the empty `.staging` parent if possible:

```csharp
            var stagingParent = Path.GetDirectoryName(stagingRoot)!;
            if (Directory.Exists(stagingParent) && !Directory.EnumerateFileSystemEntries(stagingParent).Any())
            {
                Directory.Delete(stagingParent);
            }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~ApprovedReleaseDownloadServiceTests"`
Expected: PASS, all tests including the existing ones.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "fix: require HTTPS, bound downloads and extraction, and never delete the active release on failed activation"
```

---

### Task 17: Replace the cache manager with a downloads folder

**Files:**
- Create: `TheKameleon.Superpowers.Skills/Catalog/DownloadedReleases.cs`
- Delete: `TheKameleon.Superpowers.Skills/Catalog/CatalogCacheManager.cs`, `TheKameleon.Superpowers.Tests/CatalogCacheManagerTests.cs`, `TheKameleon.Superpowers.Core/Contracts/Catalog/CachedCatalogRelease.cs`, `CatalogCacheState.cs`, `CatalogRollbackResult.cs`, `CatalogReloadState.cs`, `ActiveRunPin.cs`
- Modify: `ApprovedReleaseDownloadService.cs` (`WriteCatalogAsync` records prerelease and publish date)
- Test: `TheKameleon.Superpowers.Tests/DownloadedReleasesTests.cs`

**Interfaces:**
- Consumes: `ProfilePaths.DownloadsRoot` (Task 6), `BundledCatalogLoader`.
- Produces: `public sealed record AvailableRelease(string CatalogRoot, LoadedCatalogRelease Release, string Source)`.
- Produces: `static string DownloadedReleases.TargetDirectory(ProfilePaths paths, string releaseTag)` (throws `ArgumentException` for unsafe tags).
- Produces: `static IReadOnlyList<AvailableRelease> DownloadedReleases.Load(ProfilePaths paths)`.

- [ ] **Step 1: Write the failing tests**

Create `TheKameleon.Superpowers.Tests/DownloadedReleasesTests.cs`:

```csharp
using TheKameleon.Superpowers.Skills.Catalog;

namespace TheKameleon.Superpowers.Tests;

public sealed class DownloadedReleasesTests : IDisposable
{
    private readonly TempProfile profile = new();

    [Theory]
    [InlineData("../evil")]
    [InlineData("v1/../../x")]
    [InlineData("")]
    public void RejectsUnsafeTags(string tag)
    {
        Assert.Throws<ArgumentException>(() => DownloadedReleases.TargetDirectory(profile.Paths, tag));
    }

    [Fact]
    public void TargetIsUnderTheDownloadsRoot()
    {
        Assert.Equal(Path.Combine(profile.Paths.DownloadsRoot, "v6.5.0-beta.1"), DownloadedReleases.TargetDirectory(profile.Paths, "v6.5.0-beta.1"));
    }

    [Fact]
    public void LoadsEachDownloadedCatalogRoot()
    {
        var target = DownloadedReleases.TargetDirectory(profile.Paths, "v6.4.1");
        CopyDirectory(TestSupport.BundledCatalogRoot, target);

        var available = DownloadedReleases.Load(profile.Paths);

        Assert.NotEmpty(available);
        Assert.All(available, release => Assert.Equal("download", release.Source));
        Assert.Contains(available, release => release.Release.ReleaseTag == "v6.4.1" && release.CatalogRoot == target);
    }

    [Fact]
    public void NoDownloadsYieldsEmpty()
    {
        Assert.Empty(DownloadedReleases.Load(profile.Paths));
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    public void Dispose() => profile.Dispose();
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj --filter "FullyQualifiedName~DownloadedReleasesTests"`
Expected: build FAIL — `DownloadedReleases` does not exist.

- [ ] **Step 3: Implement and delete the old cache code**

Create `TheKameleon.Superpowers.Skills/Catalog/DownloadedReleases.cs`:

```csharp
using System.Text.RegularExpressions;
using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Skills.Catalog;

public sealed record AvailableRelease(string CatalogRoot, LoadedCatalogRelease Release, string Source);

/// <summary>Each downloaded release is its own catalog root under %LOCALAPPDATA%\TheKameleon.Superpowers\downloads\&lt;tag&gt;.</summary>
public static class DownloadedReleases
{
    private static readonly Regex SafeTag = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.CultureInvariant);

    public static string TargetDirectory(ProfilePaths paths, string releaseTag)
    {
        if (string.IsNullOrEmpty(releaseTag) || !SafeTag.IsMatch(releaseTag) || releaseTag.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Release tag '{releaseTag}' is not a safe folder name.", nameof(releaseTag));
        }

        return Path.Combine(paths.DownloadsRoot, releaseTag);
    }

    public static IReadOnlyList<AvailableRelease> Load(ProfilePaths paths)
    {
        if (!Directory.Exists(paths.DownloadsRoot))
        {
            return Array.Empty<AvailableRelease>();
        }

        return Directory.EnumerateDirectories(paths.DownloadsRoot)
            .Where(directory => !Path.GetFileName(directory).StartsWith('.'))
            .SelectMany(directory => BundledCatalogLoader.LoadFromDirectory(directory).Releases
                .Where(release => !release.HasErrors)
                .Select(release => new AvailableRelease(directory, release, "download")))
            .ToArray();
    }
}
```

Delete the old cache code:

```bash
git rm -q TheKameleon.Superpowers.Skills/Catalog/CatalogCacheManager.cs TheKameleon.Superpowers.Tests/CatalogCacheManagerTests.cs
cd TheKameleon.Superpowers.Core/Contracts/Catalog && git rm -q CachedCatalogRelease.cs CatalogCacheState.cs CatalogRollbackResult.cs CatalogReloadState.cs ActiveRunPin.cs && cd ../../..
```

In `ApprovedReleaseDownloadService.WriteCatalogAsync`, add two properties to the anonymous release object, after `resolvedCommit`:

```csharp
                    prerelease = release.IsPrerelease,
                    publishedAtUtc = release.PublishedAtUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
```

and add to `ApprovedReleaseDownloadServiceTests.DownloadsAndActivatesApprovedRelease`, after the existing asserts:

```csharp
        var loaded = Assert.Single(result.Validation.Releases);
        Assert.Equal(release.IsPrerelease, loaded.IsPrerelease);
        Assert.Equal(release.PublishedAtUtc.ToUnixTimeSeconds(), loaded.PublishedAtUtc!.Value.ToUnixTimeSeconds());
```

- [ ] **Step 4: Build and run tests**

Run: `dotnet build TheKameleon.Superpowers.slnx`
Expected: succeeded. If a kept file still references a deleted contract, stop and report it.

Run: `dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor: replace catalog cache manager with per-release download folders"
```

---

### Task 18: "Check for newer releases" in the tool window

**Files:**
- Modify: `TheKameleon.Superpowers.Vsix/SuperpowersViewModel.cs`, `SuperpowersToolWindowControl.xaml`
- Test: `TheKameleon.Superpowers.IntegrationTests/ToolWindowPackageTests.cs`

**Interfaces:** Consumes `ApprovedReleaseDiscoveryService.DiscoverAsync(ReleaseChannelFilter, CancellationToken)`, `ApprovedReleaseDownloadService.DownloadAndActivateAsync(...)`, `DownloadedReleases` (Task 17), `AvailableRelease`. Produces `[DataMember] IAsyncCommand CheckForUpdatesCommand` and `[DataMember] bool IncludePrereleases`.

- [ ] **Step 1: Write the failing package test**

In `ToolWindowPackageTests.PackageEmbedsInstallerViewWithAccessibleCommands` add `"CheckForUpdatesCommand"` to the command array, and add to the property array in `ViewModelDeclaresRemoteUiSerializationAttributes`: `"IncludePrereleases"`.

Run: `dotnet test TheKameleon.Superpowers.IntegrationTests/TheKameleon.Superpowers.IntegrationTests.csproj --filter "FullyQualifiedName~ToolWindowPackageTests"`
Expected: FAIL — no such command or property.

- [ ] **Step 2: Track each release's catalog root in the view model**

In `SuperpowersViewModel`:

1. Replace the field `private IReadOnlyList<LoadedCatalogRelease> releases` with `private IReadOnlyList<AvailableRelease> releases = Array.Empty<AvailableRelease>();` and add `using System.Net.Http;` and `using TheKameleon.Superpowers.Core.Contracts.Settings;`.
2. Add fields and members:

```csharp
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
        private bool includePrereleases;

        [DataMember]
        public bool IncludePrereleases
        {
            get => this.includePrereleases;
            set => this.SetProperty(ref this.includePrereleases, value);
        }

        [DataMember]
        public IAsyncCommand CheckForUpdatesCommand { get; }
```

and in the constructor:

```csharp
            this.CheckForUpdatesCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.CheckForUpdatesAsync, cancellationToken));
```

3. Replace the catalog-loading lines at the start of `LoadAsync` with:

```csharp
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
```

(the rest of `LoadAsync` is unchanged).

4. Update `InstallAsync` and `RepairAsync` to use the `AvailableRelease` (`selected.CatalogRoot`, `selected.Release`, `selected.Source`):

```csharp
            var selected = this.SelectedRelease();
            if (selected is null)
            {
                this.StatusText = "Choose a release first.";
                return;
            }

            var release = selected.Release;
            var source = await Task.Run(() => ReleaseSkillLoader.Load(selected.CatalogRoot, release), cancellationToken).ConfigureAwait(false);
```

and pass `selected.Source` instead of `"bundled"` to `new InstalledRelease(...)`. In `RepairAsync` look up `this.releases.FirstOrDefault(candidate => candidate.Release.ReleaseTag == installedTag) ?? this.SelectedRelease()` and do the same.

5. Replace `SelectedRelease()` and `Label(...)`:

```csharp
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
```

6. Add the operation:

```csharp
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
```

- [ ] **Step 3: Add the controls to the view**

In `SuperpowersToolWindowControl.xaml`, directly after the release `Grid`, add:

```xml
                <StackPanel Orientation="Horizontal" Margin="0,6,0,0">
                    <Button Command="{Binding CheckForUpdatesCommand}" Content="Check for newer releases" Padding="12,2" Margin="0,0,8,0" AutomationProperties.Name="Check GitHub for newer Superpowers releases" />
                    <CheckBox IsChecked="{Binding IncludePrereleases, Mode=TwoWay}" Content="Include prereleases" VerticalAlignment="Center" AutomationProperties.Name="Include prerelease versions" />
                </StackPanel>
```

- [ ] **Step 4: Run all tests**

Run: `dotnet build TheKameleon.Superpowers.slnx`, then both `dotnet test` commands.
Expected: all PASS.

- [ ] **Step 5: Manual check in VS 2026**

In the Exp instance: select **Check for newer releases**. With no newer upstream release, expect "You already have every published release." With **Include prereleases** checked, any newer prerelease should offer a download prompt; **Cancel** must change nothing. Add a row U4 for this to `docs/superpowers/acceptance/agent-skills-acceptance.md` and record the result.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: check GitHub for newer Superpowers releases and download them on request"
```

- [ ] **Step 7: Phase gate**

Run both test suites and acceptance rows U1–U4 again. Everything must pass before release work (Marketplace listing, signing) begins; release work is out of scope for this plan.
