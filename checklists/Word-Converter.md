# Word Converter — PDF Fidelity Checklist (Execution)

> Goal: Make Word export visually and structurally as close as possible to PDF output for the same DesignExportDto input.

---

## Phase 2: Fidelity Improvement — Anchored Text Boxes & Shape Rendering

**Why:** The current converter uses `<w:framePr>` (Word 6.0 legacy frames) for text positioning. These render inconsistently and clip content. Modern Word uses anchored WPS text boxes (`<wps:wsp>`) — the same as "Insert → Text Box" — for pixel-accurate absolute positioning. Shapes (rect, circle, line) are currently skipped and replaced with text annotations.

- [x] **Step 1** — Add WPS namespace aliases to `WordDocumentExporter.cs`
  (`WPS = DocumentFormat.OpenXml.Office2010.Word.DrawingShape`)
- [x] **Step 2** — Create `CreateTextBoxParagraph(el, layout, populateContent, bgHex)` helper
  that wraps any paragraph content in `<wp:anchor> + <wps:wsp> + <wps:txbx>` using EMU coordinates
- [x] **Step 3** — Replace `framePr` with anchored text box in `case "text":`
  — text paragraph goes inside the text box; background fill applied to shape fill; legacy path kept for non-v2
- [x] **Step 4** — Replace `framePr` with anchored text box in `case "richtext":`
  — all parsed rich paragraphs go inside a single text box; legacy path kept for non-v2
- [x] **Step 5** — Render `rect` / `shape` as anchored DrawingML filled rectangle
  — `CreateShapeParagraph` with `<a:prstGeom prst="rect">`, fill and border from element style
- [x] **Step 6** — Render `circle` as anchored DrawingML ellipse
  — `CreateShapeParagraph` with `<a:prstGeom prst="ellipse">`, fill and border
- [x] **Step 7** — Render `line` / `arrow` as anchored DrawingML line
  — `CreateShapeParagraph` with `<a:prstGeom prst="line">`, stroke color/width; no fill
- [x] **Step 8** — Build and run all tests — 84 unit tests pass, 0 errors
- [x] **Step 9** — Tests updated and extended: 85 unit + 15 integration tests pass; anchored text box positioning verified by EMU assertion; DrawingML shape rendering verified by DW.Anchor presence; non-v2 fallback path confirmed by feature-flag test

---

## Architecture Decision: JSON → Word (not PDF → Word)

**Decision date:** 2026-05-19
**Status:** Accepted

### Context
The Word export pipeline converts `DesignExportDto` (JSON) directly to `.docx` via the OpenXML SDK.
An alternative was evaluated: generate a PDF first, then convert PDF → DOCX.

### Why PDF → Word is rejected

| Problem | Detail |
|---|---|
| PDF loses all document structure | PDF is a write-only bitmapped format — paragraphs, tables, and headings do not survive |
| Output is non-editable | Every PDF→DOCX converter produces floating text boxes, not real Word content |
| Requires expensive paid libraries | Aspose.Words, Syncfusion, iText — no free library converts reliably |
| Two failure points | PDF render step + conversion step doubles the error surface |
| Our PDF engine is write-only | `MinimalPdf` emits raw bytes with no DOM available for re-reading |

### Why JSON → Word is correct

- `DesignExportDto` preserves all structure: text, tables, links, form fields, images, positions, styles
- OpenXML SDK is free, already integrated, produces genuinely editable `.docx` files
- Direct element mapping gives full control — no data loss between source and output
- 90.244% average fidelity score achieved across 13 real-world fixtures

---

## Current Quality Status (2026-05-19)

### OOXML Schema Fixes Applied
Nine schema violations were fixed to eliminate Word's repair dialog and missing styling:

| Fix | OOXML Rule |
|---|---|
| `RunProperties` order: `rFonts→b→i→strike→color→sz→u` | ECMA-376 §17.3.2.28 |
| `ParagraphProperties`: `shd` before `jc` | ECMA-376 §17.3.1.26 |
| All `<w:shd>` elements have `w:color="auto"` | Required third attribute |
| Note case: direct `Shading` construction (removed fragile `CloneNode`) | Runtime null-cast bug |
| `<w:sectPr>` moved to last child of `<w:body>` | OOXML §17.6.17 |
| `<w:framePr>` placed first in `<w:pPr>` (before shd/jc) | CT_PPrBase position 5 |
| `<w:jc>` placed last in `<w:pPr>` (after spacing/ind) | CT_PPrBase position 27 |
| `<w:spacing>` before `<w:ind>` in flow mode | CT_PPrBase positions 22/23 |
| Merged duplicate `SpacingBetweenLines` (typography + positioning) | One element per `pPr` |

### Test Coverage
- 84 unit tests (`Canvas.Export.Tests`) — all passing
- 15 integration tests (`Canvas.Api.Tests`) — all passing
- 13-fixture fidelity harness — **90.244% average score** (target: ≥ 90%)

### Known Remaining Gaps
- [ ] `RichTextSpanParser`: nested `<div>`/`<span>` and `<br>` handling
- [ ] Table cell vertical alignment (`<w:vAlign>` not yet mapped)
- [ ] Background color sanitization for non-hex values (rgba/hsl/named colors)
- [ ] Large doc SLA validation (50+ pages performance target)
- [ ] Beta release to internal users

---

## Progress Snapshot (2026-05-19)

- [x] Fixed 4 OOXML schema violations: RunProperties order (rFonts→b→i→strike→color→sz→u), ParagraphProperties order (shd before jc), Shading missing Color="auto", CloneNode null cast in note case.
- [x] Fixed `<w:sectPr>` placed at top of body instead of last — OOXML §17.6.17 requires it to be the final child of `<w:body>`.
- [x] Fixed `<w:framePr>` appended after shd/jc in `<w:pPr>` — must be first per CT_PPrBase schema (position 5).
- [x] Fixed `<w:jc>` appended before spacing/ind — must be last (position 27) after shd(10)/spacing(22)/ind(23).
- [x] Fixed duplicate `SpacingBetweenLines` when typography and positioning both add spacing — merged into single element with both Line and Before attributes.
- [x] Confirmed JSON→Word (OpenXML SDK) is the correct architecture; PDF→Word approach rejected (structure loss, non-editable output, paid library dependency).
- [x] Created and expanded fidelity sample pack (13 fixtures) under checklists/word-fidelity-samples.
- [x] Added automated Word vs PDF fidelity harness in tests/Canvas.Export.Tests/WordPdfFidelityTests.cs.
- [x] Harness writes baseline artifacts to tests/Canvas.Export.Tests/Fidelity/artifacts/latest.
- [x] Added DOCX-to-PDF conversion step via LibreOffice when available.
- [x] Added first-page geometry drift assertion for converted output.
- [x] Added optional first-page PNG snapshot generation and pixel diff ratio report in fidelity harness.
- [x] Fixed PDF rich-text whitespace rendering bug that previously broke one fidelity sample.
- [x] Expanded sample pack to 12+ templates.
- [x] Enforce full CI visual diff threshold.
- [x] Added centralized Word unit conversion helper and applied it to section geometry, table widths, and image sizing.
- [x] Added geometry tests for section page size, margins, and landscape orientation.
- [x] Added deterministic frontend-to-Word font fallback mapping and typography regression tests.
- [x] Added line-height paragraph spacing mapping and combined text-decoration parsing for Word text runs.
- [x] Added rich text span parsing and inline run style mapping for Word export.
- [x] Added X/Y-driven paragraph/table/image positioning pass (left indent + vertical spacing) to reduce layout drift.
- [x] Added positioning regression tests for text spacing/indent and table indentation.
- [x] Added page-number field format mapping for current/total/pageOfTotal with PAGE/NUMPAGES fields.
- [x] Added explicit link behavior tests (external hyperlink relationship and safe relative fallback).
- [x] Applied unified style mapping for signature/field/checkbox elements with regression coverage.
- [x] Added anchored image rendering for absolute-position elements with page-relative offsets.
- [x] Added image overlap z-order mapping via style zIndex to anchor relative height.
- [x] Added regression tests for anchored image offsets and z-order monotonicity.
- [x] Added safe image fallback placeholder rendering for invalid/unavailable image sources.
- [x] Added deterministic sequential drawing IDs for images and regression coverage.
- [x] Added tested Canvas width/height -> image extent EMU conversion checks.
- [x] Added fit mode mapping: fill stretches, contain/cover preserve aspect (cover uses preserve-aspect fallback).
- [x] Added deterministic element ordering (Y/X/zIndex/Id) with regression coverage for equal-coordinate elements.
- [x] Added remote image fetch retry+timeout policy with graceful placeholder fallback.
- [x] Added remote-image failure regression coverage for retry/fallback path.
- [x] Added integration parity tests for multi-page page breaks, table fixed-layout/grid widths, and image embedding.
- [x] Added explicit Word element support matrix with Supported/Partial/Skipped status and fallback notes.
- [x] Switched previously skipped shape/layout elements to textual fallback annotations (no silent drops).
- [x] Added regression coverage for unsupported element fallback annotations.
- [x] Added export warning aggregation into DOCX package description metadata for fallback paths.
- [x] Added regression coverage for warning metadata persistence.
- [x] Added cancellation-token support to Word export pipeline and remote image fetch path.
- [x] Added regression coverage for pre-cancelled and in-flight cancellation behavior.
- [x] Added production-style regression replay test over all sample-pack JSON payloads.
- [x] Added 50-page large-document stress regression to reduce crash risk.
- [x] Added 50-page memory/performance profiling artifact emission for Word export runs.
- [x] Added `word_fidelity_v2` feature flag in export options and Word exporter behavior gating.
- [x] Added regression coverage for `word_fidelity_v2` disabled mode.
- [x] Added sample-pack page-box parity test to validate section page sizes across fixtures.
- [x] Added shared layout planner primitive for page normalization, shared-element dedup, hidden filtering, and deterministic ordering.
- [x] Wired shared layout planner into Word, Markdown, HTML, and SVG exporters.
- [x] Added core unit tests for shared planner fallback/dedup/order behavior.
- [x] Added frame-based absolute positioning for text/richtext blocks in `word_fidelity_v2` mode.
- [x] Added regression coverage for frame positioning and legacy non-v2 fallback path.
- [x] Wired shared layout planner into PDF mapping path for base page elements.
- [x] Added complex positioned-overlap sample to fidelity pack and validated it through Word/PDF harness.
- [x] Added sample-based table stress regression assertions for fixed layout behavior.
- [x] Added image-heavy sample regression assertions for image embedding and no fallback placeholder.
- [x] Added utility/form sample regression assertions for links and form-like element rendering.
- [x] Added typography sample regression assertions for heading/body and inline style preservation.
- [x] Added internal beta rollout runbook for `word_fidelity_v2` promotion/rollback and artifact triage.
- [x] Added and executed local fidelity toolchain preflight script (`checklists/word-fidelity-preflight.sh`).
- [x] Confirmed current environment is missing required visual-score binaries (`soffice`, `pdftoppm`).
- [x] Installed fidelity toolchain dependencies (`soffice`, `pdftoppm`, `compare`, `magick`) and revalidated preflight as READY.
- [x] Ran full Word/PDF fidelity harness with visual scoring enabled for all 13 samples.
- [x] Verified sample-pack average fidelity score >= 90 (current: 90.244).

---

## 1. Define Fidelity Target

- [x] Define success metric per page:
  - text position drift <= 6 px
  - font size drift <= 0.5 pt
  - table cell width drift <= 4 px
  - page break positions aligned with PDF
- [x] Document acceptable differences (Word engine limitations):
  - browser HTML rich text and OpenXML rich text behavior differ
  - exact glyph metrics differ by platform font availability
  - some vector shapes need raster fallback
- [x] Add a baseline sample pack with at least 12 templates:
  - invoice, contract, form, checklist, dense table, multi-page report

---

## 2. Build Comparison Harness

- [x] Add golden output folder structure:
  - checklists/word-fidelity-samples/
  - tests/Canvas.Export.Tests/Fidelity/
- [x] For each sample input, generate both outputs:
  - PDF via current PDF renderer
  - DOCX via WordDocumentExporter
- [x] Convert DOCX to PDF in CI test step for visual diff (LibreOffice headless)
- [x] Add image diff snapshots (per-page PNG) with threshold report
- [x] Fail CI when threshold exceeded

Acceptance:
- [x] CI prints per-page fidelity score and top mismatches

---

## 3. Introduce Shared Layout Model

- [x] Extract first shared layout planning primitive consumed by Word:
  - FlowBlock, PositionedBlock, TableLayout, InlineStyle, BorderStyle
- [x] Move element ordering and dedup logic to shared layer
- [ ] Keep renderer-specific mapping only in final step (planning shared; render mapping still renderer-specific by design)

Acceptance:
- [x] Word and PDF consume the same computed blocks for at least text, table, image

---

## 4. Page Geometry and Units

- [x] Map Canvas page size to Word section page size precisely (twips)
- [x] Map margins from PageSettings to section properties
- [x] Respect page orientation per page
- [x] Replace loose px conversion with centralized converter:
  - px <-> pt <-> twips
  - DPI assumption documented and test-covered

Acceptance:
- [x] Word page dimensions match PDF page box in all baseline samples

---

## 5. Typography Fidelity

- [x] Add deterministic font mapping table:
  - frontend family -> Word family fallback chain
- [x] Add run-level style mapping parity:
  - bold, italic, underline, strike, color, line-height, letter spacing fallback
- [x] Handle rich text spans instead of full strip to plain text where possible
- [x] Add missing default style normalization before export

Acceptance:
- [x] Heading/body styles in DOCX visually match PDF baseline on all typography samples

---

## 6. Positioning Strategy

- [x] Choose explicit strategy and implement consistently:
  - paragraph flow for document-like blocks
  - anchored drawings for absolute-position image elements + frame-based absolute positioning for text/richtext
- [x] Avoid losing X/Y by dumping everything as top-to-bottom paragraphs
- [x] Ensure z-order for overlapping elements where supported

Acceptance:
- [x] Complex positioned sample stays spatially close to PDF output

---

## 7. Table Parity

- [x] Use fixed table layout with explicit grid widths from Canvas widths
- [x] Map header row shading, bold, and repeat-on-page-break behavior
- [x] Map border width/color/style consistently
- [x] Support zebra rows and per-column alignment
- [x] Handle empty/missing cells safely with width preservation

Acceptance:
- [x] Table stress sample matches PDF layout and wraps similarly

---

## 8. Images and Media

- [x] Support data URLs, http/https images, and safe fallback placeholders
- [x] Preserve fit mode semantics as close as possible:
  - contain, cover, fill fallback behavior documented
- [x] Set image extents from Canvas width/height with tested conversion
- [x] Add deterministic image ID generation per document

Acceptance:
- [x] Image-heavy sample has no missing images and matching aspect behavior

---

## 9. Links, Fields, and Special Elements

- [x] External links: safe URI validation + clickable hyperlinks
- [x] Relative/internal links: graceful fallback text with style
- [x] Page number fields: PAGE and NUMPAGES variants by NumberingFormat
- [x] Form-like elements (field, checkbox, signature) unified style spec

Acceptance:
- [x] Interactive/document utility elements render predictably and do not break export

---

## 10. Unsupported Elements Policy

- [x] Create explicit support matrix for all ElementDto types (see checklists/Word-Element-Support-Matrix.md)
- [x] For unsupported types choose one fallback strategy:
  - textual annotation
  - rasterized snapshot
  - skip with warning
- [x] Add export warnings list in result metadata/logging

Acceptance:
- [x] No silent drops for unsupported elements

---

## 11. Robustness and Performance

- [x] Add timeout and retry policy for remote image fetch
- [x] Add cancellation token support in export pipeline
- [x] Memory profile with large multi-page docs (50+ pages)
- [x] Ensure deterministic output for same input (stable ordering/ids)

Acceptance:
- [ ] Large docs export without crash and within defined SLA

---

## 12. Tests To Add

- [x] Unit tests:
  - unit conversion correctness
  - style normalization and color sanitation
  - link handling (absolute, relative, invalid)
- [x] Integration tests:
  - multi-page section/page break parity
  - table layout parity
  - image inclusion parity
- [x] Regression tests for previously failing payloads from production

Acceptance:
- [x] Word exporter test suite includes fidelity and robustness categories

---

## 13. Rollout Plan

- [x] Feature flag for "word_fidelity_v2"
- [ ] Beta release to internal users (runbook prepared: checklists/Word-Fidelity-Beta-Runbook.md)
- [x] Collect mismatch reports with sample files
- [ ] Finalize defaults and remove old path after stability period (criteria documented in runbook)

Definition of Done:
- [x] >= 90% fidelity score across baseline sample pack
- [x] No export-blocking exception on sampled real templates
- [x] Documentation updated for known differences and supported matrix
