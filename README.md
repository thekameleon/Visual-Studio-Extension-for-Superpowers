# Superpowers for Visual Studio

A Visual Studio extension shell intended to adapt upstream Superpowers workflows.

## Implementation plan and progress

The [complete implementation plan](docs/superpowers/plans/implementation-plan.md)
is the canonical roadmap and checklist for this project. It records completed work,
unverified runtime behavior, confirmed product decisions, all eight skills, the three
execution modes, phase dependencies, tests, security requirements, and release gates.

The user approved upstream reuse rather than an independent skill framework, with
a bundled stable/prerelease catalog and optional future downloads. Detailed design
decisions and foundation/runtime capability gates remain outstanding. Future phases
are not implemented features.

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
log for further diagnosis. The versioned update still needs this runtime verification.

Update: the VS 2026 experimental instance now resolves **TheKameleon Superpowers** and
**Plan** correctly on version `1.0.1.0`, and the **Superpowers** window opens, reuses a
single tab on repeat invocation, and reopens after closing. Equivalent VS 2022 host
verification is still pending.

See [the replacement specification](docs/superpowers/specs/modern-extension-project.md)
for scope, security boundaries, and acceptance criteria.
