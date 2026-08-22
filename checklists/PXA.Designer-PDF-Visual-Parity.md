# PXA Designer and PDF Visual Parity

## Goal

Use the backend PDF as the printable visual contract and continuously compare the same canonical
`DesignExportDto` across the Designer canvas, Live Preview, normal backend PDF export, and generated
C# PDF execution.

## P0 - Deterministic audit

- [x] Add a deterministic fixture pack covering every current Designer element type.
- [x] Include typography, forms, graphics, media, tables, charts, navigation, annotations, RTL,
  localization, rotation, opacity, long labels, empty values, and multiple pages.
- [x] Add an internal visual-parity route that is available only when
  `VITE_ENABLE_VISUAL_PARITY=true`.
- [x] Render Canvas and Live Preview at one CSS pixel per PDF point without editor chrome.
- [x] Generate normal backend and generated C# PDF artifacts from the identical canonical fixture.
- [x] Rasterize PDFs at 72 DPI and create page screenshots, overlays, highlighted differences, and
  per-element crops.
- [x] Produce machine-readable JSON and a side-by-side HTML audit report.
- [x] Add one `npm run test:visual-parity` command for the complete audit.
- [x] Keep the initial audit report-only so existing differences are visible instead of silently
  becoming approved baselines.

## P0 - Rendering contract

- [x] Treat positions, dimensions, borders, padding, and typography values as PDF points at zoom 1.
- [x] Use border-box sizing for rendered document elements.
- [x] Exclude selection handles, page shadows, guides, and PDF-viewer focus indicators.
- [x] Align Text Field and Text Area label offsets, input heights, borders, backgrounds,
  placeholders, required markers, and font defaults with the backend PDF appearance.
- [x] Remove Preview-only blue form decoration that is not exported to PDF.
- [x] Resolve every P0 audit finding: missing content, wrong geometry, clipping, duplication, or
  incorrect page placement.

## P1 - Element fidelity

- [ ] Consolidate duplicated Canvas and Live Preview element rendering behind a shared frontend
  document renderer while keeping editor interactions separate.
- [x] Resolve typography, padding, borders, colors, rotation, and control-appearance findings.
- [x] Use the same packaged font files in Chromium and backend PDF rendering.
- [x] Confirm tables, Rich Text, charts, barcodes, images, RTL, and multi-page layouts at 200%.
- [x] Confirm normal backend PDF and generated C# PDF are semantically and visually equivalent.

## Coverage and CI

- [x] Add a coverage contract that detects drift between the Designer catalog, canonical fixture,
  frontend renderer, and backend renderer.
- [x] Record geometry drift, perceptual difference, mismatch bounds, and severity per element.
- [x] Approve thresholds only after the report has been reviewed and P0/P1 findings are corrected.
- [x] Fail CI for geometry drift above one point, missing/substituted content, or a regression over
  the approved perceptual thresholds.
- [x] Install the lockfile-pinned Playwright Chromium, Poppler, ImageMagick, and deterministic CI
  fonts in the visual job.
- [x] Upload HTML, JSON, source screenshots, overlays, and differences from every CI audit run.

## Validation

- [x] Run the full visual-parity report locally and review every element.
- [x] Run focused Text Field, Text Area, table, Rich Text, chart, barcode, image, RTL, and multi-page
  regression tests.
- [x] Run Designer Jest tests, type-check, and production build.
- [x] Run relevant .NET API, PDF infrastructure, and Code Worker tests.
- [x] Run `git diff --check`.
- [x] Add a customer-facing patch release fragment for the completed audit foundation and initial
  parity corrections.

## Acceptance criteria

- [x] One command creates a complete visual report for every supported element.
- [x] Every element can be inspected across Canvas, Live Preview, backend PDF, and C# PDF.
- [x] Canvas and Live Preview have no visible layout differences from backend PDF at 200%.
- [x] Normal backend export and C# PDF contain the same content and layout.
- [x] Text Field and Text Area geometry and appearance match across all four outputs.
- [x] A new element type cannot be added without a visual-parity fixture.
- [x] Production builds do not expose the internal parity route or fixture UI.

## Approved thresholds

- P0 and P1 findings are not accepted in CI. Geometry drift above one point is always P0.
- Reviewed P2 findings are limited to browser/PDF rasterization and anti-aliasing differences.
- QR and barcode P2 classification additionally requires exact source-pixel verification in both
  backend and C# PDFs.
- Backend and generated C# PDF element crops must remain pixel-identical.
- Exact PDF byte equality is not required; canonical-model and visual equivalence are required.

## Latest audit baseline

- Audit date: 2026-08-22.
- Coverage: 4 pages, 39 element instances, and all 38 catalog element types.
- Geometry: zero layout-point drift across Canvas and Preview after transform-safe measurement.
- PDF paths: normal backend and generated C# PDF have zero pixel difference on every page and
  element.
- Current frontend findings: 0 P0, 0 P1, 27 P2, and 12 matches within the approved threshold.
- QR codes and barcodes use the backend-generated PNG bytes in Canvas and Preview. Source pixels
  embedded in normal and generated C# PDFs are verified exactly before rasterizer differences are
  classified as P2.
- Arrow and highlight geometry now follow the PDF contract; chart rendering uses the same series,
  axis, tick, legend, and plot-layout rules and is within the P2 range.
- Shared PDF-contract renderers now align text baselines and wrapping, Rich Text runs, number and
  page-number formatting, Checkbox, Dropdown, Checkmark, Signature, Button, Circle, Drawing, Note,
  Comment, Content Control, Area, and Subsection output across Canvas and Preview.
- Rotated rectangles are emitted as backend PDF polygons. Element crops now use the canonical
  document box instead of a transformed DOM bounding box, moving Shape rotation into P2.
- Table fills no longer cover PDF grid borders; the backend consumes the Designer cell font
  properties, empty cells remain empty, and Canvas, Preview, and PDF use deterministic equal row
  heights and identical column geometry.
- Bookmark and Page Boundary remain editor-only semantic helpers and no longer produce accidental
  Preview borders that are absent from the PDF.
- Rich Text now uses the PDF word-run, wrapping, and paragraph-spacing contract and is within P2.
- Chromium and the PDF renderer use the same packaged Liberation Sans regular, bold, italic, and
  bold-italic files. PDF composite fonts encode TrueType glyph IDs and preserve Unicode extraction.
- Severity is based on perceptual RMSE after a deterministic blur while the report retains raw
  RMSE, overlays, and highlighted pixel differences for inspection.
- `npm run test:visual-parity:ci` enforces the approved material thresholds and exact equivalence of
  normal backend and generated C# PDF output.
