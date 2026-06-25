# PDF Tools Web Viewer Feature Gaps

## Scope

This checklist tracks ideas from the **Pdftools Web Viewer** demo (`https://viewer.pdf-tools.com/v5/`)
as a reference for Canvas viewer/review workflows and Canvas.PDF-adjacent capabilities.

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
| Canvas.PDF engine parity | Partly | Some features require PDF writer/model support, for example annotations, forms, redaction, accessibility. |
| Canvas web viewer / review UX | Yes | Main value: viewing, searching, annotating, redacting, saving, printing. |
| Existing PDF editing | Partly | Save/annotation/redaction workflows need imported/existing PDF support, not only generation. |
| PDF Tools code migration | Adjacent | Separate from `Code-Migration-PdfTools.md` and `Code-Migration-PdfToolsToolbox.md`. |

## Already Related In Canvas

- [x] Canvas can generate PDFs via `Canvas.Pdf`.
- [x] Canvas has basic document preview/export flows.
- [x] Canvas.PDF supports links/bookmarks/outlines and viewer preferences.
- [x] Canvas.PDF supports basic AcroForm fields: text field, multiline text field, combo box, checkbox.
- [x] Canvas has file importer and PDF importer foundations for existing documents.
- [x] Canvas has a broader provider feature-gap roadmap in
      [CanvasPdf-Provider-Feature-Gaps.md](CanvasPdf-Provider-Feature-Gaps.md).

## P0 - Viewer Foundation

- [x] **PDF viewer shell** - Dedicated viewer route/page for opening a PDF output or uploaded PDF with page navigation, zoom, fit modes, and responsive layout.
      Implemented in `ui-designer-v2` at `/pdf-viewer`.
- [x] **Document open sources** - Open generated PDFs, uploaded local files, and backend-served PDFs through one viewer abstraction.
      Implemented for uploaded local files, direct/backend URLs, `?src=` URL handoff, migration-preview generated PDF handoff, and normal Designer PDF export handoff.
- [x] **Thumbnails/sidebar** - Page thumbnails with current-page state and click-to-navigate.
- [x] **Text search** - Search panel with result count, next/previous result, case-sensitive option, and page/result highlighting.
      Implemented with result navigation, case-sensitive search, page jump, and text-layer highlighting where PDF.js text spans contain the match.
- [x] **Print workflow** - Print current/all/range pages with an option to include annotations once annotations exist.
      Implemented all/current/range print options. Current/range print creates a temporary subset PDF with `pdf-lib`.
- [ ] **Download/save workflow** - Download the current PDF; later include annotation/form changes when persisted editing exists.
      Baseline download exists for uploaded files and URL PDFs. Save/persist edited PDF remains open.
- [x] **Viewer event API** - Emit events for open, page changed, zoom changed, print started/completed/failed, save/download, and search result selected.
      Implemented as browser `pdf-viewer:event` custom events plus a small in-view event trace.

## Canvas Viewer Adaptation Plan

Goal: make the Canvas PDF viewing experience feel comparable to the PDF Tools Web Viewer while keeping
our own implementation, UI language, and engine boundaries.

### Phase 1 - PDF Tools-like viewer baseline

- [x] Add a dedicated PDF viewer route/page in `ui-designer-v2`.
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
      Implemented as sidecar area markups. True text-selection-bound PDF markup remains a later precision step.
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
      Sidecar schema/types and parser/serializer are centralized in `ui-designer-v2/src/features/pdf-viewer/annotations.ts`.

### Phase 3 - Professional PDF workflows

- [ ] Show and edit existing AcroForm fields where import support exists.
- [ ] Support save/flatten strategy for changed form values.
- [ ] Add redaction mark mode for text/area selections.
- [ ] Add secure backend redaction application when the PDF engine can remove underlying content.
- [ ] Add accessibility/text layer support for generated and imported PDFs.
- [ ] Add localization hooks for English/German UI strings.
- [ ] Add viewer configuration API to hide/show toolbar groups and override button behavior.

### Technical Decision Points

- [ ] Decide whether the viewer rendering basis is PDF.js, browser-native PDF embedding, or a custom
      Canvas rendering layer.
- [ ] Decide how generated PDFs are passed from existing preview/export flows into the viewer route.
- [x] Decide whether annotations are stored first as sidecar JSON, embedded PDF annotations, or both.
      Decision for first implementation: sidecar JSON first; embedded PDF annotations remain a later engine/backend task.
- [ ] Decide the boundary between `Canvas.Importer` existing-PDF parsing and `Canvas.Pdf` rewritten output.
- [ ] Decide whether thumbnail rendering happens client-side, backend-side, or both.
- [ ] Decide how tests verify viewer behavior: unit tests for state, Playwright smoke tests for UI, and
      PDF binary tests for saved annotations/forms later.
      Follow-up: add focused unit tests for `annotations.ts` sidecar parsing/serialization.

## P1 - Review And Annotation Workflow

- [x] **Annotation model** - Define Canvas-side model for PDF annotations independent from UI widgets.
      Baseline sidecar model includes id, type, page, relative position/size, text, author, timestamp, and color.
- [x] **Text markup annotations** - Highlight, underline, squiggly, strikeout.
      Highlight, underline, and strikeout are implemented as movable/resizable sidecar area markups. Squiggly and text-selection-bound markup remain open.
- [x] **Free text annotations** - Add/edit text boxes with font, size, color, alignment, border/background.
      Baseline add/edit/delete/move/resize is implemented with color and size controls. Advanced font/alignment/background/border controls remain open.
- [x] **Sticky note annotations** - Add note annotations with author/date/content metadata.
- [ ] **Drawing annotations** - Ink/freehand drawing with color, opacity, thickness, and eraser.
      Baseline freehand ink drawing is implemented with color, selection, lock/unlock, delete, and sidecar persistence. Opacity, thickness, and eraser remain open.
- [ ] **Line annotations** - Lines with thickness, opacity, color, and line endings.
      Baseline line annotations are implemented with color, move, resize, and delete. Thickness, opacity, and line endings remain open.
- [ ] **Shape annotations** - Rectangle/circle annotations with fill, stroke, opacity, and thickness.
      Baseline rectangle and circle annotations are implemented with color, move, resize, and delete. Fill, opacity, and stroke thickness controls remain open.
- [x] **Stamp annotations** - Predefined text stamps such as approved/draft/confidential plus custom stamp extension point.
      Predefined Draft, Approved, Final, and Confidential stamps are implemented. Custom stamp extension point remains open.
- [ ] **Image annotations** - Place an image on a PDF page as an annotation/review mark.
- [x] **Annotation selection/editing** - Select, move, resize, lock/unlock, delete, and update annotations.
      Implemented for sidecar annotations.
- [ ] **Annotation persistence** - Save annotations back into PDF or export/import an annotation sidecar format.

## P1 - Forms And Redaction

- [ ] **Form field viewing/editing** - Fill text boxes, checkboxes, radio buttons, list boxes, and combo boxes in existing PDFs.
- [ ] **Form save strategy** - Decide between saving filled fields into PDF, flattening, or sidecar persistence.
- [ ] **Redaction marks** - Let users mark text/page areas for redaction as visible pending annotations.
- [ ] **Apply secure redactions** - Remove underlying text/graphics/resources, not only paint black rectangles.
- [ ] **Redaction audit metadata** - Preserve reason/user/timestamp metadata for review workflows.

## P2 - Accessibility, Localization, And Customization

- [ ] **Accessibility text layer** - Add/selectable/assistive text layer for generated or imported PDFs where text extraction is available.
- [ ] **Keyboard navigation** - Viewer and annotation controls navigable by keyboard.
- [ ] **Localization** - Built-in English/German support plus override hooks for UI labels.
- [ ] **Custom toolbar configuration** - Hide/show viewer components and override button behavior for product-specific workflows.
- [ ] **User identity** - Viewer-level user/author identity for annotations and review metadata.
- [ ] **Plugin extension points** - Define extension surface for custom annotation tools or workflow buttons.

## P2 - Engine/Backend Support

- [ ] **Existing PDF save/edit bridge** - Decide how `Canvas.Importer` and `Canvas.Pdf` cooperate for editing existing PDFs.
- [ ] **Annotation writer support** - Emit PDF annotations from Canvas model.
- [ ] **Annotation reader support** - Import existing PDF annotations into Canvas model.
- [ ] **Form reader support** - Import existing AcroForm fields and values.
- [ ] **Form writer/flattening support** - Save field changes and optionally flatten fields.
- [ ] **PDF-to-image/page rasterization** - Backend rasterization for thumbnails or fallback preview.
- [ ] **Incremental update strategy** - Decide whether edited PDFs are fully rewritten or saved incrementally.

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
- Canvas.PDF provider gaps: [CanvasPdf-Provider-Feature-Gaps.md](CanvasPdf-Provider-Feature-Gaps.md)
- PDF Tools SDK migration checklist: [Code-Migration-PdfTools.md](Code-Migration-PdfTools.md)
- PDF Toolbox migration checklist: [Code-Migration-PdfToolsToolbox.md](Code-Migration-PdfToolsToolbox.md)
