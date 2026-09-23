# Copilot Instructions

## Project Guidelines
- TheKameleon Superpowers must use C#, not Visual Basic, and target .NET 8 across all projects (net8.0 for portable libraries/unit tests, net8.0-windows8.0 for the VSIX and its integration tests, C# 12).
- This extension installs unmodified upstream Superpowers skills (github.com/obra/superpowers) into locations GitHub Copilot Chat discovers natively in Visual Studio 2026 18.5+: `%USERPROFILE%\.copilot\skills\`, a `superpowers.agent.md` custom agent under `%USERPROFILE%\.github\agents\`, and an optional, off-by-default always-on block in `%USERPROFILE%\copilot-instructions.md`. It does not compose prompts, capture IDE context, or automate Copilot itself — Copilot does that work with its own tools, guided by the installed skills.
- The first release supports Visual Studio 2026 18.5+ only. There is no Visual Studio 2022 support in this release; a future MCP-server-based approach may add it later, tracked separately.
- The extension changes only files it recorded as its own (`%LOCALAPPDATA%\TheKameleon.Superpowers\install-state.json` is the ownership record). It never overwrites or deletes a skill folder, agent file, or always-on block it did not install, and it writes nothing to the user's profile before they explicitly click Install.
- The bundled release catalog (`bundled-catalog/obra.superpowers/2026-09-21/`) ships all existing upstream stable releases and prereleases at the VSIX's cutoff, unchanged. Users can also check for and download newer releases from GitHub directly (HTTPS only, pinned to `github.com/obra/superpowers`, with size and path-safety bounds on extraction).
- Upstream `SKILL.md` files and their supporting files are installed byte-for-byte unchanged; environment adaptation (translating Claude-specific wording, cross-reference resolution, etc.) happens only in the versioned bootstrap text (`TheKameleon.Superpowers.Skills/Bootstrap/BootstrapText.cs`), never by rewriting upstream content.
- Before proceeding between implementation phases, the full unit and integration test suites must pass.
- The canonical solution filename is `TheKameleon.Superpowers.slnx`.
- Continue work from `docs/superpowers/plans/2026-09-23-agent-skills-installer.md`, which supersedes the earlier prompt-composition/manual-handoff product direction recorded in `docs/superpowers/plans/implementation-plan.md`. The design this plan implements is `docs/superpowers/specs/2026-09-23-agent-skills-installer-design.md`.

## User Interaction Guidelines
- Stop and acknowledge when stuck instead of repeatedly retrying the same failing edit path.
- When inspecting terminal tool results, do not assume the overall investigation is blocked if one parallel command was canceled; check the completed command output before responding.

## Extension Details
- Use 'Superpowers' as the menu label and 'Superpowers for Visual Studio' as the VSIX display name.
- The extension's job is to make GitHub Copilot Chat use Superpowers skills natively — it is an installer and status panel, not a chat integration itself. There is no in-process bridge, no custom prompt composition, and no automated build/test/edit execution in this product.

## Installation Guidelines
- When an operation reports cancellation, do not attribute it to the user without evidence. The source of cancellation is unknown and should not be assumed to be the user.
