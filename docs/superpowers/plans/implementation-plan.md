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
| D01 | Use C#, not Visual Basic; C# 12 and .NET 8 throughout. | Confirmed by user |
| D02 | Retain modern out-of-process VisualStudio.Extensibility and Remote UI, not the traditional VSSDK host. | Existing implemented direction |
| D03 | Let users select Guided, Approval-required automation, or Full automation. All three are first-release scope. | Confirmed by user |
| D04 | Use supported Copilot APIs only for AI integration; do not introduce other AI providers or ask for their keys. | Confirmed by user |
| D05 | Ship with explicit prompt preview/copy and manual handoff when supported direct Copilot integration is unavailable. | Confirmed by user |
| D06 | Full automation may build, test, edit within agreed scope, and run explicitly allowlisted custom commands in a trusted workspace. | Confirmed by user |
| D07 | Support and test Visual Studio 2022 17.14+ and Visual Studio 2026 for the first release. | Confirmed by user |
| D08 | Complete and persist this implementation plan before further product coding. | Confirmed by user |
| D09 | Adapt canonical upstream Superpowers rather than recreate its methodology or skill format. | Direction approved by user |
| D10 | Bundle all existing upstream releases, including stable releases and prereleases, at each VSIX release cutoff. | Confirmed by user |
| D11 | Let users select bundled versions or download newer upstream releases independently; cache locally, preserve selection across VSIX upgrades and pin active runs. | Direction approved by user |

Detailed proposals still need phase-level approval: Guided as the default mode, metadata-only history, separate Visual Studio adapter metadata, a Refactor composition, and project/interface boundaries. D10/D11 supersede the single-version bundle and VSIX-only skill update proposal. Each catalog version remains pinned individually. The prior JSON-in-Markdown format and independent methodology engine are superseded, not implemented. Approval of direction is not evidence of runtime compatibility or completion.

## 3. Current baseline: what has actually been done

| ID | Existing work | Evidence and limits |
| --- | --- | --- |
| B01 | [x] Seven-project solution scaffold exists. | Abstractions, Core, Context, Skills, VSIX, Tests, IntegrationTests. The four libraries still contain empty `Class1` scaffolds. |
| B02 | [x] Align all projects with C#/.NET 8. | Portable projects use `net8.0`; VSIX and integration tests use `net8.0-windows8.0`. |
| B03 | [x] Replace the traditional host with modern out-of-process registration. | [VSIX project](../../../TheKameleon.Superpowers.Vsix/TheKameleon.Superpowers.Vsix.csproj), [extension entry point](../../../TheKameleon.Superpowers.Vsix/SuperpowersExtension.cs); SDK/Build 17.14.40608. |
| B04 | [x] Add the Plan command and submenu. | [PlanCommand.cs](../../../TheKameleon.Superpowers.Vsix/PlanCommand.cs); Extensions > TheKameleon Superpowers > Plan. It opens a window only. |
| B05 | [x] Add a minimal Remote UI status window. | [Window](../../../TheKameleon.Superpowers.Vsix/SuperpowersToolWindow.cs), [control](../../../TheKameleon.Superpowers.Vsix/SuperpowersToolWindowControl.cs), [view](../../../TheKameleon.Superpowers.Vsix/SuperpowersToolWindowControl.xaml). No planning workflow or context capture yet. |
| B06 | [x] Add package regression tests. | [ExtensionPackageTests](../../../TheKameleon.Superpowers.IntegrationTests/ExtensionPackageTests.cs) and [PlanToolWindowPackageTests](../../../TheKameleon.Superpowers.IntegrationTests/PlanToolWindowPackageTests.cs). Last recorded run: 13 package tests passed on .NET 8.0.31, plus one empty unit test. This is not behavioral workflow coverage. |
| B07 | [x] Package localized labels and a versioned update. | [Resources](../../../TheKameleon.Superpowers.Vsix/.vsextension/string-resources.json); project version 1.0.1 produces VSIX/assembly 1.0.1.0. Build and version/resource tests passed. |
| B08 | [x] Receive user confirmation that an earlier package appears in the extension list. | Registration only; not proof that the latest update resolves localization or that all runtime behavior works. |
| B09 | [ ] Verify menu labels after installing 1.0.1.0. | The reported instance had command metadata cached without its resource dictionary. The versioned update has not been confirmed as resolving the visible placeholders. |
| B10 | [ ] Verify runtime behavior on the supported IDE matrix. | Activation, repeat invocation, close/reopen, no-solution operation, accessibility, themes and both IDE versions remain acceptance checks. |
| B11 | [x] Review pinned upstream Superpowers and the VS Code adapter. | [Review specification](../specs/upstream-superpowers-review.md): source format, licenses, skill mapping, installer/hook risks and proposed adapter boundary. No upstream skills are yet packaged or integrated. |

The working tree also contains a pre-existing solution rename: `TheKameleon.Superpowers.slnx` is on disk while the earlier `TheKameleon.Superpowers.Vsix.slnx` is deleted. Preserve this work; confirm the intended solution filename rather than creating a second solution. No commits are implied by this checklist.

## 4. Requirements-to-phase map

| Requirement | Delivery phase(s) | Current status |
| --- | --- | --- |
| Eight entry points over upstream skills: Plan, Execute, Debug, TDD, Review, Verify, Refactor, Finish | P03, P04, P07, P08 | Not implemented; Plan command shell only; Refactor is a proposed composition |
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
| `TheKameleon.Superpowers.Abstractions` | `net8.0`; immutable contracts, capability/result types and interfaces. No IDE/UI/process implementation. | None |
| `TheKameleon.Superpowers.Core` | `net8.0`; adapter run/task tracking, policy, evidence, prompt composition, handoff and history. Upstream owns development methodology. | Abstractions |
| `TheKameleon.Superpowers.Context` | `net8.0`; context aggregation, redaction/budgeting, Git collection and host-independent adapters. | Abstractions |
| `TheKameleon.Superpowers.Skills` | `net8.0`; upstream Markdown/YAML discovery, bounded parsing, provenance, dependency assets and separate adapter metadata. | Abstractions |
| `TheKameleon.Superpowers.Vsix` | `net8.0-windows8.0`; SDK contributions, Remote UI, dependency injection, IDE/Roslyn adapters and supported Copilot bridge. | Abstractions, Core, Context, Skills as needed |
| `TheKameleon.Superpowers.Tests` | `net8.0`; deterministic behavioral tests with fake clocks, storage and host capabilities. | Portable projects |
| `TheKameleon.Superpowers.IntegrationTests` | `net8.0-windows8.0`; packaging, cross-component and Windows integration checks. Explicitly distinguish tests requiring a VS host. | Projects under test; build-only VSIX reference for packaging |

Keep dependency arrows one-way. Do not move Visual Studio APIs into portable projects or add a manually managed worker simply because the extension is out of process; the chosen SDK already provides the host boundary.

Proposed owned interfaces include `ISkillCatalog`, `IAdapterRunCoordinator`, `IContextProvider`, `IWorkflowStore`, `IPromptComposer`, `IActionRunner`, `IApprovalService`, `IEvidenceCollector`, and `ICopilotBridge`. These are design targets, not existing code or promises about Microsoft API names. Separate read-only context capture from authorized actions; do not implement an independent methodology engine or infer executable actions from prose.

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

- [ ] P00.01 Close remaining detailed roadmap/adapter proposals, including Refactor composition, before further product coding. Upstream reuse and the stable/prerelease catalog with optional future downloads are approved (D09–D11).
- [ ] P00.02 Confirm the intended solution filename and preserve the existing rename when committing repository work.
- [ ] P00.03 Install the versioned update in an experimental instance and record installed version plus resolved submenu/Plan labels.
- [ ] P00.04 Verify window activation, repeat invocation, close/reopen and no-solution behavior.
- [ ] P00.05 Record baseline results on VS 2022 17.14 and VS 2026, including edition, architecture, version and reproduction steps for failures.

**Exit:** foundation labels and behavior are verified, or a reproduced blocking defect has a separately approved fix. Do not call the localization incident resolved solely because the version test passes.

### P01 — Prove host capabilities and freeze integration contracts (Large; highest uncertainty)

**Depends on:** P00 and roadmap approval. **Artifacts:** proposed `docs/superpowers/specs/host-capabilities.md` and capability matrix; spike code only after approval.

- [ ] P01.01 Map supported public APIs for document/selection/project context on both IDE versions.
- [ ] P01.02 Prove diagnostics, build invocation and test-result/run access with minimal host experiments.
- [ ] P01.03 Investigate supported Visual Studio Copilot handoff, response, edit and upstream-tool/subagent capabilities separately; CLI plugin support is not Visual Studio API evidence.
- [ ] P01.04 Prove solution/project/file/class/method menu placement and semantic target resolution, including C# Roslyn access.
- [ ] P01.05 Publish the per-version capability/fallback matrix and contract constraints before shared API implementation.
- [ ] P01.06 Review architecture gaps with the user; do not silently use private APIs or a legacy bridge.

**Tests/evidence:** runnable host probes for supported paths; negative results for unavailable services, no editor, no tests and absent Copilot. **Exit:** every requested integration has a supported implementation path, an approved manual fallback, or an explicit blocker. Preview/copy is approved; silently dropping required context menus is not.

### P02 — Establish portable contracts and configuration (Medium)

**Depends on:** P01. **Projects:** Abstractions, Core, Tests; project references added deliberately.

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

**Depends on:** P02/P03. **Projects:** Core, Abstractions, Tests, IntegrationTests.

- [ ] P04.01 Implement explicit adapter run/task states and evidence gates, separate from upstream instructional prose.
- [ ] P04.02 Implement upstream skill selection/composition, accepted plan task IDs and capability-aware handoff/waiting without fabricating agent actions.
- [ ] P04.03 Implement pause/cancel/retry semantics and protection against duplicate side effects.
- [ ] P04.04 Implement local versioned persistence with atomic writes and recovery.
- [ ] P04.05 Implement safe resume with snapshot/skill/policy validation.
- [ ] P04.06 Implement metadata history query/export/delete and opt-in sensitive-content retention.
- [ ] P04.07 Add behavioral state-machine and persistence tests.

**Tests:** every valid/invalid transition, approval denial, unsupported action, stale evidence, cancellation at boundaries, interrupted writes, corrupt/future-version state, reopened solution and duplicate retry. **Exit:** a multi-step workflow survives restart without falsely completing or replaying unauthorized actions.

### P05 — Implement complete context capture and privacy (Large)

**Depends on:** P01/P02; may proceed alongside P03/P04 after contracts stabilize. **Projects:** Context, VSIX adapters, Tests, IntegrationTests.

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
| R03 | Out-of-process APIs may not support all diagnostics, test-store or semantic context-menu requirements. | P01: capability evidence and user decision before architecture/scope changes. |
| R04 | Full automation can cause real side effects; builds/scripts can execute arbitrary repository code. | P02/P06: trust, scoped policy, explicit allowlists, tested denials and visible provenance. An allowlist is not a sandbox. |
| R05 | Context, prompts, logs and persisted history may contain secrets or proprietary code. | P05/P07/P10: exclusions before reads, best-effort redaction, explicit handoff and retention controls. |
| R06 | Resumed runs can use stale code, skill definitions, approvals or evidence. | P04/P06/P10: versioned snapshots, approval invalidation, bounded retries and conflict checks. |
| R07 | Declared IDE/architecture ranges exceed tested environments. | P00/P10/P11: complete matrix or obtain an explicit scope/manifest decision. |
| R08 | UI responsiveness can degrade with large context, Git output or history. | P05/P07/P10: finite limits, cancellation, async host calls, measurement before optimization. |
| R09 | Package/dependency/runtime support or licensing may change. | P11: review current support/security/licenses; no unapproved runtime migration. |
| R10 | Adapter metadata/settings become a community contract while upstream content evolves. | P02/P03: preserve upstream format; version separate schemas, document diagnostics and test old/unknown versions. |
| R11 | Upstream updates change skills, references, licenses or helper behavior. | P03: pin hashes, preserve attribution, review dependency closure and never auto-pull or execute helpers. |
| R12 | Upstream tool requirements or Git actions exceed proven host capabilities/policy. | P01/P06/P08: visible platform adaptations/manual handoff; no fabricated subagents, independent review or enforcement over manual sessions. |
| R13 | Refactor has no dedicated canonical skill at the reviewed revision. | P00.01/P08.06: approve and label the adapter composition; do not claim it is an upstream skill. |
| R14 | Bundling every release grows package/storage size and expands licensing and compatibility work. | P03/P11: verify inventory, measure size, preserve per-version notices and label support; seek approval before narrowing catalog scope. |
| R15 | Downloaded releases may be hostile, unavailable or incompatible. | P03: bounded approved-source downloads, staged validation, safe extraction, explicit selection and preserved rollback; no silent changes to active runs. |

No unsupported API capability is considered approved merely because it appears in a task list. Discovery that invalidates a dependency requires updating this document before proceeding.

## 12. Definition of done and next approval

A milestone is done only when its required code compiles, applicable tests pass, edge/failure cases are covered, relevant security concerns are reviewed, documentation is updated, and remaining debt/limits are explicit. A supported-host claim additionally needs host evidence. A public release needs all mandatory phases and explicit release authorization.

**Next action:** complete remaining P00 detailed decisions and foundation verification, then P01 capability validation before product implementation. The user approved upstream reuse and the all-release catalog/download direction (D09–D11); this does not approve unverified APIs or resolve the Refactor composition. No upstream integration is implemented by this documentation work.

### Progress update record

| Entry | Evidence/status |
| --- | --- |
| Planning baseline | Existing source/configuration inspected; Test Explorer records 13 passing package tests and one passing empty unit test. Last full build was successful. No new build/test run is implied by this documentation pass. |
| User decisions | D01–D08 above, including three selectable modes, allowlisted commands, Copilot-only integration, manual fallback and support for both IDE families. |
| Upstream review / roadmap rebase | B11 complete: pinned source/license review and skill-to-adapter mapping documented. Replaced JSON-in-Markdown/custom-methodology assumptions; retained existing task IDs and unverified product status. Product code remains unchanged. |
| Rebase validation | 23 local Markdown links resolved; all 81 phase task IDs are unique and continuous; whitespace checks passed. C#/project/XAML SHA-256 snapshot unchanged (`B1179A2A3F2E2103C75D2861C831B71579589AD3911868B1D3CDCCFFD0ED641B`). Fresh workspace build succeeded; 14/14 existing tests passed on .NET 8.0.31 (13 package cases plus one empty unit test). No upstream integration or host-runtime claims follow from these results. |
| Release-catalog direction approved | User confirmed stable releases and prereleases, then approved the direction. D09–D11 replace the single-version/VSIX-only update proposal. Added P03.07–P03.10 and P07.08; existing task IDs preserved. No product implementation changed. |
| Remaining immediate gate | Detailed P00 proposals and runtime verification, especially installed 1.0.1.0 localization, followed by P01 capability evidence. |

Append subsequent milestone records with task IDs, changed files, tests run, host/SDK versions where relevant, and known limitations. Keep the checklist and README current in the same change as the implementation.
