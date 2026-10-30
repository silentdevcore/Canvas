# PVAD Native Designer Formats

## Goal

Define safe, versioned, portable native project formats for PVADE Designer:

- `.pvadp` for PDF Designer projects.
- `.pvads` for Spreadsheet Designer projects.

These formats preserve editable project state, assets, bindings, localization,
settings, and compatibility metadata. They are project packages, not final PDF
or spreadsheet delivery formats.

## Current Naming Contract

New packages use `application/vnd.pvad.pvadp+zip` and
`application/vnd.pvad.pvads+zip`, with magic identifiers `PVAD-PVADP-1` and
`PVAD-PVADS-1`. Current API examples use `/api/pvad/v1/designer/projects`.
See the [current format guide](../docs/native-designer-project-formats.md).

Task wording uses current names. Earlier formats remain readable through
explicit compatibility readers. Editorial normalization does not change original
package bytes, test results, or pending platform and artwork approvals.

## Priorities

- [x] P0: Specify one common package envelope and separate PDF and Spreadsheet
  document schemas.
- [x] P0: Implement deterministic save, open, validation, and recovery.
- [x] P0: Protect import against hostile archives, oversized assets, and schema
  confusion.
- [x] P1: Integrate the formats with Designer persistence, versions, assets, and
  export workflows.
- [x] P1: Publish format documentation, fixtures, and compatibility guarantees.
- [ ] P2: Evaluate signatures and organization templates.

## Naming And Media Types

- [x] Use lowercase canonical file extensions `.pvadp` and `.pvads`; UI labels may
  display them as `PVADP` and `PVADS`.
- [x] Define PVADP as **PVAD PDF Project** and PVADS as **PVAD Spreadsheet Project**.
- [x] Retire the generic `.pxa` reservation. Native Designer projects use the
  explicit `.pvadp` and `.pvads` formats only.
- [x] Register internal media types:
  `application/vnd.pvad.pvadp+zip` and
  `application/vnd.pvad.pvads+zip`.
- [x] Use distinct magic/package identifiers so renamed ZIP or JSON files cannot
  be accepted as native projects.
- [ ] Define stable icons and OS file associations only after the logo system is
  approved.

## Common Package Envelope

- [x] Use a ZIP-compatible package with a mandatory `manifest.json` at its root.
- [x] Require manifest fields for format ID, schema version, minimum compatible
  PVADE version, document ID, title, created/modified timestamps, locale, entry
  document, asset index, and content checksums.
- [x] Keep generated previews, source files, fonts, images, and other assets in
  typed package folders referenced through stable asset IDs.
- [x] Prohibit executable code, macros, external packages, absolute paths, and
  network URLs as trusted package content.
- [x] Define canonical JSON serialization and deterministic ZIP entry ordering.
- [x] Compute SHA-256 for every indexed entry and a package-level canonical hash.
- [x] Store UTF-8 without a byte-order mark and normalize package paths to `/`.
- [x] Preserve unknown forward-compatible manifest and document properties in a
  bounded extension area.
- [x] Never include authentication tokens, local filesystem paths, temporary
  object-storage URLs, secrets, or tenant identifiers intended only for servers.

## PVADP Document Contract

- [x] Store the normalized `DesignExportDto` document as the canonical editable
  PDF design.
- [x] Preserve pages, per-page layouts, elements, stable IDs, styles, bindings,
  localization, RTL, assets, accessibility, forms, charts, page settings, and
  supported PDF Trust settings.
- [x] Preserve Code Workspace drafts and source maps only as optional project
  components with an explicit schema and security classification.
- [x] Keep final PDF bytes optional and non-authoritative; reopening must use the
  editable design contract.
- [x] Record required Designer features and warn before opening a project that
  uses unavailable or disabled functionality.

## PVADS Document Contract

- [x] Store the normalized workbook model as the canonical editable Spreadsheet
  design.
- [x] Preserve sheets, cells, formulas, styles, named ranges, tables, charts,
  validation, conditional formatting, print settings, bindings, localization,
  RTL, and embedded assets.
- [x] Preserve formula text and cached results separately without treating cached
  values as authoritative calculations.
- [x] Keep final XLSX/CSV/PDF exports optional and non-authoritative.
- [x] Record unsupported external links, macros, and provider-specific features as
  diagnostics rather than silently dropping them.

## Open, Save, And Persistence

- [x] Add `Open project` and `Save project` actions to the PDF and Spreadsheet
  Designer areas.
- [x] Use atomic writes through a temporary file and final rename where the host
  platform permits it.
- [x] Validate the complete package before replacing the active document.
- [x] Open a project as one undoable transaction and leave the current document
  untouched on failure.
- [x] Resolve packaged assets into the organization-scoped Asset Store without
  trusting package asset IDs as database IDs.
- [x] Generate fresh tenant-owned asset IDs and rewrite internal references.
- [x] Preserve document and element IDs unless importing would collide with an
  existing target selected by the user.
- [x] Integrate project save/open with template revisions and optimistic
  concurrency without making local files tenant-bound.
- [x] Define recovery behavior for interrupted writes and partially uploaded
  packages.

## Security And Resource Limits

- [x] Reject path traversal, symlinks, duplicate normalized paths, encrypted ZIP
  entries, nested archives, unsupported compression, and compression bombs.
- [x] Bound compressed size, expanded size, file count, nesting, JSON depth,
  element count, cells, formulas, image pixels, and parsing duration.
- [x] Validate declared sizes and checksums before materializing assets.
- [x] Sniff asset bytes instead of trusting filenames or declared MIME types.
- [x] Sanitize SVG, HTML, Rich Text, metadata, and filenames using existing PVADE
  security policies.
- [x] Do not execute Code Workspace content while opening a package.
- [x] Apply tenant, entitlement, permission, rate-limit, audit, and cancellation
  policies to server-side package operations.
- [x] Return stable `PXAPROJECT...` diagnostics without exposing filesystem or
  archive-parser internals.

## Versioning And Compatibility

- [x] Version the envelope independently from the PDF and Spreadsheet document
  schemas.
- [x] Define backward-compatible minor changes and migration-required major
  changes.
- [x] Maintain pure upgrade steps and never downgrade in place.
- [x] Preserve the original package until an upgrade and save succeeds.
- [ ] Support at least the previous major project version after the first major
  schema transition.
- [x] Reject unknown future major versions with a clear minimum-version message.
- [x] Record upgrade diagnostics and lossy transformations in a user-visible
  report.

## API, SDK, And Documentation

- [x] Add authenticated inspect, validate, import, and export endpoints under
  `/api/pvad/v1/designer/projects`.
- [x] Expose envelope metadata without extracting full assets when possible.
- [x] Add OpenAPI contracts and SDK streaming helpers for PXAP and PXAS.
- [x] Document format purpose, security model, version policy, package layout,
  recovery, and differences from PDF, XLSX, JSON, and template persistence.
- [x] Publish small synthetic reference fixtures and JSON Schemas.
- [x] Keep private customer projects out of public fixtures and telemetry.

## Validation

- [x] Roundtrip every PDF Designer element and every Spreadsheet model feature.
- [x] Test assets, bindings, localization, RTL, per-page settings, formulas,
  charts, and extension data.
- [x] Verify deterministic output and identical package hashes for identical
  normalized projects.
- [x] Test old, current, malformed, truncated, tampered, oversized, and future
  packages.
- [x] Add archive-security tests for traversal, duplicate paths, bombs, nested
  archives, invalid UTF-8, and MIME spoofing.
- [x] Verify failed opens never partially modify the active design or Asset Store.
- [x] Test desktop and mobile import states and large-project progress reporting.
- [ ] Run Designer, API, persistence, Generator, Spreadsheet, Documentation, and
  cross-platform filesystem tests.
  - Local validation on 2026-10-02 passed Designer (639 tests), Core (126), native
    project API and OpenAPI contracts (19), the full API suite (747 passed / 66 PostgreSQL-only skipped),
    Generator (137), Spreadsheet (7), Persistence (8 passed / 2 PostgreSQL-only
    skipped), Documentation (56), the complete Solution build, and both production
    builds. The Designer TypeScript check also passes.
  - The Solution build and all 220 non-LibreOffice Export tests pass. The external
    LibreOffice fidelity run also passes after aligning DOCX table minimum row heights
    and vertical alignment with the PDF renderer; `dense-table-ledger` improved from
    `0.1551` to `0.1462` against the unchanged `0.15` threshold.
  - The macOS filesystem and package behavior is covered locally. Windows and Linux
    filesystem runs remain required before this aggregate cross-platform task closes.

## Acceptance Criteria

- [x] PVADP and PVADS projects reopen with the same editable content and assets.
- [x] The two formats cannot be confused with each other or with exported files.
- [x] A hostile package cannot escape extraction limits or access another tenant.
- [x] Package upgrades are explicit, tested, reversible until save, and
  diagnostically transparent.
- [x] Format documentation and fixtures are sufficient for future SDK support.

## Dependencies And References

- [x] Align with
  [PVAD.Designer-Template-Persistence.md](PVAD.Designer-Template-Persistence.md).
- [x] Align PDF content with
  [PVAD.Code-Designer-Roundtrip.md](PVAD.Code-Designer-Roundtrip.md) and
  [PVAD.Designer-Per-Page-Settings-And-Backend-Parity.md](PVAD.Designer-Per-Page-Settings-And-Backend-Parity.md).
- [x] Align version policy with
  [PVAD.Application-Versioning-And-Releases.md](PVAD.Application-Versioning-And-Releases.md).
- [x] Replace the generic `.pxa` reservation in
  [the earlier rename record](PVAD.Legacy-Rename-Archive.md) with the implemented `.pvadp` and `.pvads`
  project formats.

## Assumptions

- PVADP and PVADS are editable project packages, not archival or final delivery
  formats.
- JSON remains available for interoperability but is not a complete substitute
  for packaged assets and workspace state.
- Local project files are portable and do not grant access to server-side assets
  or organizations.
- Digital package signing and encrypted project packages are later milestones.

## Legacy Reader Values

The earlier `.pxap` / `.pxas` packages retain the exact media types
`application/vnd.powerdox.pxap+zip` / `application/vnd.powerdox.pxas+zip` and
the `PXA-PXAP-1` / `PXA-PXAS-1` magic identifiers. Readers preserve those values
for compatibility; new writers use the current naming contract above.
