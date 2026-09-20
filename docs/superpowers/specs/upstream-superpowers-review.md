# Upstream Superpowers reuse and Visual Studio adapter boundary

## Status and scope

Source review complete. The user approved upstream reuse and a bundled catalog of
stable releases and prereleases with optional future downloads. Detailed engineering
proposals remain tracked through [P00.01 of the implementation plan](../plans/implementation-plan.md).
This review changes documentation only. The current VSIX is a modern command/window
shell: it does not yet bundle, load or execute Superpowers skills. No upstream
installer, hook or helper was executed during review.

The intended product is a Visual Studio adapter for upstream Superpowers, not a
replacement framework with independently rewritten versions of its skills. Preserve
C# 12/.NET 8, out-of-process VisualStudio.Extensibility, the three requested execution
modes, supported Copilot-only integration and explicit preview/copy fallback.

## Pinned sources and license evidence

Review references are immutable commits, not a requirement to track a moving branch.

| Source | Reviewed revision | Role and license evidence |
| --- | --- | --- |
| [obra/superpowers](https://github.com/obra/superpowers/tree/5bf4e78011075bcfc0dc295f0724994cd123ee71) | `5bf4e78011075bcfc0dc295f0724994cd123ee71` | Canonical skill instructions and supporting assets. [MIT LICENSE](https://github.com/obra/superpowers/blob/5bf4e78011075bcfc0dc295f0724994cd123ee71/LICENSE), copyright 2025 Jesse Vincent. |
| [earchibald/vsc-superpowers](https://github.com/earchibald/vsc-superpowers/tree/e4a507de8b7c228caf9ca958fd5cfa2bf51887c0) | `e4a507de8b7c228caf9ca958fd5cfa2bf51887c0` | VS Code/Copilot CLI adaptation reference, not a Visual Studio extension SDK. [LICENSE](https://github.com/earchibald/vsc-superpowers/blob/e4a507de8b7c228caf9ca958fd5cfa2bf51887c0/LICENSE) contains MIT terms, copyright 2026 Eugene Archibald, and notes bundled obra skills. GitHub API `NOASSERTION` is not the license text. |

MIT permits reuse and modification subject to retaining the copyright and permission
notice with copies or substantial portions. Before redistribution, include the obra
license with its content and in the VSIX notices. If copying adapter material, retain
its applicable notice too; its notice does not replace the original author's notice.
Review all selected supporting assets and dependencies separately. This review does
not resolve this repository's own license placeholders or imply upstream endorsement.

## What the reviewed implementations actually do

### Canonical Superpowers

The pinned tree contains **15** `skills/<name>/SKILL.md` entries. These use YAML front
matter (`name`, `description`) and Markdown instructions, with relative references to
other skills, templates and supporting files. They are agent instructions, not a
machine-readable action graph or a .NET workflow-engine package.

The [README](https://github.com/obra/superpowers/blob/5bf4e78011075bcfc0dc295f0724994cd123ee71/README.md)
also documents GitHub Copilot **CLI** plugin installation. That is not proof of an
API for invoking Copilot Chat, receiving responses or applying edits from an
out-of-process Visual Studio extension. P01 must prove those capabilities separately
for Visual Studio 2022 17.14+ and Visual Studio 2026.

Inspect and preserve dependency closure, not just the top-level skill files. Examples
include `skills/requesting-code-review/code-reviewer.md`, reviewer prompts,
`skills/using-superpowers/references/`, and brainstorming's optional visual companion.
Scripts, servers, telemetry, session readers and network helpers are not authorized
by being present in a skill bundle. Unsupported helpers stay unavailable with visible
limitations; never execute them while parsing or previewing a skill.

### VS Code and Copilot CLI adaptation

The reviewed [installer](https://github.com/earchibald/vsc-superpowers/blob/e4a507de8b7c228caf9ca958fd5cfa2bf51887c0/install-superpowers.sh)
maps **14** skill names to command aliases, pulls/clones a shared upstream cache,
creates a workspace `.superpowers` symlink, writes a managed block in
`.github/copilot-instructions.md`, and wraps skills as `.github/prompts/*.prompt.md`.
It renames planning/debugging commands to avoid VS Code command conflicts.

This is a useful discoverability/aliasing pattern, not an installer to run from the
VSIX. Noninteractive use skips confirmation. An unmanaged instructions file is moved
to `.old` rather than merged into the active instructions; prior `.superpowers.old`
directories can be deleted. Cache updates are not pinned. A workspace symlink can
refer outside the workspace, so it is not a trust or privacy boundary.

The [session-start hook](https://github.com/earchibald/vsc-superpowers/blob/e4a507de8b7c228caf9ca958fd5cfa2bf51887c0/hooks/scripts/session-start.sh)
bootstraps/updates the cache and prints an activation banner. The
[pre-command hook](https://github.com/earchibald/vsc-superpowers/blob/e4a507de8b7c228caf9ca958fd5cfa2bf51887c0/hooks/scripts/pre-command.sh)
checks a temporary marker's age for recognized Git commit/push commands and warns,
but exits successfully even without verification. This is **not** enforced approval
or evidence bound to a source snapshot. Do not port its shell-command matching or
marker heuristic as the Visual Studio security/verification mechanism.

## Proposed entry-point mapping

The requested eight Visual Studio actions remain in scope. An action is an alias or
composition of upstream skills, not necessarily a new skill file. Exact bundle
selection and adaptation manifest are approved in P03; no 14/15-count assumption is
hard-coded into the loader.

| Visual Studio action | Canonical upstream skill path(s), relative to `skills/` | VS Code alias / adapter responsibility |
| --- | --- | --- |
| Plan | `brainstorming/SKILL.md`, then `writing-plans/SKILL.md` when requirements/design are accepted | `/brainstorm`, `/write-plan`. Ask for missing requirements; present design approval and the resulting plan. Do not invent a replacement six-step definition. |
| Execute | `executing-plans/SKILL.md`; optionally `subagent-driven-development/SKILL.md` only where supported and chosen | `/execute-plan`, `/subagent-dev`. Load the accepted plan, track task/evidence state and expose actual tool availability. Never fabricate a subagent dispatch. |
| Debug | `systematic-debugging/SKILL.md` | `/investigate`. Attach diagnostics and scoped context; retain diagnosis before fixes. |
| TDD | `test-driven-development/SKILL.md` | `/tdd`. Track observed red/green evidence and authorized edits without rewriting the methodology. |
| Review | `requesting-code-review/SKILL.md`, `receiving-code-review/SKILL.md` | `/review`, `/receive-review`. Capability-gate fresh reviewer/subagent requirements; label manual review honestly rather than claiming independent review. |
| Verify | `verification-before-completion/SKILL.md` | `/verify`. Bind actual build/test results to source/configuration; do not infer success from a prompt or marker file. |
| Refactor | Composition of planning as needed, `test-driven-development/SKILL.md`, review and verification | No dedicated upstream Refactor skill in the reviewed tree. Proposed product-specific entry point with an explicitly labeled adapter recipe, not a claimed upstream skill. Approval required in P00.01. |
| Finish | `finishing-a-development-branch/SKILL.md` plus verification | `/finish-branch`. Show readiness and explicit next actions; do not automatically commit, merge, push, remove worktrees or delete branches. Unsupported actions remain manual. |

Shared bootstrap: `using-superpowers/SKILL.md` (`/superpowers`). Other reviewed
skills are `using-git-worktrees` (`/worktree`), `dispatching-parallel-agents`
(`/dispatch-agents`), `writing-skills` (`/write-skill`), and `diagnosing-superpowers`
(no alias in the reviewed 14-entry installer). Bundle referenced content where needed,
but do not silently add automated Git, arbitrary skill execution, session-transcript
collection or new public menu requirements. These require separate capability/trust
review; diagnosing-Superpowers helpers have additional privacy implications.

## Proposed adapter contract

### Approved release distribution direction

- Each VSIX release includes all existing published upstream releases, including
  stable releases and prereleases, captured at its catalog cutoff. Verify the actual
  release/tag inventory before implementation; do not silently equate every tag with
  a published release or omit older versions because they are untested.
- On first use, offer a bundled version for offline use or a user-selected download,
  including the latest available release. Clearly label prereleases and show the
  exact resolved version before confirmation; define latest/filter semantics in P03.
- Future upstream releases can be downloaded independently of VSIX updates. Downloads
  require user selection/approval; normal skill loading uses local content, not a
  moving branch. Background check cadence and settings remain to be specified.
- Store downloaded versions in a versioned user-local cache, outside the installed
  VSIX and repository. Keep prior versions for rollback; never evict active-run content.
  VSIX upgrades refresh the bundled catalog without changing the user's selection.
- Pin each selected release to its resolved commit and content hashes. Active runs
  retain their source/adapter version; selecting latest is not automatic tracking.
- Label tested, untested and incompatible versions independently of bundled/downloaded
  status. Reject invalid or incompatible activation with diagnostics while preserving
  the working selection. Unknown future releases are not guaranteed compatible.
- Validate downloads in staging before atomic activation: approved source/redirects,
  bounded transfer/extraction, traversal and link rejection, metadata, dependency
  assets, hashes and per-release license notices. Hashes alone are not publisher
  authentication. Never run downloaded scripts, hooks or installers.
- Offline, rate-limited, cancelled, corrupt or failed downloads leave bundled/cached
  content usable. No startup download may block the IDE or silently change versions.

This supersedes the single bundled revision and VSIX-only skill update proposal.
The reviewed commits below are source evidence, not the entire release catalog.
Package growth, extraction/storage costs and per-release license coverage must be
measured/reviewed before shipping; any reduction of the all-releases requirement
needs user approval.

### Reuse without rewriting the source format

- Keep canonical `SKILL.md` files and approved dependency assets intact in each
  versioned release bundle. Preserve relative paths, upstream IDs and license files.
  Do not require JSON metadata inside upstream Markdown or convert prose into an
  executable action graph.
- Keep Visual Studio aliases, context requirements, capability declarations,
  provenance, unsupported helpers and any Refactor composition in **separate adapter
  metadata**. P02/P03 specify this contract and its parser; no parser library choice
  is approved here.
- Record repository URL, full commit, relative path, file hashes and adapter version
  for the bundle and each run. Review future upstream updates explicitly, including
  changed dependencies/licenses. No startup `git pull`, installer execution or silent
  changes to active runs.
- Overlay platform guidance visibly rather than silently editing upstream policy.
  Translate unsupported tool names only to proven equivalent capabilities. Missing
  capabilities pause for a clearly labeled manual handoff or report a blocker.
- Discover trusted custom skills using the same Markdown/YAML format and bounded,
  data-only parsing. Specify packaged/user/workspace precedence, override approval,
  path and symlink checks, size/depth limits and atomic reload before implementation.
  Never auto-edit users' Copilot instructions or install symlinks/prompts.

### Own only Visual Studio integration and enforcement

The adapter owns commands/Remote UI, context snapshots/redaction, prompt preview,
supported Copilot handoff, capability diagnostics, trust and exact-action approvals,
allowlisted build/test/edit actions, cancellation, task/evidence tracking and local
history. Upstream skills own the development methodology. Adapter state records what
actually happened; it does not parse every sentence into a compulsory state transition
or pretend to control a separate manual Copilot session.

The three modes govern **extension-owned actions only**. Preview/copy is not execution
or Copilot acceptance. A manually returned result is user-provided evidence, not a
trusted IDE test run. Full mode cannot bypass missing APIs or authorize extra tools
mentioned in a skill. Build/test runs execute repository code and still require trust.

Keep platform policy outside skill source: no implicit Git mutations/publication,
no arbitrary shell interpolation, no secret disclosure, no provider/key substitution,
and no unsupported COM/private API bridge. Source instructions, model output and
repository comments cannot expand authorization. If platform restrictions prevent an
upstream workflow from finishing, report partial/blocked status rather than silently
omitting steps or claiming full upstream equivalence.

## Roadmap consequences and approval gates

1. Preserve the working modern VSIX shell and its package regressions.
2. Record the approved reuse/release-catalog direction; resolve remaining detailed
   proposals through P00.01, confirm the solution rename and complete foundation checks.
3. Prove Visual Studio/Copilot capabilities in P01, including whether upstream tool
   requirements can be satisfied or need the approved manual fallback.
4. Define adapter/catalog contracts in P02, then package all releases, implement safe
   opt-in downloads and test attribution in P03 **before shipping copied content**.
5. Implement lean run/evidence tracking, context, policy and UI in P04–P07. Wire the
   other actions to upstream sources in P08; do not author parallel built-in skills.
6. Verify command surfaces, truthful capabilities and end-to-end behavior in P09–P11.

Validation must cover bundle integrity/provenance/licenses, relative-reference
closure, unchanged upstream content, malformed or hostile custom metadata, missing
helpers, unsupported subagents, all three modes, stale evidence, manual handoff and
no unintended execution/network/instruction-file changes. Existing automated package
tests do not prove any of these future behaviors. Add catalog completeness (including
prereleases), offline selection, failed/cancelled/malicious downloads, rollback,
upgrade selection preservation and active-run pinning tests.

## Unresolved decisions

- Detailed engineering proposals, including the separate Refactor composition; the
  upstream reuse and stable/prerelease catalog direction are approved.
- Supported Visual Studio Copilot APIs and exact upstream-tool translations (P01).
- Actual published release/tag inventory, per-release dependency assets, packaging
  layout/size, latest/filter semantics, update-check settings, cache retention, YAML
  parser, adaptation manifest and custom-skill discovery paths (P02/P03).
- How to present mandatory upstream steps that cannot be performed under the approved
  policy; no silent scope reductions or claims that manual-session policy is enforced.
- License notice packaging and upstream update review ownership before distribution.
