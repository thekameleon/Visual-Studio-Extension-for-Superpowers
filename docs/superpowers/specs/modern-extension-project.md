# Modern extension project replacement

## Scope

Replace the empty traditional VSSDK scaffold with a modern out-of-process
VisualStudio.Extensibility extension. Preserve the project path, solution membership,
and extension identity. Per the user's follow-up decision, use C# and .NET 8 across
all seven projects. Add a minimal Plan command and Remote UI window to validate
activation. Do not implement planning workflows or change library behavior.

## Relationship to the planned upstream adapter

This specification records the implemented foundation only. The
[upstream reuse review](upstream-superpowers-review.md) and
[revised roadmap](../plans/implementation-plan.md) describe adapting canonical
Superpowers skills rather than building an independent skill framework. No upstream
skill bundle, loader, context capture or Copilot handoff is implemented by this shell.
The user approved upstream reuse with all stable/prerelease releases bundled per VSIX
catalog cutoff and optional user-selected future downloads. Detailed adapter metadata,
Refactor composition and runtime/capability gates remain outstanding. This approval
does not authorize running upstream installers, rewriting user instructions or adding
unsupported IDE integrations.

When later phases add Copilot integration, GitHub Copilot Chat should become the
preferred conversation surface where a supported Visual Studio/Copilot API can perform
truthful handoff. The extension shell should remain the orchestration, capability,
status and fallback layer rather than becoming a separate replacement chat surface.

The user also approved Guided as the default mode, metadata-only retention by default,
intact upstream `SKILL.md` layering, and a five-project architecture where most future
logic lives in Core/Skills rather than in the VSIX. This specification still records
the implemented shell only; it does not imply those future layers exist yet.

## Configuration

- Project: `TheKameleon.Superpowers.Vsix/TheKameleon.Superpowers.Vsix.csproj`.
- Framework: `net8.0-windows8.0`.
- Language: C# 12; no Visual Basic projects.
- SDK and Build packages: `Microsoft.VisualStudio.Extensibility.Sdk` and
  `Microsoft.VisualStudio.Extensibility.Build`, both `17.14.40608`, with private assets.
- Entry point: `TheKameleon.Superpowers.Vsix.SuperpowersExtension`, attributed with
  `VisualStudioContribution` and explicitly configured not to require in-process hosting.
- Identity: `TheKameleon.Superpowers.Vsix.8a7fab37-7cfc-4314-ae3c-946698f3ff5d`.
- Publisher: `TheKameleon`.
- Display name: `Superpowers for Visual Studio`.
- Version: derived from the extension assembly version.
- Packaging: SDK-generated manifest and `.vsextension/extension.json`; no hand-authored
  legacy manifest, VSSDK package registration, or MEF component asset.

This matches the target framework and package versions in Microsoft's
[SimpleRemoteCommandSample](https://github.com/microsoft/VSExtensibility/tree/main/New_Extensibility_Model/Samples/SimpleRemoteCommandSample).
The generated manifest declares Visual Studio 17.14 or newer and amd64/arm64 support;
these declarations are not evidence of runtime testing on all those configurations.

## Runtime compatibility

Core, Skills and unit tests target `net8.0`. The extension can reference these
portable libraries without a target-framework mismatch, but this scaffold does not
add those references. Matching runtime targets does not establish compatibility with
every IDE service or Copilot integration API.

Integration tests target `net8.0-windows8.0` to match their Windows-only project
build dependency. They inspect the generated VSIX rather than loading the extension
assembly into the test runner. Their package-copy path intentionally follows the
extension's current target framework and must change if that framework changes.

Individual projects use .NET 8-compatible C# 12. Command-line builds of the `.slnx`
solution require .NET SDK 9.0.200 or later; a newer build SDK does not change the
.NET 8 target/runtime. Visual Studio 2026 supports the solution format.

## Plan command and tool window

- Menu path: **Extensions > Superpowers > Plan**.
- Command and submenu labels reference `.vsextension/string-resources.json` using
  SDK localization tokens; package tests verify both the tokens and English labels.
- `PlanCommand` currently forwards cancellation and asynchronously calls the SDK's
  `ShowToolWindowAsync<SuperpowersToolWindow>` with activation enabled. In later
  phases this command should prefer a capability-checked Copilot Chat handoff path and
  open the tool window as a fallback/status surface when direct supported handoff is
  unavailable.
- `SuperpowersToolWindow` has the title **Superpowers** and defaults to the document
  well. Repeated invocation should activate the same logical window, not add instances.
  This window is currently the only visible UX surface; in later phases it should shift
  to workflow status, approvals, prompt preview/edit/copy fallback, handoff
  diagnostics and result-import responsibilities rather than replacing Copilot Chat.
- The window reuses its Remote UI control and disposes it with the tool window.
- `SuperpowersToolWindowControl.xaml` is an embedded DataTemplate, not an in-process
  WPF window. It uses host theme colors, wrapping text, and vertical scrolling.
- The status text is **Superpowers is active. Plan workflow is not implemented yet.**
- The command requires no solution or active editor. It does not collect context,
  load skills, produce prompts, or invoke Copilot.

The generated package includes a command-set service and a tool-window provider.
Both must use the out-of-process host; adding the tool window does not relax that
boundary or introduce a traditional VSSDK component.

## Localization update and installation identity

The original `1.0.0.0` package was installed before localization resources were added.
Investigation of the reported instance found the correct neutral resource file on
disk, but command metadata without a corresponding Superpowers resource dictionary
in the IDE metadata cache. The keys and resource location match the documented SDK
contract and the installed host's lookup rules.

Set project version `1.0.1`, producing matching assembly and manifest versions
`1.0.1.0`, to provide a distinct versioned update while retaining the extension ID.
Do not suppress localization warnings, hard-code metadata labels, or modify IDE caches.
This addresses same-version registration reuse; successful label resolution after
installing the update must still be checked in the experimental instance.

## Automated acceptance criteria

1. The full solution builds and produces a modern VSIX.
2. The generated manifest preserves the identity and current publisher/display name.
3. Installation metadata selects `VisualStudio.Extensibility` and `net8.0`.
4. Both declared architectures have the SDK's Visual Studio minimum version.
5. The service registration selects `dotnetExtensibility`, prohibits in-process
   hosting, and references the packaged, nonempty extension assembly.
6. Traditional VsPackage/MEF assets and the legacy .NET Framework dependency are absent.
7. Missing manifest, registration, or assembly payload causes an integration-test failure.
8. All seven C# projects compile for .NET 8 and both test projects run on .NET 8.
9. Exactly one Plan command is registered under the Superpowers submenu in Extensions.
10. The Superpowers window registration references an out-of-process provider.
11. The packaged assembly embeds the correctly named Remote UI DataTemplate containing
	the status message; missing or malformed view resources fail the packaging test.

12. The localized update has a version greater than the original `1.0.0.0` install,
	and its assembly and VSIX manifest versions match.

The thirteen packaging tests use the existing xUnit framework and integration-test
namespace. They do not invoke Visual Studio or prove window activation, cancellation
behavior, repeat invocation, or rendering; those still require host-level validation.
No new assertion or mocking libraries are needed for these package checks.

## Security and execution boundaries

The Plan command only asks the Visual Studio host to display a static Remote UI view.
It adds no process-launch logic, context transmission, credentials, user-input handling,
or automatic workflow execution. Registration alone does not authorize executing
repository instructions. Future Copilot Chat integration must still use supported APIs
only, must not scrape or drive the chat UI like a user, and must keep unsupported
response/edit/session automation behind explicit fallback behavior. The SDK has
transitive dependencies; this replacement does not constitute a dependency security
audit or a claim about all host-managed telemetry behavior.

## Manual acceptance and deferred work

Install in an experimental Visual Studio instance and verify extension registration.
Extension-list presence has been reported by the user; runtime activation and rendering
of the new command/window have not yet been confirmed. Follow the README's checklist:
invoke Plan with and without a solution, verify the status view, repeat the command,
close/reopen the window, resize, and check theme contrast. Record any host failures
before treating this as a validated installation.

Deferred: dynamic skills, workflow state, IDE/Git/test context, interactive Remote UI,
Copilot Chat-first handoff with supported-API capability checks, behavioral unit tests,
full supported-version testing, dependency review, and release/Marketplace hardening.
The SDK-generated package remains a preview.
