# PXA Code Designer Roundtrip and Secure Execution

## Goal

Connect JSON, C# Model, C# PDF, and the visual Designer through one canonical
`DesignExportDto` contract while preserving independent source drafts and executing C# only in an
isolated, tenant-aware PXA sandbox.

## P0 - Canonical Conversion

- [x] Define stable `json`, `csharpModel`, `csharpPdf`, and `csharpBase64` language identifiers.
- [x] Return canonical design, generated source, checksums, fidelity, diagnostics, and source maps.
- [x] Generate C# Model and C# PDF deterministically from normalized design JSON.
- [x] Convert C# Model and C# PDF back to canonical design through the sandbox worker.
- [x] Replace WebApi reflection over PDF internals with a typed PXA PDF design snapshot.
- [x] Preserve stable element IDs and diagnose evaluated control flow or unsupported constructs.
- [x] Keep assets tenant-bound and reject network or filesystem asset loading in code.

## P0 - Secure Execution

- [x] Add the `PXA.CodeWorker` executable and bounded request/response contracts.
- [x] Reject filesystem, network, process, environment, reflection, assembly loading, native interop,
  threading, `unsafe`, `dynamic`, directives, and additional references.
- [x] Enforce source, output, page, element, execution-time, and process-output limits.
- [x] Run C# outside the WebApi process and terminate timed-out worker trees.
- [x] Require authenticated Designer access, active organization, entitlement, feature gate, CSRF,
  rate limiting, and audit for code operations.
- [x] Block production code execution unless a hardened worker configuration is enabled.
- [x] Package the worker in the production image and apply non-root execution, a read-only root
  filesystem, an isolated no-exec temp mount, dropped capabilities, no-new-privileges, and
  PID/CPU/memory limits. Worker source analysis and trusted-reference restrictions block network
  APIs, and production execution remains disabled unless the hardened deployment flag is set.

## P0 - Workspace Persistence and API

- [x] Add tenant-owned mutable code workspaces and immutable workspace versions in PostgreSQL.
- [x] Persist four drafts, canonical design, source map, checksums, base template revision, and
  optimistic workspace revision.
- [x] Add tenant-safe get, update, validate, convert, execute, apply, restore, and source-map APIs.
- [x] Save a workspace snapshot when an explicit Designer template version is created.
- [x] Return HTTP 409 without overwriting data when template or workspace revisions are stale.
- [x] Reuse the background-job lifecycle for bounded execution metadata without storing source or
  document content; result documents remain response-only and transient.

## P0 - Designer Experience

- [x] Keep separate JSON, C# Model, C# PDF, and FromBase64String drafts and per-language status.
- [x] Never overwrite another language on tab selection.
- [x] Add Validate, Run, Convert, Compare, Apply to Designer, Restore, and Export PDF actions.
- [x] Show structural diffs, fidelity, diagnostics, source locations, and worker failure states.
- [x] Apply canonical design to the visual store only after explicit confirmation.
- [x] Migrate the legacy local draft once and stop using one global draft key.
- [x] Localize controls, diagnostics, and accessibility labels in all six Designer languages.

## P1 - Compatibility, Documentation, and Delivery

- [x] Protect and delegate the legacy `/api/templates/csharp-*` routes for one major version.
- [x] Register `designer.code-workspace` as a server-enforced Beta feature.
- [x] Document conversion behavior, sandbox limits, fidelity, assets, and visual apply workflow.
- [x] Add a customer-facing Minor release fragment without changing `VERSION`.
- [x] Add bounded telemetry that never records source code, document content, or asset names.

## Validation

- [x] Test all twelve directed conversions across JSON, C# Model, C# PDF, and FromBase64String
  against the packaged worker and the normalized canonical document.
- [x] Test pages, bindings, tenant-bound asset references, RTL, localization settings, unknown
  extension properties, stable element IDs, and source maps through the canonical model.
- [x] Test the Designer's two-second autosave, conversion review and diff, explicit apply, restore,
  and stale-revision conflict states.
- [x] Complete golden round trips for every supported element type, especially charts and binary
  asset resolution, across Designer and generated PDF output.
- [x] Test evaluated C# control flow and report the resulting source-structure loss.
- [x] Test forbidden C# capability families, timeout, cancellation, output limit, and worker failure.
- [x] Test the real Account login and PKCE handoff, separate Designer session, tenant isolation,
  entitlement denial, CSRF rejection, organization-partitioned rate limiting, and mutation audit.
- [x] Test code-job content cleanup and metadata expiry against PostgreSQL and the configured
  retention policies.
- [x] Run the focused .NET worker/conversion suite, all Designer tests, Designer type-check, and the
  Designer production build after applying the development database migration.
- [x] Run an authenticated desktop Chrome smoke test through Account, Designer, PostgreSQL, and the
  local CodeWorker for autosave, conversion review, apply, preview, and restore.
- [x] Run the same authenticated workspace smoke flow on supported mobile layouts.

## Deferred

- [ ] General-purpose C#, external packages, internet access, and offline code execution.
- [ ] Collaborative real-time code editing and language-server hosting.

## V2 - Exact Four-Representation Roundtrips

### P0 - Codec and contract architecture

- [x] Replace target-specific ad-hoc conversion with `json`, `csharpModel`, `csharpPdf`, and
  `csharpBase64` representation codecs using one normalize-and-generate pipeline.
- [x] Split result quality into `documentFidelity` and `sourcePreservation`, retaining `fidelity` as
  the compatibility alias.
- [x] Generate a real readable C# object initializer without Base64 or a hidden complete JSON
  payload in the C# Model tab.
- [x] Generate semantic `PxaPdfCodeBuilder` source that preserves every current `ElementDto` field
  and renders the resulting canonical document through the tenant-aware backend renderer.
- [x] Retain low-level `PdfDocument` execution with honest `compatible` or `reviewRequired`
  fidelity for one major version.
- [x] Add a reflection contract test that fails when any reachable writable canonical DTO property
  is omitted by the model writer.
- [x] Enforce unique page and element IDs, required element contracts, page/element limits, and the
  10 MiB canonical design limit for all four source representations.

### P0 - Base64, workspace, and UI

- [x] Add the visible FromBase64String tab with strict Base64, UTF-8, JSON, and canonical contract
  validation plus one payload-level source-map entry.
- [x] Add Base64 draft and checksum fields to mutable workspaces and immutable workspace versions,
  including the PostgreSQL migration and model snapshot.
- [x] Detect legacy `FromBase64String` model drafts, preserve their original source in the new tab,
  generate the readable model draft, increment the revision, and audit the migration.
- [x] Show all four localized tab and target names, independent statuses, conversion diff,
  document fidelity, and source preservation in all six Designer languages.
- [x] Keep the four-tab selector horizontally usable on narrow viewports and localize Preview states.

### Validation and delivery

- [x] Verify exact semantic roundtrips for Rich Text, tables, charts, bindings, RTL, localization,
  encryption, multiple properties, and unknown extension data through all twelve directions.
- [x] Verify duplicate page and element diagnostics and strict Base64 generation behavior.
- [x] Add a generated golden fixture containing every current Designer element type and compare the
  canonical JSON, readable model, semantic builder result, and backend PDF rendering.
- [x] Add dedicated malformed Base64, invalid UTF-8, unknown builder operation, cancellation,
  timeout, output-limit, and worker-unavailable integration tests.
- [ ] Add a live deployment-level memory-pressure test under the hardened container quota. The
  deterministic Compose resource-contract and bounded worker-output tests are complete.
- [x] Apply the new database migration to the local PostgreSQL instance.
- [x] Run authenticated autosave, conversion, restore, conflict, and atomic apply smoke tests
  against PostgreSQL; legacy draft migration is covered by the repository migration path.
- [x] Reconcile the Admin mutation contract count and run the complete .NET suite in addition to
  focused .NET, Designer tests, type-check, production build, and authenticated desktop/mobile
  smoke tests.
