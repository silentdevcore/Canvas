# Image-Converter2.md: New OCR-Based Image-to-PDF Converter

## Summary

- [x] Create a new image-to-PDF conversion path that turns raster images into editable PXA documents and exports them as PDFs with real text, image, and layout objects.
- [x] Use an embedded local OCR engine, preferably the `Tesseract` NuGet package with bundled `tessdata-fast` language files.
- [x] Do not shell out to a locally installed OCR executable.
- [x] Do not require a cloud OCR provider for the core conversion workflow.
- [x] Review the existing implementation only after this plan is saved, then reuse selected code where it accelerates implementation without carrying over the current custom glyph-recognition approach.

## Product Goal

- [x] Primary output: editable PDF generated through PXA.
- [x] Intermediate output: `DesignExportDto` with editable `text`, `image`, and layout elements.
- [x] Optional output: diagnostic/debug response for development and quality review.
- [x] Preserve visual fidelity by optionally placing the original image as a locked background layer.
- [x] Preserve editability by rendering recognized text as real PXA/PDF text objects.

## Architecture

- [x] Add a new module, for example `PXA.FileImporter.ImageOcr`, for the OCR-based conversion path.
- [x] Keep the current `PXA.FileImporter.ImageAnalysis` pipeline separate; treat it as legacy/reference code, not the new OCR core.
- [x] Define an `IOcrEngine` abstraction so Tesseract can be replaced or supplemented later.
- [x] Add a Tesseract-backed implementation behind the abstraction.
- [x] Store bundled OCR language files under a predictable deployment path such as `tessdata/`.
- [x] Default OCR languages to German plus English: `deu+eng`.
- [x] Make language selection explicit through options, not environment auto-detection.
- [x] Bundle `eng.traineddata` and `deu.traineddata` from `tessdata_fast`.
- [x] Bundle matching primary native Tesseract/Leptonica libraries for the current macOS x64 development runtime.
- [x] Relink macOS x64 native Tesseract/Leptonica bundle so transitive dependencies do not depend on Homebrew paths.
- [ ] Add native OCR bundles for non-macOS-x64 deployment runtimes.

## Public Contracts

- [x] Add `ImageToPdfConversionOptions`.
  - [x] Languages.
  - [x] App-owned native library path.
  - [x] Source DPI override.
  - [x] Target page width and height in points.
  - [x] Page sizing mode.
  - [x] Include background image layer.
  - [x] Include diagnostics.
  - [x] Include debug overlay.
  - [x] Low-confidence threshold.
  - [x] Layout reconstruction mode.
- [x] Add `ImageToPdfConversionResult`.
  - [x] `DesignExportDto`.
  - [ ] Optional PDF bytes.
  - [x] OCR blocks, lines, words, and bounding boxes.
  - [x] Diagnostics.
  - [x] Warnings.
  - [x] Optional debug overlay image.
- [x] Add OCR result models.
  - [x] `OcrPage`.
  - [x] `OcrBlock`.
  - [x] `OcrLine`.
  - [x] `OcrWord`.
  - [x] `OcrBoundingBox`.
  - [x] Confidence values at word, line, and page level.

## Input Handling

- [x] Support PNG.
- [x] Support JPG/JPEG.
- [x] Support TIFF/TIF.
- [x] Support BMP.
- [x] Support WebP when supported by the selected image decoder.
- [ ] Treat multi-page TIFF as a multi-page document.
- [x] Reject unsupported formats with a clear error message.
- [x] Enforce file size and pixel-count limits to prevent excessive memory use.
- [ ] Preserve the original encoded image bytes when possible.

## Image Preprocessing

- [x] Decode images with SkiaSharp or the existing image decoding path.
- [x] Apply EXIF orientation before OCR and layout mapping.
- [x] Read DPI metadata when available.
- [x] Default to 300 DPI when metadata is missing or invalid.
- [ ] Normalize image scale for OCR while preserving original pixels for output.
- [x] Add grayscale conversion.
- [x] Add contrast normalization.
- [x] Add adaptive binarization.
- [ ] Add conservative denoising.
- [ ] Add deskew detection and correction.
- [ ] Track every preprocessing transform so OCR coordinates can be mapped back to original image coordinates.

## OCR Pipeline

- [x] Initialize embedded Tesseract through the .NET package.
- [x] Load OCR language data from the app deployment, not from a user-installed system path.
- [x] Run OCR per page/image.
- [x] Extract text blocks.
- [x] Extract lines.
- [x] Extract words.
- [x] Extract bounding boxes.
- [x] Extract confidence values.
- [ ] Preserve OCR text exactly unless normalization is explicitly requested.
- [x] Record OCR engine version, selected languages, and runtime in diagnostics.
- [x] Handle missing language data with a clear user-facing error.
- [x] Handle missing native OCR binaries with a clear user-facing error.
- [x] Handle OCR failure without crashing the entire API process.

## Coordinate Mapping

- [x] Convert OCR pixel coordinates into PXA/PDF points.
- [ ] Account for DPI, target page size, scaling, EXIF orientation, and deskew transforms.
  - [x] Account for DPI, target page size, and proportional scaling.
  - [x] Account for EXIF orientation in decoded image mapping.
  - [ ] Account for deskew transforms.
- [x] Keep both source pixel bounds and final page bounds in diagnostics.
- [x] Round final coordinates consistently to avoid layout jitter.
- [ ] Add mapping tests for portrait, landscape, rotated, and deskewed images.
  - [x] Add portrait mapping coverage.
  - [x] Add landscape mapping coverage.
  - [x] Add rotated mapping coverage.
  - [ ] Add deskewed mapping coverage.

## Layout Reconstruction

- [x] Group OCR words into lines.
- [x] Group lines into paragraphs.
- [x] Preserve reading order.
- [x] Detect simple columns from line alignment.
- [x] Detect simple tables from aligned word groups and horizontal/vertical rules.
  - [x] Detect simple tables from aligned OCR word groups.
  - [x] Detect simple tables from split aligned OCR cell lines without visible rules.
  - [x] Use horizontal/vertical rule detection for table boundaries.
  - [x] Support empty table cells when rows align to known column anchors.
  - [x] Tolerate incomplete table rule lines conservatively.
  - [x] Ignore row/column spans unless OCR evidence is unambiguous.
- [x] Emit recognized text as PXA `text` elements.
- [x] Estimate font size from OCR word/line box height.
- [x] Split colored or differently sized OCR word runs within a line into separate text elements.
- [x] Classify conservative OCR text roles such as heading, body, and caption.
- [x] Estimate text color from the original image near the OCR bounds.
- [x] Use standard fallback fonts unless reliable font recognition is added later.
- [x] Add the original image as an optional locked background `image` element.
- [x] Reuse shape detection only where it is stable enough to improve editability.
- [x] Do not reuse the current custom glyph recognizer as the main OCR strategy.

## PXA Element Mapping From Images

- [x] Add a conservative form/document element mapper after OCR layout detection.
- [x] Keep the original image as a locked background while placing editable detected elements above it.
- [x] Use shared exclusion zones so OCR text, table regions, and table rules are not duplicated as shapes.
- [x] Resolve overlapping detections by priority: table, checkbox, signature, field, image region, shape, text.
- [x] Map detected objects to supported PXA element types.
  - [x] Map OCR text, text runs, paragraphs, headings, and captions to `text`.
  - [x] Map simple tables with empty cells to `table`.
  - [x] Map simple horizontal and vertical separators to `line`.
  - [x] Map conservative outlined rectangles to `rect`.
  - [x] Map empty square boxes to `checkbox`.
  - [x] Map checked, crossed, and dotted square boxes to `checkbox` with state.
  - [x] Map labeled empty rectangular boxes to `field`.
  - [x] Map long signature/date/name lines to `signature` when label context supports it.
  - [x] Map filled rectangles to `rect` with `backgroundColor`.
  - [x] Map clear circles and ellipses to `circle`.
  - [x] Map larger non-text bitmap regions such as logos, stamps, or icons to cropped `image` elements.
- [x] Add source diagnostics to every mapped element.
  - [x] Add `imageOcrRole` values such as `form-field`, `checkbox`, `signature`, `shape`, and `image-region`.
    - [x] Add `imageOcrRole` for checkbox elements.
    - [x] Add `imageOcrRole` for form field elements.
    - [x] Add `imageOcrRole` for signature elements.
    - [x] Add `imageOcrRole` for image region elements.
  - [x] Add `imageOcrConfidence`.
    - [x] Add `imageOcrConfidence` for checkbox elements.
    - [x] Add `imageOcrConfidence` for form field elements.
    - [x] Add `imageOcrConfidence` for signature elements.
    - [x] Add `imageOcrConfidence` for image region elements.
  - [x] Add `imageOcrDetector`.
    - [x] Add `imageOcrDetector` for checkbox elements.
    - [x] Add `imageOcrDetector` for form field elements.
    - [x] Add `imageOcrDetector` for signature elements.
    - [x] Add `imageOcrDetector` for filled rectangle elements.
    - [x] Add `imageOcrDetector` for circle and ellipse elements.
    - [x] Add `imageOcrDetector` for image region elements.
  - [x] Add `sourceBoundsPx`.
- [x] Keep uncertain candidates out of the design instead of guessing.
- [x] Add tests for PXA element mapping.
  - [x] Checkbox empty, checked, crossed, and dotted.
  - [x] Label plus empty input field.
  - [x] Signature line with label.
  - [x] Logo/stamp/icon image region.
  - [x] Filled rectangle.
  - [x] Circle and ellipse.
  - [x] Mixed form with text, checkbox, field, signature, and table.
  - [x] Negative coverage: text pixels do not create shapes.
  - [x] Negative coverage: table rules are not duplicated as shape elements.

## PDF Generation

- [x] Create a `DesignExportDto` as the canonical intermediate model.
- [x] Export the design through the existing PXA PDF renderer.
- [x] Render OCR text as real PDF text objects.
- [x] Render the original image as a background layer when enabled.
- [x] Keep low-confidence OCR visible in diagnostics.
- [x] Do not silently replace low-confidence text with guessed corrections.
- [x] Ensure generated PDFs are valid and open in standard viewers.
- [ ] Verify that text is selectable/searchable where the renderer supports real text output.

## API

- [x] Add `POST /api/document/convert-image-to-pdf`.
- [x] Accept multipart form data.
- [x] Accept `file`.
- [x] Accept `languages`.
- [x] Accept `pageWidthPt`.
- [x] Accept `pageHeightPt`.
- [x] Accept `includeBackgroundImage`.
- [x] Accept `includeDiagnostics`.
- [x] Accept `includeDebugOverlay`.
- [x] Accept `lowConfidenceThreshold`.
- [x] Return PDF bytes by default.
- [x] Return debug JSON when `debug=true`.
- [x] Return clear `400` responses for invalid files or missing OCR language data.
- [x] Return clear `415` responses for unsupported image formats.
- [x] Add API integration coverage for debug JSON, PDF bytes, and unsupported image formats.

## Frontend

- [x] Add a new import/conversion mode named `Image OCR to PDF`.
- [x] Add language controls.
- [x] Add a background-image-layer toggle.
- [x] Add diagnostics/debug toggle.
- [x] Show OCR progress states.
- [x] Show conversion warnings after completion.
- [x] Let users download the generated PDF.
- [x] Let users open the editable PXA design after conversion.

## Diagnostics And Debugging

- [x] Report source dimensions.
- [x] Report effective DPI.
- [x] Report page count.
- [x] Report OCR languages.
- [x] Report OCR engine version.
- [x] Report preprocessing scale factor.
- [ ] Report deskew angle.
- [x] Report word count.
- [x] Report line count.
- [x] Report average confidence.
- [x] Report low-confidence word count.
- [x] Report runtime and memory use.
- [x] Add warnings for low DPI.
- [ ] Add warnings for blurry or skewed images.
- [x] Add warnings for low OCR confidence.
- [x] Add warnings for missing language data.
- [x] Add debug overlay showing OCR word and line boxes.

## Existing Code To Analyze After Saving This Plan

- [x] Review `src/Importing/PXA.FileImporter.Image/ImageFileImporter.cs`.
  - [x] Reuse image decoding where appropriate.
  - [x] Reuse EXIF orientation handling where appropriate.
  - [x] Reuse data URI / image encoding logic where appropriate.
- [ ] Review `src/Importing/PXA.FileImporter.ImageAnalysis/ImageAnalysisFileImporter.cs`.
  - [ ] Reuse diagnostics ideas.
  - [ ] Reuse debug overlay concepts.
  - [ ] Reuse coordinate mapping ideas only if they fit the new OCR pipeline.
- [ ] Review `src/Importing/PXA.FileImporter.ImageAnalysis/Analysis/*`.
  - [ ] Reuse stable preprocessing helpers where they are general-purpose.
  - [ ] Reuse stable shape/region detection only as optional layout enrichment.
  - [ ] Do not use the current custom glyph recognition as the OCR core.
- [x] Review `PXA/Pdf/*` and `src/Infrastructure/PXA.Infrastructure.Pdf/*`.
  - [x] Reuse existing text rendering.
  - [x] Reuse existing image rendering.
  - [x] Reuse existing PDF serialization.

## Planned Improvement: Light Background Table And Rule Detection

- [x] Improve table mapping for light/low-contrast backgrounds.
  - [x] Replace absolute dark-only rule detection with contrast-aware rule detection.
  - [x] Detect dark lines on light backgrounds.
  - [x] Detect light lines on dark table/header backgrounds.
  - [x] Detect conservative gray table/grid lines on white or light gray backgrounds.
  - [x] Use local neighboring pixels to estimate rule/background contrast.
  - [x] Keep existing minimum-run and OCR text exclusion safeguards so text pixels are not mapped as rules.
  - [x] Keep table fallback behavior: use `rule-bounded-table` only when rules are sufficiently reliable, otherwise keep `aligned-text-table`.
  - [x] Add tests for light gray table lines on light backgrounds.
  - [x] Add tests for light table/header rules on dark backgrounds.
  - [x] Add tests for low-contrast line, rectangle, checkbox, field, and signature rules on light backgrounds.
  - [x] Keep regression coverage for black table lines, text-pixel negative coverage, checkboxes, fields, signatures, and mixed forms.

## Planned Improvement: Real-Image Table Robustness And Diagnostics

- [x] Add table and rule diagnostics to the OCR conversion result.
  - [x] Report detected horizontal and vertical rule segment counts.
  - [x] Report sampled line/background contrast for table-like rule candidates.
  - [x] Report table candidate bounds, row anchors, column anchors, detector name, acceptance status, and rejection reason.
- [ ] Add the actual failing table image as an OCR fixture.
  - [x] Store the fixture under the OCR test project.
  - [x] Add captured OCR-line data support to keep the test deterministic.
  - [x] Assert that diagnostics expose rule segments, table candidates, bounds, and anchors for the fixture when present.
  - [x] Assert that structured conversion produces a `table` element for the fixture when present.
  - [ ] Add `failing-table-01.png` and `failing-table-01.ocr.json`.
- [x] Improve rule detection for real-world table lines.
  - [x] Detect local line/background contrast beyond dark-only pixels.
  - [x] Support thin antialiased light gray rules on light backgrounds.
  - [x] Continue rejecting OCR text pixels as rule segments.
- [x] Merge fragmented table rule segments.
  - [x] Merge collinear short horizontal and vertical fragments.
  - [x] Tolerate small gaps in horizontal and vertical rules.
  - [x] Use merged coverage when evaluating table bounds.
- [x] Improve table detection when visible rules are missing or weak.
  - [x] Cluster OCR words into row candidates from word centers.
  - [x] Stabilize repeated column anchors across multiple rows.
  - [x] Use repeated X positions and numeric columns as table evidence.
- [x] Tolerate missing OCR cells in detected tables.
  - [x] Preserve empty cells when surrounding rows establish stable column anchors.
  - [x] Avoid falling back to normal paragraph text when enough table evidence remains.
- [x] Detect light table backgrounds and fills.
  - [x] Recognize light cell fills or alternating row backgrounds.
  - [x] Infer table regions from text-grid structure when borders are weak.
- [ ] Add regression and stress coverage.
  - [x] Test antialiased light gray 1px rules on light backgrounds.
  - [x] Test broken and gapped horizontal and vertical rules.
  - [x] Test OCR jitter, shifted anchors, missing cells, and numeric columns.
  - [x] Keep existing clean table, checkbox, field, signature, shape, and text fallback tests passing.

## Planned Improvement: Split OCR Text Recognition From Visual Element Detection

- [ ] Split the image OCR converter into three explicit pipeline stages: text recognition, visual element detection, and element/text fusion.
- [ ] Keep Tesseract responsible only for text extraction: pages, blocks, lines, words, bounds, confidence, language, and OCR diagnostics.
- [x] Add an immutable intermediate OCR document model that stores raw OCR output mapped back to original source pixels.
- [ ] Add a separate visual element detection stage that works from image pixels and detects rules, tables, fields, checkboxes, signatures, rectangles, filled areas, circles, and image regions.
  - [x] Add initial pixel-only visual detection for rule segments, table regions, rectangles, and checkbox-like boxes.
- [ ] Add a separate fusion stage that maps OCR text onto detected visual elements only when needed.
  - [x] Add initial `OcrVisualFusionEngine` for checkbox labels, field labels, rejected mappings, consumed OCR lines, and standalone OCR text.
- [ ] Map OCR words/lines into table cells when a visual or text-grid table candidate exists.
  - [x] Map OCR words into table cells when a visual table-region candidate exists.
- [ ] Map nearby OCR labels to form fields, checkboxes, and signature lines without consuming unrelated paragraph text.
  - [x] Map nearby OCR labels to field and checkbox candidates without consuming unrelated paragraph text.
- [x] Keep normal paragraph/body text outside detected visual element regions and emit it as standalone PXA text elements.
- [ ] Add diagnostics for every stage: OCR extraction, visual detection, fusion decisions, rejected candidates, and final PXA element output.
- [ ] Add debug overlays for each stage: OCR text bounds, visual candidates, fusion/mapping results, and final element bounds.
- [x] Add tests proving that OCR text recognition can succeed independently of element detection.
- [ ] Add tests proving that visual line/table/field detection can run from image pixels without OCR text.
  - [x] Add visual-only tests for rule/table-region and checkbox candidates without OCR text.
- [ ] Add tests proving that fusion correctly maps text into tables and fields while leaving normal text independent.
  - [x] Add fusion tests for table cells, field labels, checkbox labels, and paragraph text that must remain standalone.

### Implementation Plan

- [ ] Introduce explicit intermediate pipeline models instead of passing raw `OcrLine` lists directly into all layout logic.
  - [x] Add `OcrTextDocument` with source image size, pages, blocks, lines, words, bounds in original pixels, confidence, language, and preprocessing metadata.
  - [x] Add `VisualLayoutDocument` with detected rule segments, table regions, fields, checkboxes, signature lines, shapes, image regions, and confidence/rejection diagnostics.
  - [x] Add `FusedLayoutDocument` with final semantic candidates: tables with cell text, fields with labels, checkboxes with labels/state, signatures with labels, standalone text groups, shapes, and image regions.
  - [x] Keep these models internal to `PXA.FileImporter.ImageOcr` unless API consumers need them later.
- [ ] Refactor `ImageToPdfConverter.ConvertAsync` into an orchestrator that decodes the image, applies orientation/preprocessing/scale mapping, runs OCR extraction, runs visual detection, runs OCR/visual fusion, builds `DesignExportDto`, builds diagnostics/debug overlays, and returns the result.
  - [x] Add `OcrTextExtractor` that calls `IOcrEngine`, handles OCR bitmap scaling, maps OCR coordinates back to source pixels, and produces `OcrTextDocument`.
  - [ ] Add `VisualElementDetector` that receives original `SKBitmap`, source dimensions, and options; detects visual candidates from pixels/rules/edges/fills; avoids final text-placement decisions; and produces `VisualLayoutDocument`.
    - [x] Add initial `VisualElementDetector` for pixel-only rules, table regions, rectangles, and checkbox candidates.
  - [ ] Add `OcrVisualFusionEngine` that receives `OcrTextDocument` and `VisualLayoutDocument`, assigns text to tables/fields/signatures/checkboxes, marks consumed OCR text, and produces standalone text groups for unconsumed text.
    - [x] Add initial `OcrVisualFusionEngine` for fields, checkboxes, consumed OCR lines, rejected mappings, and standalone text.
  - [ ] Add `PxaElementBuilder` that converts fused semantic candidates into `ElementDto`, applies page placement/scaling, and adds source diagnostics to element styles.
- [ ] Implement deterministic fusion rules.
  - [ ] Prefer visual table bounds from detected rules/backgrounds, and allow OCR text-grid table candidates when no visual table exists.
    - [x] Prefer visual table-region bounds from detected rules.
  - [x] Assign words to table cells by source pixel bounds and row/column anchors while tolerating missing cells and shifted OCR words.
  - [x] Map the nearest left/above OCR line as a field label only when distance and alignment thresholds match.
  - [x] Avoid consuming OCR text inside unrelated paragraphs during field mapping.
  - [x] Map nearest right-side or same-row checkbox labels only when confidence is high.
  - [ ] Preserve checked, crossed, and dotted checkbox state from the visual detector.
  - [ ] Map signature labels such as signature, date, or name using proximity and keyword hints.
  - [x] Emit all OCR lines not consumed by table/field/checkbox/signature mapping as normal text groups.
  - [x] Exclude OCR text inside visual element bounds only after fusion has assigned it.
- [ ] Add diagnostics that clearly show where a failure happened.
  - [ ] Add OCR diagnostics for runtime, page count, word/line count, confidence, preprocessing scale, language, and engine version.
  - [ ] Add visual diagnostics for rule segment count, table candidates, field candidates, checkbox candidates, signature candidates, and rejection reasons.
  - [ ] Add fusion diagnostics for OCR words assigned to tables, OCR lines consumed by fields/signatures/checkbox labels, OCR lines left as standalone text, and rejected mappings with reasons.
    - [x] Add initial fusion diagnostics for consumed OCR lines, standalone OCR lines, and rejected mappings with reasons.
  - [ ] Add OCR-only, visual-candidates, fusion, and final PXA element debug overlays.
- [ ] Add focused tests around stage separation.
  - [x] Add OCR-only tests where a fake OCR engine returns words/lines and `OcrTextDocument` preserves text, confidence, and bounds.
  - [ ] Add visual-only tests with synthetic images containing lines, tables, fields, and checkboxes, verifying visual candidates without OCR dependency.
    - [x] Add synthetic rule/table-region and checkbox coverage without OCR dependency.
  - [x] Add fusion tests for table bounds plus OCR words mapping into table cells.
  - [x] Add fusion tests for field rectangles plus nearby labels.
  - [ ] Add fusion tests for checkboxes plus nearby labels and state.
    - [x] Add fusion test for checkbox plus nearby label.
  - [x] Add fusion tests proving normal paragraph text near shapes remains standalone when it should not be consumed.
  - [ ] Add end-to-end tests proving existing image OCR conversion still returns `DesignExportDto`.
  - [ ] Add end-to-end tests proving debug JSON includes separate OCR, visual, and fusion diagnostics.
  - [ ] Add end-to-end tests proving the large image path still avoids full expensive global scans where appropriate.

### Assumptions

- [ ] Do not replace Tesseract in this plan.
- [ ] Do not change the public endpoint: keep `POST /api/document/convert-image-to-pdf`.
- [ ] Keep the original image background layer behavior.
- [ ] Keep current PXA element types.
- [ ] Prioritize architectural separation and debuggability first; table/field quality improvements should happen inside the new stages after the split.
- [ ] Treat this as appended unchecked checklist work and do not mark existing items complete.

## Test Plan

- [x] Unit-test EXIF orientation handling.
- [x] Unit-test DPI detection and fallback.
- [ ] Unit-test deskew transform tracking.
- [x] Unit-test OCR coordinate mapping from pixels to PDF points.
- [x] Unit-test rotated image mapping.
- [ ] Unit-test multi-page TIFF handling.
- [x] Integration-test PNG to `DesignExportDto`.
- [x] Integration-test JPEG to `DesignExportDto`.
- [ ] Integration-test TIFF to multi-page `DesignExportDto`.
- [x] Integration-test image to PDF bytes.
- [x] Integration-test API image upload to debug JSON.
- [x] Integration-test API image upload to PDF bytes.
- [x] Integration-test API unsupported image format handling.
- [x] Unit-test frontend OCR service calls for debug import and PDF download.
- [x] Verify generated PDF contains image content.
- [x] Verify generated PDF contains real text objects.
- [ ] Test German OCR.
- [x] Test English OCR with real native app-bundled Tesseract libraries.
- [ ] Test mixed German/English OCR.
- [ ] Add fixtures for invoice scans.
- [ ] Add fixtures for mobile photos.
- [ ] Add fixtures for table documents.
- [ ] Add fixtures for dark headers with light text.
- [ ] Add fixtures for low-resolution images.
- [ ] Add fixture snapshots for diagnostics and debug overlays.

## Acceptance Criteria

- [x] A PNG scan can be converted into a valid PDF.
- [ ] A JPEG scan can be converted into a valid PDF.
- [x] Recognized text is represented as editable PXA text elements.
- [x] The generated PDF preserves visual fidelity through an optional original-image background layer.
- [x] OCR runs from app-bundled dependencies and language files.
- [x] OCR runs with app-bundled primary native Tesseract/Leptonica libraries on this runtime.
- [x] macOS x64 OCR native bundle is portable without Homebrew transitive dependency paths.
- [ ] OCR native bundle coverage exists for all target deployment runtimes.
- [x] No manual machine-level Tesseract installation is required.
- [x] Missing language data produces a clear error.
- [x] Low-confidence OCR is surfaced in diagnostics.
- [x] The new converter remains separate from the current partial custom image-analysis implementation.

## Assumptions

- [x] The target output is an editable PDF, not only a flattened image PDF.
- [x] OCR is embedded in the app deployment.
- [x] Default OCR languages are German and English.
- [x] Tesseract is used through a .NET package, not as a command-line executable.
- [x] Existing code may be reused after plan creation, but the core approach is new.
