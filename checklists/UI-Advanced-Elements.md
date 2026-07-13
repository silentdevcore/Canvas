# UI Advanced Elements Checklist

## Scope
Add document editing elements for annotations, markings, page controls, and reusable PDF authoring helpers.

## Definition of Done
- [x] Elements can be added from the sidebar/tool palette.
- [x] Elements render correctly on canvas and in document preview.
- [x] Elements expose focused property controls in the Properties panel.
- [x] Elements support selection, dragging, resizing, duplication, and deletion where applicable.
- [x] Elements are included in template JSON/save/load flows.
- [x] Elements are included in export/code generation where applicable.
- [x] Elements have sensible defaults and validation.

## A. Watermark
- [x] Add `Watermark` element type.
- [x] Support text watermark and optional image watermark modes.
- [~] Add opacity, rotation, scale, color, and repeat controls.
- [x] Support page scope: current page, all pages, first page only, selected range.
- [~] Render behind normal page content without blocking selection.
- [~] Export watermark with correct layering and transparency.

## B. Notizen / Notes
- [x] Add `Note` element type.
- [ ] Support visible sticky-note style and invisible PDF annotation mode.
- [~] Add title, body text, author, color, icon, and collapsed/expanded state.
- [~] Add note positioning and optional anchor target.
- [x] Preserve notes in template data and PDF export metadata where supported.
- [~] Add clear visual state for selected, hovered, and collapsed notes.

## C. Arrow
- [x] Add `Arrow` element type.
- [x] Support straight, elbow, and curved arrow modes.
- [x] Add start/end arrowhead controls.
- [x] Add stroke color, width, dash style, and line cap controls.
- [ ] Support snapping arrow endpoints to other element edges/centers.
- [~] Export arrow as vector geometry.

## D. Draw / Freihand
- [x] Add `Draw` element type for freehand drawing.
- [~] Support pen, highlighter, and eraser tools.
- [~] Add stroke color, width, opacity, smoothing, and pressure settings.
- [x] Store drawing paths as editable vector data.
- [ ] Support undo/redo for drawing strokes.
- [~] Export paths with stable visual fidelity.

## E. Date
- [x] Add `Date` element type.
- [x] Support static date and dynamic render-date modes.
- [x] Add locale, timezone, and date/time format controls.
- [x] Support binding to template data fields.
- [x] Add fallback text for missing/invalid dynamic date values.
- [~] Export formatted date consistently across preview and PDF.

## F. Markieren / Highlight
- [x] Add `Highlight` element type.
- [x] Support rectangular highlight and text-marker modes.
- [~] Add color, opacity, blend mode, and rounded-corner controls.
- [~] Allow highlights to sit above or below text content.
- [x] Support resize handles without shifting underlying text.
- [~] Export highlight with transparent fill.

## G. Ankreuzen / Check Mark
- [x] Add `CheckMark` element type.
- [x] Support check, cross, dot, and empty states.
- [~] Add size, stroke width, color, and label controls.
- [~] Support linked checkbox behavior for forms/templates.
- [~] Support dynamic checked state from template data.
- [~] Export as vector mark or form field depending on mode.

## H. Seiten Anfang und Ende
- [x] Add page-start and page-end control markers.
- [x] Support visual page boundary handles on canvas.
- [~] Add controls for forcing content to start on a new page.
- [~] Add controls for keeping sections together until page end.
- [ ] Support first-page-only and last-page-only content placement.
- [ ] Validate that page boundary controls do not create empty pages unexpectedly.

## I. Nummerierung / Page Numbering
- [x] Add `PageNumber` element type.
- [x] Support current page, total pages, and `Page X of Y` formats.
- [x] Add numbering offset and start number controls.
- [x] Support Roman numerals, alphabetic numbering, and custom prefixes/suffixes.
- [x] Support scope: all pages, odd/even pages, selected page range.
- [~] Export page numbers after pagination is finalized.

## J. Shared Properties
- [~] Add consistent position controls: x, y, width, height, rotation.
- [ ] Add common style controls: color, opacity, stroke, fill, shadow where relevant.
- [~] Add layer controls: bring forward, send backward, lock, hide.
- [ ] Add accessibility metadata where export format supports it.
- [x] Add validation messages for invalid size, page scope, or binding values.

## K. Testing
- [x] Test adding each element from the sidebar/tool palette.
- [~] Test selection, drag, resize, rotate, duplicate, and delete flows.
- [x] Test save/load round trip for each element.
- [x] Test JSON/template export for each element.
- [~] Test generated PDF output for visual parity.
- [ ] Test mobile/tablet usability for element controls.

## Progress (2026-05-14)
- [x] First implementation slice added all advanced element types to `pxa-designer`.
- [x] Sidebar tool group added for Watermark, Notiz, Arrow, Draw, Date, Markieren, Ankreuzen, Page Start/End, and Nummerierung.
- [x] PXA and Live Preview renderers added for the new elements.
- [x] Inspector controls added for the main editable properties.
- [x] ExportService and C# CodeGenerator mappings added for the new elements.
- [x] Production build verification passed with `npm run build`.
- [x] Element duplication added through inspector action and `Ctrl/Cmd+D`.
- [x] Element locking added to prevent accidental drag/resize/keyboard movement.
- [x] Inline validation messages added for page range, binding, draw path data, and opacity bounds.
- [x] Pre-existing unused-variable type-check blockers fixed.
- [x] TypeScript verification passed with `npm run type-check`.
- [x] Domain `ElementType` enum extended with all advanced element types.
- [x] Domain `DesignerElement` extended with typed configs for Watermark, Note, Arrow, Draw, Date, Highlight, CheckMark, PageBoundary, and PageNumber.
- [x] Application document converter recognizes advanced element types.
- [x] Frontend C# generator aligned with domain enum names for `Draw` and `PageBoundary`.
- [~] PDF renderer fallback output added for advanced elements while full-fidelity rendering remains open.
- [x] `PXA.Domain` build passed.
- [~] `PXA.Application` build could not be conclusively verified because the local `dotnet build` process stalled and returned no compiler diagnostics.
