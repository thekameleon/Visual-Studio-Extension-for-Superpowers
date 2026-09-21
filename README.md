# Superpowers for Visual Studio

A Visual Studio extension shell intended to adapt upstream Superpowers workflows.

## Implementation plan and progress

The [complete implementation plan](docs/superpowers/plans/implementation-plan.md)
is the canonical roadmap and checklist for this project. It records completed work,
unverified runtime behavior, confirmed product decisions, all eight skills, the three
execution modes, phase dependencies, tests, security requirements, and release gates.

The user approved upstream reuse rather than an independent skill framework, with
a bundled stable/prerelease catalog and optional future downloads. Guided is the
default mode, metadata-only retention is the default, upstream `SKILL.md` files stay
intact, and the preferred five-project split keeps most future logic outside the VSIX.
Foundation/runtime capability gates still remain outstanding. Future phases are not
implemented features.

## Relationship to upstream Superpowers

The intended skill source is [obra/superpowers](https://github.com/obra/superpowers).
[earchibald/vsc-superpowers](https://github.com/earchibald/vsc-superpowers) is a useful
VS Code/Copilot CLI adaptation reference, not a Visual Studio SDK integration.

The [pinned source and license review](docs/superpowers/specs/upstream-superpowers-review.md)
maps the eight requested Visual Studio actions to canonical upstream skills. It
proposes preserving upstream Markdown/YAML and supporting assets, with separate
Visual Studio metadata for commands, capabilities, context and policy. Refactor is
an explicitly labeled composition, not a dedicated skill in the reviewed upstream.

**The current VSIX does not yet bundle, load or run Superpowers skills.** It is only
the command/window shell described below. The proposed adapter will own IDE context,
UI, permissions, evidence tracking and supported Copilot handoff—not reimplement the
upstream methodology. Copilot CLI support does not prove Visual Studio API support;
preview/copy/manual handoff remains the approved fallback. No upstream installer or
hook has been run, and no automatic cache update or instruction-file rewrite is
authorized by this design.

Most future implementation is intended to live in `TheKameleon.Superpowers.Core` and
`TheKameleon.Superpowers.Skills`, not in the VSIX project. The VSIX should remain a
thin Visual Studio shell for commands, tool windows, Remote UI and host-specific
adapters that cannot be portable.

### Approved skill version distribution (not implemented yet)

- Each VSIX release will bundle all existing upstream releases at its catalog cutoff,
  including clearly labeled stable releases and prereleases.
- Users can select a bundled version offline or explicitly download the latest or
  another available release without waiting for a new VSIX.
- Downloads will be validated and cached locally with previous versions retained for
  rollback. Normal skill use reads local content, not GitHub on every invocation.
- Latest resolves to an exact confirmed version. Active runs remain pinned, and VSIX
  upgrades preserve the user's selection while refreshing the bundled catalog.
- Tested, untested and incompatible versions are distinguished; bundling a version
  does not promise compatibility. Invalid downloads never replace a working selection.

Actual upstream release/tag inventory, package size, per-release licenses and update
settings still require validation/design. This supersedes the single-version bundle
and VSIX-only skill update proposal; no skills are bundled in the current scaffold.

## Extension model

`TheKameleon.Superpowers.Vsix` uses the modern, out-of-process
`Microsoft.VisualStudio.Extensibility` SDK, not the traditional in-process VSSDK.
The extension entry point is `SuperpowersExtension.cs`. Its configuration supplies
the package metadata; the build generates the VSIX manifest and service registration.
Do not add a legacy `source.extension.vsixmanifest`.

The extension provides **Extensions > TheKameleon Superpowers > Plan**, which opens
a **Superpowers** tool window using the modern SDK's Remote UI. The window currently
displays a status message only; planning workflows, skills, context collection, and
Copilot integration are not implemented.

## Runtime and tooling

- Language: C# 12 throughout; there are no Visual Basic projects.
- Extension: `net8.0-windows8.0`, Extensibility SDK/Build `17.14.40608`.
- Libraries and unit tests: `net8.0`.
- Package integration tests: `net8.0-windows8.0`.
- Build environment: Windows, Visual Studio with .NET development tooling, and a
	.NET 8 or later SDK. The extension and tests use the .NET 8 runtime.

The extension target follows Microsoft's current
[out-of-process sample](https://github.com/microsoft/VSExtensibility/tree/main/New_Extensibility_Model/Samples/SimpleRemoteCommandSample).
All projects now target .NET 8, so the extension can reference the portable libraries
when needed without the previous target-framework mismatch. Those references are
not added by this scaffold. Runtime alignment does not guarantee support for every
Visual Studio or Copilot API.

The generated manifest declares Visual Studio 17.14 or newer, for amd64 and arm64.
These declarations are tested; installation and activation on each supported IDE
version and architecture still require manual validation. The SDK currently marks
the generated package as a preview.

## Build and test

Open `TheKameleon.Superpowers.slnx` in Visual Studio and build the solution. Run
`ExtensionPackageTests` and `PlanToolWindowPackageTests` in Test Explorer to validate
the generated package and its command/window contributions.

The `.slnx` solution format requires a supporting Visual Studio/MSBuild version, or
.NET SDK 9.0.200 or later for `dotnet build`. This is a build-tool requirement, not
a change to the .NET 8 target/runtime. With only the .NET 8 SDK, build individual
`.csproj` files instead.

From a terminal at the repository root with a `.slnx`-capable SDK:

```powershell
dotnet build TheKameleon.Superpowers.slnx
dotnet test TheKameleon.Superpowers.IntegrationTests/TheKameleon.Superpowers.IntegrationTests.csproj
```

The integration test project builds the extension and copies its VSIX into the test
output. Its thirteen tests check identity, modern host/runtime registration, both
declared architectures, the entry-point assembly, absence of legacy registrations,
Plan menu placement, the tool-window provider, the embedded Remote UI status view,
and matching manifest/assembly versions newer than the original installation.
The separate unit test project still contains its original empty placeholder test.

The Debug package is generated at:

`TheKameleon.Superpowers.Vsix/bin/Debug/net8.0-windows8.0/TheKameleon.Superpowers.Vsix.vsix`

## Prepare the bridge for debugging

The in-process bridge has its own VSIX and is a build-only dependency of the modern
project. The solution enables native **Deploy** for the bridge in Debug, alongside
the modern VSIX. F5 uses Visual Studio's VSIX deployment provider for both packages;
ordinary builds only produce packages. This replaces the ineffective
`DeploymentAssetsOutputGroup` hook, which was not called by the actual F5 path.

### Native F5 deployment

1. Set `TheKameleon.Superpowers.Vsix` as the startup project in Debug configuration.
2. In its debug properties, choose the specific Visual Studio installation instead
   of leaving the target at default. The selection must populate
   `DeployTargetInstanceId` in the local `.csproj.user` file. On the current machine,
   VS 2026 is `c9c360fa` and VS 2022 is `dcdb5f81`; these IDs are machine-specific.
3. Use the **Exp** profile and close the previous experimental IDE before F5.
4. Reload the solution once after this deployment-configuration change. In
   Configuration Manager, Debug should have **Deploy** checked for both
   `TheKameleon.Superpowers.InProcess` and `TheKameleon.Superpowers.Vsix`.
5. Press F5. Build/Deploy Output must show deployment of **both** projects to the
   selected IDE. The bridge reads the modern project's local `.csproj.user` target
   selection; it does not require a separate instance choice or PowerShell command.
6. In a C# editor, check **Superpowers: Probe Document Compiler Diagnostics**.
	  Invoke it and compare **Loaded build** with the version in Build Output;
   **Loaded DLL** must identify the active deployment, not an old backup.
   Close Exp and repeat F5 with no source changes. Both packages should receive
   the same newer build revision and the diagnostics command should remain present.

`build/BridgeDebugDeployment.targets` now configures native deployment and validates
the selected instance and both root-suffix settings. Blank instance IDs or non-Exp
settings disable bridge debug deployment and fail IDE Debug build validation.
`DeployExtension` remains false, so ordinary builds/tests do not install anything.
The build-only reference does not bundle bridge files in the modern VSIX or attach
the debugger to devenv. The old preparation marker and hook-specific properties
are obsolete. Earlier VS 2026 native deployment was user-confirmed. VS 2022
stale-copy cleanup and the new version display require an F5 runtime retest;
a successful build/configuration check is not runtime proof.

### Automatic build identity

Each real bridge build allocates a shared numeric version, starting at `1.0.1.1`.
The dependent modern extension consumes that same version. Both VSIX identities,
DLL file versions and informational versions match. Assembly binding versions
remain fixed (`1.0.0.0` for the bridge, `1.0.1.0` for the modern extension).
Bridge dialogs and modern probe results display the actual loaded DLL's file
version and full path; Manage Extensions shows the installed package version.

`build/SuperpowersBuildVersion.targets` maintains an exclusively locked, persistent
local counter in `.superpowers-build/revision.txt`, excluded from Git. Clean does
not reset it; do not delete it to restart numbering. Revision rollover advances
the third version component while keeping all components within assembly limits.
Failed build attempts can leave gaps. The counter is local to this checkout, not
a cross-machine release-number service.

IDE design-time builds do not allocate a revision, and their fallback metadata
constants are separate from real-build constants. Fast up-to-date skipping is
disabled for the two extension projects so F5/builds without source changes still
produce an identifiable new build. Builds/tests still do not install extensions;
native F5 deploys both packages. The bridge dependency must be built, not bypassed,
when producing a matching modern package.

### Manual fallback / External Tools

Use the explicit preparation command only as a fallback if native deployment fails:

1. Stop the debug session and close the target **experimental** IDE. Keep the main
   development IDE open. Save any unsaved work first.
2. From the repository root, preview the exact target:
   `pwsh -NoProfile -File .\scripts\Prepare-BridgeDebug.ps1 -VisualStudioVersion 2026 -WhatIf`
3. Run without `-WhatIf` to build and deploy the bridge to **that installation's Exp
   profile only**. Use `-VisualStudioVersion 2022` for VS 2022 instead. If multiple
   matching installations exist, pass the reported `-InstanceId` explicitly.
4. Start F5 with `TheKameleon.Superpowers.Vsix` as the startup project, selecting
   the **same IDE installation** and **Exp** profile in its debug settings.
5. In that experimental IDE, open a C# file and check the editor's
   **Superpowers: Probe Editor Context** command. The preparation command's success
   is deployment evidence, not proof of menu activation or semantic results.

For a direct IDE menu command, add **Tools > External Tools > Add**:

- Title: `Prepare Superpowers Bridge (2026 Exp)`
- Command: the full path to `pwsh.exe` on your machine
- Arguments: `-NoProfile -File "$(SolutionDir)scripts\Prepare-BridgeDebug.ps1" -VisualStudioVersion 2026`
- Initial directory: `$(SolutionDir)`
- Enable **Use Output window**; create a second entry using `2022` if needed.

The script uses the selected IDE's MSBuild and the public VSSDK deployment target,
which refreshes the existing extension deployment by identity. No manual version
bump or uninstall is part of this workflow. It refuses a running target Exp process
or ambiguous target and stops on build/deployment failure. It does not terminate
processes, delete caches, elevate, launch an IDE, or install into the normal profile.
Ordinary builds and tests default to no bridge deployment. Use this preparation
only when the normal native F5 deployment fails, not before every F5. A loaded
net472 bridge requires closing/relaunching Exp to use new binaries.

The manual command remains available independently of the deployment hook. Debugger
attachment to in-process bridge code is separate from the modern extension debugger.
Combined Marketplace packaging and remaining host verification are open in the plan.

## Manual installation and activation check

Use an experimental Visual Studio instance rather than the normal development instance.
Close that instance before installing the rebuilt package. With the matching Visual
Studio installation's `VSIXInstaller.exe`, install using `/rootSuffix:Exp`, then launch
`devenv.exe /RootSuffix Exp`. If the installer rejects the unchanged version as already
installed, remove the previous copy from the experimental instance before reinstalling.

1. Confirm **Superpowers for Visual Studio** appears in the installed extensions list
   with version **1.0.1.0** (project version `1.0.1`).
2. With no solution open, select **Extensions > TheKameleon Superpowers > Plan**.
3. Confirm a **Superpowers** tab opens in the document area and displays:
   **Superpowers is active. Plan workflow is not implemented yet.**
4. Invoke Plan again and confirm it activates the same window rather than a duplicate.
5. Close the window and invoke Plan again to confirm it reopens correctly.
6. Repeat with a solution open, resize the window, and check readability in the
   Visual Studio themes you use, including high contrast when applicable.

The command only opens the window. It does not read code, generate a plan, execute
repository instructions, or contact Copilot. Package tests verify registration and
embedded content but do not prove runtime activation or rendering. The checks above
remain manual and are not implied by a successful build or test run.

### Percent-delimited menu labels

If the menu shows `%Superpowers.Menu.DisplayName%` or `%Superpowers.PlanCommand.DisplayName%`,
Visual Studio has not resolved the localization key. In the reported experimental
instance, the installed neutral resource file was correct, but the metadata cache
contained Superpowers command metadata without its resource dictionary.

The package version is now `1.0.1` to provide a distinct update to the original
`1.0.0.0` installation, which predated the localization resources. Close the experimental
instance, install the rebuilt VSIX as an update, reopen it, and verify the installed
version and both labels. Do not copy new files over an installed same-version package
or delete IDE caches as a first step. If the labels remain unresolved on `1.0.1.0`,
launch the experimental instance with `/Log` and capture the exact labels and activity
log for further diagnosis. Recheck labels when testing another package or host.

Update: the VS 2026 experimental instance now resolves **TheKameleon Superpowers** and
**Plan** correctly on version `1.0.1.0`, and the **Superpowers** window opens, reuses a
single tab on repeat invocation, and reopens after closing. On VS 2022 Community
17.14.41 (amd64), the user confirmed F5 deployment opens the Plan window, labels
resolve without placeholders, repeat invocation reuses one window, close/reopen
works, and Plan works with no solution open. This is user-confirmed runtime evidence;
the exact F5-deployed package version was not independently inspected.

P00.05 baseline recording is complete. Standalone VSIX installation into VS2022's
experimental instance remains unresolved: two logged attempts returned exit code 1
with `RequiresInstallerException` and a multiple-target error. F5 deployment does
not resolve that distribution issue; it remains tracked for installation/release
validation in the implementation plan.

See [the replacement specification](docs/superpowers/specs/modern-extension-project.md)
for scope, security boundaries, and acceptance criteria.
