# Host capabilities matrix for Visual Studio 2022 and 2026

## Status and purpose

This specification records the P01 capability baseline before shared workflow
implementation. It documents what the current public SDK/package evidence proves,
what still requires in-host probes, and what currently has no public API evidence in
this repository's referenced packages.

This is a capability and fallback document, not a promise that every requested
integration exists. Public-package inspection and runtime/package observations must be
preferred over guessed API names. Missing evidence is treated as a gap until proven.

## Scope

The first release must support Visual Studio 2022 17.14+ and Visual Studio 2026.
This document covers:

- document, selection, open-document and workspace context
- diagnostics, build and test-result access
- command/menu placement and tool-window hosting
- semantic target resolution for solution/project/file/class/method actions
- Copilot handoff, response, edit and related automation boundaries
- per-version capability outcomes, fallbacks and blockers

It does not authorize private APIs, COM automation, reflection over internal assemblies
or unsupported Copilot providers. A minimal in-process VSSDK bridge is approved for
capabilities unavailable through supported out-of-process contracts, but every bridged
API and process-boundary contract still requires evidence on both supported IDE families.

## Evidence sources used so far

### In-process package checkpoint (not runtime proof)

The separate `TheKameleon.Superpowers.InProcess.vsix` now contains registered
editor/project/item/solution capability commands and Roslyn workspace adapters.
`SComponentModel` / `IComponentModel.GetService<VisualStudioWorkspace>()` is the
implemented acquisition path; its success still needs host observation. The editor
command now captures the active WPF snapshot/caret through public editor services
and invokes a tested semantic resolver asynchronously. It reports a declaration
signature and location or an unavailable reason. Other commands still show capability
flags; diagnostic adapter invocation and IPC are not implemented by these commands.

Semantic regression tests exposed and now guard against first-match linked-file
selection, stale editor text and ignored cancellation. The probe rejects ambiguous
paths rather than claiming linked-document support, and rechecks editor/solution
freshness. Full solution build and 37 tests (11 unit, 26 integration) passed. No host
semantic-target verification has been performed for this increment.

Menu-group parenting and the registered/embedded `SuperpowersBridge.CTMENU` name
were corrected. The solution builds and all 26 integration tests pass, including
command-table relationship and compiled resource/registration checks. No current
matrix runtime status is upgraded by this result. In particular, Roslyn compilation
diagnostics are not Error List contents or proof of the last build outcome; Test
Window acquisition, IPC and class/method invocation remain unproven. The existing
out-of-process matrix below retains its API-surface findings.

### Verified repository/runtime evidence

- `TheKameleon.Superpowers.Vsix/PlanCommand.cs`
- `TheKameleon.Superpowers.Vsix/SuperpowersExtension.cs`
- `TheKameleon.Superpowers.Vsix/SuperpowersToolWindow.cs`
- `TheKameleon.Superpowers.Vsix/SuperpowersToolWindowControl.cs`
- `TheKameleon.Superpowers.Vsix/.vsextension/string-resources.json`
- `docs/superpowers/plans/implementation-plan.md`
- VS 2026 experimental-instance runtime verification already recorded in P00
- User-confirmed VS 2022 F5 runtime activation already recorded in P00

### Package evidence

Referenced package resolved by the VSIX project:

- `Microsoft.VisualStudio.Extensibility` `17.14.2098`
- `Microsoft.VisualStudio.Extensibility.Editor.Contracts` `17.14.275`
- `Microsoft.VisualStudio.ProjectSystem.Query` `17.14.145`

Primary inspected metadata:

- `TheKameleon.Superpowers.Vsix/obj/project.assets.json`
- `%USERPROFILE%/.nuget/packages/microsoft.visualstudio.extensibility/17.14.2098/microsoft.visualstudio.extensibility.nuspec`
- `%USERPROFILE%/.nuget/packages/microsoft.visualstudio.extensibility/17.14.2098/lib/net8.0-windows8.0/Microsoft.VisualStudio.Extensibility.xml`

## Current capability matrix

| Capability area | Public SDK/package evidence | VS 2022 17.14+ status | VS 2026 status | Current conclusion | Required fallback or next proof |
| --- | --- | --- | --- | --- | --- |
| Extensions menu command placement | `CommandPlacement.KnownPlacements.ExtensionsMenu`; existing Plan command/package tests | Observed through F5 by user | Observed in Exp instance | Proven | Reuse for future top-level menu commands |
| Tool window hosting | `ToolWindow`, `ToolWindowConfiguration`, `ShowToolWindowAsync<T>`; existing runtime checks | Observed through F5 by user | Observed in Exp instance | Proven | Reuse for workflow UI shell |
| Active text view access | `ExtensionMethods.GetActiveTextViewAsync(IClientContext, CancellationToken)` and `EditorExtensibility.GetActiveTextViewAsync(...)` | Present view and active document URI observed; absent view observed with solution open and closed | Observed with active editor, no editor and no solution | Probe A active/no-editor/no-solution scenarios verified on both IDEs | Preserve explicit absent-view handling; no text-selection contents/range proof |
| Selected path/workspace-tree path | `ExtensionMethods.GetSelectedPathAsync(IClientContext, CancellationToken)` | Project/file paths observed; solution-root and closed-solution cases report handled `UriFormatException` | File/project paths observed; no selection and solution-root selection throw `UriFormatException` | File/project cases and root/no-selection limitations observed on both IDEs | Treat failures as unavailable and do not depend on a solution-root URI |
| Open-document enumeration | `DocumentsExtensibility.GetOpenDocumentsAsync`, `GetOpenDocumentAsync`, `OpenDocumentAsync` | Zero and one document observed; first moniker returned for BridgeCapability.cs | Counts and first moniker observed with zero, one and two open documents | Empty/nonempty enumeration observed on both IDEs | Closed-document lookup remains unproven |
| Text-document snapshot/edit support | `DocumentExtensions.AsTextDocumentAsync`, `EditorExtensibility.EditAsync` | Public evidence only | Public evidence only | Public API exists; host probe still required | Probe document snapshot/version/edit rejection behavior |
| Workspace/project queries | `WorkspacesExtensibility.QueryProjectsAsync`, `QueryProjectByPathAsync`, `QuerySolutionAsync` | Seven projects, one solution and canonical path when loaded; zero projects/solutions and unavailable path when closed | Observed with no solution and a 19-project solution; solution identity returned | Probe A loaded/no-solution queries verified on both IDEs | Preserve explicit empty-workspace handling; not proof of every query shape |
| Extension-owned diagnostics reporting | `LanguagesExtensibility` + `DiagnosticsExtensionMethods.GetDiagnosticsReporter` + `DiagnosticsReporter.ReportDiagnosticAsync/ClearDiagnosticsAsync` | Publish and clear calls succeeded; Error List publication and removal user-confirmed | `TKSPROBE001` publish and clear observed in Error List | Publish/clear user-verified on both IDEs | Keep reporter alive while diagnostics are active; not evidence for reading compiler/build diagnostics |
| Reading host compiler diagnostics | No inspected out-of-process API retrieves Error List/compiler diagnostics; Shell Error List provider types are in-process and provider-oriented rather than proven readers. The in-process probe now implements read-only active-document compiler diagnostics through public Roslyn workspace/editor services rather than Error List readers. | Supported read-only probe implemented; no separate screenshot evidence recorded for this command | Supported read-only probe implemented; no separate screenshot evidence recorded for this command | Supported minimal bridge path established for active-document compiler diagnostics only | Reuse the current DTO/probe shape; do not describe it as an Error List snapshot or build outcome |
| Build invocation/results | `Microsoft.VisualStudio.ProjectSystem.Query` 17.14.145 exposes `UpdateExtensions.BuildAsync` for project snapshots and solution queries | Selected Core-project invocation completed; separate Build output reported 1 succeeded, 0 failed | Selected Core-project invocation observed; task completed and independent Build output reported 0 failures | Selected-project invocation user-verified on both IDEs; API itself returns no build outcome | Collect outcome separately and never equate task completion with build success; solution-wide invocation not proven by this probe |
| Test discovery/run/results | Installed `Microsoft.VisualStudio.TestWindow.Interfaces.dll` documents external-facing `ITestsService`, `ITest` and `IResult`, but no out-of-process broker/accessor is exposed by the referenced Extensibility SDK; `IVsTestServiceInternal` explicitly targets internal extensions. Direct installed-assembly inspection further shows `ITestsService`, `ITest`, `IResult` and `IVsTestServiceInternal` are non-public in the shipped interface assembly, while only unrelated public test-container/stats extensibility types are exported. | No proven bridge path | No proven bridge path | Out-of-process gap; bridge investigation remains blocked on public acquisition | Prove supported in-process acquisition and event lifetimes without using internal or non-public interfaces; otherwise use scoped runners, VSTest Platform-based execution/result import, or manual/imported evidence |
| Class/method semantic targeting | The out-of-process SDK documents no semantic model, symbol, syntax-tree or compilation contract | No proven bridge path | No proven bridge path | Out-of-process gap; bridge investigation approved | Prove supported public in-process Roslyn workspace/document mapping and return immutable symbol DTOs; do not load IDE-local implementation DLLs directly |
| Solution/project/file context-menu placement | Out-of-process `CommandPlacement.KnownPlacements` documents only Tools, View Other Windows and Extensions menus | No proven bridge path | No proven bridge path | Out-of-process gap; bridge investigation approved | Prove supported public VSSDK command placement on both IDEs; retain the Extensions submenu until verified |
| Shell prompts/choices | XML docs show shell prompt/selection APIs | Public evidence only | Public evidence only | Likely available | Use for user approvals/settings when needed after probe |
| Copilot prompt handoff | No Copilot contract in the referenced Extensibility SDK or local NuGet cache; installed product-private Copilot assemblies are not an approved dependency | Unsupported through the approved SDK | Unsupported through the approved SDK | Supported direct handoff unavailable | Use explicit preview/copy/manual handoff |
| Copilot response retrieval | Installed implementation XML documents responder/session types, but no supported third-party Extensibility contract/package exposes them | Unsupported through the approved SDK | Unsupported through the approved SDK | Supported response retrieval unavailable | Accept explicit manual result import only |
| Copilot edit application | No supported Copilot edit/session contract in the referenced SDK; installed Copilot implementation DLLs are extension-local | Unsupported through the approved SDK | Unsupported through the approved SDK | Supported Copilot edit integration unavailable | Use extension-owned edits under policy plus manual Copilot handoff |
| Upstream subagent/tool automation | Installed implementation XML includes agent/subagent concepts and `UnstableInternalApi` types, but no supported Extensibility package exposes session dispatch | Unsupported through the approved SDK | Unsupported through the approved SDK | Out of scope through direct Copilot integration | Manual fallback only; do not reference installed implementation assemblies |

### VS 2022 runtime probe evidence

User-provided screenshot labeled VS 2022, probe timestamp
`2026-09-21T03:33:53.0336178+00:00`, shows:

- Status: all public context calls completed.
- No active text view; active document URI unavailable.
- Selected path points to `TheKameleon.Superpowers.Bridge.Contracts.csproj`.
- Zero open documents; first open-document moniker unavailable.
- Project query succeeded with seven projects.
- Solution query succeeded with one solution and the canonical
  `C:\Users\kamel\source\repos\TheKameleon.Superpowers.Vsix\TheKameleon.Superpowers.slnx` path.

This first screenshot establishes the loaded-solution/no-editor/project-selection scenario.

A second user screenshot in the VS 2022 verification sequence, timestamp
`2026-09-21T03:35:20.6977588+00:00`, shows all public context calls completed with:

- Active text view present and active document URI pointing to
  `TheKameleon.Superpowers.Bridge.Contracts/BridgeCapability.cs`.
- Selected path pointing to the same C# file.
- One open document; first moniker points to that file with `?vs=version:1`.
- Successful queries returning seven projects, one solution and the canonical solution path.

A third user screenshot, with the solution root selected, timestamp
`2026-09-21T03:37:29.8866776+00:00`, shows:

- Status: completed with one unavailable or failed call.
- Selected path failed with `UriFormatException`: "Invalid URI: The format of the URI could not be determined."
- No active text view, unavailable active document URI and zero open documents.
- Successful queries returning seven projects, one solution and the canonical solution path.

This matches the handled VS 2026 solution-root limitation; it is not successful
selected-path acquisition.

A fourth user screenshot, after closing the solution, timestamp
`2026-09-21T03:38:44.9281580+00:00`, shows:

- Status: completed with one unavailable or failed call.
- No active text view, unavailable active document URI, zero open documents and unavailable first moniker.
- Project and solution queries succeeded, each returning zero results; solution path unavailable.
- Selected path failed with `UriFormatException`: "Invalid URI: The format of the URI could not be determined."

Probe A's required context-mapping scenarios are now user-verified on both IDE
families, completing P01.01 with the documented selected-path limitation. The
screenshots do not establish the IDE servicing version or text-selection
contents/range. They provide no additional P01.02 or P01.04 bridge evidence, editor
edit proof or closed-document lookup proof.

VS 2022 Probe C output at `2026-09-21T03:40:27.2253488+00:00` reports
"Synthetic diagnostic report call succeeded." The accompanying Error List entry
shows `TKSPROBE001`, "Synthetic Superpowers capability probe diagnostic. Safe to
clear.", on `TheKameleon.Superpowers.Bridge.Contracts/BridgeCapability.cs`, line 1.
Subsequent output at `2026-09-21T03:46:50.5456947+00:00` reports "Synthetic
diagnostic clear call succeeded." for the same diagnostic and document. The user
confirmed it was cleared. The screenshot proves call completion; removal is
user-attested. This completes Probe C publish/clear evidence on VS 2022, not host
diagnostic reading or the other P01.02 gates.
The accompanying `NU1702` warning is a separate integration-test build-reference
issue, not failure of the synthetic diagnostic reporter.

VS 2022 Probe E output at `2026-09-21T03:48:23.7917634+00:00` identifies
`TheKameleon.Superpowers.Core.csproj` and reports "Build invocation task completed."
It explicitly reports outcome unavailable from the API. The separate user-supplied
Build Output shows Core built in Debug Any CPU to
`TheKameleon.Superpowers.Core/bin/Debug/net8.0/TheKameleon.Superpowers.Core.dll`,
with `1 succeeded, 0 failed, 0 up-to-date, 0 skipped`. Output reports completion at
22:48 and duration 0.602 seconds. Together these establish invocation and an
independently observed successful build; they do not establish automatic outcome
collection, test-result access or bridge communication.

### VS 2026 runtime probe evidence

User-observed runs in Visual Studio Community 2026 18.10.1 established:

- With no solution and a temporary active editor, active-view/open-document capture worked, project and solution queries returned zero results, and selected-path capture reported `UriFormatException` without crashing.
- With no solution and no editor, the probe reported an absent active view, zero open documents and zero workspace results; selected-path capture again reported `UriFormatException`.
- With a 19-project solution and no active text editor, project and solution queries returned 19 and 1 results respectively, and a selected project path was captured.
- With an active C# editor, the active document URI, selected file path, two open documents, 19 projects and the solution path were captured.
- With the solution root selected, workspace queries remained successful but selected-path capture reported `UriFormatException`.
- Publishing `TKSPROBE001` displayed the synthetic diagnostic in Error List; clearing it removed the entry. The reporter must remain alive between those calls.
- The Remote UI rendered probe status after its model and properties were marked with `DataContract`/`DataMember` as required by the installed SDK template.
- The selected-project build probe resolved `TheKameleon.Superpowers.Core.csproj` and its `BuildAsync` task completed. Separately observed Build output reported `0 succeeded, 0 failed, 1 up-to-date, 0 skipped`; the separate evidence is required because the API returns no outcome.

## Proven public API details

### Commands, menus and tool windows

Already used in the current shell:

- `Microsoft.VisualStudio.Extensibility.Commands.Command`
- `CommandConfiguration`
- `CommandPlacement.KnownPlacements.ExtensionsMenu`
- `Microsoft.VisualStudio.Extensibility.ToolWindows.ToolWindow`
- `ToolWindowConfiguration`
- `VisualStudioExtensibility.Shell().ShowToolWindowAsync<T>()`

This proves a supported out-of-process command and tool-window path for the existing
Plan entry point and likely for additional submenu commands.

### Documents and editor

Public XML documentation shows:

- `ExtensionMethods.GetActiveTextViewAsync(IClientContext, CancellationToken)`
- `ExtensionMethods.GetSelectedPathAsync(IClientContext, CancellationToken)`
- `DocumentsExtensibility.GetOpenDocumentsAsync(...)`
- `DocumentsExtensibility.GetOpenDocumentAsync(...)`
- `DocumentsExtensibility.OpenDocumentAsync(...)`
- `DocumentExtensions.AsTextDocumentAsync(...)`
- `DocumentExtensions.SaveAsync(...)`
- `DocumentExtensions.CloseAsync(...)`
- `Editor.EditorExtensibility.GetActiveTextViewAsync(...)`
- `Editor.EditorExtensibility.EditAsync(...)`

This is sufficient to justify targeted probes for document, text-view and selected-path
context capture without inventing private editor integrations.

### Workspace queries

Public XML documentation shows:

- `WorkspacesExtensibility.QueryProjectsAsync(...)`
- `WorkspacesExtensibility.QueryProjectByGuidAsync(...)`
- `WorkspacesExtensibility.QueryProjectsByGuidAsync(...)`
- `WorkspacesExtensibility.QueryProjectByPathAsync(...)`
- `WorkspacesExtensibility.QueryProjectsByPathAsync(...)`
- `WorkspacesExtensibility.QueryProjectsByCapabilitiesAsync(...)`
- `WorkspacesExtensibility.QuerySolutionAsync(...)`

This establishes a public query path for solution/project-level context, but not yet
what query shapes are needed for all requested scenarios.

### Diagnostics

Public XML documentation shows:

- `DiagnosticsExtensionMethods.GetDiagnosticsReporter(...)`
- `Documents.DiagnosticsReporter.ReportDiagnosticAsync(...)`
- `Documents.DiagnosticsReporter.ReportDiagnosticsAsync(...)`
- `Documents.DiagnosticsReporter.ClearDiagnosticsAsync(...)`

This proves the extension can contribute its own diagnostics into Visual Studio's error
list experience. It does **not** prove the extension can read existing compiler/build
errors from the host.

## Gaps that still require probes or follow-up investigation

### Requires minimal host probes

These have public API evidence but still need runtime validation in VS 2022 and VS 2026:

- active editor present vs absent
- selected path behavior from different invocation targets
- open document enumeration and closed-document lookup
- document snapshot conversion and edit/version behavior
- workspace query behavior in single-project and multi-project solutions

Targeted command placement is excluded from this list after the P01 step 4 review:
the inspected SDK documentation did not establish public solution/project/file
placement identifiers, so there is no supported probe path to validate yet.

The complete `CommandPlacement.KnownPlacements` documentation in the referenced
17.14.2098 contracts exposes only Tools, View Other Windows and Extensions menu
placements. The resolved project contains no Roslyn/CodeAnalysis package, and the
Extensibility XML surface contains no semantic model, symbol, syntax-tree or compilation
contract. Installed IDE implementation assemblies are not an approved substitute.

### Requires separate public-API investigation

No acceptable public evidence has been found yet for:

- reading host compiler/build diagnostics
- obtaining build outcomes after the supported Project System Query invocation task completes
- acquiring the installed external-facing `ITestsService` from an out-of-process extension
- Roslyn semantic targeting for class/method command scopes
- any Visual Studio Copilot chat/prompt/response/edit public API
- any subagent/session-control API analogous to upstream CLI workflows

Until these are proven, the product must not claim those capabilities exist.

The resolved `Microsoft.VisualStudio.ProjectSystem.Query` 17.14.145 package provides
public project `BuildAsync`, `RebuildAsync` and `CleanAsync` operations and solution-query
counterparts. Their documented return is only a task completed when the operation is
completed; no success/failure result or diagnostic collection is returned. The
`Microsoft.VisualStudio.Extensibility.Build` package is build-time packaging tooling,
not a runtime IDE-build service. No namespace-bounded Testing, Build or Error List
runtime contract was found in the installed Visual Studio Extensibility XML surface.

The installed `Microsoft.VisualStudio.TestWindow.Interfaces.dll` refines the Test
Explorer finding. Its `Microsoft.VisualStudio.TestWindow.Extensibility` namespace
documents `ITestsService`, `ITest`, `IResult` and run methods, while
`IVsTestServiceInternal` explicitly states that it supports internal extensions without
exposing too much externally. Direct inspection of the shipped assembly shows that
`ITestsService`, `ITest`, `IResult` and `IVsTestServiceInternal` are not exported as
public types, whereas public exports in this area are limited to unrelated container,
discoverer, run-settings, stack-trace and stats interfaces. No separate
`Microsoft.VisualStudio.TestWindow.Extensibility.dll` is installed, no Test Window
contract package is resolved or present in the local NuGet cache, and the referenced
out-of-process Extensibility SDK exposes no accessor or broker. `Microsoft.VisualStudio.Shell.15.0`
is a traditional in-process path. The approved hybrid architecture now permits a minimal
bridge investigation, but the presence of this assembly is not proof that `ITestsService`
can be acquired by a supported public extension contract. A probe must establish
acquisition, event lifetime and compatibility without using internal or non-public
interfaces before the product can depend on this path.

## Approved hybrid boundary

The out-of-process .NET 8 extension remains the primary product host and owns Remote UI,
workflow orchestration, policy, settings and skill behavior. A minimal in-process VSSDK
component may be added only for capabilities unavailable through supported
out-of-process contracts. It may target the framework required by supported public
VSSDK APIs as a narrow exception to the repository's .NET 8 rule.

The bridge must expose explicit, versioned, serializable DTO operations rather than
Visual Studio SDK objects. It must not expose arbitrary service lookup, arbitrary
command execution, COM automation, reflection over internal assemblies, private APIs or
product workflow logic. Transport, authentication/session binding, cancellation,
shutdown, reconnection, version negotiation and failure isolation must be proven before
expanding beyond the first capability spike.

The Visual Studio 2026 installation contains extension-local
`Microsoft.VisualStudio.Copilot.dll` and `Microsoft.VisualStudio.Copilot.Core.dll`
implementation assemblies. Their XML documentation includes responder/session/agent
types and explicitly named `UnstableInternalApi` types, but the referenced
`Microsoft.VisualStudio.Extensibility` SDK exposes no Copilot/chat/agent/language-model
contract, the project resolves no Copilot contract package, and the local NuGet cache
contains no Copilot-named package. These installed implementation DLLs are not an
approved third-party dependency. P01 therefore selects preview/copy/manual handoff and
manual result import rather than direct prompt, response, edit or subagent integration.

## Proposed probe set for the next implementation step

The next probe pass should remain minimal and evidence-focused:

1. **Context probe command**
   - report whether an active text view exists
   - report selected path
   - report count of open documents
   - report a simple project/solution query result
2. **Targeting placement investigation — deferred**
   - do not add solution/project/file commands until documented public placement
	 identifiers are found
   - continue using the proven Extensions submenu for temporary probe commands
3. **Edit safety probe**
   - convert the active document to a text snapshot and perform a no-op or reversible edit path only if explicitly approved
4. **Diagnostics probe**
   - publish and clear a synthetic diagnostic to prove extension-owned Error List integration

No build/test/Copilot probe should be implemented until a supported public surface is
identified or the fallback path is explicitly approved.

## Current fallback policy implied by evidence

- **Document/workspace context:** proceed with public probes.
- **Extension-owned diagnostics:** proceed with public probes.
- **Solution/project/file context menus:** retain the Extensions submenu fallback;
  targeted placement remains blocked pending documented public API evidence.
- **Build/test/host diagnostic reading:** manual or imported evidence only until a supported API is proven.
- **Copilot integration:** preview/copy/manual handoff remains the approved default until a public API is proven.
- **Class/method semantic targeting:** do not promise this scope until public semantic APIs are proven.

## Constraints for implementation

- Do not use private, internal or reflection-only APIs to fill capability gaps.
- Do not treat CLI plugins, VS Code adapters or user-observed behavior in another host
  as Visual Studio public API evidence.
- Do not widen permissions or automation claims because a workflow would benefit from
  an unavailable capability.
- Keep probes easy to remove or evolve after P01 concludes.
- Record negative results explicitly; absence of evidence is an architectural input.

## Open questions for P01 completion

- Is there any supported public path outside `Microsoft.VisualStudio.Extensibility`
  for build/test/diagnostic reading that is still compatible with the approved
  out-of-process design?
- Is there a documented supported API, package, or future SDK version that exposes
  project/file context-menu placements while preserving the approved out-of-process
  architecture?
- Is any public Visual Studio Copilot extensibility surface available in these IDEs,
  or must first release rely entirely on preview/copy/manual handoff?
- Can class/method targeting be implemented through public workspace/language APIs, or
  must first release narrow scope before implementation?
