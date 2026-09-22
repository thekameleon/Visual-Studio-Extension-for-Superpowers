# Release catalog verification and packaging rules

## Status and purpose

This specification starts P03.01. It defines how TheKameleon Superpowers must verify
upstream published releases and turn them into a versioned local catalog before any
loader or packaging implementation proceeds.

This is a catalog-definition and verification document. It does not implement the
loader, downloader or bundle packaging pipeline.

## Scope

P03.01 must define:

- what counts as a releasable upstream inventory entry
- how the VSIX catalog cutoff is determined
- how stable/prerelease/latest/filter semantics are interpreted
- how compatibility labels are recorded without overstating support
- how separate adapter metadata relates to each upstream release
- what dependency closure must be preserved for each release
- what per-release notices and provenance records are required

## Source inventory verification

The catalog must be based on the actual published upstream release inventory, not on a
moving branch and not on tag names alone.

Verification requirements:

- enumerate published upstream releases explicitly
- record whether each entry is stable or prerelease
- distinguish a published release from a bare tag or reviewed commit
- retain older published releases unless an approved policy says otherwise
- record the exact upstream source URL, release identifier and resolved commit for each catalog entry

The reviewed commits documented in `upstream-superpowers-review.md` are source evidence
and review anchors, not the full catalog.

## Catalog cutoff

Each VSIX release must capture a fixed catalog cutoff. The cutoff rules are:

- the bundled catalog is a snapshot taken at a defined time or approved release boundary
- every bundled entry must be attributable to that cutoff snapshot
- later upstream releases do not implicitly appear in an already-shipped VSIX
- future download/update checks may discover newer releases, but must not rewrite the bundled cutoff

The cutoff itself must be recorded in catalog metadata so later diagnostics can explain
why a version is or is not bundled.

## Stable, prerelease and latest/filter semantics

The catalog must distinguish stable and prerelease entries explicitly.

Required rules:

- stable-only and include-prerelease filtering must be deterministic
- prereleases must be visibly labeled; they are not treated as stable by omission
- `latest` must resolve to an exact catalog entry, not a floating concept after selection
- the selected version remains pinned even if a different entry later becomes latest
- bundled, cached, downloaded, tested, untested and incompatible labels are independent concerns

A future implementation may expose multiple “latest” views, but each must be defined in
terms of explicit filter rules and exact resolved catalog entries.

## Compatibility labeling

Catalog compatibility labels must describe product knowledge, not wishful support.
Each entry should be able to record at least:

- adapter/schema compatibility status
- tested versus untested status
- incompatible or blocked reason
- whether the release is bundled, cached or download-only

Unknown future releases are not guaranteed compatible merely because they parse or share
a versioning pattern.

## Separate adapter manifest requirement

Each upstream release remains canonical source content. Product-specific integration data
must live in separate adapter metadata.

Per-release adapter metadata should be able to describe:

- adapter schema version
- Visual Studio action mappings and aliases
- capability/fallback declarations
- supported composition notes such as Refactor being product-specific
- compatibility constraints for IDE/product versions
- references to required upstream skills/assets by relative path or stable identifier

The adapter manifest must not silently rewrite or normalize upstream `SKILL.md` source
into a replacement workflow language.

## Dependency closure requirements

A release entry is not complete if only top-level `SKILL.md` files are captured. The
catalog must preserve dependency closure for each included release, including:

- referenced Markdown/template/helper assets that are approved content dependencies
- relative-path relationships used by upstream content
- license and attribution files required for redistribution
- hashes or equivalent integrity records for included files

The catalog must also record when a helper or external tool is referenced but not
approved for execution by this product. Presence in the bundle is not authorization to
execute it.

## Per-release notices and provenance

Each release entry requires per-version provenance and notice data sufficient for
attribution and diagnostics. At minimum, record:

- upstream repository URL
- release identifier/version
- resolved commit
- retrieval or bundling time
- source/content hashes
- license/notice references included for redistribution
- adapter metadata version paired with the release

If copied content is redistributed, the per-release notice set must remain intact.
Hash records help integrity checking but are not, by themselves, publisher authentication.

## Parser expectations for later implementation

P03.01 does not choose a concrete parser library, but it constrains later parser work.
The parser stage must be:

- bounded and data-only
- safe against malformed or oversized input
- separate from execution and approval logic
- able to report unsupported schema/metadata versions clearly
- able to preserve upstream content without normalizing away important distinctions

Detailed parser implementation belongs to P03.02.

## Non-goals

P03.01 does not yet implement:

- runtime bundle loading
- VSIX packaging of all releases
- user-approved downloads
- cache activation/rollback
- YAML parser selection
- compatibility testing of every upstream release

Those are subsequent P03 tasks.

## Validation expectations for later P03 work

Later P03 work should validate:

- catalog completeness, including prereleases
- latest/filter resolution behavior
- dependency closure completeness
- source hash and notice preservation
- incompatible/untested labeling
- offline and failed-update behavior
- that no loader/discovery step executes bundled content as commands

## Relationship to other specifications

This specification narrows the approved catalog direction recorded in:

- `docs/superpowers/specs/upstream-superpowers-review.md`
- `docs/superpowers/specs/portable-contracts.md`
- `docs/superpowers/plans/implementation-plan.md`

It is the canonical starting point for P03.01 and should be extended rather than
replaced by later P03 implementation notes.
