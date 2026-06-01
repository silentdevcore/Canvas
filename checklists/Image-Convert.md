# Image -> Canvas JSON / PDF Conversion Checklist

Scope: build a custom raster-image analysis engine (`Canvas.FileImporter.ImageAnalysis`) that converts images into editable Canvas designs by recognizing layout, colors, shapes, and eventually text content without external OCR/CV binaries such as Tesseract. The engine should mirror the staged architecture of `Canvas.Importer`, but stay deterministic, testable, and self-contained.

---

## Current State

- [x] `Canvas.FileImporter.ImageAnalysis` project exists and is wired into the Web API.
- [x] Smart image endpoint exists: `POST /api/document/import-image-analysis`.
- [x] Frontend route exists through the `image-analysis` importer option.
- [x] Preprocessing exists: scaling, grayscale conversion, global Otsu binarization.
- [x] Color analysis exists: border background sampling, simple palette, seed-based flood-fill regions.
- [x] Shape analysis exists: Sobel edges, H/V line runs, rectangle assembly, simple ellipse hints.
- [x] Text region detection exists: connected components, candidate filtering, line grouping, font-size/color estimation.
- [x] Scene assembly exists: color regions -> shapes -> text mapped to `DesignExportDto`.
- [x] Local Tesseract bridge removed from the ImageAnalysis pipeline.
- [x] First own glyph recognizer is connected for clean synthetic printed text.
- [x] Basic punctuation recognition works for controlled synthetic text.
- [ ] Own glyph/character recognition is not production-ready yet.
- [ ] Current `TextEngine` can emit text content for controlled cases, but broad OCR fidelity is still limited.
- [x] Punctuation recognition has a passing .NET verification run.
- [ ] Existing tests mostly prove pipeline stability, not real recognition fidelity.

---

## Non-Goals / Constraints

- [x] Do not call local `tesseract`.
- [x] Do not shell out to OCR tools installed on the user's machine.
- [x] Do not depend on external CV/OCR services for the core engine.
- [ ] Avoid hidden behavior differences based on what happens to be installed locally.
- [ ] Prefer explicit engine options over environment auto-detection.

---

## Phase 0 - Stabilize The No-External-OCR Baseline

- [x] Replace `OcrEngine.Analyze(prepared)` with `TextEngine.Analyze(prepared)`.
- [x] Remove the local Tesseract subprocess bridge from `Canvas.FileImporter.ImageAnalysis`.
- [x] Add/adjust tests so image-analysis import does not depend on local OCR output.
- [x] Add a small engine result diagnostic model:
  - [x] input dimensions
  - [x] preprocessing scale factor
  - [x] region count
  - [x] shape count
  - [x] text-line count
  - [x] warning list
- [x] Add debug overlay export for detected regions, shapes, text lines, words, and glyph boxes.
- [x] Update API response/form flags to expose diagnostics during development.

---

## Phase 1 - Preprocessing Improvements

- [x] Normalize to max 2400 px on longest side.
- [x] Convert to grayscale.
- [x] Build binary bitmap via global Otsu threshold.
- [x] Add conservative local/adaptive thresholding for uneven neutral backgrounds.
- [x] Add conservative denoise pass for isolated speckle pixels.
- [ ] Add broader small-component removal and light morphology.
- [x] Recognize normal and inverted text passes with the matching binary polarity.
- [ ] Add foreground polarity detection per region beyond text-only passes.
- [ ] Preserve DPI/page-scale metadata where possible.
- [ ] Add tests for:
  - [x] uneven neutral background
  - [x] isolated speckle noise
  - [ ] anti-aliased text
  - [ ] JPEG compression noise
  - [x] low-contrast gray text
  - [x] white text on dark background

---

## Phase 2 - Color & Region Analysis

- [x] Detect background from border sampling.
- [x] Build simple dominant palette.
- [x] Segment filled regions with seed-based flood-fill.
- [x] Map color regions to `ElementDto` shape elements.
- [ ] Replace fixed 20px seed grid with full connected-component color segmentation or adaptive scan seeds.
- [ ] Merge adjacent color regions using perceptual color distance.
- [ ] Distinguish page background, panels, images, icons, and decorative blocks.
- [ ] Detect gradients as image/bitmap fallback regions instead of many noisy shapes.
- [ ] Add region confidence and source classification.

---

## Phase 3 - Shape Detection

- [x] Sobel edge map exists.
- [x] H/V line detection exists.
- [x] Rectangle assembly exists.
- [x] Simple ellipse/circle hints exist.
- [x] Thin line mapping exists.
- [ ] Fix horizontal gap handling in line run detection.
- [ ] Add connected-component based rectangular fill detection, not just edge rectangles.
- [x] Add first-pass grid-line classification for intersecting H/V rules.
- [ ] Add table/grid detection as first-class layout primitive.
- [ ] Add rounded-rectangle detection.
- [ ] Add icon/image fallback classification for complex vector-like clusters.
- [x] Add text-stroke suppression rules that preserve larger panels and long rules.
- [ ] Add broader confidence and suppression rules that do not hide real shapes inside dark panels.

---

## Phase 4 - Text Region Detection

- [x] Connected-component labeling exists.
- [x] Character candidate filtering exists.
- [x] Line assembly exists.
- [x] Font-size estimation exists.
- [x] Text color sampling exists.
- [x] Add word assembly from character gaps.
- [x] Split distant same-baseline text runs without relying on OCR metadata.
- [ ] Add broader column detection and reading-order handling.
- [x] Add baseline estimation metadata for detected text runs.
- [ ] Improve baseline detection for mixed fonts and multi-line paragraphs.
- [x] Add first-pass text block metadata for nearby multiline text.
- [ ] Detect richer multiline paragraphs and preserve complex reading order.
- [x] Reject decorative leading/trailing symbol words such as bullets and border fragments.
- [ ] Reject broader non-text blobs such as icons and complex UI decoration.
- [ ] Add tests that assert bounding boxes, not only element existence.

---

## Phase 5 - Own Glyph Recognition

- [x] `CharacterTemplates` exists as an experimental NCC atlas.
- [x] NCC scorer exists.
- [x] Reconnect glyph recognition through a dedicated `GlyphRecognizer`.
- [ ] Render template atlas for multiple families:
  - [ ] Arial / Helvetica-like
  - [ ] Courier / monospace
  - [ ] Times-like serif
  - [ ] bold and regular weights
  - [ ] multiple font sizes
- [ ] Normalize glyph patches with padding, aspect ratio, baseline, and stroke thickness.
- [ ] Combine multiple cheap recognizers:
  - [ ] NCC
  - [ ] horizontal/vertical projection profiles
  - [ ] zoning features
  - [ ] connected-component holes/enclosures
  - [ ] simple structural features
- [x] Emit `RecognizedChar` with confidence.
- [x] Emit `?` below threshold with zero confidence for unresolved glyphs.
- [ ] Reduce hallucination with richer ambiguity detection beyond NCC thresholding.
- [ ] Add golden tests:
  - [x] `"Hello"` -> exact content for controlled synthetic image
  - [x] `"Hello World"` word spacing
  - [x] `"Invoice"` mixed case
  - [x] `"12345"` controlled digits
  - [x] punctuation for controlled dot, dash, slash, and colon-like cases
  - [x] ambiguous glyphs: `O/0`, `I/l/1`, `S/5`
  - [x] unknown low-confidence blob -> `?`

---

## Phase 6 - Scene Assembly & DTO Mapping

- [x] Z-order is color regions -> shapes -> text.
- [x] Pixel-to-page coordinate mapping exists.
- [x] `PageSettings` come from image dimensions or target page size.
- [x] Add per-element `imageAnalysisConfidence`.
- [x] Add per-element `sourceBoundsPx`.
- [x] Add fallback image layer option for low-confidence areas.
- [x] Prevent duplicate/overlapping shape artifacts from text strokes without suppressing real UI panels.
- [x] Preserve background color at page level.

---

## Phase 7 - Verification & Quality Gates

- [x] Existing ImageAnalysis tests pass as pipeline smoke tests.
- [ ] Add recognition-quality tests with expected JSON snapshots.
- [x] Add debug overlay generation for visual review.
- [ ] Add saved visual overlay snapshots for regression review.
- [ ] Add benchmark cases:
  - [ ] clean screenshot
  - [ ] scanned document
  - [ ] mobile photo
  - [ ] invoice/table
  - [ ] dark header with light text
- [ ] Track metrics:
  - [ ] text-line detection recall
  - [ ] glyph exact-match rate
  - [ ] shape IoU
  - [ ] element count noise
  - [ ] runtime and memory

---

## Immediate Next Steps

- [x] Make the pipeline self-contained by removing Tesseract usage.
- [x] Add diagnostics and debug overlays.
- [x] Expose diagnostics/debug overlay through the Image Smart importer UI.
- [x] Introduce `GlyphRecognizer` behind `TextEngine`.
- [x] Make `"Hello"` synthetic OCR pass without external OCR.
- [x] Expand controlled OCR tests from "element exists" to exact text content.
- [x] Add controlled text-line ink-bounds tests.
- [x] Add controlled word-bound splitting tests.
- [x] Add full-pipeline Canvas coordinate mapping test for positioned text.
- [x] Add color-region bounds test.
- [x] Add full-pipeline shape/region Canvas bounds test.
- [x] Suppress duplicate filled-rect edge shape when a matching color region exists.
- [x] Prevent filled rectangles from being classified as ellipses.
- [x] Draft punctuation detection for dots, dashes, slashes, and colon-like dot pairs.
- [x] Run focused punctuation verification outside the sandbox.
- [x] Run full ImageAnalysis verification after punctuation changes.
- [x] Add conservative adaptive thresholding for neutral uneven backgrounds.
- [x] Run full ImageAnalysis verification after adaptive thresholding.
- [x] Add conservative isolated-speckle denoise pass.
- [x] Run full ImageAnalysis verification after denoise changes.
- [x] Add controlled low-contrast gray text test.
- [x] Fix inverted-polarity text recognition for white text on dark backgrounds.
- [x] Run full ImageAnalysis verification after polarity changes.
- [x] Tighten text-stroke shape suppression while preserving panels and long rules.
- [x] Run full ImageAnalysis verification after shape-suppression changes.
- [x] Add grid-line metadata classification for intersecting H/V rules.
- [x] Add full-pipeline 2x2 grid smoke test.
- [x] Run full ImageAnalysis verification after grid-line changes.
- [x] Split distant same-baseline text runs into separate text primitives.
- [x] Run full ImageAnalysis verification after text-run splitting.
- [x] Add baseline metadata to text primitives and exported text styles.
- [x] Run full ImageAnalysis verification after baseline metadata changes.
- [x] Add `textBlockId` and `textBlockLineIndex` metadata for nearby multiline text.
- [x] Run full ImageAnalysis verification after text-block metadata changes.
- [x] Filter decorative symbol-only text words at text-run edges.
- [x] Run full ImageAnalysis verification after non-text symbol filtering.
- [x] Set unresolved glyph confidence to zero and cover unknown blob recognition.
- [x] Run full ImageAnalysis verification after glyph confidence changes.
