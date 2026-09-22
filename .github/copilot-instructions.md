# Copilot Instructions

## Project Guidelines
- TheKameleon Superpowers must use C#, not Visual Basic, and target .NET 8 across all projects. Use net8.0 for portable libraries/unit tests and net8.0-windows8.0 for the modern extension and its integration tests; use C#12 for .NET 8 SDK compatibility. Prefer .NET 8 where needed for compatibility with the chosen VisualStudio.Extensibility SDK.
- TheKameleon Superpowers automation must use supported Copilot APIs only, with manual fallback when unavailable; do not introduce other AI providers. Full automation may build, test, edit within agreed scope, and run explicitly allowlisted custom commands in a trusted workspace.
- TheKameleon Superpowers requirements: users can choose Guided, Approval-required automation, or Full automation modes. Guided is the default mode but user-changeable by setting; a prompt preview/copy fallback is acceptable when supported direct Copilot handoff is unavailable. The first release must support and test Visual Studio 2022 17.14+ and Visual Studio 2026. For P00, do Visual Studio 2026 runtime checks now and defer Visual Studio 2022 checks.
- Before proceeding to a new phase, all unit tests and integration tests must pass. The full unit and integration test gate applies only between implementation phases, not between tasks within the same phase; for example, between P04 and P05 or P06 and P07.
- For the Superpowers Visual Studio adapter, bundle all existing upstream releases, including stable releases and prereleases, with each VSIX release. Let users select a bundled version or download newer upstream releases independently; future VSIX releases should refresh the bundled catalog.
- Prefer adapting canonical upstream Superpowers skills rather than recreating its methodology or inventing a replacement skill format. Finish and approve the complete upstream-reuse implementation plan before further product coding. Refactor is approved as a product-specific composition over upstream skills rather than a claimed canonical upstream skill. The canonical solution filename is TheKameleon.Superpowers.slnx.
- Store metadata by default with a setting to retain more; keep upstream SKILL.md files intact; most implementation should live outside the VSIX project; the proposed interfaces are acceptable.
- Continue work from the existing canonical docs/superpowers/plans/implementation-plan.md rather than creating a separate implementation plan.
- Change TheKameleon Superpowers from a strictly out-of-process design to a hybrid architecture with a minimal in-process Visual Studio bridge, including a narrow target-framework exception for that bridge, while retaining supported public APIs only.
- The debugging workflow must provide a way to install the in-process bridge automatically or directly as part of the debug process. The bridge refresh should be part of the F5 debug workflow; do not present manual bridge preparation as a routine required step. Treat the script as a fallback when automatic deployment fails.
- Current priority is to prove a supported VSIX-to-in-process transport so the active-document text blocker can be removed.
- User approved end-to-end implementation across P04.04 through P05 and wants work to continue without routine interruptions, stopping only for a required product decision, an unresolved build/test blocker, or a codebase mismatch that invalidates the plan.
- For P05.01 and similar context capture work, use out-of-process APIs for document presence, URI, and selection, and use the approved minimal in-process bridge for document text. Deliver active-document capture as the document scope; open-document collection can remain presence/URI-focused unless safe bridge-backed multi-document text capture is added later.

## User Interaction Guidelines
- Stop and acknowledge when stuck instead of repeatedly retrying the same failing edit path.
- When inspecting terminal tool results, do not assume the overall investigation is blocked if one parallel command was canceled; check the completed command output before responding.

## Extension Details
- Use 'Superpowers' as the menu label and 'SuperPowers for Visual Studio' as the VSIX display name.
- The extension's core purpose is to leverage Superpowers within GitHub Copilot Chat, so direct Copilot Chat integration should be the default path rather than a separate experience whenever supported APIs allow it.
- The extension should use GitHub Copilot Chat as the core integration path where supported; that is the intended product direction and the point of the extension, with fallback only when direct supported integration is unavailable.

## Installation Guidelines
- When an operation reports cancellation, do not attribute it to the user without evidence. The source of cancellation is unknown and should not be assumed to be the user.