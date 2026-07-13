# PDF Tools Web Viewer Feature Gaps

## Scope

This checklist tracks ideas from the **Pdftools Web Viewer** demo (`https://viewer.pdf-tools.com/v5/`)
as a reference for PXA viewer/review workflows and PXA.PDF-adjacent capabilities.

This is **not** a report-designer migration checklist. Pdftools Web Viewer is a browser PDF viewer and
annotation SDK, not a format like `.rdl`, `.repx`, `.jrxml`, or `.frx`.

Observed demo/source facts:

- Product/demo: `Pdftools Web Viewer`
- Demo version observed: `5.16.0`
- Browser packages observed in the demo bundle: `@pdftools/pdf-web-viewer` and `@pdftools/pdf-web-sdk`
- Demo initialization uses `PdfToolsViewer.initialize(...)`
- Demo options include `inputDocument`, `licenseKey`, and `accessibilityLayerEnabled`
- Viewer API surface observed: `document`, `documentView`, `search`, `toolbar`, `topbar`, `dialogs`,
  annotation APIs, localization APIs, and component visibility/override hooks

## Positioning

| Area | Belongs here? | Notes |
| --- | --- | --- |
| Report designer migration | No | No designer file format is involved. |
| PXA.PDF engine parity | Partly | Some features require PDF writer/model support, for example annotations, forms, redaction, accessibility. |
| PXA web viewer / review UX | Yes | Main value: viewing, searching, annotating, redacting, saving, printing. |
| Existing PDF editing | Partly | Save/annotation/redaction workflows need imported/existing PDF support, not only generation. |
| PDF Tools code migration | Adjacent | Separate from `Code-Migration-PdfTools.md` and `Code-Migration-PdfToolsToolbox.md`. |

## Already Related In PXA

- [x] PXA can generate PDFs via `PXA.Pdf`.
- [x] PXA has basic document preview/export flows.
- [x] PXA.PDF supports links/bookmarks/outlines and viewer preferences.
- [x] PXA.PDF supports basic AcroForm fields: text field, multiline text field, combo box, checkbox.
- [x] PXA has file importer and PDF importer foundations for existing documents.
- [x] PXA has a broader provider feature-gap roadmap in
      [PxaPdf-Provider-Feature-Gaps.md](PxaPdf-Provider-Feature-Gaps.md).

## Current Implementation Status

Branch `feature/pdf-tools-web-viewer` now contains a usable PDF viewer and review baseline:

- [x] Frontend route: `/pdf-viewer`
- [x] Viewer entry points: local upload, direct/backend URL, migration preview handoff, normal Designer PDF export handoff
- [x] Viewer controls: thumbnails, page navigation, zoom, fit page/width, search, download, all/current/range print
- [x] Review sidecar model: note, free text, stamp, line, rectangle, circle, ink, highlight, underline, strikeout
- [x] Review editing: select, move, resize, recolor, edit text, delete, lock/unlock
- [x] Sidecar persistence: JSON import/export plus backend save/load/delete with durable JSON file storage
- [x] Flatten workflow: current sidecar annotations can be rendered into a reviewed PDF download
- [x] Native annotation embed workflow: supported sidecar annotations can be written back as editable PDF annotation objects
- [x] Native annotation import workflow: supported existing PDF annotations can be extracted into the viewer sidecar model
- [x] Native annotation appearance baseline: exported editable annotations include normal appearance Form XObjects
- [x] Text-selection-bound markup baseline: highlight, underline, and strikeout can be created from browser text selection and persisted/exported with PDF `QuadPoints`
- [x] Native markup `QuadPoints` import baseline: existing highlight, underline, and strikeout annotations preserve text-selection quads in the viewer sidecar model
- [x] Form workflow: existing AcroForm fields can be detected, edited, downloaded as filled PDFs, and optionally flattened
- [x] Backend form reader baseline: existing AcroForm fields can be extracted through WebApi into the viewer form model
- [x] Backend form writer baseline: supported AcroForm field values can be saved through WebApi incremental updates
- [x] Backend form flattening baseline: supported AcroForm values can be rendered as page content and widgets removed
- [x] Secure redaction workflow: redaction marks can be applied through the backend to remove covered imported content and download a redacted PDF
- [x] Redaction audit metadata baseline: backend redaction output preserves count, page/area, reason, author, and timestamp data in PDF document info metadata
- [x] Backend routes:
      `POST /api/pdf-viewer/annotations`,
      `GET /api/pdf-viewer/annotations/{documentId}`,
      `DELETE /api/pdf-viewer/annotations/{documentId}`,
      `POST /api/pdf-viewer/annotations/flatten`,
      `POST /api/pdf-viewer/annotations/embed`,
      `POST /api/pdf-viewer/annotations/extract`,
      `POST /api/pdf-viewer/annotations/redact`,
      `POST /api/pdf-viewer/forms/extract`,
      `POST /api/pdf-viewer/forms/fill`
- [x] Tests:
      `PdfViewerAnnotationsControllerTests`,
      `pdfViewerAnnotations.test.ts`,
      `pdfViewerAnnotationApi.test.ts`,
      `pdfViewerForms.test.ts`,
      `pdfViewerSmoke.test.tsx`

Intentional remaining gaps:

- [ ] Sidecar backend storage is durable JSON file storage, but user ownership/access control is still open.
- [ ] Native annotation embedding is a baseline writer path for note, free text/stamp, highlight, underline, strikeout,
      rectangle, circle, and redaction annotations. Basic normal appearance streams and text-selection-bound `QuadPoints`
      exist; richer viewer-specific appearance fidelity remains open.
- [ ] Native annotation import is a baseline reader path for text, free text, highlight, underline, strikeout,
      square, circle, and redaction annotations. Markup `QuadPoints` are preserved for highlight/underline/strikeout;
      appearance streams, replies/threads, rich metadata, and unsupported annotation subtypes remain open.
- [ ] Backend form reader extracts text, multiline text, checkbox, combo/dropdown, list, and radio-like button fields
      from AcroForm dictionaries. Backend form writing/flattening and richer inherited-field edge cases remain open.
- [ ] Backend form writer updates text, multiline text, checkbox, dropdown/list, and radio-like button values with
      incremental PDF updates. Backend flattening baseline renders supported values into page content and removes
      widgets; full field appearance regeneration and complex form hierarchies remain open.
- [ ] Text markup now supports browser-selection `QuadPoints` for new highlight/underline/strikeout annotations, while
      native imported markup `QuadPoints` are preserved. Legacy/area-only markups and non-standard text-layer edge cases
      still use rectangle fallback behavior.
- [ ] Advanced controls now cover ink/line/shape stroke width, opacity, ink eraser, line endings, shape fill, custom stamps, image annotations, pending redaction marks, form filling, English/German viewer labels, keyboard shortcuts, and Jest component smoke coverage.
- [ ] Secure redaction removes importer-supported content under redaction rectangles during regenerated output, then draws black redaction boxes, and now writes a PDF-info audit metadata baseline. Remaining gap: validate/extend coverage for complex PDFs, unsupported image/resource patterns, externally signed audit logs, and optional Playwright browser smoke tests if Playwright is added to the project.

## P0 - Viewer Foundation

- [x] **PDF viewer shell** - Dedicated viewer route/page for opening a PDF output or uploaded PDF with page navigation, zoom, fit modes, and responsive layout.
      Implemented in `pxa-designer` at `/pdf-viewer`.
- [x] **Document open sources** - Open generated PDFs, uploaded local files, and backend-served PDFs through one viewer abstraction.
      Implemented for uploaded local files, direct/backend URLs, `?src=` URL handoff, migration-preview generated PDF handoff, and normal Designer PDF export handoff.
- [x] **Thumbnails/sidebar** - Page thumbnails with current-page state and click-to-navigate.
- [x] **Text search** - Search panel with result count, next/previous result, case-sensitive option, and page/result highlighting.
      Implemented with result navigation, case-sensitive search, page jump, and text-layer highlighting where PDF.js text spans contain the match.
- [x] **Print workflow** - Print current/all/range pages with an option to include annotations once annotations exist.
      Implemented all/current/range print options. Current/range print creates a temporary subset PDF with `pdf-lib`.
- [x] **Download/save workflow** - Download the current PDF; later include annotation/form changes when persisted editing exists.
      Baseline download exists for uploaded files and URL PDFs. Review annotations can be downloaded as a flattened PDF. AcroForm changes can be downloaded as filled PDFs with optional flattening.
- [x] **Viewer event API** - Emit events for open, page changed, zoom changed, print started/completed/failed, save/download, and search result selected.
      Implemented as browser `pdf-viewer:event` custom events plus a small in-view event trace.

## PXA Viewer Adaptation Plan

Goal: make the PXA PDF viewing experience feel comparable to the PDF Tools Web Viewer while keeping
our own implementation, UI language, and engine boundaries.

### Phase 1 - PDF Tools-like viewer baseline

- [x] Add a dedicated PDF viewer route/page in `pxa-designer`.
- [x] Add a reusable viewer component that accepts a generated PDF blob, upload file, or backend URL.
      Implemented as `PdfViewer` plus a thin route page. Upload, direct URL, backend URL, and session handoff are supported through `handoff.ts`.
- [x] Add top toolbar actions: open, save/download, print, search, thumbnails, zoom out/in, fit page,
      fit width, previous page, next page.
- [x] Add a left thumbnails panel with current-page highlight.
- [x] Add page navigation state: current page, total pages, direct page number input.
- [x] Add zoom state and fit modes that do not disturb page navigation.
- [x] Add search panel with result count, next/previous match, case-sensitive option, and highlighted matches.
- [x] Add print modal with all/current/range pages.
- [x] Add download/save button for the currently opened PDF.
- [x] Add responsive layout for desktop/tablet/mobile.

### Phase 2 - Review mode baseline

- [x] Add an annotation toolbar mode, separate from normal view/search mode.
      Implemented as a `Review` panel with View, Note, and Text tools.
- [x] Add text markup tools: highlight, underline, strikeout.
      Implemented as sidecar area markups plus browser-selection-bound `QuadPoints` for highlight/underline/strikeout.
      Existing area-only annotations still work as fallback.
- [x] Add free-text comment tool with font size, color, background, and border.
      Baseline free-text sidecar annotation exists with color and size controls. Advanced font/background/border controls remain open.
- [x] Add sticky note/comment tool with author and timestamp metadata.
      Baseline note annotations include author and creation timestamp in the sidecar model.
- [x] Add line, rectangle, circle, and freehand ink tools.
      Line, rectangle, circle, and freehand ink sidecar annotations are implemented. Advanced ink eraser/thickness/opacity controls remain open.
- [x] Add predefined stamps: Draft, Approved, Final, Confidential.
      Implemented as sidecar stamp annotations with placement, move, resize, color, and delete support.
- [x] Add selection/editing for annotations: move, resize, delete, lock/unlock.
      Selection, text editing, move, resize, color, size, delete, lock, and unlock are implemented for sidecar annotations.
- [x] Store annotations initially as a sidecar JSON model if writing them into PDF is not ready.
      Implemented import/export for annotation sidecar JSON.
      Sidecar schema/types and parser/serializer are centralized in `pxa-designer/src/features/pdf-viewer/annotations.ts`.

### Phase 3 - Professional PDF workflows

- [x] Show and edit existing AcroForm fields where import support exists.
      Implemented client-side AcroForm detection/editing for text, multiline text, checkbox, radio, dropdown, and list fields through `pdf-lib`.
- [x] Support save/flatten strategy for changed form values.
      Implemented download of filled PDFs plus an optional flatten-fields toggle. This is a client-side viewer workflow; backend/engine persistence remains a later bridge task.
- [x] Add redaction mark mode for text/area selections.
      Pending redaction area marks can be placed, moved, resized, persisted in sidecar JSON, and rendered into flattened reviewed PDFs.
- [x] Add secure backend redaction application when the PDF engine can remove underlying content.
      Implemented `POST /api/pdf-viewer/annotations/redact`, which removes imported graphics/text elements covered by redaction marks during regeneration and returns a redacted PDF.
- [ ] Add accessibility/text layer support for generated and imported PDFs.
- [x] Add localization hooks for English/German UI strings.
      Implemented a PDF-viewer-local EN/DE label map and visible language selector. It can later be wired to a global app locale if one is introduced.
- [ ] Add viewer configuration API to hide/show toolbar groups and override button behavior.
- [x] Add backend sidecar storage API for viewer review state.
      Implemented `POST/GET/DELETE /api/pdf-viewer/annotations` with durable version-1 sidecar JSON storage and wired UI Save/Load/Delete controls.
- [x] Add backend flatten API for reviewed PDF downloads.
      Implemented `POST /api/pdf-viewer/annotations/flatten` for PDF + sidecar input and reviewed PDF output.
- [x] Add backend native annotation embed API for editable reviewed PDF downloads.
      Implemented `POST /api/pdf-viewer/annotations/embed` for PDF + sidecar input and baseline editable PDF annotation output.
- [x] Add backend native annotation extraction API for existing reviewed PDFs.
      Implemented `POST /api/pdf-viewer/annotations/extract` for PDF input and viewer-sidecar output.
- [x] Add backend redaction API for reviewed PDF downloads.
      Implemented `POST /api/pdf-viewer/annotations/redact` for PDF + sidecar redaction marks.

### Technical Decision Points

- [ ] Decide whether the viewer rendering basis is PDF.js, browser-native PDF embedding, or a custom
      PXA rendering layer.
- [ ] Decide how generated PDFs are passed from existing preview/export flows into the viewer route.
- [x] Decide whether annotations are stored first as sidecar JSON, embedded PDF annotations, or both.
      Decision implemented: sidecar JSON remains the review-state source for the viewer, with a backend export path that
      embeds supported annotations as native PDF annotation objects.
- [ ] Decide the boundary between `PXA.Importer` existing-PDF parsing and `PXA.Pdf` rewritten output.
- [ ] Decide whether thumbnail rendering happens client-side, backend-side, or both.
- [ ] Decide how tests verify viewer behavior: unit tests for state, Playwright smoke tests for UI, and
      PDF binary tests for saved annotations/forms later.
      Added focused Jest tests for `annotations.ts` sidecar parsing/serialization and `annotationApi.ts` API client behavior. Playwright smoke tests and PDF binary tests remain open.
      Added API coverage for durable sidecar reload from disk.
      Added a Jest/jsdom PDF viewer smoke test because Playwright is not currently configured in `pxa-designer`.
      Added focused Jest coverage for AcroForm read/fill helper behavior.
      Added API coverage for applying redaction marks and verifying covered text is no longer extractable.

## P1 - Review And Annotation Workflow

- [x] **Annotation model** - Define PXA-side model for PDF annotations independent from UI widgets.
      Baseline sidecar model includes id, type, page, relative position/size, text, author, timestamp, and color.
- [x] **Text markup annotations** - Highlight, underline, squiggly, strikeout.
      Highlight, underline, and strikeout are implemented as movable/resizable sidecar area markups and can now be created
      from browser text selection with persisted/exported `QuadPoints`. Squiggly remains open.
- [x] **Free text annotations** - Add/edit text boxes with font, size, color, alignment, border/background.
      Baseline add/edit/delete/move/resize is implemented with color and size controls. Advanced font/alignment/background/border controls remain open.
- [x] **Sticky note annotations** - Add note annotations with author/date/content metadata.
- [x] **Drawing annotations** - Ink/freehand drawing with color, opacity, thickness, and eraser.
      Freehand ink drawing is implemented with color, opacity, stroke width, eraser, selection, lock/unlock, delete, sidecar persistence, and flattened PDF output.
- [x] **Line annotations** - Lines with thickness, opacity, color, and line endings.
      Line annotations support color, opacity, stroke width, start/end line endings, move, resize, delete, sidecar persistence, and flattened PDF output.
- [x] **Shape annotations** - Rectangle/circle annotations with fill, stroke, opacity, and thickness.
      Rectangle and circle annotations support stroke color, stroke width, opacity, optional fill color, move, resize, delete, sidecar persistence, and flattened PDF output.
- [x] **Stamp annotations** - Predefined text stamps such as approved/draft/confidential plus custom stamp extension point.
      Predefined Draft, Approved, Final, and Confidential stamps are implemented, and users can place/edit custom stamp text without changing the sidecar schema.
- [x] **Image annotations** - Place an image on a PDF page as an annotation/review mark.
      Image annotations support upload as sidecar data URLs, page placement, move/resize/lock/delete, opacity, sidecar persistence, and flattened PDF output.
- [x] **Annotation selection/editing** - Select, move, resize, lock/unlock, delete, and update annotations.
      Implemented for sidecar annotations.
- [x] **Annotation persistence** - Save annotations back into PDF or export/import an annotation sidecar format.
      Sidecar import/export exists in the UI, backend durable sidecar save/get/delete is wired through the viewer,
      reviewed PDFs can be downloaded with flattened annotations, and supported annotations can be exported as editable
      native PDF annotation objects. Existing-PDF annotation import and full appearance stream fidelity remain open.

## P1 - Forms And Redaction

- [x] **Form field viewing/editing** - Fill text boxes, checkboxes, radio buttons, list boxes, and combo boxes in existing PDFs.
      Implemented in the PDF viewer Forms panel for pdf-lib-readable AcroForm fields.
- [x] **Form save strategy** - Decide between saving filled fields into PDF, flattening, or sidecar persistence.
      Decision for viewer baseline: write values into a downloaded PDF and optionally flatten fields. Durable backend-side form persistence remains open under Engine/Backend Support.
- [x] **Redaction marks** - Let users mark text/page areas for redaction as visible pending annotations.
      Implemented as area-based pending redaction marks with sidecar persistence and flattened PDF output. These marks are visual and do not remove underlying PDF content.
- [x] **Apply secure redactions** - Remove underlying text/graphics/resources, not only paint black rectangles.
      Implemented backend redaction for importer-supported graphics elements under area marks, with viewer download action. Complex PDF/resource coverage still needs broader corpus testing.
- [x] **Redaction audit metadata** - Preserve reason/user/timestamp metadata for review workflows.
      Implemented backend baseline writes `RedactionAudit` PDF document info metadata with redaction count, page/area,
      reason, author, and created timestamp. External audit stores, signatures, and tamper-evident review history remain open.

## P2 - Accessibility, Localization, And Customization

- [ ] **Accessibility text layer** - Add/selectable/assistive text layer for generated or imported PDFs where text extraction is available.
- [x] **Keyboard navigation** - Viewer and annotation controls navigable by keyboard.
      Implemented viewer-scoped shortcuts for previous/next page, zoom in/out, search focus, Escape panel/selection cleanup, and Delete/Backspace for selected unlocked annotations.
- [x] **Localization** - Built-in English/German support plus override hooks for UI labels.
      The PDF viewer now has a localized EN/DE label map and viewer-level language selector for the main toolbar, search, print, review, annotation controls, and empty/loading states.
- [ ] **Custom toolbar configuration** - Hide/show viewer components and override button behavior for product-specific workflows.
- [ ] **User identity** - Viewer-level user/author identity for annotations and review metadata.
- [ ] **Plugin extension points** - Define extension surface for custom annotation tools or workflow buttons.

## P2 - Engine/Backend Support

- [ ] **Existing PDF save/edit bridge** - Decide how `PXA.Importer` and `PXA.Pdf` cooperate for editing existing PDFs.
- [x] **Annotation writer support** - Emit PDF annotations from PXA model.
      Baseline writer support exists for sticky note, free text, highlight, underline, strikeout, square, circle, and
      redaction annotations, wired to the viewer sidecar embed endpoint. Basic normal appearance Form XObjects are
      emitted for supported types; richer viewer-specific appearance fidelity remains open.
- [x] **Annotation reader support** - Import existing PDF annotations into PXA model.
      Baseline reader support extracts text, free text, highlight, underline, strikeout, square, circle, and redaction
      annotations into the viewer sidecar model. Highlight/underline/strikeout `QuadPoints` are preserved as sidecar
      text-selection quads. Rich annotation metadata, appearance stream fidelity, replies, and less common annotation
      subtypes remain open.
- [x] **Form reader support** - Import existing AcroForm fields and values.
      Frontend viewer baseline exists through `pdf-lib`; backend baseline now extracts AcroForm fields through
      `POST /api/pdf-viewer/forms/extract` into the viewer form model.
- [x] **Form writer/flattening support** - Save field changes and optionally flatten fields.
      Frontend viewer baseline can download filled/flattened PDFs through `pdf-lib`; backend baseline can save
      supported AcroForm values through `POST /api/pdf-viewer/forms/fill`, and `flatten=true` renders supported
      values into page content while removing editable widgets. Full appearance regeneration remains open.
- [ ] **PDF-to-image/page rasterization** - Backend rasterization for thumbnails or fallback preview.
- [ ] **Incremental update strategy** - Decide whether edited PDFs are fully rewritten or saved incrementally.

## PdfPreview V2 Roadmap

The current PDF Viewer/PdfPreview baseline is considered complete enough for a first professional version.
V2 should focus on hardening, configurability, collaboration boundaries, and fidelity for complex existing PDFs.

### V2 - Product And UX

- [ ] **Viewer configuration API** - Allow host pages to hide/show toolbar groups, disable workflows, and override core actions such as download, print, save, embed, redact, and form-fill.
      Acceptance: `/pdf-viewer` can be instantiated with a typed configuration object and at least one test verifies hidden toolbar groups and overridden action callbacks.
- [ ] **User identity and ownership** - Replace the local `Reviewer` default with a viewer-level identity model for author names, review sessions, and server-side ownership.
      Acceptance: annotations saved through the backend include an authenticated or configured user identity, and users cannot load/delete another user's sidecar by guessing `documentId`.
- [ ] **Access control for sidecar storage** - Move durable sidecar storage from document-id-only JSON files to an ownership-aware storage contract.
      Acceptance: save/load/delete APIs validate owner/workspace context and have negative tests for unauthorized access.
- [ ] **Plugin extension points** - Define a small extension surface for custom toolbar buttons, annotation tools, review workflows, and metadata panels.
      Acceptance: a sample custom action can be registered without editing `PdfViewer.tsx`.
- [ ] **Viewer polish pass** - Review responsive layout, dense toolbar behavior, empty states, error states, loading states, and long file names.
      Acceptance: desktop/tablet/mobile smoke screenshots show no overlapping controls or clipped critical text.

### V2 - Accessibility And Text Layer

- [ ] **Accessibility/text layer support** - Add a deliberate accessibility mode for generated and imported PDFs where text extraction is available.
      Acceptance: pages expose selectable/assistive text consistently, and keyboard users can navigate viewer controls, search results, annotations, and form fields.
- [ ] **Selection robustness** - Harden text-selection-bound highlight/underline/strikeout for rotated pages, zoom changes, multi-page drags, browser differences, and unusual PDF text segmentation.
      Acceptance: tests or smoke fixtures cover at least normal text, multiline selection, zoomed selection, and rectangle fallback.
- [ ] **Search-to-markup workflow** - Let users turn a search result into a highlight/underline/strikeout annotation.
      Acceptance: selecting a search hit creates a markup annotation with `QuadPoints` when text bounds are available.

### V2 - Annotation Fidelity

- [ ] **Richer annotation appearance fidelity** - Improve native annotation appearance streams for highlights, underline, strikeout, stamps, shapes, and opacity across common PDF viewers.
      Acceptance: exported editable annotations render recognizably in Apple Preview, Chrome, Acrobat, and PDF.js.
- [ ] **Annotation metadata import/export** - Preserve richer fields such as modified date, subject, title, name, flags, read-only/locked state, popup state, and intent where supported.
      Acceptance: supported metadata survives extract -> sidecar -> embed for baseline annotation types.
- [ ] **Replies and threads** - Import/export annotation replies, review threads, and popup comments.
      Acceptance: existing threaded comments can be extracted into sidecar metadata and re-embedded without losing ordering.
- [ ] **Additional annotation subtypes** - Evaluate and add common missing subtypes such as squiggly, caret, file attachment, polygon/polyline, and sound only where product value is clear.
      Acceptance: each added subtype has sidecar schema, UI behavior, native extract/embed coverage, and tests.
- [ ] **Complex form appearance regeneration** - Improve full appearance regeneration for edited fields, including inherited resources, fonts, radio groups, list boxes, and multiline layout.
      Acceptance: filled PDFs display changed values consistently in Acrobat/PDF.js without relying on viewer-side appearance fallback.

### V2 - Redaction And Audit Hardening

- [ ] **Complex PDF redaction corpus** - Validate secure redaction against PDFs with images, clipping paths, nested forms, transparency groups, rotated pages, and unusual resources.
      Acceptance: corpus tests prove removed content is not extractable and no visible sensitive fragments remain for covered importer-supported content.
- [ ] **Image/resource redaction coverage** - Extend redaction beyond text/vector graphics where imported image/resource patterns can be safely removed or raster-masked.
      Acceptance: covered image content is removed or irreversibly masked in regenerated output, with tests.
- [ ] **Tamper-evident audit trail** - Move beyond PDF info metadata to an external or signed audit trail for redaction workflows.
      Acceptance: redaction reason/user/timestamp/area data can be stored in a tamper-evident record and correlated with the exported PDF.
- [ ] **Redaction preview validation** - Add warnings when a redaction mark intersects unsupported content that cannot be confidently removed.
      Acceptance: backend returns structured warnings and the viewer displays them before download.

### V2 - Engine And Save Strategy

- [ ] **Existing PDF edit bridge decision** - Define when we rewrite through `PXA.Importer` + `PXA.Pdf`, when we patch incrementally, and when we reject unsupported edits.
      Acceptance: architecture note documents boundaries, failure modes, and test expectations.
- [ ] **Incremental update strategy** - Decide and implement incremental save for annotation/form changes where full rewrite is not required.
      Acceptance: simple annotation/form edits can be appended incrementally and preserve unrelated original PDF structures.
- [ ] **Backend page rasterization** - Add backend PDF-to-image/page rasterization for thumbnails, fallback preview, visual diff tests, and headless smoke checks.
      Acceptance: endpoint/service can rasterize selected pages at a requested scale with deterministic output for tests.
- [ ] **Playwright/browser smoke tests** - Add end-to-end coverage for open, search, select text, annotate, save/load sidecar, embed, redact, and form-fill flows.
      Acceptance: CI or local command runs a small smoke suite against the dev server.

## Recommendation

Start with **P0 viewer foundation** only when we are ready to invest in a PDF review workflow. The first
implementation should not try to match every PDF Tools annotation feature. A good first milestone is:

1. Viewer route/page.
2. Open generated/uploaded PDF.
3. Navigation/zoom/thumbnails.
4. Search.
5. Download/print.

After that, add annotations in this order: text markup, free text, notes, ink/line/shape, stamps, then
redaction and form editing.

## References

- Pdftools Web Viewer demo: https://viewer.pdf-tools.com/v5/
- PXA.PDF provider gaps: [PxaPdf-Provider-Feature-Gaps.md](PxaPdf-Provider-Feature-Gaps.md)
- PDF Tools SDK migration checklist: [Code-Migration-PdfTools.md](Code-Migration-PdfTools.md)
- PDF Toolbox migration checklist: [Code-Migration-PdfToolsToolbox.md](Code-Migration-PdfToolsToolbox.md)
