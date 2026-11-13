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

- [x] Consolidate duplicated Canvas and Live Preview element rendering behind a shared frontend
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
- [x] Canvas and Live Preview meet the reviewed 200% layout contract for the
  audited fixtures, including the owner's explicitly accepted residual bold-text
  raster/subpixel differences recorded below. Literal pixel identity is not claimed.
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
- [x] Resolve or explicitly approve the six remaining P2 findings after individual
  image review: four corrected; the final two explicitly approved on 2026-11-13.
- [x] Extend the standard-table parity guarantee to the advanced per-cell-style rendering path
  for canonical cell matrices; see the 2026-11-13 validation below.

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
- [x] Obtain explicit visual acceptance for the two residual bold-text raster
  findings; the repository owner approved both 200% comparisons on 2026-11-13.

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

## Individual table cell styles (2026-11-13)

The preceding rendering corrections were committed as `f5aec107`. This follow-up
extends the shared Canvas/Live Preview table renderer and normal/C# PDF export
to individual cell styles without changing the approved comparison thresholds.

- [x] Map all 16 current cell-style properties into the PDF table renderer.
- [x] Match cell backgrounds, text colors, fonts, explicit false bold/italic,
  alignment, padding and automatic row heights.
- [x] Paint each shared border once: explicit overrides beat defaults, wider
  explicit borders win, and ties use the right/bottom cell. A zero-width
  override suppresses an adjacent default edge.
- [x] Preserve first-entry precedence for duplicate cell styles and ignore
  out-of-range entries; account for inserted RDL matrix header rows.
- [x] Keep font-family fallback within the requested PDF standard family rather
  than silently replacing serif or monospace text with a sans-serif font.
- [x] Add three styled-table visual fixtures and focused API/frontend tests,
  including conflicting shared borders and invalid numeric style values.
- [x] Document the Inspector workflow, relative column widths, sparse style
  overrides, border precedence and editable JSON example in public Documentation.
- [x] Reject per-cell styles in the flow-pagination API instead of silently
  discarding them; explicit-page `PdfPage.DrawSimpleTable` supports them.
- [x] Add per-cell-style support to flow-paginated tables; see the follow-up below.
- [ ] Review native serif/monospace raster output across platforms, or bundle
  approved matching font faces, before claiming the same visual guarantee for
  those families. This fixture uses the bundled sans-serif faces only.

The styled-table pack reports 3 matches and zero P0/P1/P2 findings. The existing
all-elements pack remains at 39 matches and one P2; mixed layout remains at two
matches and one P2. Normal and C# PDFs are pixel-identical across all eight
pages. All four Trust corpus and four Trust operation comparisons pass their
unchanged references. The two residual bold-text findings still require the
explicit visual acceptance recorded above; no blanket 200% approval is implied.

Validation: 855 Designer tests, 116 focused API tests (including 13 styled-table
cases), 141 Generator tests, 57 PDF infrastructure tests and 65 Documentation
tests passed. Designer typecheck, Designer and Documentation production builds,
and the versioning/visual-tool checks passed. This is focused regression
evidence, not a new full API or complete Solution certification. Published
releases, product version and approved invoice/Trust evidence remain unchanged.

Local review report: `artifacts/styled-table-parity-2026-11-13/REPORT.md`.
The cell-style changes remain uncommitted; no push or deployment was performed.

## Flow-paginated styled tables (2026-11-13)

This follow-up supersedes the explicit-page-only restriction recorded above.
Native C# flow tables now use the same measured cell layout as explicit-page
tables. This does not change the Designer's explicitly positioned page model.

- [x] Share row measurement and validation between `DrawSimpleTable` and flow.
- [x] Plan all chunks before painting, using actual font/padding/wrapping heights
  instead of dividing remaining space by a fixed row height.
- [x] Map original cell-style indices, explicit heights and line heights into
  each chunk; retain repeated header and repeated/final-only footer styles.
- [x] Keep headers with data rows, account for partially occupied pages, and
  reserve space for both continuation markers.
- [x] Preserve alternating data-row fills across odd-sized page chunks.
- [x] Render header/footer-only tables and report total actual painted height.
- [x] Reject oversized or non-finite rows before painting any table chunk;
  do not silently clip, split rows or create an unbounded sequence of pages.
- [x] Add 20 flow regression cases and an independently grouped three-page
  explicit-layout fixture; PDFs and page images match exactly.
- [x] Publish the native C# guide, link it from the PDF SDK table topic and
  include it in the public Documentation build.
- [x] Extend the pending patch fragment without changing `VERSION` or published releases.
- [x] Run Designer tests/typecheck/build, focused API, Generator, PDF
  infrastructure, Documentation and visual/versioning regression checks.

The dedicated `node tools/visual-parity/flow-tables.mjs` command writes
`artifacts/flow-table-parity/report.html`, JSON, overlays and pixel diffs.
All three pages have zero changed pixels at 72 DPI. Its synthetic automatic
and independently planned explicit PDFs are also byte-identical.

Validation: 855 Designer tests, 136 focused API tests (including the 20 flow
cases), 141 Generator tests, 57 PDF infrastructure tests, 66 Documentation
tests and 37 tooling tests passed. Designer typecheck and both frontend
production builds passed. Full API and full Solution certification were not
repeated. Existing invoice pins and Trust references remain unchanged.

Rows intentionally remain indivisible. An over-height row requires smaller
content/font/padding or a larger page and returns a clear layout error. The two
residual bold-text visual findings and native serif/monospace cross-platform
review remain open; this milestone does not silently approve them.

Public guide: [Styled Table Pagination](../docs/guides/PVAD.Styled-Table-Pagination.md).
No commit, push, deployment or product-version bump was performed.

## Scoped bold-text visual approval (2026-11-13)

- [x] Regenerate normal and C# PDFs for `text` and `custom-title` and capture
  Canvas/Live Preview at two device pixels per PDF point.
- [x] Rerasterize PDF vectors at 144 DPI after supersampling for actual 200%
  comparisons rather than enlarging the existing 72-DPI images.
- [x] Verify the loaded browser font and the embedded PDF font against the exact
  packaged bold-face SHA-256; confirm matching font size, weight and style.
- [x] Compare every Latin glyph offset and advance, not just total run width.
- [x] Add negative tests for local drift, swapped/missing glyphs, substituted
  font bytes, missing style metadata and changed review evidence.
- [x] Obtain explicit owner approval for both displayed 200% comparisons.
- [x] Preserve the approved images with fixture/font hashes and exact browser,
  platform and scale scope; changed evidence requires a new approval.
- [x] Keep P2 scores, the existing 0.1-point metric guard, invoice evidence and
  all previously approved image thresholds unchanged.

The owner confirmed: "Beide Vergleiche visuell freigeben". The record is
`test-assets/visual-parity/approvals/2026-11-13-bold-text/approval.json`.
It applies only to the two current plain Latin fixtures on macOS with the
recorded lockfile-pinned Chromium version and packaged bold font. No independent
reviewer, Linux approval or arbitrary font/script certification is implied.

No further content, wrapping, font-family, style or position defect was found
in these two cases. Maximum run-width drift is 0.02225 point; maximum local
glyph drift is 0.035125 point. The remaining differences combine native
rasterization and subpixel advance quantization; they are accepted residuals,
not falsely reported as zero-pixel differences. Normal and C# PDF crops match
exactly at 200%.

Reproduce with `node tools/visual-parity/bold-text-review.mjs` after building
the API test project. The local HTML/JSON report is under
`artifacts/bold-text-review/`. The broader shared-renderer consolidation and
native serif/monospace cross-platform review remain separate open work.

No application rendering behavior, product version or published release was
changed by this review. No commit or push was performed.

Validation: both regenerated fixture PDFs and browser surfaces pass the
per-glyph guard; all 43 visual-tool/versioning tests, version consistency,
release-fragment validation and `git diff --check` pass. Review report:
`artifacts/bold-text-review/REPORT.md`. Earlier complete Designer and focused
API regression counts remain evidence from the preceding table/rendering pass,
not a newly repeated full suite for this test-only review.

Approved original PNG bytes remain hash-pinned. Repeat runs compare the exact
decoded RGBA16 pixel hash including dimensions, so PNG timestamp/encoding
metadata alone does not revoke approval. A changed pixel does. This is not a
perceptual tolerance or an image-normalization exception.

## Shared Document Renderer (2026-11-13)

- [x] Route all 38 element types in Canvas and Live Preview through one typed
  dispatcher; keep selection, drag controls and non-printing editor markers separate.
- [x] Share localized display-field substitution, language visibility and effective
  geometry without changing stable IDs, form names or binding paths.
- [x] Preserve the displayed target language in Preview exports, including the
  format modal, without mutating persisted page settings.
- [x] Share image fitting, clipping, zero-valued focus coordinates and a local
  missing-image placeholder; allow recovery when the source changes.
- [x] Consolidate legacy table and date rendering and apply zoom once to all
  printable content, including table fonts, padding and borders.
- [x] Reject catalog/dispatcher coverage drift and localized-field drift against
  the backend mapper in contract tests.
- [x] Fix backend language substitution to render a private document copy;
  repeated language exports preserve the original canonical JSON and checksum.
- [x] Add a synthetic French-language fixture, compare both UI surfaces with
  normal and real C# PDF output, and verify the canonical-model checksum.
- [x] Check synthetic Canvas/Preview surfaces at desktop and mobile sizes.
- [x] Publish the Preview workflow and limits in the Documentation guide and
  add a customer-facing patch release fragment without bumping the product version.

The localized fixture has six matches with no P0/P1/P2 findings. The repeated
full-element pack has 39 matches and one existing P2 finding across 40 instances
and 38 types. Styled tables have three matches; mixed-layout pages have two
matches and one existing P2 finding. The normal and C# PDF paths remain
pixel-identical. Existing Trust references and the two owner-approved bold-text
comparisons are unchanged; no threshold or baseline was relaxed.

Desktop (1280 x 900) and mobile (390 x 844) checks cover four synthetic document
surfaces, with identical content/geometry and no browser page errors. They do
not certify the full authenticated mobile editing workflow.

Current validation: 909 Designer tests in 92 suites and one snapshot, TypeScript
type-check, Designer production build, 68 Documentation tests and production
build, 137 focused API tests (rendering, forms, exports, conversion and canonical
content isolation), and 43 visual-tool/versioning tests pass. Production JS
excludes the internal parity route and synthetic fixture markers. This is not a
new full-Solution or full-API test run.

Local reports: `artifacts/shared-renderer-validation/REPORT.md`,
`artifacts/shared-renderer-validation/localized/visual-parity-report.html`, and
`artifacts/shared-renderer-validation/browser/report.html`.
Public workflow: [Designer Canvas and Live Preview](../docs/guides/PVAD.Designer-Preview.md).

### Remaining Variant Coverage

- [x] Extend backend image parity beyond the audited contained/stretched cases
  to all cover/none cropping and focus-coordinate combinations.
- [ ] Define and test the full supported cross-runtime date-format contract;
  consolidating the existing formatter is not arbitrary .NET format support.
- [ ] Complete native serif/monospace cross-platform raster review with the
  intended font files; this consolidation does not approve new font baselines.

No commit, push, deployment, product-version bump or published-release edit was
performed for this consolidation. Unrelated pending work remains untouched.

## Image Fit And Focus Parity (2026-11-13)

- [x] Render contain, cover, fill and natural-size none with browser-equivalent
  focus percentages and a clipped PDF image box.
- [x] Preserve the legacy aspect-ratio override, alpha transparency, natural
  image dimensions, zero focus and normalized out-of-range focus values.
- [x] Use effective language-specific image bounds, including placeholders.
- [x] Embed PNG/JPEG bytes directly without temporary image files.
- [x] Keep zero-valued focus coordinates visible in the actual image Inspector.
- [x] Add independent backend geometry, clipping, alpha, JPEG, invalid input,
  finite numeric range and source-immutability tests.
- [x] Add a 34-case synthetic image fixture covering wide/tall sources, all four
  fits, focus edges, small natural-size sources, PNG alpha, JPEG and localization.
- [x] Correct expected PDF crops and geometry checks for canonical language
  overrides, with independent tests; do not derive expected bounds from the DOM.
- [x] Wait for document images to decode before browser captures.
- [x] Enforce the image pack in CI and retain separate failure artifacts before
  the catalog run and its fidelity aggregation.
- [x] Validate all four outputs, repeat the complete catalog regression and
  preserve existing image thresholds and approved baseline data.
- [x] Document the contract and known limits and add a patch release fragment.

The new image fixture has 34 matches across three pages, no P0/P1/P2 findings,
and zero geometry drift. Normal and real C# PDF image crops are pixel-identical.
The full catalog retains 39 matches and its one existing P2 finding across 40
instances/38 types. All four PDF-Trust corpus references and four operation
references remain unchanged and pass.

Desktop and mobile checks cover four synthetic document surfaces with 34 loaded
images each, matching fit/focus settings and no browser page errors. A separate
desktop check uses the actual Inspector and confirms both focus inputs show 0.
This is not a complete authenticated mobile editing or customer-database test.

Validation: 915 Designer tests, 159 focused API tests (including 22 image tests),
141 Generator tests, 57 PDF infrastructure tests, 69 Documentation tests and 46
visual-tool/versioning tests pass. Type-check and Designer/Documentation builds
pass. Known dependency, nullable and bundle warnings remain; none was suppressed.

Report: `artifacts/image-fit-parity/visual-parity-report.html`.
Desktop/mobile screenshots: `artifacts/image-fit-parity/browser/report.html`.
Reproduce with the image-fit command in `tools/visual-parity/README.md`.

The tested contract covers embedded PNG/JPEG image-box fitting and clipping,
not arbitrary remote URLs, EXIF autorotation, source-crop editing fields,
rounded masks or every decorative border/padding combination. Cross-runtime
date formats and native serif/monospace platform validation remain open above.

No commit, push, deployment, product-version bump or published-release change
was performed for this image correction. Unrelated pending changes are preserved.
