# Superpowers for Visual Studio — Agent Skills Installer Design

**Status:** Approved design, pending written-spec review · **Date:** 2026-09-23
**Supersedes:** the prompt-composition / manual-handoff product direction (implementation plan P06–P11), the in-process bridge (D02/D19), and the VS 2022 first-release requirement (D07).

## 1. Summary

Visual Studio 2026 (18.5+) GitHub Copilot natively discovers `SKILL.md` Agent Skills and user-level custom agents. A spike on 2026-09-23 proved that unmodified upstream `obra/superpowers` skills, installed into `~/.copilot/skills/` and paired with a small bootstrap delivered as a custom agent, make Copilot Chat load and follow Superpowers skills on its own — the same model Superpowers uses in Claude Code.

The extension therefore becomes an **installer, version manager and status surface** for upstream Superpowers skills and their Copilot bootstrap. It no longer composes prompts, captures IDE context, executes builds/tests/edits, or hands prompts off manually: Copilot does that work with its own tools, guided by the skills.

## 2. Evidence (spike, VS Community 2026 18.10.2, Copilot chat 18.10.1201, model claude-sonnet-5)

| Observation | Log evidence |
|---|---|
| Personal skills are discovered | `Found 15 skill files in: C:\Users\<user>\.copilot\skills`; all 15 listed in the "Available Skills" system message with file paths |
| Without a bootstrap, skills are not used | "Add a feature" prompt → no `get_file` on `brainstorming/SKILL.md`; went straight to code search |
| A user-level custom agent is loaded | `Loaded 1 custom agent file(s) from user directory: C:\Users\<user>\.github\agents` |
| With the agent selected, skills load unprompted | `get_file(...using-superpowers\SKILL.md)`, then `get_file(...systematic-debugging\SKILL.md)`; announced "Using systematic-debugging…"; stated root cause before editing; ran tests before claiming success |
| Brainstorming behaviour is followed | Classified the request, wrote no code, asked one question via `ask_question` |
| Gaps | Referenced skills (`superpowers:test-driven-development`, `superpowers:verification-before-completion`) were not loaded; "reproduce consistently" step skipped; default `get_file` range is 200 lines while 8 of 15 skills are longer; Autopilot answers `ask_question` with "user is not available" |
| UI | The Skills panel is Insiders-only; stable VS shows no skill list even though discovery works |

## 3. Decisions

| ID | Decision |
|---|---|
| N01 | First release targets **Visual Studio 2026 18.5+ only**. Amends D07. VS 2022 may follow later via an MCP server; out of scope here. |
| N02 | **Retire aggressively.** Delete the in-process bridge, Bridge.Contracts, context capture, prompt composition, preview handoff, workflow orchestration/persistence, execution runners and action policy. They remain in git history. Keep and fix the release catalog, download, cache and settings code. Supersedes D02/D19 and P05–P07 product scope. |
| N03 | **Activation:** a user-level custom agent named *Superpowers* is always installed. An optional, off-by-default **always-on** mode adds a delimited bootstrap block to `%USERPROFILE%\copilot-instructions.md`. Amends the "no rewriting of Copilot instruction files" constraint: edits are explicit opt-in, delimited and reversible. |
| N04 | Upstream `SKILL.md` files are installed **byte-for-byte unchanged** (D16 retained). Environment differences are handled only by the bootstrap text. |
| N05 | The extension modifies only files it has recorded as its own, plus its delimited block. It never overwrites or deletes anything it does not own. |
| N06 | Nothing is written to the user profile until the user clicks **Install**. |
| N07 | The extension ships an original icon. It does not use upstream Superpowers branding or superhero-style trademark imagery. |

Retained: D04 (supported surfaces only; no other AI provider), D09 (adapt upstream, do not invent methodology), D10/D11 (bundle all releases; user-selected version; pinned selection across VSIX upgrades), D13, D16.

## 4. Architecture

```
TheKameleon.Superpowers.Vsix  (net8.0-windows8.0, out-of-process VisualStudio.Extensibility, VS 2026 18.5+)
 ├─ SuperpowersToolWindow     Remote UI: release picker, Install/Update/Repair/Remove, status checks, always-on toggle, tips
 ├─ OpenSuperpowersCommand    Extensions ▸ Superpowers ▸ Open (custom icon)
 └─ Images/                   Superpowers.16.16.png, .20.20.png, .xaml; extension Icon + PreviewImage

TheKameleon.Superpowers.Skills  (net8.0)
 ├─ Catalog/      BundledCatalogLoader, ApprovedReleaseDiscoveryService, ApprovedReleaseDownloadService, CatalogCacheManager  (existing, security-fixed)
 ├─ Install/      SkillInstaller, InstallStateStore, ProfilePaths, InstallLock
 ├─ Bootstrap/    BootstrapText (embedded resource), AgentFileWriter, AlwaysOnBlockEditor
 └─ Status/       StatusProbe, CopilotLogDiagnostic

TheKameleon.Superpowers.Core    (net8.0)  settings + result contracts
TheKameleon.Superpowers.Tests / IntegrationTests
```

Deleted projects: `TheKameleon.Superpowers.InProcess`, `TheKameleon.Superpowers.Bridge.Contracts`. The solution drops to five projects and one VSIX.

### Units and interfaces

| Unit | Responsibility | Depends on |
|---|---|---|
| `ProfilePaths` | Resolves `~/.copilot/skills`, `%USERPROFILE%\.github\agents`, `%USERPROFILE%\copilot-instructions.md`, state dir `%LOCALAPPDATA%\TheKameleon.Superpowers`. Injectable root for tests. | — |
| `InstallStateStore` | Reads/writes `install-state.json` atomically (temp + replace). Corrupt → returns `Unknown` state, never empty. | ProfilePaths |
| `InstallLock` | Named mutex `Local\TheKameleon.Superpowers.Install` around every mutating operation. | — |
| `SkillInstaller` | `InstallAsync(release)`, `RemoveAsync()`, `RepairAsync()`; staging, validation, conflict detection, per-skill swap with rollback. | Catalog, InstallStateStore, InstallLock |
| `AgentFileWriter` | Writes/refreshes/removes `superpowers.agent.md` from `BootstrapText`. | ProfilePaths, InstallStateStore |
| `AlwaysOnBlockEditor` | Pure text transform: insert/replace/remove the delimited block, preserving everything else. | — |
| `StatusProbe` | Produces the check list in §7. Read-only. | all of the above |
| `CopilotLogDiagnostic` | Best-effort scan of the newest Copilot chat log. Read-only; returns Confirmed/Unknown only. | — |

## 5. Install state

`%LOCALAPPDATA%\TheKameleon.Superpowers\install-state.json`, outside every folder VS scans:

```json
{
  "schemaVersion": 1,
  "release": { "tag": "v6.4.1", "commit": "<sha>", "source": "bundled|cache" },
  "skills": [ { "name": "brainstorming", "files": { "SKILL.md": "<sha256>", "...": "..." } } ],
  "agentFile": { "path": "...\\superpowers.agent.md", "sha256": "...", "bootstrapVersion": 1 },
  "alwaysOn": { "enabled": false, "createdFile": false, "blockSha256": null }
}
```

## 6. Flows

### First run
The tool window shows what will be written and where. **Install** is offered with the newest **stable** bundled release preselected. Nothing is written before the click.

### Install / switch release
1. Acquire `InstallLock`.
2. Resolve the release (bundled or cached download) and extract its `skills/` into `~/.copilot/.superpowers-staging/<guid>/` (same volume as the target).
3. Validate each skill: folder name equals front-matter `name` (lowercase, digits, hyphens, ≤64 chars); `description` present and ≤1,024 characters; no path escapes the skill folder. A skill that fails validation is reported and skipped; the rest proceed.
4. For each target folder:
   - does not exist → install;
   - exists and is owned with matching hashes → replace;
   - exists and is owned but hashes differ (user edited) → ask *Keep mine / Overwrite*;
   - exists and is not owned → **conflict**: skip and report.
5. Swap each folder: rename existing → `<name>.superpowers-backup`, move staged → `<name>`, delete backup. On any failure, restore every backup taken in this operation and report failure; the prior install stays intact.
6. Remove owned skills absent from the new release (hash-checked; edited ones are reported, not deleted).
7. Write/refresh the agent file (§8), then write `install-state.json` last. Delete staging.

### Extension (VSIX) upgrade
The installed skill release is never changed automatically. If the bundled `BootstrapText` version is newer and the agent file is unmodified, it is refreshed silently; if modified, the tool window asks.

### Always-on toggle
- **On:** insert the block at the end of `%USERPROFILE%\copilot-instructions.md`, creating the file if absent (`createdFile = true`).
- **Off:** remove the block; delete the file only if `createdFile` and it is now empty or whitespace.
- Content outside the markers is preserved byte-for-byte, including encoding, BOM and line endings.
- Missing, duplicated or out-of-order markers → refuse, report, change nothing.

Markers:
```
<!-- superpowers:begin (managed by Superpowers for Visual Studio; edit outside this block) -->
...bootstrap text...
<!-- superpowers:end -->
```

### Remove from my profile
Deletes owned, hash-matching skill folders, the agent file (if unmodified) and the always-on block, then deletes `install-state.json`. Edited files are listed and left in place. The modern SDK exposes no VSIX-uninstall hook, so the README and Marketplace page tell users to click **Remove** before uninstalling.

### Repair
Re-runs install of the recorded release. With `Unknown` state (corrupt/missing state file), Repair is the only mutating action offered; it treats matching-name folders whose content hashes equal the release's files as owned and everything else as conflicts.

### Picking up changes
Whether a running VS notices new or changed skills and agents without a restart is unverified. Until tested, the tool window says: "Start a new chat thread. If *Superpowers* is not in the agent picker, restart Visual Studio."

## 7. Status panel

Checks run on open and on **Refresh**; each is Pass / Warning / Fail with a one-line reason:

1. **Visual Studio version** ≥ 18.5.
2. **Skills** — every skill of the installed release present with matching hashes; edited, missing and conflicting folders listed.
3. **Agent file** — present, unmodified, bootstrap version.
4. **Always-on** — setting value; block present and well-formed when enabled.
5. **Copilot discovery (diagnostic)** — scans the newest `%TEMP%\VSGitHubCopilotLogs\*.chat.log` for `Found N skill files in ...\.copilot\skills` and `Registered custom agent: ...superpowers.agent.md`. The log format is undocumented, so the result is only *Confirmed* or *Unknown*, never *Fail*. Read-only; the log content is never displayed or stored.

Static tips: select **Superpowers** in the agent picker or type `@Superpowers`; turn off **Autopilot** for brainstorming and planning; one starter prompt per core skill.

## 8. Bootstrap text (version 1)

Stored once as an embedded resource. The agent file wraps it with front matter; the always-on block wraps it with markers. It adapts the environment only and contains no methodology of its own (D09).

Agent file `%USERPROFILE%\.github\agents\superpowers.agent.md`:

```markdown
---
name: Superpowers
description: Agent mode with Superpowers skills — brainstorming, planning, TDD, systematic debugging, code review and verification.
---
```

No `tools` property: the spike showed that omitting it keeps all tools available.

Body (`BootstrapText` v1):

```markdown
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
```

## 9. Icon

| Surface | Mechanism |
|---|---|
| Extension Manager / Marketplace | `ExtensionMetadata.Icon` (128×128 PNG, shown at 32×32) and `PreviewImage` (200×200 PNG) |
| Extensions ▸ Superpowers ▸ Open | `ImageMoniker.Custom("Superpowers")` backed by `Images/Superpowers.16.16.png`, `Images/Superpowers.20.20.png`, `Images/Superpowers.xaml` |
| Tool window content | `<vs:Image Source="Superpowers"/>` |
| Tool window tab | Same moniker if the tool window configuration exposes an icon; verify during implementation |
| Copilot agent picker | Not possible: `.agent.md` has no icon property; VS shows its default agent icon |

Requirements:
- Original artwork; no upstream Superpowers branding or superhero trademark imagery.
- Legible at 16×16: bold, simple glyph (proposed concept: lightning bolt on a rounded square).
- The XAML variant uses VS theme brushes so it works in light, dark and high-contrast themes.
- One SVG master is committed under `art/`; the PNG sizes are generated from it.

## 10. Error handling

- Every operation returns a typed result: `Succeeded`, `Partial(conflicts, skipped)` or `Failed(reason)`. The UI never throws; failures show the reason and what was left unchanged.
- Install/switch is all-or-nothing per operation (§6 step 5).
- Catalog security fixes are prerequisites before the download path is re-enabled:
  - require the `https` scheme for approved URLs;
  - fix the zip-slip containment check by comparing against the destination root plus a trailing separator;
  - enforce the size limit while streaming, and cap entry count and uncompressed size;
  - fix the activation `catch` so it never deletes the pre-existing active release;
  - un-ignore `bundled-catalog/**/releases/` (the default `.gitignore` rule `[Rr]eleases/` excludes them), so clean clones and CI package the archives;
  - default release selection = newest stable, not `LastOrDefault()`.
- Paths in the state file are validated against `ProfilePaths` roots before any delete.

## 11. Testing

**Unit (TheKameleon.Superpowers.Tests), temp-directory profile root:**
- install fresh; switch release (skills added/removed); reinstall same release idempotent;
- conflict with a non-owned folder; user-edited owned file (keep/overwrite paths); failure mid-swap restores backups;
- remove with edited files; corrupt/missing state → `Unknown` + Repair behaviour;
- `AlwaysOnBlockEditor`: absent file, existing content, UTF-8 BOM, UTF-16, CRLF/LF, trailing text, malformed/duplicate markers, remove-and-delete-if-created;
- `BootstrapText` contains the required translation rules (get_file, full-read, cross-reference, ask_question/Autopilot, subagent);
- `CopilotLogDiagnostic` against sample log excerpts, including unknown formats → `Unknown`.

**Integration (TheKameleon.Superpowers.IntegrationTests):**
- install every bundled release into a temp profile; every installed skill satisfies the VS front-matter rules;
- VSIX contents: catalog archives present, icon + preview image present, custom moniker images present, no in-process bridge VSIX or assembly;
- catalog download hardening cases (http scheme, sibling-prefix zip-slip, oversize stream, activation failure preserves original).

**Manual acceptance on VS 2026 (checklist, evidence from the Copilot chat log):**

| # | Prompt (Superpowers agent, Autopilot off) | Pass criteria |
|---|---|---|
| A1 | "I want to add a loyalty discount that stacks with the percentage discount." | `brainstorming` read; one question at a time; no code written |
| A2 | "The RoundsHalfAwayFromZero test is failing. Fix it." | `systematic-debugging` read fully; test run **before** the edit; root cause stated; tests re-run before claiming success |
| A3 | "Write an implementation plan for adding sales tax to PriceCalculator." | `writing-plans` read |
| A4 | "I think the fix is done — is this ready to merge?" | `verification-before-completion` read; tests run before any success claim |
| A5 | Any flow that references another skill | the referenced skill is read with `get_file` |
| A6 | "Help me write a new skill." | `writing-skills` (681 lines) read to its last line |
| A7 | A1 with Autopilot on | agent stops and says interactive Agent mode is required |
| A8 | Always-on enabled, default Agent (not Superpowers) | `using-superpowers` read without selecting the agent |

## 12. Retirement and plan changes

- **Delete:** `TheKameleon.Superpowers.InProcess`, `TheKameleon.Superpowers.Bridge.Contracts`, `build/BridgeDebugDeployment.targets`, `scripts/Prepare-BridgeDebug.ps1`, `Vsix/Bridge/*`, `Vsix/Context/*`, `Vsix/Execution/*`, probe commands, `SuperpowersWorkflowViewModel`/`ProbeResultsViewModel` and their XAML, `Skills/Context/*`, `Skills/Execution/*`, `Skills/Workflow/*`, `Skills/Composition/*`, the corresponding Core contracts (`Contracts/Context`, `Contracts/Runs`) and their tests.
- **Keep:** `Skills/Catalog/*`, `Skills/Parsing/SkillDocumentParser` (validation only), `Core/Contracts/Catalog`, `Core/Contracts/Settings` (trimmed to catalog/release settings and always-on), `build/Generate-BundledCatalog.ps1`, `build/SuperpowersBuildVersion.targets`.
- **Implementation plan:** mark P05–P07 product scope superseded; replace P08–P11 with the phases produced from this spec; add N01–N07 to the decision register with the amendments to D02, D07, D19 and the instruction-file constraint.
- **README:** rewrite around install → select agent → use; VS 2026 18.5+ only; Autopilot note; Remove-before-uninstall note.

## 13. Risks and open questions

| Risk | Mitigation |
|---|---|
| Copilot skill/agent behaviour and log format change between VS releases | Upstream files unchanged; bootstrap versioned; the log check is diagnostic-only; acceptance checklist re-run per VS minor release |
| Other tools read the same folders: `~/.copilot/skills` is also read by Copilot CLI; users may have Superpowers in `~/.claude/skills` or `~/.agents/skills` (duplicates) | Status panel warns when a same-name skill exists in `~/.claude/skills` or `~/.agents/skills`; the extension never writes there |
| Cross-reference loading remains unreliable even with the stronger rule | Acceptance A5 gates the release. Fallback, requiring a new decision: install a generated `superpowers-index` skill (extension-authored, not upstream) that maps names to paths |
| VS base prompt ("Keep your answers short") dilutes skill adherence | Measured by acceptance A1–A4; no mitigation designed until observed |
| No uninstall hook leaves files behind | Remove button, documentation, and every file clearly labelled as managed by the extension |
