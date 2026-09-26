# PXA Native Designer Formats

## Goal

Define safe, versioned, portable native project formats for PXA Designer:

- `.pxap` for PDF Designer projects.
- `.pxas` for Spreadsheet Designer projects.

These formats preserve editable project state, assets, bindings, localization,
settings, and compatibility metadata. They are project packages, not final PDF
or spreadsheet delivery formats.

## Priorities

- [ ] P0: Specify one common package envelope and separate PDF and Spreadsheet
  document schemas.
- [ ] P0: Implement deterministic save, open, validation, and recovery.
- [ ] P0: Protect import against hostile archives, oversized assets, and schema
  confusion.
- [ ] P1: Integrate the formats with Designer persistence, versions, assets, and
  export workflows.
- [ ] P1: Publish format documentation, fixtures, and compatibility guarantees.
- [ ] P2: Evaluate signatures, organization templates, and a future generic
  `.pxa` exchange container.

## Naming And Media Types

- [ ] Use lowercase canonical file extensions `.pxap` and `.pxas`; UI labels may
  display them as `PXAP` and `PXAS`.
- [ ] Define PXAP as **PXA PDF Project** and PXAS as **PXA Spreadsheet Project**.
- [ ] Reserve `.pxa` for a possible future multi-document exchange package; do
  not alias it to either initial format.
- [ ] Register internal media types:
  `application/vnd.powerdox.pxap+zip` and
  `application/vnd.powerdox.pxas+zip`.
- [ ] Use distinct magic/package identifiers so renamed ZIP or JSON files cannot
  be accepted as native projects.
- [ ] Define stable icons and OS file associations only after the logo system is
  approved.

## Common Package Envelope

- [ ] Use a ZIP-compatible package with a mandatory `manifest.json` at its root.
- [ ] Require manifest fields for format ID, schema version, minimum compatible
  PXA version, document ID, title, created/modified timestamps, locale, entry
  document, asset index, and content checksums.
- [ ] Keep generated previews, source files, fonts, images, and other assets in
  typed package folders referenced through stable asset IDs.
- [ ] Prohibit executable code, macros, external packages, absolute paths, and
  network URLs as trusted package content.
- [ ] Define canonical JSON serialization and deterministic ZIP entry ordering.
- [ ] Compute SHA-256 for every indexed entry and a package-level canonical hash.
- [ ] Store UTF-8 without a byte-order mark and normalize package paths to `/`.
- [ ] Preserve unknown forward-compatible manifest and document properties in a
  bounded extension area.
- [ ] Never include authentication tokens, local filesystem paths, temporary
  object-storage URLs, secrets, or tenant identifiers intended only for servers.

## PXAP Document Contract

- [ ] Store the normalized `DesignExportDto` document as the canonical editable
  PDF design.
- [ ] Preserve pages, per-page layouts, elements, stable IDs, styles, bindings,
  localization, RTL, assets, accessibility, forms, charts, page settings, and
  supported PDF Trust settings.
- [ ] Preserve Code Workspace drafts and source maps only as optional project
  components with an explicit schema and security classification.
- [ ] Keep final PDF bytes optional and non-authoritative; reopening must use the
  editable design contract.
- [ ] Record required Designer features and warn before opening a project that
  uses unavailable or disabled functionality.

## PXAS Document Contract

- [ ] Store the normalized workbook model as the canonical editable Spreadsheet
  design.
- [ ] Preserve sheets, cells, formulas, styles, named ranges, tables, charts,
  validation, conditional formatting, print settings, bindings, localization,
  RTL, and embedded assets.
- [ ] Preserve formula text and cached results separately without treating cached
  values as authoritative calculations.
- [ ] Keep final XLSX/CSV/PDF exports optional and non-authoritative.
- [ ] Record unsupported external links, macros, and provider-specific features as
  diagnostics rather than silently dropping them.

## Open, Save, And Persistence

- [ ] Add `Open project` and `Save project` actions to the PDF and Spreadsheet
  Designer areas.
- [ ] Use atomic writes through a temporary file and final rename where the host
  platform permits it.
- [ ] Validate the complete package before replacing the active document.
- [ ] Open a project as one undoable transaction and leave the current document
  untouched on failure.
- [ ] Resolve packaged assets into the organization-scoped Asset Store without
  trusting package asset IDs as database IDs.
- [ ] Generate fresh tenant-owned asset IDs and rewrite internal references.
- [ ] Preserve document and element IDs unless importing would collide with an
  existing target selected by the user.
- [ ] Integrate project save/open with template revisions and optimistic
  concurrency without making local files tenant-bound.
- [ ] Define recovery behavior for interrupted writes and partially uploaded
  packages.

## Security And Resource Limits

- [ ] Reject path traversal, symlinks, duplicate normalized paths, encrypted ZIP
  entries, nested archives, unsupported compression, and compression bombs.
- [ ] Bound compressed size, expanded size, file count, nesting, JSON depth,
  element count, cells, formulas, image pixels, and parsing duration.
- [ ] Validate declared sizes and checksums before materializing assets.
- [ ] Sniff asset bytes instead of trusting filenames or declared MIME types.
- [ ] Sanitize SVG, HTML, Rich Text, metadata, and filenames using existing PXA
  security policies.
- [ ] Do not execute Code Workspace content while opening a package.
- [ ] Apply tenant, entitlement, permission, rate-limit, audit, and cancellation
  policies to server-side package operations.
- [ ] Return stable `PXAPROJECT...` diagnostics without exposing filesystem or
  archive-parser internals.

## Versioning And Compatibility

- [ ] Version the envelope independently from the PDF and Spreadsheet document
  schemas.
- [ ] Define backward-compatible minor changes and migration-required major
  changes.
- [ ] Maintain pure upgrade steps and never downgrade in place.
- [ ] Preserve the original package until an upgrade and save succeeds.
- [ ] Support at least the previous major project version after the first major
  schema transition.
- [ ] Reject unknown future major versions with a clear minimum-version message.
- [ ] Record upgrade diagnostics and lossy transformations in a user-visible
  report.

## API, SDK, And Documentation

- [ ] Add authenticated inspect, validate, import, and export endpoints under
  `/api/pxa/v1/designer/projects`.
- [ ] Expose envelope metadata without extracting full assets when possible.
- [ ] Add OpenAPI contracts and SDK streaming helpers for PXAP and PXAS.
- [ ] Document format purpose, security model, version policy, package layout,
  recovery, and differences from PDF, XLSX, JSON, and template persistence.
- [ ] Publish small synthetic reference fixtures and JSON Schemas.
- [ ] Keep private customer projects out of public fixtures and telemetry.

## Validation

- [ ] Roundtrip every PDF Designer element and every Spreadsheet model feature.
- [ ] Test assets, bindings, localization, RTL, per-page settings, formulas,
  charts, and extension data.
- [ ] Verify deterministic output and identical package hashes for identical
  normalized projects.
- [ ] Test old, current, malformed, truncated, tampered, oversized, and future
  packages.
- [ ] Add archive-security tests for traversal, duplicate paths, bombs, nested
  archives, invalid UTF-8, and MIME spoofing.
- [ ] Verify failed opens never partially modify the active design or Asset Store.
- [ ] Test desktop and mobile import states and large-project progress reporting.
- [ ] Run Designer, API, persistence, Generator, Spreadsheet, Documentation, and
  cross-platform filesystem tests.

## Acceptance Criteria

- [ ] PXAP and PXAS projects reopen with the same editable content and assets.
- [ ] The two formats cannot be confused with each other or with exported files.
- [ ] A hostile package cannot escape extraction limits or access another tenant.
- [ ] Package upgrades are explicit, tested, reversible until save, and
  diagnostically transparent.
- [ ] Format documentation and fixtures are sufficient for future SDK support.

## Dependencies And References

- [ ] Align with
  [PXA.Designer-Template-Persistence.md](PXA.Designer-Template-Persistence.md).
- [ ] Align PDF content with
  [PXA.Code-Designer-Roundtrip.md](PXA.Code-Designer-Roundtrip.md) and
  [PXA.Designer-Per-Page-Settings-And-Backend-Parity.md](PXA.Designer-Per-Page-Settings-And-Backend-Parity.md).
- [ ] Align version policy with
  [PXA.Application-Versioning-And-Releases.md](PXA.Application-Versioning-And-Releases.md).
- [ ] Replace the generic `.pxa` reservation in
  [Rename-To-PXA.md](Rename-To-PXA.md) only after the future container is
  explicitly specified.

## Assumptions

- PXAP and PXAS are editable project packages, not archival or final delivery
  formats.
- JSON remains available for interoperability but is not a complete substitute
  for packaged assets and workspace state.
- Local project files are portable and do not grant access to server-side assets
  or organizations.
- Digital package signing and encrypted project packages are later milestones.

