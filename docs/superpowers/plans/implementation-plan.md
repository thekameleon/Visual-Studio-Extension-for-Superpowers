# TheKameleon Superpowers — Implementation Plan

**Document status:** Upstream reuse and the stable/prerelease release-catalog direction are approved. Detailed engineering proposals and P00/P01 verification gates remain outstanding; product integration is not implemented.

**Purpose:** This is the repository's canonical implementation checklist. It covers the complete requested product, not just the extension scaffold. Checked tasks identify existing work; unchecked tasks are future work or outstanding verification. Preparing this document does not approve or execute its tasks.

## 1. How to use this plan

- `[x]` means the stated task has supporting evidence. It does not imply related manual checks passed.
- `[ ]` means not done, not verified, or awaiting a decision; the task text identifies which.
- Keep task IDs stable. Update the relevant task immediately after completing it, with source/test links and validation results.
- Never mark a feature complete solely because a prompt was generated or a package built.
- Record blocked work under the decision/risk register; do not silently substitute unsupported behavior.
- Before each implementation phase, confirm its prerequisites and write failing behavioral tests where applicable. Update the associated specification before changing a shared contract.
- Changes to scope, integration strategy, automation permissions, or supported platforms require explicit approval. This plan does not authorize modifying an installed IDE, deleting caches, or publishing a release.

Related documents:
- [README and manual installation checks](../../../README.md)
- [Modern extension foundation specification](../specs/modern-extension-project.md)
- [Pinned upstream review and proposed adapter boundary](../specs/upstream-superpowers-review.md)
- [Repository coding instructions](../../../.github/copilot-instructions.md)

The foundation specification describes the existing scaffold. This roadmap governs the remaining product scope; planned capabilities must not be described as already implemented.

## 2. Confirmed requirements and decisions

| ID | Decision | Status |
| --- | --- | --- |
| D01 | Use C#, not Visual Basic; C# 12 and .NET 8 throughout except for a minimal in-process bridge that may target the framework required by supported public VSSDK contracts. | Confirmed by user; bridge exception approved |
| D02 | Retain modern out-of-process VisualStudio.Extensibility and Remote UI as the primary host; permit a minimal in-process VSSDK bridge only for capabilities unavailable through supported out-of-process contracts. | Hybrid architecture approved by user |
| D03 | Let users select Guided, Approval-required automation, or Full automation. All three are first-release scope. | Confirmed by user |
| D04 | Use supported Copilot APIs only for AI integration; do not introduce other AI providers or ask for their keys. | Confirmed by user |
| D05 | Ship with explicit prompt preview/copy and manual handoff when supported direct Copilot integration is unavailable. | Confirmed by user |
| D06 | Full automation may build, test, edit within agreed scope, and run explicitly allowlisted custom commands in a trusted workspace. | Confirmed by user |
| D07 | Support and test Visual Studio 2022 17.14+ and Visual Studio 2026 for the first release. | Confirmed by user |
| D08 | Complete and persist this implementation plan before further product coding. | Confirmed by user |
| D09 | Adapt canonical upstream Superpowers rather than recreate its methodology or skill format. | Direction approved by user |
| D10 | Bundle all existing upstream releases, including stable releases and prereleases, at each VSIX release cutoff. | Confirmed by user |
| D11 | Let users select bundled versions or download newer upstream releases independently; cache locally, preserve selection across VSIX upgrades and pin active runs. | Direction approved by user |
| D12 | Refactor is a product-specific composition over upstream skills, not a claimed canonical upstream skill. | Approved by user |
| D13 | Treat `TheKameleon.Superpowers.slnx` as the canonical solution filename. | Approved by user |
| D14 | Guided is the default execution mode, but users can change it by setting. | Approved by user |
| D15 | Persist metadata-only history by default, with a setting to retain more content. | Approved by user |
| D16 | Keep upstream `SKILL.md` files intact and layer Visual Studio metadata separately. | Approved by user |
| D17 | Keep most future implementation outside the VSIX project. The original five-project shape is extended by isolated Bridge.Contracts and InProcess projects for the approved hybrid boundary. | Approved by user; seven-project shape scaffolded |
| D18 | The proposed interfaces are acceptable design targets for implementation. | Approved by user |
| D19 | The in-process bridge must remain narrow, use supported public APIs only, exchange versioned serializable DTOs across an explicit process boundary, and contain no general service locator, arbitrary command execution, private API, or product workflow logic. | Approved by user |

Remaining detailed proposals move to later phases: exact release/tag inventory rules, latest semantics, prerelease/update filtering, cache retention and update cadence, parser selection, discovery paths and unsupported-step presentation. D10/D11 supersede the single-version bundle and VSIX-only skill update proposal. Each catalog version remains pinned individually. The prior JSON-in-Markdown format and independent methodology engine are superseded, not implemented. Approval of direction is not evidence of runtime compatibility or completion.

## 3. Current baseline: what has actually been done

| ID | Existing work | Evidence and limits |
| --- | --- | --- |
| B01 | [x] Seven-project solution shape exists. | Core, Skills, VSIX, Tests, IntegrationTests, Bridge.Contracts and InProcess are in the canonical solution. The bridge projects are scaffolds only; no VSSDK package or IPC transport is implemented. |
| B02 | [x] Align primary projects with C#/.NET 8 and isolate the bridge exception. | Portable product projects use `net8.0`; VSIX and integration tests use `net8.0-windows8.0`; Bridge.Contracts uses `netstandard2.0`; InProcess uses `net472` pending host compatibility proof. |
| B03 | [x] Replace the traditional host with modern out-of-process registration. | [VSIX project](../../../TheKameleon.Superpowers.Vsix/TheKameleon.Superpowers.Vsix.csproj), [extension entry point](../../../TheKameleon.Superpowers.Vsix/SuperpowersExtension.cs); SDK/Build 17.14.40608. |
| B04 | [x] Add the Plan command and submenu. | [PlanCommand.cs](../../../TheKameleon.Superpowers.Vsix/PlanCommand.cs); Extensions > TheKameleon Superpowers > Plan. It opens a window only. |
| B05 | [x] Add a minimal Remote UI status window. | [Window](../../../TheKameleon.Superpowers.Vsix/SuperpowersToolWindow.cs), [control](../../../TheKameleon.Superpowers.Vsix/SuperpowersToolWindowControl.cs), [view](../../../TheKameleon.Superpowers.Vsix/SuperpowersToolWindowControl.xaml). No planning workflow or context capture yet. |
| B06 | [x] Add package regression tests. | [ExtensionPackageTests](../../../TheKameleon.Superpowers.IntegrationTests/ExtensionPackageTests.cs) and [PlanToolWindowPackageTests](../../../TheKameleon.Superpowers.IntegrationTests/PlanToolWindowPackageTests.cs). Last recorded run: 13 package tests passed on .NET 8.0.31, plus one empty unit test. This is not behavioral workflow coverage. |
| B07 | [x] Package localized labels and a versioned update. | [Resources](../../../TheKameleon.Superpowers.Vsix/.vsextension/string-resources.json); project version 1.0.1 produces VSIX/assembly 1.0.1.0. Build and version/resource tests passed. |
| B08 | [x] Receive user confirmation that an earlier package appears in the extension list. | Registration only; not proof that the latest update resolves localization or that all runtime behavior works. |
| B09 | [ ] Verify menu labels after installing 1.0.1.0. | The reported instance had command metadata cached without its resource dictionary. The versioned update has not been confirmed as resolving the visible placeholders. |
| B10 | [ ] Verify runtime behavior on the supported IDE matrix. | Activation, repeat invocation, close/reopen, no-solution operation, accessibility, themes and both IDE versions remain acceptance checks. |
| B11 | [x] Review pinned upstream Superpowers and the VS Code adapter. | [Review specification](../specs/upstream-superpowers-review.md): source format, licenses, skill mapping, installer/hook risks and proposed adapter boundary. No upstream skills are yet packaged or integrated. |

The working tree contains the approved solution rename: `TheKameleon.Superpowers.slnx` is canonical, and the earlier `TheKameleon.Superpowers.Vsix.slnx` is deleted. Preserve this rename and do not create a second solution. No commits are implied by this checklist.

## 4. Requirements-to-phase map

| Requirement | Delivery phase(s) | Current status |
| --- | --- | --- |
| Eight entry points over upstream skills: Plan, Execute, Debug, TDD, Review, Verify, Refactor, Finish | P03, P04, P07, P08 | Not implemented; Plan command shell only; Refactor is an approved adapter composition |
| Solution/project/active file/open documents/selection context | P01, P05 | Not implemented |
| Build errors, compiler warnings and test failures | P01, P05, P06 | Not implemented |
| Git branch, status and recent commits | P05 | Not implemented |
| Context automatically included in prompts, with preview/privacy controls | P05, P07 | Not implemented |
| Skill list, context overview, workflow status, prompts and history in tool window | P07 | Status-only view implemented |
| All eight Extensions menu commands | P08, P09 | Plan command only |
| Solution/project/file/class/method context-menu actions | P01, P09 | Not implemented; SDK feasibility unverified |
| Multi-step execution, state, orchestration and resume | P02, P04, P06 | Not implemented |
| All-release catalog (stable/prerelease), local selection, optional future downloads and external skills | P02, P03, P07, P11 | Approved direction; not implemented |
| Selectable Guided/Approval-required/Full automation | P02, P04, P06, P07 | Not implemented |
| Supported Copilot handoff or visible preview/copy fallback | P01, P06, P07 | Not implemented |
| Evidence-backed verification and completion | P04, P08, P10 | Not implemented |
| Public, accessible, contributor-friendly VSIX | P10, P11 | Packaging scaffold only |

## 5. Target architecture

### Project responsibilities

| Project | Target and responsibility | Permitted project dependencies |
| --- | --- | --- |
| `TheKameleon.Superpowers.Core` | `net8.0`; primary implementation project. Owns contracts, adapter run/task tracking, policy, evidence, prompt composition, context models, orchestration and history so most product logic stays outside the VSIX. Upstream owns development methodology. | None |
| `TheKameleon.Superpowers.Skills` | `net8.0`; upstream Markdown/YAML discovery, bounded parsing, provenance, dependency assets, release catalog/caching and separate adapter metadata. Keeps upstream content/version logic outside the VSIX. | Core |
| `TheKameleon.Superpowers.Vsix` | `net8.0-windows8.0`; thin Visual Studio shell only: SDK contributions, Remote UI, dependency injection, IDE/Roslyn adapters, supported Copilot bridge and host-specific collectors that cannot be portable. | Core, Skills as needed |
| `TheKameleon.Superpowers.Tests` | `net8.0`; deterministic behavioral tests with fake clocks, storage and host capabilities. | Portable projects |
| `TheKameleon.Superpowers.IntegrationTests` | `net8.0-windows8.0`; packaging, cross-component and Windows integration checks. Explicitly distinguish tests requiring a VS host. | Projects under test; build-only VSIX reference for packaging |
| `TheKameleon.Superpowers.Bridge.Contracts` | `netstandard2.0`; shared protocol version and future versioned serializable DTOs only. Contains no Visual Studio SDK types or product logic. | None |
| `TheKameleon.Superpowers.InProcess` | `net472`; minimal traditional VSSDK bridge scaffold. Future supported host adapters and IPC endpoints only; no product logic. The target remains subject to VS 2022/2026 host proof. | Bridge.Contracts only |

Keep dependency arrows one-way. Do not move Visual Studio APIs into portable projects. The approved in-process bridge is an exception only where the out-of-process SDK lacks a required supported capability; it must communicate through narrow versioned DTO contracts and must not become a second workflow host. Future implementation should default to Core first, then Skills for upstream/release concerns, and remain host-specific only when required by supported Visual Studio APIs.

Approved design targets include `ISkillCatalog`, `IAdapterRunCoordinator`, `IContextProvider`, `IWorkflowStore`, `IPromptComposer`, `IActionRunner`, `IApprovalService`, `IEvidenceCollector`, and `ICopilotBridge`. These are not existing code or promises about Microsoft API names. Separate read-only context capture from authorized actions; do not implement an independent methodology engine or infer executable actions from prose.

Roslyn should operate on the actual document/selection snapshot exposed by supported host APIs. If a necessary service is unavailable out of process, record a capability gap and seek approval before changing architecture; do not silently add legacy interop, COM automation or private APIs.

### Upstream content and adapter metadata proposal

- Use canonical `obra/superpowers` `skills/<name>/SKILL.md` files with their YAML front matter and Markdown intact. Preserve approved referenced assets, relative paths and upstream names; do not author eight replacement definitions.
- Bundle all published stable/prerelease versions at the VSIX catalog cutoff; pin each resolved commit and content hashes. Record per-version source, adapter compatibility and licenses before distributing copied content. The [review](../specs/upstream-superpowers-review.md) records reference commits, not the complete release inventory.
- Keep Visual Studio aliases, context needs, capability requirements and the Refactor composition in separate versioned adapter metadata. P02/P03 specify its schema and parser; upstream prose is not an executable step graph.
- Discover packaged defaults and opt-in user/trusted-workspace skills in the upstream format. Finalize paths, precedence and explicit override approval in P03; repository content cannot replace built-ins or grant trust silently.
- Load local data only with bounded parsing and reference/path checks. Separate explicit approved downloads from discovery; no execution of installers, hooks, helpers or commands and no rewriting of Copilot instruction files.
- Offer bundled/cached releases or opt-in future downloads with prerelease and tested/untested/incompatible labels. Resolve latest to an exact confirmed version; no silent tracking or activation. Preserve the selected version across VSIX upgrades.
- Stage and validate downloads before atomic activation; enforce source/redirect, size, archive-path/link, metadata, dependency and license checks. Cache user-locally outside the VSIX/repository, retain rollback versions and protect active-run content. Offline/failure paths preserve working local versions; hashes alone do not authenticate the publisher.
- Reload atomically without rebuilding; active runs pin source and adapter hashes. Validate updates and dependency changes explicitly. Future release availability does not guarantee compatibility.
- Report invalid files, unresolved references and unsupported tools/helpers visibly. Keep unrelated valid skills usable without silently ignoring a selected override.
- Arbitrary community DLL loading remains outside first-release scope. A source bundle does not authorize the tools described in it.

## 6. Execution modes and permission model

Proposed default: **Guided**. The selected mode and effective capabilities must always be visible. Mode selection is not authorization to access secrets, alter unrelated files or transmit context.

| Operation | Guided | Approval-required automation | Full automation |
| --- | --- | --- | --- |
| Capture permitted context and compose/preview prompts | User initiates capture; show omissions/redaction | Same | Same |
| Handoff to supported Copilot API or clipboard | Explicit user action | Explicit approval | Allowed only within a pre-authorized run/data scope; otherwise pause |
| Build/test | Provide instructions; record imported/user evidence distinctly | Preview action and approve before execution | Run within trusted workspace and approved configuration |
| Apply an edit | User applies it; capture resulting evidence | Preview exact diff/scope and approve | Only supported API/patch action inside approved scope; enforce snapshot checks |
| Run custom command | Explain it; do not execute automatically | Require allowlist entry and approval | Require explicit allowlist entry within run policy |
| Unsupported AI step | Pause for manual Copilot handoff/result | Same | Same; do not pretend it executed automatically |
| Git commit/push, release, destructive cleanup or external deployment | Outside initial automated action scope | Outside initial automated action scope | Not implicitly permitted; requires separate future scope/permission design |

Full automation is not unrestricted shell execution and is not a promise that every skill can finish unattended on every IDE. All modes must work with capability reporting; unsupported steps enter an explicit waiting state and explain what the user must do. Modes constrain extension-owned actions, not a separate manual Copilot session. Upstream instructions cannot expand authorization; Git mutations and unsupported subagents/helpers remain manual or blocked with visible limitations.

- Trust approval is scoped to a workspace and invalidated when its relevant identity changes. Repository-provided allowlists and skill updates cannot grant their own trust.
- An approval binds to the exact operation, arguments, working directory, target files, context snapshot and skill version. Revalidate before execution; changed inputs require a new approval.
- Custom-command policy uses a resolved executable, constrained argument definitions, trusted working directory, timeout and bounded output. Do not interpolate model text into a shell command line or grant a wildcard shell allowlist.
- Builds, tests and approved scripts can execute repository code. Explain this at trust time; an allowlist is not an OS sandbox. Reject implicit privilege escalation and enforce the product's operation restrictions even when evaluating custom commands.
- Show proposed edits/diffs, reject traversal/out-of-workspace writes, account for symlinks and unsaved buffers, and detect concurrent changes before applying anything.
- Cancellation stops further scheduling, cancels cooperative SDK work and terminates only owned subprocesses when necessary. Report already-completed side effects; cancellation is not a rollback guarantee.
- Recovery must not overwrite user changes, reset a branch, silently stash files, or discard dirty buffers. A rollback may restore only known tool-owned changes after conflict checks and explicit consent.

## 7. Context, state, evidence and privacy contracts

### Context snapshot

Capture on workflow invocation and refresh at meaningful boundaries, not by continuously scraping the IDE. Each snapshot records capture time, solution/repository identity, document version, selected target, collector status, omissions and truncation.

Required collectors:
- Current solution and project; invocation target takes precedence over incidental focus.
- Active document, open document metadata and selected code. Handle unsaved/untitled files, linked files, multiple owning projects and no open solution explicitly.
- Build errors and compiler warnings, scoped to project/configuration/build and source location. Distinguish current live diagnostics from older build output.
- Test failures and run summary: test IDs, outcomes, counts, timestamps and associated build. No recent run or zero discovered tests is not a passing result.
- Git branch, bounded working-tree status and recent commit metadata. Handle missing Git, non-repositories, detached HEAD, worktrees, paths with spaces and large histories; do not run fetch/pull or repository hooks to collect context.

Collector results must distinguish available/empty from unavailable, partial, timed out, denied and stale. Automatically include relevant, permitted context in prompts. Show unavailable fields rather than inventing values. Use documented, configurable finite size/time limits and cancellation; finalize those limits in the context specification before coding collectors.

### Adapter run tracking and handoff

Model adapter states such as Created, Ready, Running, AwaitingApproval, AwaitingExternalAction, Paused, Completed, Failed and Cancelled. Keep completion separate from verification outcome. Persist accepted task identity, attempts, results, timestamps, authorized actions, capabilities and evidence references. Do not convert upstream prose into an independently enforced methodology state machine or claim to observe unintegrated Copilot activity.

- No advance past a mandatory gate without the required evidence.
- Retries are bounded and explicit. Do not reapply a non-idempotent edit merely because an acknowledgment was lost.
- Resume validates workspace, skill hash, document/build state and policy. It cannot silently reuse obsolete approvals.
- Define stable task IDs for generated plans and their implementation tasks.
- Support Plan -> Generate Tasks -> Generate Tests -> Implement -> Review -> Verify through upstream skill selection, accepted task tracking and handoff. Validate adapter compositions for missing references/cycles; do not replace upstream instructions with hard-coded stages.
- Persistence is local, versioned and atomic, with corrupt-data recovery and schema migration behavior. A database is not required for the first release.
- Proposed storage: user-local application data partitioned by a non-reversible workspace identifier, not committed into the repository. Store metadata by default; storing prompts/code is explicit opt-in with retention and clear/export controls.

### Verification evidence

An evidence record includes source, timestamp, affected requirements/files, solution/project/build configuration, relevant snapshot or content identity, outcome and provenance. Distinguish tool-observed results from user-attested or imported results. Importing an AI response does not authenticate its claims.

Verify/Finish must check build success, test results, critical-warning policy, security-review status, requirement coverage and edge-case coverage. Define what qualifies as a critical warning in P02/P08; compiler warnings do not all have a universal critical severity. Stale, missing, unknown, skipped and failing results are not success. Any waiver must remain explicit in the final report and must not be labeled fully verified.

### Security and data boundaries

- Preview included context and exclusions before handoff. Redact likely credentials and sensitive paths where practical; redaction is best effort, not proof of absence.
- Apply repository/user exclusions before reading file content, not merely after generating a prompt. Keep `.env`, credentials and private keys out by default; specify and test the exact policy.
- Treat skill Markdown, source comments, diagnostic text and model output as untrusted data, never authority to expand permissions or bypass workflow gates.
- Use supported Copilot authentication/session mechanisms; do not collect Copilot tokens, scrape UI state, or install a hidden provider.
- Copying context to the clipboard is explicit and disclosed; do not silently auto-copy or later overwrite unrelated clipboard content.
- Do not log raw prompts, selected code, tokens or command secrets by default. Audit actions with sanitized metadata and provenance.
- Rate-limit/retry supported service calls with cancellation and visible failures; avoid runaway loops. No extra network service or telemetry backend is required for this release.

## 8. Ordered delivery phases and checklist

Complexity labels below are relative planning estimates, not date commitments. P00/P01 establish evidence needed to refine them. Each phase ends with tests, security review for changed behavior and updated documentation.

### P00 — Close foundation verification gaps (Small; runtime-dependent)

**Prerequisite:** review this roadmap. **Files:** existing VSIX, package tests, README and foundation specification.

- [x] P00.01 Close remaining detailed roadmap/adapter proposals before further product coding. Upstream reuse, the stable/prerelease catalog with optional future downloads, the Refactor composition, default mode/retention settings, intact `SKILL.md` layering, the original five-project split and the proposed interfaces were approved (D09–D18); D19 later adds the isolated bridge projects.
- [x] P00.02 Confirm the intended solution filename and preserve the existing rename when committing repository work.
- [x] P00.03 Install the versioned update in an experimental instance and record installed version plus resolved submenu/Plan labels.
- [x] P00.04 Verify window activation, repeat invocation, close/reopen and no-solution behavior.
- [x] P00.05 Record baseline results on VS 2022 17.14 and VS 2026, including edition, architecture, version and reproduction steps for failures. VS2022 F5 runtime checks are user-confirmed; the standalone installer failure remains open and reproduced below.

**Exit:** foundation labels and behavior are verified, or a reproduced blocking defect has a separately approved fix. Do not call the localization incident resolved solely because the version test passes.

### P01 — Prove host capabilities and freeze integration contracts (Large; highest uncertainty)

**Depends on:** P00 and roadmap approval. **Artifacts:** proposed `docs/superpowers/specs/host-capabilities.md` and capability matrix; spike code only after approval.

- [x] P01.01 Map supported public APIs for document/selection/project context on both IDE versions. Probe A's active-editor, no-editor, no-solution and file/project/solution-root selection scenarios are user-verified on VS 2022 and VS 2026; timestamped evidence is recorded in `docs/superpowers/specs/host-capabilities.md`. The final VS 2022 screenshot at `2026-09-21T03:38:44.9281580+00:00` confirms no active editor, zero documents, successful queries returning zero projects and zero solutions, and an unavailable solution path. Selected-path capture reports a handled `UriFormatException` for solution-root/no-selection contexts on both IDEs; this is a mapped limitation, not successful path acquisition. Completion covers Probe A's context-mapping scope, not text-selection contents/range, editor edits, closed-document lookup or bridge semantics.
- [ ] P01.02 Prove diagnostics, build invocation and test-result/run access with minimal host experiments. Extension-owned `TKSPROBE001` publish/clear and selected Core-project build invocation are user-verified in VS 2022 and VS 2026. The API task completed on both IDEs; separate Build output reported one succeeded and zero failed on VS 2022, and zero failures with one up-to-date project on VS 2026. The task itself still provides no outcome. Installed `Microsoft.VisualStudio.TestWindow.Interfaces.dll` documents external `ITestsService`/`ITest`/`IResult` APIs, but no supported out-of-process broker/accessor exposes them. A minimal supported in-process bridge is approved for investigation; compiler-diagnostic reading, test-result/run acquisition and bridge transport/lifecycle proof remain.
- [x] P01.03 Investigate supported Visual Studio Copilot handoff, response, edit and upstream-tool/subagent capabilities separately. The referenced Extensibility SDK and local NuGet cache expose no supported Copilot contract package. Installed product-private assemblies document responder/session/agent types, including `UnstableInternalApi`, but are not approved extension dependencies. Use preview/copy/manual handoff and manual result import; do not reference those DLLs or treat CLI plugin support as Visual Studio API evidence.
- [x] P01.04 Prove solution/project/file/class/method menu placement and semantic target resolution, including C# Roslyn access. The in-process probe has menu placement/invocation evidence and screenshot-verified C# class/method resolution on both IDE versions. The user confirmed non-C# and top-level using-directive negative checks on both IDEs, then confirmed the selected-node identity verification worked in VS 2026 and VS 2022. Completion covers the read-only targeting probe, including selected solution/project/file identities; it does not establish production workflow integration or IPC. Linked-file ambiguity, stale text and cancellation have automated coverage, not equivalent host evidence. Detailed evidence and limits are recorded below.
- [x] P01.05 Publish the per-version capability/fallback matrix and contract constraints before shared API implementation. `docs/superpowers/specs/host-capabilities.md` records VS 2022/VS 2026 status, supported public contracts, observed runtime evidence, blockers and required fallbacks without treating installed implementation assemblies as supported APIs.
- [x] P01.06 Review architecture gaps with the user. The user approved a hybrid architecture with a minimal in-process VSSDK bridge and a narrow target-framework exception. This approval does not authorize private APIs or prove any bridge capability; each proposed API, transport and VS-version path still requires evidence before implementation claims.

**VS 2022 diagnostics checkpoint:** The user supplied Probe C output at
`2026-09-21T03:40:27.2253488+00:00` reporting successful publication, plus an Error
List entry for `TKSPROBE001` on `BridgeCapability.cs`, line 1. The subsequent output
at `2026-09-21T03:46:50.5456947+00:00` reports a successful clear call, and the user
confirmed removal. Publish/clear is now user-verified on both IDE families. This
is not evidence of reading compiler diagnostics. The separately reported `NU1702` concerns
framework negotiation on the integration tests' artifact-only bridge reference;
that reference now skips framework negotiation while retaining build ordering and
`ReferenceOutputAssembly="false"`, without suppressing warnings. VS 2022 MSBuild
solution restore/build passed with `/warnaserror:NU1702`, and all 26 integration
tests passed. No running extension was reloaded by this change.

**VS 2022 build checkpoint:** User-supplied Probe E output at
`2026-09-21T03:48:23.7917634+00:00` identifies `TheKameleon.Superpowers.Core.csproj`
and reports that the build invocation task completed, with outcome unavailable from
the API. Separate Build Output shows Core built in Debug Any CPU to its `net8.0`
DLL, with `1 succeeded, 0 failed, 0 up-to-date, 0 skipped` at 22:48 (0.602 seconds).
These are separate invocation and outcome evidence records, not an API success result.

**Bridge probe checkpoint:** The workspace now contains a separate in-process probe
VSIX with public VSSDK registration and Roslyn workspace adapters. Corrected two
P01.04 package defects: buttons parent package-owned groups rather than menus, and
`ProvideMenuResource` now matches the embedded `SuperpowersBridge.CTMENU` resource.
Added the missing solution-node probe alongside editor/project/item probes. The full
solution builds; all 26 integration tests pass, including five new menu/resource
regressions that failed before the corrections. No installation or runtime checks
were performed in this increment.

**Editor semantic probe checkpoint:** `CapabilityProbeCommands` now uses public
editor adapters/document factory services to capture C# text, path and caret, then
invokes `SemanticTargetResolver` without a blocking UI-thread wait. It reports a
signature and one-based declaration location, or unavailable/failure details.
Snapshot/solution changes, linked-path ambiguity, virtual space, oversized files
and unsupported contexts are rejected; shutdown cancellation suppresses late dialogs.
The full solution builds and all 37 tests pass (11 unit, 26 integration), including
three regressions observed failing before correction. Tests link the exact
host-independent resolver source under .NET 8 without loading the net472 host.

For the next experimental-instance check, use the newly built standalone
`TheKameleon.Superpowers.InProcess/bin/Debug/net472/TheKameleon.Superpowers.InProcess.vsix`.
In each supported IDE, right-click a class name in a C# file and select
`Superpowers: Probe Editor Context`; repeat inside a method. Record the signature,
path and declaration location, plus any unavailable/failure message. Check non-C#
and changed-context handling too. This package has not been installed or runtime
verified by this increment. Do not use the modern out-of-process `Probe Context`
command as evidence of this separate semantic probe.

**Bridge debug preparation:** `scripts/Prepare-BridgeDebug.ps1` provides an explicit
VS 2022/2026 pre-debug command, also usable through Visual Studio External Tools.
It discovers one IDE with vswhere and invokes that IDE's full-framework MSBuild to
build/deploy the bridge through VSSDK, with explicit instance ID and `Exp` only.
Normal builds default to `DeployExtension=false`. Deployment requires opt-in,
Debug configuration and Exp; unsafe direct SDK target invocations are rejected.
`-WhatIf` previews selection without building/deploying. No main-IDE modification,
process termination, cache cleanup or automatic IDE launch is part of this workflow.
Use it with target Exp closed before modern-extension F5, selecting the same IDE.
This meets the direct debug-preparation requirement, not a verified automatic F5
hook, debugger attachment, runtime activation or combined Marketplace installation.
See [README debug steps](../../../README.md#prepare-the-bridge-for-debugging).

Validation: solution build passed; 34 integration tests and 11 unit tests passed.
PowerShell parsing and `-WhatIf` discovery succeeded for VS 2026 instance
`c9c360fa` and VS 2022 instance `dcdb5f81`. Guard-only MSBuild checks rejected
normal-profile, missing opt-in, missing instance and Release configurations, and
accepted the explicit Debug/Exp configuration. These checks did not deploy any
files or launch an IDE. Subsequently the user confirmed the PowerShell deployment
worked and the context menu appeared in the VS 2026 Exp verification sequence.
This is deployment/menu-presence evidence, not class/method resolution proof.

**F5 deployment-hook increment:** The modern project imports
`build/BridgeDebugDeployment.targets` before the SDK's `DeploymentAssetsOutputGroup`.
The hook is gated to Debug IDE/non-design-time deployment queries, invokes the
existing preparation script using the explicit debug `DeployTargetInstanceId`, and
propagates failures. Normal builds/tests do not install the bridge. Blank/default
instance selection fails with guidance; script selection remains Exp-only. The
script now supports instance-only targeting as well as the manual version selector.
The hook logs `Superpowers F5 bridge preparation` for host verification. Explicit
deployment queries can invoke it too; this is not a general F5 event subscription.
Actual F5 callback ordering, up-to-date-project invocation and failure handling on
both IDEs still require observation. Manual PowerShell success must not be substituted
for that evidence. No combined Marketplace packaging or debugger attachment is claimed.

Noninteractive selection fix: removed overlapping mandatory PowerShell parameter
sets; missing selection now fails immediately with guidance instead of prompting.
All 14 deployment regression tests passed, including bounded instance-only,
version-only and missing-selection checks against the script preamble. The solution
build passed. An instance-only `-WhatIf` run and direct invocation of
`PrepareBridgeBeforeDebugDeployment` with `BridgeDebugDeploymentWhatIf=true`
both selected VS 2026 instance `c9c360fa`, profile Exp, without prompting.
These previews did not deploy files or launch an IDE and do not prove actual F5
callback ordering or invocation for an up-to-date project.

Subsequent host feedback: the user reported that the VS 2026 F5 retry worked and
later confirmed the bridge context-menu item is visible in VS 2022 as well.
This is user-attested menu-presence evidence on both IDEs, not verification of all
four menu placements or semantic probe results. The VS 2022 report followed manual
preparation guidance and does not establish which deployment path succeeded;
automatic F5 deployment there remains unconfirmed. Up-to-date-project invocation
and failure handling remain unverified on both IDEs.

**F5 refresh regression — confirmed stale VS 2026 bridge:** The user reported the
new document compiler-diagnostics editor command missing after F5. Read-only
inspection found that all three bridge DLLs installed under `18.0_c9c360faExp`
lacked `DocumentCompilerDiagnostics`, while the built DLL contained it. The newest
installed copy was modified at 01:32:29 on 2026-09-21; the built copy at 01:40:20.
Build/Deploy Output for the 01:42 launch shows only the modern VSIX deployment and
no bridge-preparation message. This supersedes any interpretation of earlier menu
visibility as proof of reliable F5 refresh. The missing new command is consistent
with the confirmed stale bridge, not missing VSCT source registration.

The installed SDK defines `DeploymentAssetsOutputGroup` as an output query; the
inspected SDK targets/settings did not establish a guaranteed public pre-launch
callback for this debugger. Its debug profile uses `RootSuffix`, whereas the current
hook checks `VSSDKTargetPlatformRegRootSuffix`; a replacement must validate the actual
launch profile too. Three installed copies share the bridge extension ID; the active
registration was not established, so duplicate-registration causality is unproven.
No installations, cache changes, process termination or registry edits were made.
Automatic F5 refresh remains an open debugging-workflow defect. Do not work around
it by adding installation to ordinary builds or describe manual preparation as the
normal workflow. A supported launch/deployment integration path must be established
before claiming this requirement complete; the script remains an explicit fallback.

**Native F5 deployment correction:** Further tracing established that the installed
VSIX project-system deploy provider reads `TargetVsixContainer` and deploys the VSIX
directly; it does not query the old hook target. The canonical solution marked only
the modern project for Deploy. The bridge is now a build-only dependency of the
modern VSIX and has `<Deploy Solution="Debug|*" />` in the solution. Both are deployed
by the native VSIX provider during F5; no custom/private API dependency was added.
The old output-query installer was replaced with native `VsixDeployOnDebug`
configuration. Inside the IDE, the bridge imports the modern `.csproj.user` target
selection while preserving its own debugger-profile settings. Explicit instance and
Exp checks gate bridge deployment; ordinary builds retain `DeployExtension=false`.
The bridge remains a separate package and the manual script is fallback only.

Validation: full solution build and all 77 tests passed, including modern-package
isolation and Debug-only solution-deployment regressions. Read-only MSBuild evaluation
selected `c9c360fa`/Exp with native deployment enabled for IDE Debug; Release, CLI
and a non-Exp profile disabled bridge deployment. `DeployExtension` remained false
in all evaluated cases. The solution Debug mapping parsed successfully. A subsequent
VS 2022/missing-instance guard check reported cancellation; its result is not claimed.
No installation, process termination or cache cleanup was performed. Reload the
solution once, confirm Deploy for both VSIX projects in Debug Configuration Manager,
then F5 and verify both native deployment entries plus the new diagnostic command.
Repeat without code changes and on VS 2022. Runtime F5 verification remains pending;
the earlier output-query-hook claims are superseded by this correction.

**Native F5 runtime follow-up:** After the solution reload and test instructions,
the user reported that the VS 2026 test worked. This is user-confirmed runtime
evidence, not an automated UI verification. The subsequent VS 2022 test reported
the context menu missing; VS 2022 menu activation remains unresolved.

The VS 2022 02:08 Build/Deploy Output records both VSIX projects deploying
successfully to `dcdb5f81`/Exp. The bridge was installed Enabled under
`17.0_dcdb5f81Exp/Extensions/1tm2qbmy.d2e`. Read-only inspection confirmed that its
DLL SHA-256 matches the current build and includes `DocumentCompilerDiagnostics`;
its pkgdef registers `SuperpowersBridge.CTMENU` for the expected package GUID.
Three older folders with the same bridge extension identity remain in this profile
and lack the new diagnostics symbol. Active registration was not established, so
duplicate-folder causality is unproven. No `ActivityLog.xml` was found in the
checked local/roaming profile paths, and VS 2022 was no longer running.

Requested diagnostic check: launch the already-deployed VS 2022 experimental instance
with `/RootSuffix Exp /Log`, open a C# editor and its context menu, and inspect the
resulting activity log for bridge registration/load failures. Also distinguish
all bridge commands missing from only the document diagnostics command missing.
Do not claim a code fix or prescribe manual bridge preparation/cache cleanup from
the current evidence. This investigation changed no product code, installations,
caches, registry settings or running processes.

**VS 2022 captured activity log:** The user captured the requested log. Read-only
inspection of the Exp profile's `ActivityLog.xml` (last modified 2026-09-21
02:14:57 local time) found two Error entries. Record 375, Extension Manager,
reports `Unable to load DLL 'PkgDefMgmt.dll' or one of its dependencies` with
`0x8007007E`, while updating the incompatible-extension registry list. Record 569
reports the same failure through `StreamJsonRpc.RemoteInvocationException` during
extension update checking. The VS 2022 IDE directory contains `PkgDefMgmt.dll`,
file version `17.0.37708.7`; the specific missing dependency/loading condition is
not established. Startup reports the pkgdef cache current. No bridge package-load
attempt or bridge-specific menu failure is recorded. A modern tool-window provider
`NotLocallyRegistered` warning does not establish an in-process bridge failure.

The Extension Manager errors are a diagnostic lead, not a proven cause of the
missing menu. The user subsequently confirmed that Manage Extensions lists the
bridge as enabled, displaying version `1.0.0.0`. This confirms discovery/enabled
state, not package activation or which installed copy supplies the menu. The user
confirmed `Probe Editor Context` is present but `Probe Document Compiler Diagnostics`
is missing. Native module enumeration did not list a Superpowers DLL; this alone
does not establish whether the managed assembly is loaded. The user then invoked
`Probe Editor Context` and left VS 2022 Exp open for inspection.

**VS 2022 stale active bridge confirmed:** Activity log records 689/690 show
successful `SuperpowersBridgePackage` loading. Read-only mapped-file inspection
using the public Windows process-query APIs identifies the active bridge under
`17.0_dcdb5f81Exp/Extensions/TheKameleon/Superpowers for Visual Studio - In-Process Bridge/1.0.0`.
Its DLL hash is `8E91606D5C75E3C1AA2DD7063A34AA393F9BA2B0DB89E2FBFAB4F6CB055D4D82`,
modified 01:32:45, and it lacks `DocumentCompilerDiagnostics`. The newer native
F5 deployment under `Extensions/1tm2qbmy.d2e` has hash
`7091AA42595A424BAABB8707068E185639FE3A3EAD0196E091EA3D96FA3CD522`, matches the
current build, and contains the diagnostics code. The older and newer copies
register the same package GUID/menu resource. Thus the loaded bridge is stale;
the earlier unrelated Extension Manager errors need not be assumed causal.

At this checkpoint the proposed action was: with VS 2022 Exp closed and explicit
approval, back up the three previously identified stale bridge folders outside
extension discovery, remove only those stale copies from this Exp profile,
refresh registration, then retest native F5 deployment and the diagnostics command
including a no-source-change launch. Revalidate folder identities and hashes before
moving anything. Preserve the current F5-deployed copy, normal IDE profiles and
VS 2026 installations. This is a one-time stale-installation correction, not a
return to routine manual bridge preparation. Approval/closed-instance confirmation
was unavailable; no product code, installation, cache, registry or process changes
were made. Runtime resolution is still pending.

**Approved cleanup and automatic build identity implemented:** The user approved
both the one-time cleanup and shared auto-incremented package/file versions with
loaded version/path display. This supersedes the approval-pending checkpoint above.

Before cleanup, process checks confirmed VS 2022 was closed. The exact bridge
extension identity and DLL hashes were revalidated for `qoqreane.o2s`,
`yyktcvac.4re`, and the old `TheKameleon/.../1.0.0` folder. Every file was copied
and hash-verified before removing only those three folders from VS 2022 Exp.
Backup and restore mapping are under
`.superpowers-build/backups/vs2022-exp-20260921-023729/restore-manifest.json`.
The retained `1tm2qbmy.d2e` DLL hash was unchanged; a later inventory found exactly
one bridge copy in this Exp profile. Normal profiles and VS 2026 installations
were not changed. No processes were terminated or caches manually deleted.

The explicit VS 2022 executable ran `/RootSuffix Exp /UpdateConfiguration /Log`
and exited 0. `registration-refresh.xml` beside the backup records discovery of
`1tm2qbmy.d2e` and successful loading of the bridge extension. It also records
other VS UI/template/service errors, including `0x800a006f`; these were preserved,
not repaired or assumed causal. This is registration evidence, not a menu UI test.

`build/SuperpowersBuildVersion.targets` now assigns a locked, atomically persisted
local revision from `.superpowers-build/revision.txt` during real bridge assembly
generation. The modern project consumes the dependency's configuration-specific
stamp. Both VSIX manifests and DLL file/informational versions share `1.0.1.N`
(with bounded revision rollover); binding versions remain bridge `1.0.0.0` and
modern `1.0.1.0`. The source bridge manifest is not rewritten: its intermediate
manifest is stamped. Modern package metadata uses generated compile-time constants
because the SDK evaluator rejects reflection (`CEE0005`). Runtime probe identity
uses the loaded assembly's file-version attribute and location via the linked
`build/BuildIdentity.cs`, displayed in bridge dialogs and modern probe results.

Design-time builds do not allocate a revision; their generated constants use a
separate file to avoid racing real builds. Clean retains the git-ignored local
counter, and failed builds may leave gaps. The counter is per checkout, not a
global CI/release allocator. Both extension projects disable fast up-to-date
skipping, so no-source-change F5 builds also get a new shared revision. Native
deployment remains unchanged, and ordinary builds/tests still do not install.

Validation: full solution build succeeded; all 52 integration tests and 35 unit
tests passed (87 total). Tests cover consecutive/shared revisions, concurrent
allocation, missing/invalid state, rollover, design-time exclusion and isolation,
runtime identity, VSIX/DLL version matching, and stable binding versions. Real
MSBuild design-time checks left counter 7 and real metadata source hashes unchanged.
Validated output packages/DLLs were `1.0.1.7`; `git diff --check` passed. Existing
bridge threading-analyzer warnings were observed and left outside this change.

Next runtime check: keep the explicit VS 2022 target and Exp profile, confirm
Deploy for both extension projects, then F5. Verify both deployment entries and
the same new build version, invoke `Probe Document Compiler Diagnostics` in a C#
editor, and confirm its Loaded build/Loaded DLL reflect the new deployment. Check
the modern probe's identity too. Close Exp and repeat F5 without source changes:
both packages and loaded probes must report the same newer revision. Repeat on
VS 2026 afterward. The new packages have been built/tested, not yet F5-deployed;
post-cleanup menu activation and the new version displays remain user runtime checks.

**VS 2022 build 1.0.1.8 frame failure:** The subsequent 06:06 F5 output confirms
both projects built/deployed successfully as `1.0.1.8`. Native bridge deployment
replaced `1tm2qbmy.d2e` with `Extensions/mewc52v2.zm2`; the modern package deployed
to `VSExtensions/TheKameleon/Superpowers for Visual Studio/1.0.1.8`. The user then
reported a Superpowers frame-construction `NullReferenceException` inside VS
`WindowFrame.ConstructContentFromRemoteAsync`. ActivityLog record 310 identifies
`RST:0:0:{27675d8b-d55a-a399-9b2a-5d3d96313939}`, caption `Superpowers`, during
startup. The deployed tool-window moniker matches its registered provider,
`TheKameleon.Superpowers.Vsix.GeneratedToolWindowProvider;1.0`. No provider-host
log was found for this failing session, unlike earlier sessions; absence of that
log alone does not prove that activation was never attempted.

Separately, records 70/71 report bridge UI-library error `0x800a006f`, specifically
`Resource not found: VSMenus.ctmenu`. Read-only inspection finds the deployed DLL
contains `SuperpowersBridge.CTMENU`, matching its pkgdef registration. Why VS asks
for a different name is not established. Other Microsoft packages also report
UI-resource failures, and the session's Extension Manager log again reports
`PkgDefMgmt.dll` dependency/load failure `0x8007007E`. Do not treat these as proven
causes of the remote-frame exception or hide them by speculative resource renaming.

Captured ActivityLog and relevant ServiceHub logs are preserved under
`.superpowers-build/diagnostics/vs2022-frame-20260921-0606/`. No product code,
installations, registry settings or caches were changed in this investigation;
no new build/revision or test run was needed for this documentation-only checkpoint.
The requested check was: in VS 2022 Exp, close only the failed Superpowers tab and explicitly
invoke Extensions > Superpowers > Plan. Report normal opening, the same exception,
or a missing menu, and leave Exp open for activation-log inspection. The subsequent
RPC evidence below shows that this alone did not force fresh content construction.
Do not reset the IDE profile, disable auto-creation or change stable binding
versions without evidence. Runtime resolution remains open.

**Explicit Plan activation reuses the failed frame:** The user reported the same
exception after the requested check. In session `D09E3481`, the command provider
records successful `ActivateCommandSetAsync` and `ExecuteCommandAsync` at 06:13:59.
The tool-window provider records only `SubscribeAsync`, two `OnHideAsync` calls
at 06:13:51, and successful `OnShowAsync` at 06:13:59. It records no incoming
`CreateToolWindowAsync` request and no provider exception. The ActivityLog frame
exception is still the startup event at 06:13:48 (record 308). Thus the command
redisplays the already-failed frame; it does not retry content construction.
The earlier inference that a repeated error ruled out saved-frame restoration was
incorrect. The original startup failure's cause is not yet established.

Live logs were captured using shared read-only access without stopping the host:
`.superpowers-build/diagnostics/vs2022-frame-20260921-0613/`. No product edits,
builds, installation changes or resets were performed. Local SDK XML searches did
not establish a public force-recreate API; do not invent private frame-reset calls.
The next requested test was: close the failed Superpowers tab, save work, then exit VS 2022 Exp while
that tab remains closed. Relaunch with F5, let startup finish, and invoke Plan.
Report separately whether the failure appears before invoking Plan or only after
it, and leave Exp open. This tests a fresh frame rather than another show/hide of
the failed object; success is not yet claimed. Keep the main VS 2026 IDE open.

**Close/restart did not clear the restored frame:** The user reports that the
Superpowers tab still opens at startup with the same exception, despite closing
it before stopping VS. Do not repeat that test or claim it created a fresh frame.
Proposed next diagnostic action: use the supported Window > Reset Window Layout
command in VS 2022 Exp only, then test explicit Plan activation. This discards
that instance's custom docking/tab layout and requires approval; it is not a
full profile reset, cache deletion, extension reinstall, or proven fix. Approval
was requested but unavailable, so no reset or product-code change was performed.
The original frame-construction failure remains unresolved.

**Approved Exp layout reset did not resolve the frame error:** The user approved
the scoped layout reset. The running target was verified by executable path and
`/rootSuffix exp` (PID 26196). The supported DTE `Window.ResetWindowLayout` command
was invoked through that process's exact running-object-table entry and returned;
no confirmation dialog remained. No other IDE, full profile, installation or cache
was reset. Diagnostic helpers are confined to the ignored `.superpowers-build`
directory, and no product code was changed or build revision consumed.

The live Extensions menu was labeled `TheKameleon Superpowers`, not `Superpowers`;
the guarded UI lookup stopped on the mismatch before using the observed label.
UI Automation then invoked its Plan command at `2026-09-21T11:33:15Z`, scoped to
the verified Exp process. The displayed frame still contained the same
`WindowFrame.ConstructContentFromRemoteAsync` NullReferenceException, verified
by reading its error text through UI Automation. Its automation ID was still
`RST:0:0:{27675d8b-d55a-a399-9b2a-5d3d96313939}`. Current provider logs record
`SubscribeAsync` and `OnShowAsync`, but no `CreateToolWindowAsync` request; the
ActivityLog frame error remains the startup event at `11:24:15Z`.

Read-only mapped-file inspection of the active provider host (PID 40720) identifies
the modern DLL under `VSExtensions/.../1.0.1.12`, not an older deployment folder.
This rules out simply assuming an old modern DLL, but does not establish which
shell registration/layout data caused the frame failure. Post-reset activity and
RPC snapshots are saved in
`.superpowers-build/diagnostics/vs2022-layout-reset-20260921-0633/`.
Do not repeat layout resets or claim recovery. Further investigation must focus
on effective host registration/frame activation before changing content code;
the menu-label discrepancy is evidence to check, not a proven cause. VS 2022 Exp
was left running; VS 2026 and installed extensions were untouched.

**VS 2022 semantic class checkpoint:** User-supplied screenshot at probe timestamp
`2026-09-21T06:11:45.9810213+00:00` shows `Invocation scope: Editor`,
`Kind: NamedType` and signature
`TheKameleon.Superpowers.IntegrationTests.BridgeDebugDeploymentTests`.
The file is `TheKameleon.Superpowers.IntegrationTests/BridgeDebugDeploymentTests.cs`
under the repository root; the declaration is line 6, column 1, with captured caret
offset 140. This verifies class semantic resolution through the in-process editor
probe on the user-identified VS 2022 host. The dialog explicitly reports a read-only
probe with no IPC.

**VS 2022 semantic method checkpoint:** User-supplied screenshot at probe timestamp
`2026-09-21T06:13:45.8206186+00:00` shows `Invocation scope: Editor`,
`Kind: Method` and signature
`TheKameleon.Superpowers.IntegrationTests.BridgeDebugDeploymentTests.OrdinaryBridgeBuildDefaultsToNoDeployment()`.
The file is the same `BridgeDebugDeploymentTests.cs`; the declaration is line 11,
column 5, with captured caret offset 350. The dialog reports a read-only probe with
no IPC. Together these screenshots verify class and method semantic resolution on
the user-identified VS 2022 host.

**VS 2026 semantic checkpoints:** User-supplied screenshots identify the same file
and fully qualified class/method signatures as the VS 2022 checks above, with
`Invocation scope: Editor` in both dialogs:
- At `2026-09-21T06:14:49.4969518+00:00`, `Kind: NamedType` resolves
  `BridgeDebugDeploymentTests` at line 6, column 1, with captured caret offset 144.
- At `2026-09-21T06:15:17.6480301+00:00`, `Kind: Method` resolves
  `OrdinaryBridgeBuildDefaultsToNoDeployment()` at line 11, column 5, with captured
  caret offset 366.

Both dialogs explicitly report a read-only probe with no IPC. Class and method
semantic resolution are now screenshot-verified on both user-identified IDE families.
Negative cases and the remaining menu placements still require evidence; P01.04
remains open.

**VS 2026 Solution Explorer menu checkpoint:** User-supplied screenshots following
the project/file/solution context-menu checks show successful probe invocation:
- `2026-09-21T06:16:35.1250706+00:00`: `Invocation scope: Solution`.
- `2026-09-21T06:17:03.2032307+00:00`: `Invocation scope: Project`.
- `2026-09-21T06:17:28.8450748+00:00`: `Invocation scope: Item`.

All three dialogs report `CompilerDiagnostics`, `SemanticTarget` and
`ContextMenuPlacement` as available. Together with the editor screenshots, these
provide user-supplied placement/invocation evidence for all four probe menus on
VS 2026. The capability summaries and command scope labels do not establish actual
compiler-diagnostic retrieval, selected-node identity or IPC behavior. Equivalent
Solution Explorer checks on VS 2022 were subsequently confirmed as described below.

**VS 2022 Solution Explorer menu checkpoint:** The user confirmed that all three
Solution, Project and Item probes worked the same as the VS 2026 checks above.
This is user-attested evidence, without additional screenshots or timestamps.
Together with the editor screenshots, all four probe menu placements/invocations
now have evidence on both IDE families. Capability availability is not evidence of
compiler-diagnostic retrieval, selected-node identity or IPC.

**Negative-context semantic checkpoint:** The user confirmed successful results
for both requested checks in VS 2022 and VS 2026: a non-C# editor context reports
unavailable without crashing, and a caret on a top-level C# `using` directive
reports no class/method target rather than the previously resolved symbol. This is
user-attested evidence without additional screenshots or timestamps. It does not
verify the separate changed-context-during-analysis, linked-file or shutdown cases.
At this checkpoint, project/item/solution selection-identity proof remained open;
the subsequent user confirmations below close that targeting gate.

**Selected-node host verification and P01.04 closure:** Following the selected-node
probe implementation and the supplied verification procedure, the user reported
"Works for VS2026" and then "Worked for VS2022". These are user-attested passes
for the selected-node verification sequence, without additional screenshots or
timestamps. The procedure checks solution/project/file names and paths, file-owning
project identity, and selection changes while another file remains active in the
editor. It also requests multiple-selection/folder checks wherever commands are
offered; the confirmations do not separately identify which unsupported-selection
menus were available, so no specific menu-availability claim is made for those cases.
Together with the earlier menu, semantic and negative-context evidence, this closes
P01.04 for the read-only targeting-probe scope on both IDE families. It does not
prove automatic deployment, compiler-diagnostic retrieval, IPC, or bridge lifecycle.

**P01.02 document compiler-diagnostic increment:** Added the editor context command
`Superpowers: Probe Document Compiler Diagnostics`. It reads public Roslyn
semantic-model diagnostics for the captured active C# document, off the UI thread,
without invoking the older solution-wide adapter. Unique document mapping, exact
snapshot text and unchanged workspace/editor context are required. Unavailable
contexts are not presented as zero diagnostics. The read-only dialog reports the
file, total non-hidden/non-suppressed source diagnostics and at most 10 entries
with severity, ID, message and one-based location. Messages are capped at 300
characters with explicit truncation. Analysis requests cooperative cancellation
after 30 seconds and on shutdown; existing single-operation and exception guards
are retained. No builds, file edits, analyzer execution, Error List publication or
IPC are performed by this command, and no new dependencies/contracts were added.

Validation: solution build passed; all 76 tests passed (35 unit, 41 integration).
Eight new reader tests cover CS1029/source location, successful zero diagnostics,
other-file exclusion, missing/stale/linked documents, cancellation, suppression and
output limits. The new command-placement regression was observed failing before
registration and passing afterward. These are automated results, not host evidence.
No IDE was deployed or launched. Refresh the bridge in each experimental IDE and
test a temporary `#error TKSPROBE_READ` directive in a scratch C# project file;
verify CS1029, message and location, remove the directive and verify it disappears.
Also check a clean document and non-C# unavailable handling. Restore manual edits.
P01.02 remains open for both-IDE diagnostic evidence, Test Window acquisition and
bridge transport/lifecycle proof.

P01.01's Probe A verification is complete with the limitations listed above. Remaining gates: P01.02 needs
actual compiler-diagnostic adapter invocation, test-result/run acquisition and
bridge transport/lifecycle proof on both IDEs. P01.04 is complete for the targeting
probe, with selected-node identity confirmed on both IDEs alongside the earlier
menu/semantic evidence. The standalone package is not yet an IPC-connected bridge
or proof of combined VSIX packaging. P01 as a whole remains open for P01.02; do not
treat the targeting results as evidence of diagnostic/test acquisition or IPC.

**Tests/evidence:** runnable host probes for supported paths; negative results for unavailable services, no editor, no tests and absent Copilot. **Exit:** every requested integration has a supported implementation path, an approved manual fallback, or an explicit blocker. Preview/copy is approved; silently dropping required context menus is not.

### P02 — Establish portable contracts and configuration (Medium)

**Depends on:** P01. **Projects:** Core, Skills, Tests; project references added deliberately.

- [ ] P02.01 Specify release catalog/selection/provenance/compatibility, separate adapter metadata, context, run, evidence, capability and action/result models without redefining the upstream skill format.
- [ ] P02.02 Define execution-mode, workspace-trust, approval and command-allowlist contracts.
- [ ] P02.03 Define validated settings for exclusions, budgets, history/cache retention, release filters/update checks, mode defaults and critical-warning policy.
- [ ] P02.04 Add intended project references and remove only superseded empty scaffolds.
- [ ] P02.05 Add contract and serialization tests using the existing test namespaces/folder conventions.

**Tests:** required/optional values, immutable snapshots, invalid settings, unknown enum/schema versions and round trips. **Exit:** contracts compile across all .NET 8 targets, no circular references, and no IDE dependency leaks into portable code.

### P03 — Package release catalog and load upstream Superpowers content (Large)

**Depends on:** P02. **Projects/files:** Skills, versioned release bundles/catalog, adapter metadata, Tests, VSIX packaging and third-party notices; exact layout specified before coding.

- [ ] P03.01 Verify the published release/tag inventory and specify parser, catalog cutoff, latest/filter semantics, compatibility, separate adapter manifest, dependency closure and per-release notices.
- [ ] P03.02 Implement bounded data-only parsing and adapter/reference validation without interpreting prose as executable commands.
- [ ] P03.03 Implement packaged/user/solution discovery with explicit trust, approved paths, precedence and override diagnostics.
- [ ] P03.04 Implement atomic reload with pinned active-run hashes and selected-version persistence across VSIX upgrades.
- [ ] P03.05 Package all published stable/prerelease bundles at the cutoff with unchanged skills/assets, per-version licenses/source hashes and separate Plan/platform metadata; measure package size.
- [ ] P03.06 Add loader, provenance/license/integrity, dependency-closure and real-directory/package tests.
- [ ] P03.07 Implement approved-source release discovery with bounded, cancellable requests and offline/rate-limit diagnostics.
- [ ] P03.08 Implement user-approved downloads with staged safe extraction, source/metadata/license validation and atomic activation; failures preserve the working selection.
- [ ] P03.09 Implement versioned local caching and rollback with active-run protection and explicit retention controls.
- [ ] P03.10 Test catalog completeness, prereleases, offline selection, corrupt/malicious/cancelled downloads, incompatible versions, rollback and upgrade selection preservation.

**Tests:** upstream and custom Markdown/YAML, Unicode, missing/empty/malformed/oversized files, duplicate IDs, unsupported adapter schemas, traversal/symlinks, unsafe YAML constructs, missing referenced assets, invalid encoding, reload races, composition cycles and attempted execution. Assert source hashes, attribution and no loader network/process/instruction-file mutations. **Exit:** trusted custom skills load without recompilation; pinned upstream content is attributable and intact. P08 wires remaining entry points, not replacement skill definitions.

### P04 — Implement adapter run tracking, handoff and history (Large)

**Depends on:** P02/P03. **Projects:** Core, Skills, Tests, IntegrationTests.

- [ ] P04.01 Implement explicit adapter run/task states and evidence gates, separate from upstream instructional prose.
- [ ] P04.02 Implement upstream skill selection/composition, accepted plan task IDs and capability-aware handoff/waiting without fabricating agent actions.
- [ ] P04.03 Implement pause/cancel/retry semantics and protection against duplicate side effects.
- [ ] P04.04 Implement local versioned persistence with atomic writes and recovery.
- [ ] P04.05 Implement safe resume with snapshot/skill/policy validation.
- [ ] P04.06 Implement metadata history query/export/delete and opt-in sensitive-content retention.
- [ ] P04.07 Add behavioral state-machine and persistence tests.

**Tests:** every valid/invalid transition, approval denial, unsupported action, stale evidence, cancellation at boundaries, interrupted writes, corrupt/future-version state, reopened solution and duplicate retry. **Exit:** a multi-step workflow survives restart without falsely completing or replaying unauthorized actions.

### P05 — Implement complete context capture and privacy (Large)

**Depends on:** P01/P02; may proceed alongside P03/P04 after contracts stabilize. **Projects:** Core context models, VSIX host adapters, Tests, IntegrationTests.

- [ ] P05.01 Implement solution/project/document/open-document collectors using supported host adapters.
- [ ] P05.02 Implement selection and semantic target snapshots, including unsaved document versions.
- [ ] P05.03 Implement scoped build-error and compiler-warning collection.
- [ ] P05.04 Implement test-failure/run-summary collection with explicit unavailable/stale results.
- [ ] P05.05 Implement bounded Git branch/status/recent-commit collection without network or repository mutation.
- [ ] P05.06 Implement exclusions, redaction, context budgets and visible capture diagnostics before prompt inclusion.
- [ ] P05.07 Add collector, privacy and host-availability tests.

**Tests:** no solution/project/editor/Git/tests, empty selection, binary/large files, dirty buffers, linked projects, permission errors, process/API timeout, detached HEAD/worktrees, secret canaries and deterministic truncation. **Exit:** all required context fields are represented with provenance and honest status, without unapproved disclosure or an unresponsive UI.

### P06 — Implement three-mode execution and Copilot handoff (Large)

**Depends on:** P01/P04/P05. **Projects:** Core policy/actions, VSIX host/Copilot adapters, Tests, IntegrationTests.

- [ ] P06.01 Implement the shared capability/permission policy used by all three modes.
- [ ] P06.02 Implement exact-action approval, rejection, revocation and trust prompts.
- [ ] P06.03 Implement supported build/test runners with scoped evidence, timeouts and cancellation.
- [ ] P06.04 Implement scoped edit application with preimage checks, diff preview and conflict-safe recovery where APIs support it.
- [ ] P06.05 Implement explicitly allowlisted custom-command execution with safe arguments and bounded output.
- [ ] P06.06 Implement the supported Copilot bridge only for capabilities proven in P01.
- [ ] P06.07 Implement explicit preview/copy/manual-result handoff when bridge capabilities are absent.
- [ ] P06.08 Add policy, execution and fallback tests across Guided, Approval-required and Full modes.

**Tests:** allow/deny for every operation/mode, no network provider fallback, missing Copilot, stale approval, trust revoked mid-run, prompt-injected commands, scope escape, concurrent edits, hung child process and action failure. **Exit:** all modes are selectable and truthful; Full cannot bypass policy or claim unsupported AI actions completed. No Git commits/pushes occur implicitly.

### P07 — Deliver the real tool window and Plan workflow (Large)

**Depends on:** P03–P06. **Projects:** VSIX Remote UI/view models, Core prompt composition, upstream brainstorming/writing-plans content and adapter metadata, tests.

- [ ] P07.01 Add reusable Remote UI/view-model components for skill selection and effective execution mode.
- [ ] P07.02 Add context summary, exclusions/redaction preview and capability diagnostics.
- [ ] P07.03 Add workflow progress and start/next/pause/cancel/retry/resume controls with valid-state enablement.
- [ ] P07.04 Add generated prompt preview/edit/copy and handoff status without discarding user edits.
- [ ] P07.05 Add searchable execution history and evidence details with retention/export/delete controls.
- [ ] P07.06 Wire Plan to upstream brainstorming/writing-plans with captured context, design approval, visible platform guidance and accepted plan artifacts.
- [ ] P07.07 Add the end-to-end Plan test and accessible UI/host checks in both IDE versions.
- [ ] P07.08 Add first-use and subsequent version selection for bundled/cached/latest/chosen downloads, prerelease and compatibility labels, confirmation, progress/errors and rollback.

**Plan output:** requirements, impacted files, risks/dependencies, ordered stable-ID tasks, testing strategy and explained complexity estimate. Unread/unavailable context must not become invented facts. **Exit:** Plan is a real multi-step workflow, works with no solution through explicit manual input, and distinguishes generated prompts from returned/accepted planning artifacts.

### P08 — Wire the other seven entry points to upstream skills (Large)

**Depends on:** P07. **Projects:** upstream content references, separate adapter mappings/gates, VSIX bindings, behavioral tests. Preserve upstream methodology rather than authoring parallel definitions.

- [ ] P08.01 Wire Execute to executing-plans and optionally supported/chosen subagent-driven-development with accepted task/evidence tracking.
- [ ] P08.02 Wire Debug to systematic-debugging with diagnostic context and diagnosis before permitted fixes.
- [ ] P08.03 Wire TDD to test-driven-development with observed red/green evidence.
- [ ] P08.04 Wire Review to requesting/receiving-code-review with findings, disposition and explicit independent-review capability limits.
- [ ] P08.05 Wire Verify to verification-before-completion with scoped evidence provenance and product completion criteria.
- [ ] P08.06 Implement the approved, clearly labeled Refactor composition using upstream planning/TDD/review/verification and a behavior baseline.
- [ ] P08.07 Wire Finish to finishing-a-development-branch with truthful readiness reporting and manual-only Git/integration actions outside policy.
- [ ] P08.08 Add parameterized happy-path, edge and failure tests for every entry-point mapping/mode, including unsupported tools and manual handoff.

**Exit:** all eight entry points select attributable upstream content or the approved Refactor composition; adapter tracking supports the requested plan/test/implement/review/verify chain without bypassing gates or claiming unavailable automation. See section 9 for product acceptance rules and the review specification for exact source mappings.

### P09 — Complete commands and context menus (Medium; SDK-dependent)

**Depends on:** P01/P07/P08. **Projects:** VSIX contributions, context targeting, localization, package/host tests.

- [ ] P09.01 Add Execute, Debug, Review, Verify, TDD, Refactor and Finish beside Plan under the existing submenu.
- [ ] P09.02 Add solution/project/file context actions with explicit target capture.
- [ ] P09.03 Add class/method context actions using proven semantic APIs and language eligibility rules.
- [ ] P09.04 Route all entry points through the same workflow/mode/policy path, not duplicated prompts.
- [ ] P09.05 Localize command/menu labels and validate resources plus fresh-install/update behavior.
- [ ] P09.06 Add all-contribution package tests and host target-selection tests.

**Tests:** no eligible selection, multiple projects, stale editor context, non-C# file, generated/read-only file, class overloads/method boundaries and changed focus after invocation. **Exit:** every requested context scope is supported on both IDE families, or a user-approved scope adjustment is documented before release. No silent replacement of a class/method target with an entire solution.

### P10 — Verify the complete system and harden failure behavior (Large)

**Depends on:** P08/P09. **Projects:** all components and test suites; full-product specifications.

- [ ] P10.01 Run a representative repository through Plan -> tasks -> tests -> implementation -> review -> verification in each mode.
- [ ] P10.02 Prove evidence invalidation after edits, branch/build changes, test reruns and skill updates.
- [ ] P10.03 Exercise failure recovery, interrupted/resumed workflows and denied/expired permissions.
- [ ] P10.04 Review privacy, prompt injection, path/argument handling, secret retention and dependency security.
- [ ] P10.05 Verify keyboard/screen-reader use, focus, resizing, themes, high contrast and long-output handling.
- [ ] P10.06 Measure responsiveness with large solutions/output; establish acceptance budgets before optimizing bottlenecks.
- [ ] P10.07 Run the supported IDE/architecture installation and activation matrix, including clean/update/uninstall scenarios.
- [ ] P10.08 Resolve or explicitly defer findings with severity, owner and rationale; do not waive Critical/High release blockers silently.

**Exit:** tests and host evidence support claimed behavior. Passing a package test or user-attested result cannot overwrite a failed observed verification. Arm64 is currently declared by the manifest; either validate that declaration or obtain approval to narrow it before release.

### P11 — Prepare and publish the public extension (Medium)

**Depends on:** P10 and explicit release approval. **Artifacts:** CI, documentation, package metadata and distribution materials.

- [ ] P11.01 Add Windows CI for restore/build/unit/integration/package validation and retained artifacts; keep host-dependent checks visibly separate.
- [ ] P11.02 Replace the empty unit test with meaningful coverage rather than counting it as product verification.
- [ ] P11.03 Complete user documentation for modes, trust, limitations, skills, privacy, evidence and troubleshooting.
- [ ] P11.04 Add contributor guidance for upstream-format skills, separate adapter metadata, reviewed content updates, architectural decisions, issue templates and a security-reporting policy.
- [ ] P11.05 Resolve repository MIT license placeholders and review dependency/upstream licenses; verify P03 attribution remains packaged. Add FluentAssertions/Moq only where useful and license-compatible.
- [ ] P11.06 Finalize publisher identity, icons, versioning, support range, release notes and any signing/certificate requirements without committing secrets.
- [ ] P11.07 Produce and validate the release VSIX on the declared compatibility matrix.
- [ ] P11.08 Obtain explicit publication approval and publish via the chosen Marketplace/release process.

**Exit:** a new contributor can build/test the repository and add a validated skill without rebuilding the extension for user skill changes. Release claims match tested capabilities; manual fallback is disclosed. No automatic publication is authorized by Full mode.

## 9. Product acceptance rules for upstream-backed entry points

Use the upstream mapping in the [review specification](../specs/upstream-superpowers-review.md). The table below describes product acceptance outcomes and platform safeguards, not eight replacement skill definitions or a fixed machine-readable sequence. Upstream source retains its methodology; any product-specific overlay is separately labeled. The chosen mode, capability matrix, trust, cancellation and evidence contracts govern extension-owned actions only. If a required upstream tool is unavailable, label the manual handoff or blocked result rather than claiming equivalent execution.

| Skill | Required ordered behavior | Completion/failure rules |
| --- | --- | --- |
| Plan | Analyze requirements -> identify impacted files -> identify risks/dependencies -> generate tasks -> recommend tests -> estimate complexity. | Wait for clarification where material facts are missing. Accept a structured plan artifact before Execute; sending a prompt is not plan completion. |
| Execute | Load accepted plan -> select eligible task -> refresh relevant context -> perform permitted actions/handoff -> record results -> review/verify task -> advance. | Dependencies and approvals gate advancement. Stop/report conflicts and failures; no silent task skipping or replay of edits on resume. |
| Debug | Capture symptoms -> generate multiple root-cause hypotheses -> rank likelihood -> explain confidence/evidence -> recommend/run permitted validation -> diagnose -> propose/apply permitted fix -> regression check. | No fix before diagnosis. Unknown/inconclusive remains explicit; confidence is reasoned qualitative support, not invented certainty. Use 3–5 hypotheses when the evidence supports them, without padding with implausible claims. |
| TDD | Analyze requirement -> create failing tests -> observe intended failure -> minimal implementation -> observe passing tests -> refactor -> rerun tests. | No green claim without results. Distinguish expected assertion failure from infrastructure/compile failure; already-passing tests do not establish red. |
| Review | Establish scope -> inspect changes/context -> identify findings -> classify Critical/High/Medium/Low -> recommend fixes/tests -> record disposition. | Cover bugs, security, performance, maintainability, architecture, missing tests and technical debt. Distinguish not-reviewed from no findings; no automatic approval of the agent's own claims. |
| Verify | Map requirements to evidence -> check build -> tests -> critical-warning policy -> security review -> requirements -> edge cases -> report outcome. | Each gate is passed, failed, unknown or explicitly waived with provenance. Missing/stale evidence blocks fully verified status. |
| Refactor | Explain purpose/benefits -> capture behavior/test baseline -> plan scoped changes -> execute permitted edits -> compare behavior -> rerun tests/review. | Preserve behavior; stop on unintended contract changes or failing regressions. Do not claim equivalence solely from a generated explanation. |
| Finish | Gather current plan/results -> check mandatory Verify/Review gates -> enumerate unresolved items/debt -> summarize changes/tests/requirements -> offer explicit next actions. | Cannot mark verified with unresolved mandatory gates. No automatic commit, push, merge, deletion or publication. Provide a truthful partial/blocked summary when needed. |

## 10. Test and evidence strategy

- Use existing xUnit projects, namespaces and naming conventions. Favor deterministic fakes for IDE/capability/clock/storage boundaries. Do not add libraries solely because they appeared in the vision statement; review useful FluentAssertions/Moq additions first.
- Portable unit coverage: upstream front matter, adapter schemas/compositions, discovery rules, run tracking, policy, mode transitions, prompt composition, evidence freshness and privacy transformations.
- Integration coverage: real upstream/custom skill directories, pinned hashes, license notices, referenced-asset closure, atomic persistence/recovery, Git fixture repositories, owned-process cancellation, deterministic action outputs and VSIX contents.
- Host coverage: public SDK adapters, actual menu targeting, Remote UI rendering, Copilot availability/fallback, IDE diagnostics/tests and lifecycle. Package inspection cannot stand in for these.
- Every feature needs happy-path, edge and failure tests. Security-sensitive negative tests include attempted scope escape, untrusted overrides, prompt-injected operations and leaked secret canaries.
- Retain the existing package regressions, including neutral resources and matching manifest/assembly versions. Reusing a versioned installed directory must not be treated as testing a clean upgrade.
- Track tests by stable task/requirement IDs and retain build/test/artifact evidence for milestones. Report skipped host tests, unavailable capabilities and placeholder tests separately from pass counts.
- CI must not silently require a local interactive IDE or paid service. Define host-test prerequisites and manual gates explicitly where automation cannot run reliably.

### Compatibility matrix to complete

| Environment | Required evidence before release |
| --- | --- |
| VS 2022 17.14 minimum supported release, amd64 | Install, localization, commands/context scopes, Remote UI, collectors, all execution modes and fallback |
| Current supported VS 2022 servicing release, amd64 | Regression smoke and capability differences from minimum |
| VS 2026, amd64 | Same full functional matrix; current developer baseline is Community 18.10.1 |
| Arm64 targets currently declared in the manifest | Equivalent smoke/runtime evidence or explicit approval to narrow declarations |
| Copilot available/authenticated; unavailable/disabled/not authenticated | Accurate capability reporting, graceful errors and approved manual fallback; no private authentication workaround |

A compatible target framework is necessary but does not establish all of this compatibility. Monitor .NET 8 support lifecycle and SDK support separately; do not silently upgrade beyond the user's chosen runtime.

## 11. Decision and risk register

| ID | Question/risk | Resolution gate |
| --- | --- | --- |
| R01 | The 1.0.1.0 localization update has not been runtime-confirmed. | P00: observe actual installed labels; retain a defect if unresolved. |
| R02 | Supported Copilot APIs may not provide prompt submission, response retrieval or editing on both IDEs. | P01: prove each separately. D04/D05 require manual fallback, not another provider. P06 exposes partial automation honestly. |
| R03 | Out-of-process APIs do not expose all diagnostics, test-store or semantic context-menu requirements found so far. | Hybrid architecture approved: prove each supported public in-process API and the narrow bridge boundary on VS 2022 and VS 2026 before relying on it; retain explicit fallbacks where proof fails. |
| R04 | Full automation can cause real side effects; builds/scripts can execute arbitrary repository code. | P02/P06: trust, scoped policy, explicit allowlists, tested denials and visible provenance. An allowlist is not a sandbox. |
| R05 | Context, prompts, logs and persisted history may contain secrets or proprietary code. | P05/P07/P10: exclusions before reads, best-effort redaction, explicit handoff and retention controls. |
| R06 | Resumed runs can use stale code, skill definitions, approvals or evidence. | P04/P06/P10: versioned snapshots, approval invalidation, bounded retries and conflict checks. |
| R07 | Declared IDE/architecture ranges exceed tested environments. | P00/P10/P11: complete matrix or obtain an explicit scope/manifest decision. |
| R08 | UI responsiveness can degrade with large context, Git output or history. | P05/P07/P10: finite limits, cancellation, async host calls, measurement before optimization. |
| R09 | Package/dependency/runtime support or licensing may change. | P11: review current support/security/licenses; no unapproved runtime migration. |
| R10 | Adapter metadata/settings become a community contract while upstream content evolves. | P02/P03: preserve upstream format; version separate schemas, document diagnostics and test old/unknown versions. |
| R11 | Upstream updates change skills, references, licenses or helper behavior. | P03: pin hashes, preserve attribution, review dependency closure and never auto-pull or execute helpers. |
| R12 | Upstream tool requirements or Git actions exceed proven host capabilities/policy. | P01/P06/P08: visible platform adaptations/manual handoff; no fabricated subagents, independent review or enforcement over manual sessions. |
| R13 | Refactor has no dedicated canonical skill at the reviewed revision. | Approved via D12; implement and label the adapter composition in P08.06 without claiming it is an upstream skill. |
| R14 | Bundling every release grows package/storage size and expands licensing and compatibility work. | P03/P11: verify inventory, measure size, preserve per-version notices and label support; seek approval before narrowing catalog scope. |
| R15 | Downloaded releases may be hostile, unavailable or incompatible. | P03: bounded approved-source downloads, staged validation, safe extraction, explicit selection and preserved rollback; no silent changes to active runs. |

No unsupported API capability is considered approved merely because it appears in a task list. Discovery that invalidates a dependency requires updating this document before proceeding.

## 12. Definition of done and next approval

A milestone is done only when its required code compiles, applicable tests pass, edge/failure cases are covered, relevant security concerns are reviewed, documentation is updated, and remaining debt/limits are explicit. A supported-host claim additionally needs host evidence. A public release needs all mandatory phases and explicit release authorization.

**Next action:** continue P01 with the supported context and diagnostics probes. P01 step 4 applied the targeting-probe stop condition because the referenced SDK documentation does not establish public solution/project/file context-menu placement identifiers; retain the existing Extensions submenu fallback and do not add speculative context-menu contributions. P00.05 baseline results remain recorded for both IDEs, including user-confirmed VS2022 F5 checks and a separate failed standalone installation path. Retain the installer defect for P10/P11 installation/release validation; baseline completion is not a claim that distribution installation succeeds. No upstream integration is implemented by this documentation work.

### Progress update record

| Entry | Evidence/status |
| --- | --- |
| Planning baseline | Existing source/configuration inspected; Test Explorer records 13 passing package tests and one passing empty unit test. Last full build was successful. No new build/test run is implied by this documentation pass. |
| User decisions | D01–D08 above, including three selectable modes, allowlisted commands, Copilot-only integration, manual fallback and support for both IDE families. |
| Upstream review / roadmap rebase | B11 complete: pinned source/license review and skill-to-adapter mapping documented. Replaced JSON-in-Markdown/custom-methodology assumptions; retained existing task IDs and unverified product status. Product code remains unchanged. |
| Rebase validation | 23 local Markdown links resolved; all 81 phase task IDs are unique and continuous; whitespace checks passed. C#/project/XAML SHA-256 snapshot unchanged (`B1179A2A3F2E2103C75D2861C831B71579589AD3911868B1D3CDCCFFD0ED641B`). Fresh workspace build succeeded; 14/14 existing tests passed on .NET 8.0.31 (13 package cases plus one empty unit test). No upstream integration or host-runtime claims follow from these results. |
| Release-catalog direction approved | User confirmed stable releases and prereleases, then approved the direction. D09–D11 replace the single-version/VSIX-only update proposal. Added P03.07–P03.10 and P07.08; existing task IDs preserved. No product implementation changed. |
| P00 approval updates | User approved Refactor as an adapter composition (D12), confirmed `TheKameleon.Superpowers.slnx` as canonical (D13), approved Guided as the default mode, metadata-only retention by default, intact upstream `SKILL.md` layering, the preferred five-project/Core-first split and the proposed interfaces (D14–D18), chose VS2026 runtime verification now with VS2022 deferred, and closed P00.01/P00.02. VS2022 remains the only deferred part of P00.05. |
| Project consolidation | User removed the separate Abstractions and Context projects. The canonical solution now contains Core, Skills, VSIX, Tests and IntegrationTests; documentation was updated to keep most implementation outside the VSIX project. |
| VS2026 runtime verification | `TheKameleon.Superpowers.slnx` built successfully; generated VSIX version `1.0.1.0` was installed into the VS 2026 experimental instance with `VSIXInstaller.exe /rootSuffix:Exp` exit code `0`. UI Automation verified the resolved **Extensions > TheKameleon Superpowers > Plan** path, enabled `Plan` command, one **Superpowers** tab on first and repeated invocation, the expected status text (**Superpowers is active. Plan workflow is not implemented yet.**), close/reopen behavior, and no-solution operation in the experimental instance. Debug logs confirmed the out-of-process extensibility host was running. |
| VS2022 installation blocker | Community 17.14.41 instance `dcdb5f81` is complete/launchable and its extension-development workload was verified. On 2026-09-20, VSIX `1.0.1.0` installation into `Exp` returned exit code `1` twice, including explicit instance/SKU/path targeting. Default logs report `RequiresInstallerException`: requested operation must use Visual Studio Installer; multiple targets are unsupported. The installer attempted a handoff to `setup.exe`. Reproduction: launch the VS2022 `Common7/IDE/VSIXInstaller.exe` with `/quiet /rootSuffix:Exp /instanceIds:dcdb5f81 /skuName:Community /skuVersion:17.0 /appIdName:VS`, `/appIdInstallPath` set to the VS2022 Community directory, `/logFile` set to a filename, and the VSIX path. Local log: `%TEMP%/Superpowers-VS2022-Exp-20260920-163421.log`. Workload installation did not resolve this error; no user cancellation was involved in these two attempts. Underlying cause and successful installation remain unverified; P00.05 stays open. |
| VS2022 F5 baseline complete | User reports opening the solution in VS2022, starting the VSIX with F5 and seeing the Plan window, then confirms all four follow-up checks: resolved menu labels (no placeholders), repeat invocation reuses one window, close/reopen works, and Plan works without a solution. Evidence is user-attested, not an automated UI run. Detected IDE: Community 17.14.41, instance `dcdb5f81`; installed executable machine `0x8664` (amd64), file version `17.14.37710.0`. The exact F5-deployed extension version was not independently inspected. Standalone installation still fails as recorded above. |
| VS2026 baseline environment | Detected IDE: Community 18.10.1, instance `c9c360fa`; installed executable machine `0x8664` (amd64), file version `18.10.12210.168`. Earlier UI observations above are retained, not a new runtime test. Neither baseline establishes Arm64, accessibility/theme or full release-matrix coverage. |
| P00.05 closure | Supersedes earlier deferred/open P00.05 status entries: both IDE baseline outcomes are recorded, including the installation failure reproduction. All four requested VS2022 runtime checks passed by user confirmation. Proceed to P01; preserve the unresolved standalone installer issue for P10/P11, without treating F5 deployment as distribution validation. |

Append subsequent milestone records with task IDs, changed files, tests run, host/SDK versions where relevant, and known limitations. Keep the checklist and README current in the same change as the implementation.
