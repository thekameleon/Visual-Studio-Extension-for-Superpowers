# Agent Skills acceptance checklist (VS 2026)

Run on a clean profile after installing the VSIX. Use the **Superpowers** agent with **Autopilot off** unless a row says otherwise, and a new chat thread per row. Evidence comes from the newest `%TEMP%\VSGitHubCopilotLogs\*.chat.log`: search for `get_file(` lines and the assistant text.

Test solution: a small C# class library with `PriceCalculator.ApplyDiscount(decimal price, int percent)` that rounds with `MidpointRounding.ToZero`, and an xUnit test expecting `ApplyDiscount(11.25m, 10) == 10.13m` (fails until `AwayFromZero` is used).

| # | Prompt | Pass criteria | VS version | Result | Date |
|---|---|---|---|---|---|
| A1 | "I want to add a loyalty discount that stacks with the percentage discount." | `brainstorming` read; one question at a time; no code written | | | |
| A2 | "The RoundsHalfAwayFromZero test is failing. Fix it." | `systematic-debugging` read to its last line; test run **before** the edit; root cause stated; tests rerun before claiming success | | | |
| A3 | "Write an implementation plan for adding sales tax to PriceCalculator." | `writing-plans` read | | | |
| A4 | "I think the fix is done — is this ready to merge?" | `verification-before-completion` read; tests run before any success claim | | | |
| A5 | Any flow above that names another skill | the referenced skill is read with `get_file` | | | |
| A6 | "Help me write a new skill." | `writing-skills` (681 lines) read to its last line | | | |
| A7 | A1 with Autopilot on | agent stops and says interactive Agent mode is required | | | |
| A8 | Always-on enabled, default Agent (not Superpowers) | `using-superpowers` read without selecting the agent | | | |
| U1 | Install, Repair, Remove, always-on on/off from the tool window | each reports success; files appear/disappear as the README table says | | | |
| U2 | Status panel after Install and a new chat thread | all checks OK; the diagnostic line shows OK | | | |
| U3 | Icon in menu, tool window, Manage Extensions; light, dark, high contrast | legible in every theme | | | |
| U4 | Select **Check for newer releases**. With no newer upstream release, expect "You already have every published release." With **Include prereleases** checked, any newer prerelease should offer a download prompt; **Cancel** must change nothing. | status text matches; Cancel leaves the release list and installed state unchanged | | NOT RUN — requires a live Visual Studio Exp instance with network access to GitHub, unavailable in this automated environment | |

A5 gates the release: if referenced skills are still not loaded, record the evidence and decide on the index-skill fallback (spec §13) before publishing.

## Findings from a manual run (Calc / PriceCalculator test solution, no git repository)

Tool behavior observed by the agent while following the skills in VS 2026. These are now covered by rules in the bootstrap text (`BootstrapText.Version` 2).

| # | Finding | Status | Workaround / rule |
|---|---|---|---|
| F1 | `create_file` reported success twice for `PriceCalculatorTests.cs` (relative and absolute path), but the build ignored the file and `Test-Path` returned False. Possibly only an unsaved editor buffer. | Suspected, unconfirmed | Confirm the file exists on disk after writing it; fall back to writing through the terminal (`Set-Content`). |
| F2 | The "should fail" test run reported "No test is available" instead of a compile error. Running the test first (TDD) is what exposed F1. | Observed | Treat "No test is available" as a problem to investigate, not as a failing test. |
| F3 | A multi-line PowerShell here-string (`@' ... '@`) was sent line by line; PowerShell parsed `using Calc;` on its own, failed, and the call was canceled. | Observed | Keep each terminal command on one line; build newlines with escape sequences. |
| F4 | Long skill files came back cut off from `get_file`. | Observed | Already covered: keep reading from the next line until the end. |
| F5 | Subagent-driven development was chosen, but no subagent tool exists; the skill redirects to `executing-plans`. | Expected | Already covered: do the work sequentially. |
| F6 | No git repository: worktrees, commits and git-based helper scripts were skipped; progress was kept in a markdown file. | Expected | Say so, skip git steps, record progress in markdown. |
| F7 | No independent reviewer: the final review was a self-review. | Expected | Do the review yourself and say it was not independent. |
| F8 | The agent did not read the `test-driven-development` and `verification-before-completion` skill files, working from the plan and `executing-plans` instead. | Gap | Covered by A2/A4/A5 pass criteria: referenced skills must be read with `get_file`. Recheck on the next run. |
| F9 | After failed `create_file` attempts, the open editor tab may hold an unsaved copy that differs from the file on disk. | Suspected | Close without saving and reopen to see the on-disk version. |
