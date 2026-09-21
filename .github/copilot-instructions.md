# Copilot Instructions

## Project Guidelines
- TheKameleon Superpowers must use C#, not Visual Basic, and target .NET 8 across all projects. Use net8.0 for portable libraries/unit tests and net8.0-windows8.0 for the modern extension and its integration tests; use C#12 for .NET 8 SDK compatibility. Prefer .NET 8 where needed for compatibility with the chosen VisualStudio.Extensibility SDK.
- TheKameleon Superpowers automation must use supported Copilot APIs only, with manual fallback when unavailable; do not introduce other AI providers. Full automation may build, test, edit within agreed scope, and run explicitly allowlisted custom commands in a trusted workspace.
- TheKameleon Superpowers requirements: users can choose Guided, Approval-required automation, or Full automation modes. Guided is the default mode but user-changeable by setting; a prompt preview/copy fallback is acceptable when supported direct Copilot handoff is unavailable. The first release must support and test Visual Studio 2022 17.14+ and Visual Studio 2026. For P00, do Visual Studio 2026 runtime checks now and defer Visual Studio 2022 checks.
- For the Superpowers Visual Studio adapter, bundle all existing upstream releases, including stable releases and prereleases, with each VSIX release. Let users select a bundled version or download newer upstream releases independently; future VSIX releases should refresh the bundled catalog.
- Prefer adapting canonical upstream Superpowers skills rather than recreating its methodology or inventing a replacement skill format. Finish and approve the complete upstream-reuse implementation plan before further product coding. Refactor is approved as a product-specific composition over upstream skills rather than a claimed canonical upstream skill. The canonical solution filename is TheKameleon.Superpowers.slnx.
- Store metadata by default with a setting to retain more; keep upstream SKILL.md files intact; most implementation should live outside the VSIX project; the proposed interfaces are acceptable.
- Continue work from the existing canonical docs/superpowers/plans/implementation-plan.md rather than creating a separate implementation plan.
- Change TheKameleon Superpowers from a strictly out-of-process design to a hybrid architecture with a minimal in-process Visual Studio bridge, including a narrow target-framework exception for that bridge, while retaining supported public APIs only.
- The debugging workflow must provide a way to install the in-process bridge automatically or directly as part of the debug process. The bridge refresh should be part of the F5 debug workflow; do not present manual bridge preparation as a routine required step. Treat the script as a fallback when automatic deployment fails.
- Use an automatically incremented shared build revision for both VSIX package and DLL file versions, keep assembly binding versions stable, and display the loaded build version and DLL path in probe results so stale deployments are identifiable.

## Extension Details
- Use 'Superpowers' as the menu label and 'SuperPowers for Visual Studio' as the VSIX display name.

## Installation Guidelines
- When an operation reports cancellation, do not attribute it to the user without evidence; the source of cancellation is unknown and should not be assumed to be the user.