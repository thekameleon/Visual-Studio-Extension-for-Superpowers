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

A5 gates the release: if referenced skills are still not loaded, record the evidence and decide on the index-skill fallback (spec §13) before publishing.
