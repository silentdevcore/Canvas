# PXA Designer Open Work Audit

## Purpose

This checklist is the current index for unfinished PXA Designer work. It reconciles older UI
checklists with the implementation as of 2026-08-23. Detailed feature requirements remain in their
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
- [x] Make release-read mutations idempotent under concurrent browser requests.
- [x] Accept the implemented Spreadsheet navigation decisions and removed migration hub.
- [x] Mark the old JSON "Open in Editor" proposal as superseded by transactional Apply to Designer.
- [x] Record that element-level visual regression tooling now exists.

## P0 - Completed Verification

- [x] Run one authenticated desktop interaction pass for drag, resize, marquee selection, context
  menus, and drop coordinates after horizontal and vertical workspace scrolling.
- [x] Run the scrollable workspace at 1280x720, 1440x900, and 1920x1080 and close its five
  acceptance checks only after inspecting overflow, panel height, and page-level scrollbars.
- [x] Run the remaining Home and navigation visual walkthrough, including the Sign DOCX toast,
  disabled Spreadsheet conversion card, sidebar persistence, and legacy route redirects.
- [x] Verify the migration handoff and old spreadsheet-draft fallback in a real browser profile.
  The anonymous Home usage keys are superseded by authenticated persisted templates.
- [x] Switch through all six UI languages in a browser, verify Arabic chrome direction and font,
  reload persistence, and add/remove mixed LTR/Arabic document languages in Page Settings.
- [x] Complete the app-shell visual consistency pass across Home, Designer, Documentation,
  migrations, importer, and PDF Viewer.

## P1 - Recommended Next Product Work

The current audit milestone is complete. The unchecked P1 items below are independently scoped
product milestones, not unverified defects in the completed Designer optimization work.

- [ ] Add versioned localization for authored release-note and notification payloads. Feature names,
  descriptions, maturity labels, decision states, notification categories, and UI fallbacks are now
  localized in all six Designer languages.
- [ ] Finish the remaining toast polish. Release drawer, notification center, feature badges, and
  product-experience states now use the current Designer color and typography system.
- [ ] Add release and notification authoring tools to PXA Admin.
- [ ] Consolidate the remaining Canvas and Live Preview element dispatchers behind the shared
  PDF-contract renderers. Track implementation and parity in
  `PXA.Designer-PDF-Visual-Parity.md`.
- [ ] Complete deterministic foreign-PDF chart recognition, raster/OCR candidates, original-asset
  restoration, and chart screenshot coverage. Track this dedicated milestone in
  `PXA.Chart-Engine-And-PDF-Recognition.md`.
- [ ] Finish active advanced-element gaps: annotation display modes, arrow endpoint snapping,
  drawing-stroke undo/redo, first/last-page placement, and export accessibility metadata. Track
  these capabilities in `UI-Advanced-Elements.md`.
- [x] Implement the remaining Code Preview polish: auto-fit, first-error Monaco marker,
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

- [x] Full Designer test run passed: 49 suites and 380 tests.
- [x] Designer product-experience API tests passed: 8 tests.
- [x] Existing generated-code, per-page layout, element-library, and visual-parity milestone tests
  remain represented by their completed source checklists.
- [x] Re-run focused Designer suites, type-check, production build, the authenticated 54-second
  Account-to-Designer flow, and the dedicated tablet/phone overflow test.

## Handoff - 2026-08-23

- [x] The current Designer audit and optimization milestone is complete.
- [x] The implementation, checklist updates, and `designer-audit-closure` release fragment are
  validated on branch `feature/designer-optimization`.
- [ ] Commit and push the completed change set before resuming this milestone.
- [ ] Resume later with one separately scoped P1 or P2 roadmap item listed above; those items do not
  block the completed milestone.
