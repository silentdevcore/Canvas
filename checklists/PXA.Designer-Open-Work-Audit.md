# PXA Designer Open Work Audit

## Purpose

This checklist is the current index for unfinished PXA Designer work. It reconciles older UI
checklists with the implementation as of 2026-08-22. Detailed feature requirements remain in their
original checklists; this file identifies which items are still actionable and which older proposals
have been completed, superseded, or intentionally deferred.

## Audit Rules

- [x] Treat checked implementation and validated acceptance items as completed history.
- [x] Keep manual browser checks open when unit or contract tests cannot prove visual interaction.
- [x] Close old proposals that a newer authenticated or revision-safe workflow supersedes.
- [x] Keep future platform capabilities separate from current Designer product defects.
- [x] Use this file as the starting point for the next Designer milestone.

## Reconciled During This Audit

- [x] Confirm the Home, PDF, Spreadsheet, and Code routes through focused route tests.
- [x] Confirm German switching and Arabic document-direction behavior through localization tests.
- [x] Confirm the page-strip, page viewport, persistent scrollbar, and stage-height panel contracts.
- [x] Confirm release-read persistence across later requests and reopening already-read releases.
- [x] Accept the implemented Spreadsheet navigation decisions and removed migration hub.
- [x] Mark the old JSON "Open in Editor" proposal as superseded by transactional Apply to Designer.
- [x] Record that element-level visual regression tooling now exists.

## P0 - Remaining Verification

- [ ] Run one authenticated desktop interaction pass for drag, resize, marquee selection, context
  menus, and drop coordinates after horizontal and vertical workspace scrolling.
- [ ] Run the scrollable workspace at 1280x720, 1440x900, and 1920x1080 and close its five
  acceptance checks only after inspecting overflow, panel height, and page-level scrollbars.
- [ ] Run the remaining Home and navigation visual walkthrough, including the Sign DOCX toast,
  disabled Spreadsheet conversion card, sidebar persistence, and legacy route redirects.
- [ ] Verify the legacy Home usage keys, migration handoff, and old spreadsheet-draft fallback in a
  real browser profile containing pre-migration local storage.
- [ ] Switch through all six UI languages in a browser, verify Arabic chrome direction and font,
  reload persistence, and add/remove mixed LTR/Arabic document languages in Page Settings.
- [ ] Complete the app-shell visual consistency pass across Home, Designer, Documentation,
  migrations, importer, and PDF Viewer.

## P1 - Recommended Next Product Work

- [ ] Localize release notes, notification content, feature descriptions, and fallback states in all
  six Designer languages.
- [ ] Align the release drawer, notification center, feature badges, and toast presentation with the
  current Designer visual system.
- [ ] Add release and notification authoring tools to PXA Admin.
- [ ] Consolidate the remaining Canvas and Live Preview element dispatchers behind the shared
  PDF-contract renderers.
- [ ] Complete deterministic foreign-PDF chart recognition, raster/OCR candidates, original-asset
  restoration, and chart screenshot coverage.
- [ ] Finish active advanced-element gaps: annotation display modes, arrow endpoint snapping,
  drawing-stroke undo/redo, first/last-page placement, and export accessibility metadata.
- [ ] Decide and implement the remaining Code Preview polish: auto-fit, first-error Monaco marker,
  and editor line/character count.

## P2 - Scalability And Hardening

- [ ] Run Code Worker memory-pressure tests under the hardened container quota.
- [ ] Move generated template previews, source documents, and attachments to the object-storage
  abstraction with orphan cleanup and retention behavior.
- [ ] Define privileged template ownership transfer and permanent deletion.

## Deferred Platform Work

- [ ] Build and validate the production Designer container and shared PXA Server Compose bundle.
- [ ] Add Enterprise OIDC/SAML, MFA step-up, and coordinated global sign-out.
- [ ] Add real-time notification push.
- [ ] Add general-purpose C#, external packages, internet access, and offline code execution.
- [ ] Add real-time collaborative editing, presence, branches, review, and merge workflows.
- [ ] Add native Spreadsheet chart authoring and XLSX chart import/export.
- [ ] Build a dedicated Spreadsheet code workspace, template gallery, and read-only viewer when the
  Spreadsheet roadmap prioritizes full parity with PDF.

## Validation Record

- [x] Focused Designer audit suites passed: 5 suites and 20 tests.
- [x] Designer product-experience API tests passed: 8 tests.
- [x] Existing generated-code, per-page layout, element-library, and visual-parity milestone tests
  remain represented by their completed source checklists.
- [ ] Re-run the full Designer suite after the next implementation milestone, not for checklist-only
  wording changes.
