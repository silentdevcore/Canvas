# Image-Converter2.md: New OCR-Based Image-to-PDF Converter

## Summary

- [x] Create a new image-to-PDF conversion path that turns raster images into editable Canvas documents and exports them as PDFs with real text, image, and layout objects.
- [x] Use an embedded local OCR engine, preferably the `Tesseract` NuGet package with bundled `tessdata-fast` language files.
- [x] Do not shell out to a locally installed OCR executable.
- [x] Do not require a cloud OCR provider for the core conversion workflow.
- [x] Review the existing implementation only after this plan is saved, then reuse selected code where it accelerates implementation without carrying over the current custom glyph-recognition approach.

## Product Goal

- [x] Primary output: editable PDF generated through Canvas.
- [x] Intermediate output: `DesignExportDto` with editable `text`, `image`, and layout elements.
- [x] Optional output: diagnostic/debug response for development and quality review.
- [x] Preserve visual fidelity by optionally placing the original image as a locked background layer.
- [x] Preserve editability by rendering recognized text as real Canvas/PDF text objects.

## Architecture

- [x] Add a new module, for example `Canvas.FileImporter.ImageOcr`, for the OCR-based conversion path.
- [x] Keep the current `Canvas.FileImporter.ImageAnalysis` pipeline separate; treat it as legacy/reference code, not the new OCR core.
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

- [x] Convert OCR pixel coordinates into Canvas/PDF points.
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
- [ ] Group lines into paragraphs.
- [ ] Preserve reading order.
- [ ] Detect simple columns from line alignment.
- [ ] Detect simple tables from aligned word groups and horizontal/vertical rules.
  - [x] Detect simple tables from aligned OCR word groups.
  - [x] Use horizontal/vertical rule detection for table boundaries.
- [x] Emit recognized text as Canvas `text` elements.
- [x] Estimate font size from OCR word/line box height.
- [x] Estimate text color from the original image near the OCR bounds.
- [x] Use standard fallback fonts unless reliable font recognition is added later.
- [x] Add the original image as an optional locked background `image` element.
- [ ] Reuse shape detection only where it is stable enough to improve editability.
- [x] Do not reuse the current custom glyph recognizer as the main OCR strategy.

## PDF Generation

- [x] Create a `DesignExportDto` as the canonical intermediate model.
- [x] Export the design through the existing Canvas PDF renderer.
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
- [x] Let users open the editable Canvas design after conversion.

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

- [x] Review `src/Canvas.FileImporter.Image/ImageFileImporter.cs`.
  - [x] Reuse image decoding where appropriate.
  - [x] Reuse EXIF orientation handling where appropriate.
  - [x] Reuse data URI / image encoding logic where appropriate.
- [ ] Review `src/Canvas.FileImporter.ImageAnalysis/ImageAnalysisFileImporter.cs`.
  - [ ] Reuse diagnostics ideas.
  - [ ] Reuse debug overlay concepts.
  - [ ] Reuse coordinate mapping ideas only if they fit the new OCR pipeline.
- [ ] Review `src/Canvas.FileImporter.ImageAnalysis/Analysis/*`.
  - [ ] Reuse stable preprocessing helpers where they are general-purpose.
  - [ ] Reuse stable shape/region detection only as optional layout enrichment.
  - [ ] Do not use the current custom glyph recognition as the OCR core.
- [x] Review `Canvas/Pdf/*` and `src/Canvas.Infrastructure.Pdf/*`.
  - [x] Reuse existing text rendering.
  - [x] Reuse existing image rendering.
  - [x] Reuse existing PDF serialization.

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
- [x] Recognized text is represented as editable Canvas text elements.
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
