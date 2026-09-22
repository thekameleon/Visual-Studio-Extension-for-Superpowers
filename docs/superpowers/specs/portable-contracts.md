# Portable contracts for catalog, adapter metadata, context, runs and evidence

## Status and purpose

This specification starts P02.01. It defines the portable contract boundaries for
`TheKameleon.Superpowers.Core` and `TheKameleon.Superpowers.Skills` before broad
implementation begins. These contracts must remain independent of Visual Studio SDK
assemblies, bridge-only DTOs and product-private Copilot assemblies.

This specification does not redefine the upstream Superpowers skill format. Upstream
`SKILL.md` files, their front matter and their supporting assets remain source content
that the product loads, validates and layers with separate adapter metadata.

## Goals

P02.01 must define portable models for:

- release catalog, selection, provenance and compatibility
- adapter metadata layered separately from upstream skills
- captured context snapshots and their provenance
- run, task, evidence and status tracking
- capability declarations and fallback reporting
- action requests, approvals and results

These models are intended for .NET 8 portable libraries and tests. They are not host
service wrappers, IDE object graphs or transport-specific envelopes.

## Non-goals

P02.01 does not yet implement:

- runtime loading of upstream bundles
- network discovery or download flows
- Visual Studio host adapters
- Copilot session invocation
- arbitrary command execution
- bridge transport/session protocols
- replacement skill definitions or a new upstream skill language

Those concerns are handled in later plan phases.

## Contract layering

The portable layer should be separated into these conceptual groups:

1. **Catalog contracts**
   - describe bundled/downloaded upstream releases
   - track provenance, licensing, integrity and compatibility
   - identify the selected and pinned release without loading IDE services
2. **Adapter metadata contracts**
   - describe Visual Studio action mappings, presentation metadata and supported
	 execution/fallback expectations
   - reference upstream skills by stable identifiers or relative paths
   - remain separate from upstream `SKILL.md` content
3. **Context contracts**
   - represent captured workspace, solution, project, document, selection, build,
	 test and source-control inputs
   - record availability, provenance, truncation/redaction and staleness honestly
4. **Run and evidence contracts**
   - represent a run, its tasks, state transitions, approvals, pauses, cancellations,
	 collected evidence and resulting outputs
   - separate observed evidence from inferred workflow state
5. **Capability contracts**
   - describe what the current host/runtime can and cannot do through supported APIs
   - record fallback or blocker status without embedding host SDK types
6. **Action/result contracts**
   - describe requested operations, approval requirements, execution bounds,
	 outcomes and captured artifacts
   - remain implementation-agnostic so Guided, Approval-required and Full modes can
	 share the same contract family

## Model families

### Release catalog and selection

Portable catalog contracts should cover at least:

- release identity and version
- source kind: bundled, downloaded or solution-local
- provenance: upstream source, reviewed revision, publisher/source URI, retrieval time
- integrity: hashes, manifest/schema version and dependency closure status
- compatibility: minimum adapter version, schema compatibility and unsupported reasons
- selection state: available, selected, pinned for active runs, superseded or invalid
- notice/license references required for redistribution

These contracts must describe both bundled releases shipped in the VSIX and future
user-approved downloaded releases without changing the consuming APIs.

### Adapter metadata

Adapter metadata should cover at least:

- stable adapter manifest/schema version
- action identifiers such as Plan, Execute, Debug, TDD, Review, Verify, Refactor,
  and Context Publish/Clear where applicable
- references to upstream skills, templates and related assets
- display metadata and grouping for Visual Studio presentation
- fallback behavior when a required capability is unavailable
- compatibility constraints for IDE family, adapter version or execution mode
- whether a step requires approval, preview/copy fallback or manual evidence import

Adapter metadata must not rewrite the upstream methodology into a new executable
format. It composes and annotates upstream content for this product.

### Context snapshots

Context contracts should model snapshots rather than live IDE handles. Each captured
value should be able to say:

- value present
- value unavailable
- value redacted
- value truncated/budgeted
- value stale or derived from an earlier capture

At minimum the family should leave space for:

- workspace and solution identity
- project identity and selected project/file targets
- open documents and active document/selection state
- semantic-target summaries where supported
- compiler/build diagnostics and build/test summaries
- Git branch/status/recent-commit summaries
- source/provenance timestamps and collection method

The contracts should distinguish user content from metadata summaries so privacy and
retention policies can apply consistently.

### Run, task and evidence tracking

Run contracts should represent:

- run identity and schema version
- selected release and adapter metadata versions
- selected execution mode and trust basis
- current state and allowed transitions
- task list with accepted plan IDs and task-local state
- evidence records tied to observed events or artifacts
- pause/cancel/retry markers
- persisted snapshot/version information for safe resume

Evidence contracts must distinguish:

- observed evidence
- missing evidence
- stale evidence
- blocked evidence
- imported/manual evidence

No contract should imply success solely because a task was requested.

### Capabilities and fallbacks

Capability contracts should be portable summaries, not IDE-specific service objects.
They should be able to represent:

- capability identifier and schema version
- availability state: available, unavailable, blocked or manual-only
- scope/version applicability
- detail/reason text for diagnostics and UI
- fallback kind such as preview/copy, manual import, scoped runner or unsupported

This contract family should align conceptually with the evidence already recorded in
`docs/superpowers/specs/host-capabilities.md`, while remaining portable and host-neutral.

### Actions, approvals and results

Action/result contracts should cover:

- requested action kind and target scope
- required capability and required approval mode
- bounded parameters only; no arbitrary command payloads
- start/completion timestamps and cancellation
- outcome: succeeded, failed, blocked, canceled, rejected, unavailable
- produced artifacts such as summaries, diffs, logs, build/test outputs or evidence IDs

Approvals, trust checks and allowlists are elaborated in P02.02, but the action/result
models should already leave room for those decisions to be attached cleanly.

## P02.02 execution, trust, approval and allowlist contracts

P02.02 extends the portable contract family with policy-oriented models that remain
portable and host-neutral. These contracts define what may be requested and authorized;
they do not themselves execute IDE actions, shell commands or Copilot sessions.

### Execution modes

Portable contracts should define an explicit execution-mode model for at least:

- `Guided`
- `ApprovalRequired`
- `Full`

The execution-mode contract should capture:

- the selected mode and schema version
- whether it is the workspace default or a run-specific override
- visible capability limits for the current mode
- whether unsupported steps must pause, fall back to preview/copy, or wait for manual input

The model must preserve the approved semantics:

- Guided is the default mode.
- Approval-required mode still binds each action to explicit approval.
- Full mode is not unrestricted execution and cannot bypass trust, allowlist,
  capability or scope checks.

### Workspace trust

Portable trust contracts should represent a workspace-scoped decision rather than a
machine-wide or user-wide blanket grant. The contract should cover:

- workspace identity basis used for trust decisions
- trust status: trusted, untrusted, revoked, changed or unknown
- the policy version and time of the trust decision
- why trust is required for a requested action family
- invalidation triggers when relevant workspace identity changes

Repository content, downloaded skills, adapter metadata and allowlists must not be able
to grant their own trust. Trust contracts describe the decision and its basis, not an
OS sandbox or a claim that repository code is safe.

### Approval contracts

Portable approval contracts should bind authorization to a specific requested action and
its exact inputs. The contract should be able to record:

- approval identity and schema version
- the exact operation/action kind being approved
- required capability and execution mode
- arguments, working directory, target files and related context snapshot identifiers
- selected skill/release/adapter versions bound to the approval
- approval status: pending, approved, rejected, expired, superseded, revoked, executed
- issued/expiry timestamps and the principal or source of the decision

Approvals must be invalidated when relevant inputs change. The contract should support
revalidation rules so stale approvals are not silently reused after workspace, command,
context or policy changes.

### Command allowlist contracts

Portable allowlist contracts should represent constrained executable policies, not free-
form shell permission. The allowlist model should cover:

- stable entry identity and schema version
- resolved executable or approved command identity
- bounded argument definitions or parameter schema
- approved working-directory scope
- timeout and output-capture limits
- whether the command is available in Guided, ApprovalRequired or Full mode
- whether a separate approval is still required even when allowlisted
- disallowed expansion rules such as wildcard shell access or model-text interpolation

Allowlist contracts should be able to distinguish:

- built-in safe operations
- user-approved custom entries
- invalid, revoked or incompatible entries

The contract must not authorize arbitrary shell execution, wildcard interpreters, or
command construction directly from model output.

### Relationship between policy contracts and action contracts

Execution mode, trust, approval and allowlist contracts should attach to the existing
action/result family without changing it into an IDE-specific executor. In practice,
portable action models should be able to reference:

- required execution mode
- trust requirement and trust decision reference
- approval requirement and approval decision reference
- allowlist entry reference when a custom command or approved runner is involved
- blocker/fallback reason when authorization or capability is insufficient

This keeps policy state explicit and serializable across runs, persistence and resume.

### Non-goals for P02.02

P02.02 does not yet implement:

- UI prompts or shell dialogs
- concrete command execution
- OS/process sandboxing
- host-specific trust stores
- automatic approval inheritance across changed inputs
- unrestricted repository-script execution

Those behaviors are implemented later, but they must conform to these portable
contracts.

## P02.03 validated settings contracts

P02.03 extends the portable contract family with validated settings models. These
settings must remain portable, serializable and independent of Visual Studio-specific
storage or UI. They describe user/workspace policy and defaults; they do not by
themselves read files, execute tools or override trust/approval requirements.

### Settings model expectations

Portable settings contracts should support:

- explicit schema versioning
- required versus optional fields
- defaults that can be explained to the user
- validation failures reported as product configuration errors
- separation between user-global settings, workspace-scoped overrides and effective values

Settings must not be treated as trusted simply because they are present in a repository.
Workspace content may propose configuration, but policy application must preserve the
product's trust and approval rules.

### Exclusions and redaction settings

The settings family should include contracts for exclusions and redaction policy that
apply before reading file contents where required by the workflow. These settings should
be able to represent:

- excluded paths, file patterns and content classes
- always-excluded sensitive categories such as secrets/credentials/private keys
- whether an exclusion is built-in, user-defined or workspace-scoped
- redaction policy for paths, tokens and other likely-sensitive values
- diagnostics for excluded, redacted or partially captured context

The contract must support the repository rule that exclusions are applied before prompt
composition, not merely after capture.

### Budgets and bounded capture settings

Portable settings should define bounded collection limits for context and evidence. The
model should leave space for:

- maximum file/content sizes
- item-count limits for diagnostics, tests, documents and history entries
- time budgets/timeouts for collection or execution-related observation
- truncation behavior and user-visible diagnostics when a budget is hit

Budgets should be explicit, finite and configurable. Over-budget results must be
reported honestly as truncated or partial rather than complete.

### History and retention settings

Portable settings should define history and retention policy for both local workflow
history and cached release content. The contracts should represent:

- metadata-only retention as the default
- explicit opt-in for retaining prompt/code/content beyond metadata
- retention durations or counts for run history
- cache retention and rollback preservation rules for downloaded releases
- clear/export/delete policy flags and related diagnostics

Retention contracts must distinguish workflow history from versioned upstream caches.
Active-run content and required rollback state must not be evicted merely because a
general cleanup policy exists.

### Release filter and update-check settings

Portable settings should define how release selection/update behavior is filtered without
silently changing the user's active version. The model should be able to represent:

- whether prereleases are included or excluded
- latest/filter semantics chosen by the product
- update-check enablement, cadence and bounded network behavior
- offline/rate-limited behavior and diagnostics expectations
- whether bundled-only use is enforced or optional downloads are allowed

These settings must align with the approved catalog direction: downloads are user-
approved, local content remains usable when checks fail, and startup must not silently
change the selected version.

### Mode default settings

Portable settings should define the default execution mode while preserving the
previously approved semantics. The model should represent:

- default mode selection
- whether a workspace may request a different default without self-authorizing actions
- visibility requirements for effective mode/capability state

Guided remains the approved default unless the user changes it through settings.

### Critical-warning policy settings

Portable settings should define a configurable critical-warning policy used by Verify,
Finish and related evidence gates. The contract should support:

- explicit warning categories or identifiers considered critical
- policy scope by tool/source where relevant
- treatment of unknown, stale, missing or waived warning evidence
- diagnostics that explain why verification is blocked or downgraded

The policy must not assume all compiler warnings are universally critical. Criticality is
a product policy decision that must be recorded explicitly.

### Non-goals for P02.03

P02.03 does not yet implement:

- the concrete persisted storage format
- Visual Studio settings UI
- network clients for update checks
- the actual cache cleanup engine
- secret-detection guarantees
- verification logic beyond defining the policy inputs

Those behaviors are implemented later, but they must conform to these validated portable
settings contracts.

## Dependency and project boundaries

The initial intended boundaries are:

- `TheKameleon.Superpowers.Core`
  - owns portable contract types, serialization helpers and validation logic
- `TheKameleon.Superpowers.Skills`
  - owns loaders/composition over upstream skills and adapter metadata using Core contracts
- `TheKameleon.Superpowers.Vsix`
  - owns host adapters, UI and approved integration entry points
- `TheKameleon.Superpowers.InProcess`
  - owns narrow bridge-only host probes and bridge transport DTOs only where approved
- `TheKameleon.Superpowers.Bridge.Contracts`
  - remains limited to bridge boundary DTOs, not the general product contract layer

Portable contracts must not depend on Visual Studio SDK assemblies, WPF types, COM,
`MessagePack`, bridge-only types or product-private Copilot binaries.

## Serialization and versioning expectations

Portable contracts should be versioned explicitly and support stable round-tripping.
The design should assume:

- forward-compatible unknown enum/schema handling where possible
- immutable snapshots preferred over mutable live graphs
- deterministic serialization for persisted state and tests
- explicit distinction between required and optional fields
- validation errors reported as product data issues, not host crashes

The concrete serializer choice remains open in P02.05, but the contracts should be
serializer-friendly and avoid host-specific object graphs.

## Bridge transport and invocation contracts

The approved hybrid boundary requires a narrow, versioned bridge transport between
the out-of-process VSIX host and the in-process VSSDK component. This transport is
a separate concern from both portable product contracts and in-process editor/Roslyn
adapters. The first release must keep the transport surface intentionally small and
must not expose arbitrary service lookup, arbitrary command execution, or general
Visual Studio object graphs across the process boundary.

Bridge transport contracts should cover at least:

- request/response envelopes with explicit protocol version
- stable operation identifiers for each approved bridge call
- request identity/correlation for diagnostics and cancellation
- structured failure kinds rather than exception-only behavior
- capability discovery before operation use
- workspace/session identity binding sufficient to reject stale or mismatched calls
- deterministic DTO serialization suitable for tests and version negotiation

The initial approved bridge operation set is intentionally narrow:

1. `GetCapabilities`
2. `GetActiveDocumentText`

The bridge transport contract must not initially include:

- arbitrary Roslyn queries
- arbitrary file reads
- arbitrary command execution
- workflow orchestration commands
- unrestricted service-broker passthrough
- product policy or approval decisions

### Bridge request/response envelopes

`TheKameleon.Superpowers.Bridge.Contracts` may define bridge-only request/response
envelopes separate from the portable product contract layer. At minimum, these
envelopes should leave room for:

- `ProtocolVersion`
- `Operation`
- `RequestId`
- `WorkspaceId` or equivalent non-secret workspace/session identity
- request timestamp
- success/failure flag
- structured failure kind
- diagnostic detail safe for logs/UI without leaking document text

A version mismatch must produce a structured failure result rather than undefined
behavior. For the initial bridge, exact protocol-version matching is acceptable.

### Named-pipe transport design (approved next step)

To remove the previously unproven cross-process gap without depending on any
Visual Studio-private broker, the approved next transport is a custom
`System.IO.Pipes` (`NamedPipeServerStream`/`NamedPipeClientStream`) channel built
entirely from supported, public .NET APIs. This avoids brokered-service,
RPC-contract or other VS-private infrastructure while still satisfying every
constraint above.

Shape:

- **Server**: hosted in `TheKameleon.Superpowers.InProcess`, started alongside
  `BridgeHost` when the VSSDK package loads; listens on a per-VS-process pipe.
- **Client**: implemented in `TheKameleon.Superpowers.Vsix/Bridge` as a new
  `IBridgeClient` implementation (e.g. `PipeBridgeClient`) that connects lazily
  and falls back to `UnavailableBridgeClient` behavior if the pipe cannot be
  opened.
- **Pipe naming/scoping**: pipe name includes the hosting Visual Studio process
  id (and, where available, an instance/session token) so each VS instance
  binds only to its own in-process bridge and never to another instance's pipe.
- **Security**: local-machine named pipe only, no network exposure;
  `PipeSecurity`/ACL restricted to the current Windows user (current identity
  only, no broader group grants). Cross-user or cross-session connections must
  be rejected.
- **Payload**: the same versioned DTO request/response envelopes defined above
  (`ProtocolVersion`, `Operation`, `RequestId`, `WorkspaceId`, timestamp,
  success/failure, structured failure kind), serialized with the serializer
  chosen in P02.05 (JSON initially acceptable) and framed with a length prefix
  over the pipe stream.
- **Cancellation/shutdown**: reads/writes must observe `CancellationToken`;
  server must close the pipe cleanly on IDE shutdown/package unload, and the
  client must treat a broken/closed pipe as a structured "unavailable" failure
  rather than an unhandled exception.
- **Reconnection**: the client may attempt a single reconnect on a broken pipe
  before falling back to unavailable; it must not retry indefinitely or block
  the calling context-capture path.

This design must still be proven (see the host-capabilities.md and
p01-probe-design.md checkpoints) before any bridge-backed document text is
claimed as supported in production workflows.

### Bridge failure semantics

Bridge transport failures must be modeled explicitly. The contract should support
at least:

- unavailable
- unsupported
- version mismatch
- cancelled
- timeout
- shutdown/disconnected
- context changed
- internal error

The VSIX host must convert these failures into honest context states such as
`Unavailable` or `Partial`; it must not invent captured content or silently report
success.

### Bridge cancellation, shutdown and privacy rules

Bridge operations must accept cancellation and stop cooperatively during IDE
shutdown. Failure to complete before cancellation/shutdown is an unavailable or
cancelled result, not a host crash.

Bridge diagnostics and logs must not capture raw document text by default. Logging
may include operation name, request identity, timing, protocol version and
failure category, but not prompt/document payloads unless a later explicitly
approved diagnostic mode is added.

## Validation goals for later P02 tasks

Later P02 work should add tests for:

- required/optional field validation
- schema/version round trips
- invalid/unknown enum values
- immutable snapshot behavior
- compatibility mismatches
- absence of IDE dependency leaks into portable assemblies
- execution-mode, trust, approval and allowlist policy validation
- approval invalidation when bound inputs change
- rejection of wildcard or model-interpolated command policies
- settings validation for exclusions, budgets, retention, release filters and defaults
- critical-warning policy handling for unknown, missing, stale and waived evidence

## Initial implementation direction

The first code increment after this spec should likely:

1. create named contract namespaces in `TheKameleon.Superpowers.Core`
2. replace empty scaffold types with focused model roots
3. add unit tests for versioned round trips and validation behavior
4. add only the project references needed for portable layers

That work belongs to later P02 tasks; this document establishes the contract boundary
first.
