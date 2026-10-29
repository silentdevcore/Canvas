# PXA Code Designer Roundtrip and Secure Execution

> Naming history: **PXA has been renamed to PVAD (technical name). PVADE is the market name.** PDF uses PVADP and Spreadsheet uses PVADS. Historical PXA/Canvas product references, implementation notes and compatibility identifiers are retained; task statuses are unchanged.

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

## Reviewed Apply Follow-up (2026-10-25)

- [x] Compare an older workspace with the current tenant-owned template without
  rebasing or regenerating language drafts.
- [x] Require explicit replacement approval, with element/page/document-field
  changes and full document inspection in all six Designer languages.
- [x] Send reviewed revisions and canonical checksum; reject concurrent changes
  or a changed execution result without partial writes.
- [x] Test real PostgreSQL rollback of template/workspace/audit on concurrent writes.
- [x] Flush visual autosave before comparison and acknowledge atomic Apply without
  issuing an immediate second visual save.
- [x] Pass the full customer workflow behind the repository Nginx/CSP profile,
  including desktop/mobile comparison screenshots and four code representations.
- [x] Extend that real workflow with local font/image checks, binding-preserving
  JSON export and separate PDF/Report/Spreadsheet migration previews/downloads.

See [scoped validation and remaining rename acceptance boundaries](../docs/pvad-code-workspace-apply-validation.md).
This evidence does not certify native OS printing, public deployment, or every
migration workflow. Historical completed tasks above keep their original scope.

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

## V3 - Generated Code Viewer Parity

### Viewer contract

- [x] Preserve complete page objects, including per-page physical layout, when canonical generated
  code results are adapted for Live Preview.
- [x] Normalize every document Page Settings field through the same defaults used by the visual
  Designer instead of maintaining a partial Preview-only settings model.
- [x] Bind every non-JSON Preview result to its exact language and source text.
- [x] Invalidate stale C# Model, C# PDF, and FromBase64String Preview output after a manual edit.
- [x] Keep generated conversion output previewable before execution only when its canonical result
  and generated source were returned atomically by the same conversion.
- [x] Preserve a valid C# PDF result while switching tabs and clear it only when its source changes
  or a new execution starts.

### Audit and validation

- [x] Test all four generated representations against the same canonical page layouts and document
  settings used by Canvas and normal backend PDF export.
- [x] Add UI regression coverage proving stale generated-code results are never shown as current.
- [x] Re-run all twelve backend conversion directions, the generated PDF parity fixture, Designer
  tests, type-check, and production build.
- [x] Document Preview freshness, re-execution behavior, and complete page-layout preservation in
  the Code Designer guide and public Documentation page.

## V4 - Designer Code Export Parity

- [x] Generate the JSON tab from `ExportService.buildDesignPayload`, the same complete canonical
  `DesignExportDto` used by normal backend exports.
- [x] Preserve all language-scoped elements instead of filtering the exported source to the active
  Preview language.
- [x] Preserve per-page layouts, metadata, numbering, watermarks, styles, custom properties,
  protection, encryption, localization, bindings, shared elements, and extension fields.
- [x] Generate the C# tab through the authenticated backend JSON-to-C# PDF codec using the exact
  JSON shown in the panel.
- [x] Require a persisted template before exact C# generation instead of displaying lossy local
  fallback code.
- [x] Remove the obsolete browser-side `jsonToCode` PDF generator and its divergent tests.
- [x] Add UI regression coverage for complete canonical JSON, exact C# request input, mixed page
  layouts, language-scoped elements, and unsaved-template handling.
- [x] Document the Code Export panel contract and add a customer-facing Patch release fragment.

## Native Project Format Integration

- [x] Use the canonical `DesignExportDto` shared by JSON and Code Workspace conversion as the
  editable document inside `.pxap` packages.
- [x] Keep Code Workspace drafts and source maps outside the initial package contract until their
  schema and security classification are explicitly approved in
  [PVAD.Native-Designer-Formats.md](PVAD.Native-Designer-Formats.md).
