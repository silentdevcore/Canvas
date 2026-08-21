# PXA Designer Document Naming and Export Audit

## Goal

Provide persistent document renaming and make every Create PDF export action produce exactly one
correctly named artifact whose media type, extension, and bytes agree.

## P0 - Document Naming

- [x] Add accessible inline title editing with explicit edit affordance, Enter/blur commit, and
  Escape cancel.
- [x] Validate trimmed document names between 1 and 200 characters in all six Designer languages.
- [x] Persist renames through the existing optimistic autosave revision.
- [x] Synchronize Designer template metadata and the nested draft document name atomically.
- [x] Treat server metadata as authoritative when loading legacy inconsistent drafts.
- [x] Use one safe, non-empty document name for gallery entries, versions, metadata, and exports.

## P0 - Export Reliability

- [x] Ensure each quick-export action produces exactly one intended result.
- [x] Remove the implicit JSON download after PDF, image, and print actions.
- [x] Build JSON, PDF, backend formats, and code exports from one canonical design payload.
- [x] Validate successful PDF responses by status, media type, and `%PDF-` signature.
- [x] Print the backend-rendered PDF instead of the Designer HTML page.
- [x] Handle blocked print windows, offline services, invalid responses, and duplicate clicks.
- [x] Prefer Content-Disposition filenames and use the sanitized document name as fallback.
- [x] Disable unavailable formats with an actionable reason while keeping them visible.

## P0 - Format Audit

- [x] Verify PDF, JSON, multilingual PDF ZIP, DOCX, ODT, HTML, XML, XLSX, CSV, PNG, JPEG,
  TIFF, SVG, and Markdown exports.
- [x] Return native image bytes for one-page PNG, JPEG, TIFF, and SVG exports.
- [x] Return `application/zip` with one correctly encoded image per page for multi-page image
  exports.
- [x] Ensure response Content-Type, Content-Disposition, extension, and bytes always agree.
- [x] Keep the Code Workspace PDF export on the same validated PDF download path.

## Validation

- [x] Test inline rename commit, cancel, validation, autosave, conflict, and reload behavior.
- [x] Test atomic name synchronization and optimistic revision conflicts in the API.
- [x] Add an API export matrix covering media types, filenames, and file signatures.
- [x] Inspect multi-page image ZIP entries and verify every encoded page.
- [x] Add a regression test proving PDF export never triggers a JSON download.
- [x] Test that print renders a PDF and never calls `window.print()` on the Designer page.
- [x] Run authenticated desktop smoke coverage for rename, PDF, JSON, DOCX, PNG ZIP, and print.
- [x] Run Designer tests, type-check, production build, relevant .NET tests, and `git diff --check`.

## Spreadsheet And Standalone PDF Viewer Audit

- [x] Validate Spreadsheet XLSX responses by status, media type, ZIP signature, and
  Content-Disposition filename before downloading.
- [x] Use one sanitized workbook name for XLSX, CSV, JSON, and rendered Spreadsheet PDF files.
- [x] Reject unsupported Spreadsheet export formats instead of returning mislabeled XLSX bytes.
- [x] Prevent rapid duplicate Spreadsheet exports.
- [x] Download standalone Viewer PDFs from validated bytes rather than navigating to a remote URL.
- [x] Open the Viewer print target synchronously, render the selected PDF pages, and invoke that
  target's native PDF print dialog.
- [x] Keep print/download failures visible and localized, including invalid PDF responses and
  popup blocking.
- [x] Add frontend and API regression coverage for Spreadsheet exports and Viewer download/print.
