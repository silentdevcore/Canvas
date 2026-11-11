# PVADE Designer and PDF Visual Parity

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
- [ ] Canvas and Live Preview have no visible layout differences from backend PDF at 200%.
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

## Historical audit baseline

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

## November 2026 regression follow-up

The 2026-11-11 image inspection supersedes the earlier blanket 200% acceptance:
aggregate scores missed meaningful Arabic, font-style and control differences.
The strict acceptance item above remains open until all remaining images are
reviewed, not merely until the metric passes.

- [x] Honor bold/italic options when the page's default font family is used.
- [x] Package Noto Sans Arabic for browser and API output with its original OFL license.
- [x] Shape Arabic glyphs through HarfBuzz and retain logical PDF text.
- [x] Correct RTL SVG anchoring and reject clipped/missing RTL text in the browser audit.
- [x] Verify Arabic text extraction in both normal and generated C# PDFs.
- [x] Give checkbox widgets explicit checked/unchecked normal appearances.
- [x] Share radio rendering and honor stored selection in Canvas and Preview.
- [x] Share TOC rendering with configured titles, levels and page-number settings.
- [x] Remove double watermark rotation and preserve weight, case and opacity.
- [x] Add focused backend and frontend rendering regression tests.
- [x] Complete the final post-fix API regression run and archive its results.
- [x] Review the final image reports after all corrections, without replacing approved baselines.

Post-fix validation: 1,223 API tests, 833 Designer tests, 57 PDF infrastructure
tests, TypeScript checking and the Designer production build passed. All eight
PDF Trust corpus/operation comparisons retained their approved references.
The final all-elements report has 0 P0, 0 P1, 18 P2 and 22 matches; mixed layout
has 0 P0, 0 P1, 1 P2 and 2 matches. Backend and C# output are pixel-identical
on all seven pages. The remaining P2 details, including small wrapping/style
differences, still require inspection; they are not automatically reclassified
as anti-aliasing or approved as perfect 200% parity.

Local before/after evidence is written under
`artifacts/visual-validation-2026-11-11` and `artifacts/visual-fixes-2026-11-11`.
These ignored artifacts are not public documentation or release approvals.

## Remaining P2 correction pass (2026-11-11)

- [x] Inspect all 19 P2 findings from the preceding all-elements and mixed-layout runs.
- [x] Match field labels, required markers and widget-border placement to PDF output.
- [x] Reuse the PDF paragraph estimator for note and content-control wrapping.
- [x] Replace editor-only footnote blocks with the actual reference and page-footer body.
- [x] Reject clipped footnote bodies in the browser audit, including ancestor clipping.
- [x] Match centered shape borders, circle radii and fixed PDF line dash patterns.
- [x] Render standard tables with canonical cell data, relative column weights and PDF row heights.
- [x] Keep advanced cell-style rendering instead of silently dropping existing overrides.
- [x] Synchronize table dimension edits with cell data and preserve retained values/settings.
- [x] Render PDF underline and strikethrough strokes in the text color.
- [x] Re-run both visual fixtures and all eight Trust visual comparisons without baseline changes.
- [x] Run the current focused API tests, PDF infrastructure tests, Designer tests, typecheck and build.
- [ ] Resolve or explicitly approve the six remaining P2 findings after individual image review.
- [ ] Extend the standard-table parity guarantee to the advanced per-cell-style rendering path.

Results: all-elements has 0 P0, 0 P1, 5 P2 and 35 matches; mixed-layout has
0 P0, 0 P1, 1 P2 and 2 matches. Across both packs, P2 findings decreased from
19 to 6, and all seven backend/C# page pairs remain pixel-identical. Element
box drift is zero; this does not certify equal internal glyph geometry.

Remaining items are `text`, `dropdown`, `optionlist`, `qrcode`, `link`, and
mixed-layout `custom-title`. QR source pixels are verified identical in both
PDFs; native image resampling differs. The other five still show small glyph,
widget-baseline or decoration-extent differences and are not silently accepted
as anti-aliasing. The strict 200% acceptance above therefore stays open.

Current validation: 845 Designer tests, 95 focused API tests (including the
new decoration cases), 57 PDF infrastructure tests and 26 versioning/release
tests passed, along with typecheck and production build. The earlier full
1,223-test API run is evidence for the preceding pass, not a newly executed
full-suite certification of these later changes. Product version, published
releases, canonical fixtures and approved Trust references remain unchanged.

Detailed local report: `artifacts/visual-p2-2026-11-11/REPORT.md`; side-by-side
HTML reports and unchanged-threshold metrics are under its `final` directory.

## Choice, link and raster follow-up (2026-11-11)

- [x] Give Dropdown and form Option List explicit, clipped normal PDF appearances.
- [x] Embed the packaged regular font in these appearances when a font loader is configured.
- [x] Match first-option defaults, minimum widget font size, and the non-interactive dropdown fallback.
- [x] Invalidate obsolete choice appearances after form-value updates; retain fill and flatten coverage.
- [x] Draw link underlines with the PDF offset, thickness, color and measured unkerned advance.
- [x] Wait for browser font readiness before final underline measurement.
- [x] Use normal QR downsampling while retaining barcode rendering and original symbol pixels.
- [x] Check real PDF text positions, baselines and advances independently of image scores.
- [x] Add negative tests for missing text, wrong pages, repeated labels and text-metric drift.
- [x] Inject a five-point baseline error in an isolated browser run; verify rejection and browser/server cleanup.
- [x] Preserve all approved invoice hash pins and all eight Trust visual references.
- [x] Run the final Designer, focused API, Generator, PDF infrastructure and tooling checks.
- [ ] Obtain explicit visual acceptance for the two residual bold-text raster findings.

Final results: all-elements has 0 P0, 0 P1, 1 P2 and 39 matches; mixed layout
has 0 P0, 0 P1, 1 P2 and 2 matches. Four of the preceding six findings are
resolved; only `text` and `custom-title` remain P2. Both PDF paths remain
pixel-identical on all seven pages. Twelve browser text-run comparisons pass
the additional 0.1-point guard; maximum observed drift is 0.0255 point.
The remaining bold text uses the same packaged font bytes and matching
baselines. Small native rasterization/subpixel-width differences are retained
in the reports rather than automatically approved as perfect 200% parity.

An experiment with finer CID-width serialization was rejected because it
changed pinned electronic-invoice hashes without resolving these residual
findings. The final implementation preserves the existing width contract and
the approved invoice manifests, rather than updating references to obtain a pass.

Final validation: 850 Designer tests, 103 focused API tests, 141 Generator tests,
57 PDF infrastructure tests and 37 visual-tool/versioning/release tests passed.
Designer typecheck and production build passed. This is not a new full API
suite or complete Solution certification. Existing advanced cell-style table
coverage and the strict 200% acceptance item remain separate open work.

Current local report: `artifacts/visual-final-alignment-2026-11-11/REPORT.md`.
Previous reports remain historical evidence. No commit, version bump or
publication was performed.
