# P01 minimal probe design

## Status and purpose

This document defines the minimal host probes for P01. The goal is to gather
capability evidence with the smallest possible product change, using only supported
public APIs already identified in the host-capabilities matrix.

The probes are evidence tools, not feature implementation. They must remain:

- non-destructive by default
- easy to remove or evolve after P01
- explicit about unsupported/missing capabilities
- truthful about whether evidence is static, probed, automated or user-attested

## Design principles

1. Prefer one probe command per capability cluster instead of implementing workflow UI.
2. Keep probe output simple and inspectable in the existing Superpowers tool window.
3. Avoid persistence, background services or hidden collection for P01.
4. Do not add probe paths for build/test/Copilot/Roslyn semantic features until a
   supported public API surface and acquisition path are identified. An approved
   minimal in-process bridge may be used to probe supported public VSSDK contracts.
5. Separate "public API exists" from "works on both VS versions in host runtime".
6. Record negative/empty results explicitly.

## Probe set overview

### Probe A — Context capability probe

**VS 2026 status:** Implemented and user-verified in Visual Studio Community 2026
18.10.1. Active/no-editor/no-solution cases and file/project selections report without
crashing. `GetSelectedPathAsync` succeeds for file and project selections but throws
`UriFormatException` for no selection and solution-root selection; the probe records
that call as unavailable while preserving other results.

**VS 2022 status:** User screenshot at probe timestamp
`2026-09-21T03:33:53.0336178+00:00` verifies the loaded-solution/no-editor scenario:
all calls completed, the Bridge.Contracts project path was selected, zero documents
were open, seven projects and one solution were returned with the canonical solution
path. A second screenshot at `2026-09-21T03:35:20.6977588+00:00` verifies an active
`BridgeCapability.cs` editor, matching selected file path and one open document with
its first moniker, with all calls completed and unchanged workspace counts.
The solution-root screenshot at `2026-09-21T03:37:29.8866776+00:00` reports one
unavailable/failed call: selected path throws `UriFormatException`. No editor and
zero documents are reported; project/solution queries still succeed with seven
projects, one solution and the canonical path. This matches the handled VS 2026
limitation. The final screenshot at `2026-09-21T03:38:44.9281580+00:00`, after closing
the solution, shows no active editor, zero open documents, successful queries with
zero projects and zero solutions, unavailable solution path and a handled selected-path
`UriFormatException`. Probe A's required scenarios are now user-verified on both IDE
families; P01.01 is complete for this context-mapping scope. Text-selection contents
and range are not shown by this output; editor edits and bridge semantics are not
validated by Probe A.

**Purpose**
Validate the public APIs for active text view, selected path, open documents and
workspace queries.

**Entry point**
A temporary command under the existing Superpowers menu, for example:
- `Probe Context`

**Operations**
On invocation:
1. Read the current `IClientContext`.
2. Attempt to get the active text view.
3. Attempt to get the selected path.
4. Enumerate open documents.
5. Run a minimal workspace query for solution/project presence.
6. Render a structured result in the tool window.

**Expected fields**
- IDE version label entered manually or inferred outside the probe
- invocation timestamp
- active text view present: yes/no
- active document URI if available
- selected path value or explicit unavailable/empty result
- open document count
- first open document moniker sample if available
- solution/project query succeeded: yes/no
- project count or explicit unavailable result
- exception type/message if any public call fails

**Success criteria**
- Works in both VS 2022 and VS 2026.
- Produces honest output with no crashes for:
  - no solution open
  - solution open with no editor
  - active text editor open
  - selected Solution Explorer item

**Non-goals**
- semantic symbol discovery
- hidden background capture
- prompt composition

## Probe B — Command targeting probe

**P01 step 4 decision: deferred**

The referenced `Microsoft.VisualStudio.Extensibility` 17.14.2098 XML documentation
does not identify supported solution/project/file context-menu placement identifiers.
The current extension proves only `CommandPlacement.KnownPlacements.ExtensionsMenu`.
The stop condition below therefore applies: do not implement Probe B in the first
probe pass. Continue using the existing Superpowers submenu for Probe A and Probe C.

**Purpose**
Determine whether supported public command placements can target solution/project/file
scopes beyond the existing Extensions submenu.

**Entry point**
Add the smallest possible experimental commands only if the SDK surface clearly
supports the placement being tested.

Suggested staged targets:
1. solution context command
2. project context command
3. file/item context command

**Operations**
Each probe command should only:
- open the tool window or prompt
- report the invocation context that Visual Studio provided
- never mutate files or state

**Success criteria**
- command appears only where expected
- invocation context is distinguishable
- behavior is stable on both IDE versions

**Stop conditions**
If the placement API is not clearly public/supported from the SDK surface, do not
implement this probe yet. Record the gap and continue with other probes.

## Probe C — Diagnostics reporter probe

**Runtime evidence:** Publish and clear are user-verified on VS 2026. On VS 2022,
the user supplied output at `2026-09-21T03:40:27.2253488+00:00` showing a successful
report call and an Error List entry for `TKSPROBE001` on `BridgeCapability.cs`, line 1.
The subsequent output at `2026-09-21T03:46:50.5456947+00:00` shows a successful
clear call for the same diagnostic and document; the user confirmed removal.
Probe C publish/clear is user-verified on both IDE families. This does not prove
host compiler-diagnostic reading or complete the other P01.02 gates.

**Purpose**
Prove that the extension can publish and clear its own diagnostics using the public
Languages/DiagnosticsReporter surface.

**Entry point**
A temporary command under the existing Superpowers menu, for example:
- `Probe Diagnostics`

**Operations**
1. Require an active text document or selected document URI.
2. Publish one synthetic diagnostic with a clearly fake probe ID/message.
3. Confirm the call succeeds.
4. Provide a second explicit action to clear the probe diagnostic.

**Expected evidence**
- report call succeeded/failed
- document target used
- clear call succeeded/failed
- user-visible Error List confirmation noted in probe output or manual verification

**Safety constraints**
- synthetic IDs/messages must clearly indicate probe-only data
- do not emit diagnostics repeatedly or automatically
- do not attempt to read host diagnostics as part of this probe

## Probe D — Optional edit safety probe

**Purpose**
Validate document snapshot conversion and editor edit API behavior without performing
meaningful content changes.

**Status**
Deferred unless explicitly approved after Probes A and C.

**Reason for deferral**
Even reversible edits modify user buffers and are unnecessary for initial capability
mapping. Public API existence is already known from the SDK docs.

**If later approved**
Use only a reversible/no-op path on a disposable sample file or clearly warn before
running. Do not include this in the first probe pass by default.

## Probe E — Selected-project build invocation probe

**Status:** Implemented and user-verified on VS 2022 and Visual Studio Community
2026 18.10.1 for the selected Core project. Separate Build Output supplies outcome
evidence; the probe itself does not return an outcome.

The resolved `Microsoft.VisualStudio.ProjectSystem.Query` 17.14.145 surface exposes
`UpdateExtensions.BuildAsync(IProjectSnapshot, CancellationToken)`. The probe requires
an explicitly selected `.csproj`, resolves that exact project through the workspace
query API and invokes its build only when the user chooses the probe command.

The API documents task completion but returns no build outcome. Probe output must say
only that invocation completed, never that the build succeeded. Normal build evidence
must be inspected separately. This operation can execute repository build targets and
must not run automatically.

The VS 2026 probe resolved `TheKameleon.Superpowers.Core.csproj` and the invocation task
completed. Independent Build output reported zero failures and one up-to-date project.
These remain two distinct evidence records; the probe did not infer success from task
completion.

The VS 2022 probe at `2026-09-21T03:48:23.7917634+00:00` resolved the same Core
project and reported task completion with outcome unavailable. Separate Build Output
shows Debug Any CPU compilation to the `net8.0` Core DLL and
`1 succeeded, 0 failed, 0 up-to-date, 0 skipped`, completed at 22:48 in 0.602 seconds.
This completes selected-project invocation verification on both IDE families, not
automatic build-outcome collection or the remaining P01.02 bridge/test-result gates.

## Explicit exclusions from the first probe pass

These areas should not receive implementation probes in the next step because no
acceptable public API evidence exists yet:

- host compiler/build diagnostics reading
- build outcome collection after the supported invocation task completes
- Test Explorer discovery/run/result collection through an approved out-of-process service acquisition path
- Visual Studio Copilot prompt, response or edit APIs
- Roslyn semantic symbol/class/method resolution
- subagent/session dispatch or workflow automation beyond manual probes

For these, the correct P01 outcome may be one of:
- no supported API found
- fallback required
- scope must narrow before implementation

The Copilot investigation is complete for the referenced SDK and installed VS 2026
surface. Product-local Copilot assemblies are not a supported extension contract, so
no Copilot runtime probe will be added. Preview/copy/manual handoff remains the approved
path unless Microsoft publishes a supported contract package in a future SDK.

The semantic-targeting investigation is also complete for the referenced SDK. No
Roslyn/CodeAnalysis dependency or semantic model/symbol contract is exposed to this
out-of-process extension, and the only documented known command placements are Tools,
View Other Windows and Extensions. Probe B therefore remains blocked rather than being
implemented with legacy identifiers or IDE-local assemblies.

The installed Test Window interface assembly does document an external-facing
`ITestsService` with test enumeration, run and result models. It is product-local and
the approved Extensibility SDK exposes no broker/accessor for it; the related
`IVsTestServiceInternal` explicitly serves internal extensions. The approved hybrid
architecture permits a minimal in-process acquisition probe only through supported
public contracts; the probe must not use this internal interface or treat assembly
presence as evidence of support.

## Probe F — Minimal in-process bridge feasibility

**Status:** A separate in-process probe VSIX exists in the workspace. It registers an
`AsyncPackage`, acquires `VisualStudioWorkspace` through `SComponentModel` /
`IComponentModel`, and contains Roslyn diagnostic and semantic-target adapters. The
editor command invokes guarded C# semantic analysis, with positive class/method
and two negative-context checks recorded on both IDE families in the canonical plan.
The project/item/solution commands now capture selected-node identity through public
VSSDK services; their updated identity output still requires host validation on both
IDE families. Cross-process communication has not been demonstrated.

### Context-menu placement increment (P01.04)

Use the public VSSDK `IDM_VS_CTXT_CODEWIN`, `IDM_VS_CTXT_PROJNODE`,
`IDM_VS_CTXT_ITEMNODE` and `IDM_VS_CTXT_SOLNNODE` identifiers. Each context menu must
parent a package-owned group; each probe button must parent that group, not the menu
directly. Regression tests check all four relationships in the command-table source
and the packaged compiled menu resource. Compilation alone is not placement evidence.

In an experimental instance of each supported IDE, verify that the corresponding
editor/project/item/solution command appears and invokes the package. Record the IDE
version and actual target separately: the current scope label is a command identifier,
not proof that the selected project, file, class or method was resolved. Class/method
semantic invocation, linked-document context, diagnostic collection, Test Window
acquisition and IPC lifecycle remain subsequent proof gates. Roslyn compilation
diagnostics must not be described as an Error List snapshot or a completed build result.

Do not install this probe into the main IDE or assume its separate VSIX establishes
combined modern/in-process packaging. Existing out-of-process Probe A, diagnostic
publish/clear and selected-project build checks are now user-verified on both IDE families.

### Editor semantic-target increment (P01.04)

The editor command captures the active WPF text view through public VSSDK editor
adapters and obtains its file path through `ITextDocumentFactoryService`. Capture
the caret and immutable snapshot on the UI thread, then analyze asynchronously.
Limit this probe to C# documents of at most 1,000,000 characters; reject virtual
space, missing views, unmapped buffers and ambiguous linked-file paths explicitly.
Compare the captured text with Roslyn's document text before resolving a declaration;
do not silently use disk contents or choose the first project for a linked file.
Before displaying results, reject changes to the solution, editor snapshot, caret,
active view or document path. Cancellation during shutdown must not display a dialog.

Report the enclosing named type or method, its signature and source location
(displayed one-based; DTO coordinates remain zero-based). No matching declaration
is an unavailable result, not an empty successful target. Exceptions are reported
Other menu scopes use the selection-identity increment below. No file writes, builds, Test
Explorer operations or IPC are part of this command.

Unit tests cover method/class resolution, bounds, missing/ambiguous documents,
snapshot mismatch and cancellation. In both IDE experimental instances, invoke on a
class name and within a method, then exercise non-C# and no-editor cases. Record
the displayed signature/path/location and unavailable reasons. Unit and package
tests are not evidence of host service acquisition or context-menu visibility.

### Active-document compiler-diagnostic increment (P01.02)

Add `Superpowers: Probe Document Compiler Diagnostics` to the editor context menu.
Reuse the semantic probe's C# file, size, snapshot and caret capture guards. Analyze
the exact captured Roslyn document off the UI thread using public semantic-model
diagnostics, not the Error List or analyzer services. Reject missing/ambiguous
document mappings, stale text, missing models and changed workspace/editor context.
An unavailable result must not be reported as zero diagnostics.

Report the file, total non-hidden/non-suppressed document compiler diagnostics and
up to 10 entries with ID, severity, message and one-based source location. Bound
each displayed message to 300 characters and explicitly label truncation. Request
cancellation after 30 seconds and on package shutdown; this is cooperative, not a
hard execution deadline. Empty successful results explicitly say zero diagnostics
for this document only. Do not invoke a build, modify files, publish diagnostics,
run analyzers, use Test Explorer or perform IPC.

In both experimental IDEs, invoke on a loaded C# document with a known compiler
error (for example, temporarily insert `#error TKSPROBE_READ` into a scratch project
file). Verify CS1029, the message and source location. Remove the directive and
repeat to confirm it disappears; a clean file should report zero. Check non-C#,
unmapped/linked files and changed-context results where reproducible. Restore any
manual test edits. Keep host checks distinct from unit tests and leave P01.02 open
for Test Window access and bridge transport/lifecycle work.

### Selected-node identity increment (P01.04)

Capture the current selection synchronously on the UI thread through public
`IVsMonitorSelection.GetCurrentSelection`, with services acquired at command
initialization. Use the selected `IVsHierarchy` and `IVsProject`, not the active
editor or a guessed project. Release returned hierarchy/selection-container COM
pointers in `finally`; do not force-release shared RCWs.

Report selected kind, name and absolute path. Files must be physical-file hierarchy
items with an owning project name/path. Projects must be project roots; solutions
must be solution roots with an available solution file from `IVsSolution`.
Reject multiple selections, missing selection, mismatched command scopes, virtual
items, folders and incomplete identities explicitly. Never substitute the open
solution merely because the solution command was invoked. Capture and display the
identity without an intervening asynchronous yield. This is read-only and has no IPC.

On each IDE, run the solution/project/file probes and compare the displayed names
and paths with Solution Explorer. Repeat with another project/file while keeping a
different file active in the editor. Check multi-selection and folder/virtual-item
behavior where the menu is offered. Record unavailable results rather than treating
them as successful target capture. Automated validation does not establish host
selection acquisition; P01.04 remains open pending these results.

**Purpose**
Validate the hybrid process boundary with one narrowly scoped capability before adding
other in-process adapters. Test-result snapshot/event access is the preferred first
candidate because the installed public-facing Test Window interfaces are known, while
their supported acquisition path remains unproven.

**Required proof**
1. Acquire the target service through a supported public in-process extension contract.
2. Return versioned serializable DTOs without leaking Visual Studio SDK objects.
3. Demonstrate request cancellation, IDE shutdown, disconnect and reconnect behavior.
4. Verify packaging and runtime behavior on VS 2022 17.14+ and VS 2026.
5. Confirm that no private/internal API, arbitrary service lookup or arbitrary command
   execution is exposed.

**Stop condition**
If supported service acquisition cannot be demonstrated, retain the existing scoped
runner or manual/imported fallback and do not broaden the bridge.

### Bridge transport design checkpoint

The current bridge feasibility work establishes in-process capability acquisition
patterns only; it does not establish cross-process invocation. Before production
features depend on bridge-backed active-document text or other bridge operations,
the product must define and prove a transport with these properties:

- explicit protocol version on every request/response
- serializable DTO envelopes only
- narrow operation list (`GetCapabilities`, `GetActiveDocumentText` first)
- cancellation and shutdown handling
- structured failure results for unavailable/version-mismatch/context-changed cases
- no arbitrary service lookup or command execution
- no leakage of Visual Studio SDK objects outside the in-process boundary

A bridge transport implementation is not proven merely because the in-process
package and probe commands load successfully. If supported acquisition of the
transport cannot be demonstrated, retain metadata-only out-of-process capture
and do not claim bridge-backed document text in production workflows.

**Chosen transport for this checkpoint**: a custom `System.IO.Pipes`
named-pipe channel (server in `TheKameleon.Superpowers.InProcess`, client
implementing `IBridgeClient` in `TheKameleon.Superpowers.Vsix/Bridge`), built
from public .NET APIs only. This was selected specifically because no usable
VS-provided broker/transport API was found during assembly inspection, and a
plain OS/.NET-level named pipe avoids depending on any VS-private or internal
surface while still being fully provable. Proof work for this checkpoint must
demonstrate, specifically for the named-pipe implementation:

1. supported public pipe creation/connection APIs on both VS2022 17.14+ and
   VS2026 (no VS-private broker involved)
2. version negotiation and structured mismatch handling over the pipe
3. cancellation-aware reads/writes and clean shutdown on IDE/package unload
4. disconnect/reconnect behavior (single bounded reconnect attempt, then
   honest "unavailable" fallback)
5. failure isolation — no Visual Studio SDK objects or internal exceptions
   cross the pipe boundary, only structured DTOs
6. local-user-only ACL enforcement (no cross-session/cross-user connections)

Only after this checklist passes may bridge-backed active-document text be
claimed as supported in production workflows.

## Output strategy

The current Superpowers tool window is sufficient for probe output if expanded to show:

- probe name
- timestamp
- structured key/value results
- explicit success/failure state per call
- notes for manual follow-up

Avoid creating a second probe-specific tool window unless the existing one cannot host
simple results cleanly.

## Recommended implementation order

1. Expand the existing tool window to display probe results.
2. Implement `Probe Context`.
3. Implement `Probe Diagnostics` with explicit publish/clear actions.
4. Evaluate whether public context-menu placement APIs are sufficiently clear.
   - Completed: the inspected SDK documentation is insufficiently clear.
5. Only then decide whether `Probe Targeting` is implementable in this phase.
   - Decision: defer Probe B and retain the Extensions submenu fallback.
6. Keep edit probes deferred unless specifically needed.
7. Implement the selected-project build probe after public Project System Query build
   methods were found; require explicit invocation and report outcome as unavailable.

## Expected P01 decisions enabled by these probes

After the first probe pass, we should be able to answer:

- whether the approved out-of-process SDK is enough for document/workspace context
- whether extension-owned diagnostics integration is viable
- whether context-menu targeting is publicly supported and stable enough for v1
- which requested capabilities must remain manual fallback or be narrowed in scope

## Proposed next implementation scope

If implementation proceeds after this design, the minimal code change should be:

- one lightweight result model in `Core` for probe output if needed
- minimal menu/command additions in `VSIX`
- minimal tool-window view-model updates in `VSIX`
- no Skills changes
- no production workflow engine changes

This keeps P01 evidence gathering narrow and aligned with the approved hybrid
architecture: a .NET 8 out-of-process primary host plus a minimal, capability-specific
in-process bridge.
