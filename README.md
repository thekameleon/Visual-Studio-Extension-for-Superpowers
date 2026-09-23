# Superpowers for Visual Studio

Brings the [Superpowers](https://github.com/obra/superpowers) skills (brainstorming, writing plans, test-driven development, systematic debugging, code review and verification) to GitHub Copilot Chat in **Visual Studio 2026 18.5 or later**.

Copilot in Visual Studio natively discovers Agent Skills. This extension installs the unmodified upstream skills where Copilot finds them, and adds a **Superpowers** agent that makes Copilot use them.

## Use it

1. Open **Extensions ▸ Superpowers ▸ Open**.
2. Pick a release (the newest stable one is preselected) and select **Install**.
3. In Copilot Chat, choose **Superpowers** in the agent picker (or type `@Superpowers`) and start a new thread.
4. Turn **Autopilot off** for brainstorming and planning; those skills ask you questions.

## What it changes on your machine

| Location | What |
|---|---|
| `%USERPROFILE%\.copilot\skills\<skill>\` | The upstream skill folders, unchanged |
| `%USERPROFILE%\.github\agents\superpowers.agent.md` | The Superpowers agent |
| `%USERPROFILE%\copilot-instructions.md` | Only if you turn on **always-on**: one marked block; the rest of the file is untouched |
| `%LOCALAPPDATA%\TheKameleon.Superpowers\install-state.json` | What the extension installed, so it never touches anything else |

The extension never overwrites a skill it did not install, and asks before replacing files you edited.

**Before uninstalling the extension, open the tool window and select Remove from my profile.** Visual Studio gives extensions no uninstall hook, so the files stay otherwise.

## Status panel

The tool window checks that the skills and agent are present and unmodified, and warns if the same skills also exist in `~/.claude/skills` or `~/.agents/skills` (Copilot would see duplicates). A diagnostic line reads Copilot's own log to confirm it discovered them; it reports *Unknown* rather than failing when the log cannot confirm.

## Build and test

Requires Windows, Visual Studio 2026 with the extension-development workload, and a `.slnx`-capable .NET SDK (9.0.200 or later). All projects target .NET 8.

```powershell
dotnet build TheKameleon.Superpowers.slnx
dotnet test TheKameleon.Superpowers.Tests/TheKameleon.Superpowers.Tests.csproj
dotnet test TheKameleon.Superpowers.IntegrationTests/TheKameleon.Superpowers.IntegrationTests.csproj
```

Unit tests install into temporary folders and never touch your profile. The F5 experimental instance uses your **real** profile, so Install there changes your real `%USERPROFILE%`.

Manual acceptance on VS 2026: [docs/superpowers/acceptance/agent-skills-acceptance.md](docs/superpowers/acceptance/agent-skills-acceptance.md). Design: [docs/superpowers/specs/2026-09-23-agent-skills-installer-design.md](docs/superpowers/specs/2026-09-23-agent-skills-installer-design.md).

## Licenses

Superpowers skills are © Jesse Vincent and contributors, MIT licensed; each bundled release includes its `LICENSE.txt`. The icon is original artwork for this extension.
