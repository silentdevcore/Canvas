# Canvas Importer — Handoff Document

**Date:** 2026-05-24  
**Branch:** main  
**Backend:** http://localhost:5086  
**Frontend:** http://localhost:5174 (or 5175 if port is busy)

---

## What This Is

Canvas is a .NET 10 + React document design tool. This handoff covers the **PDF importer pipeline** — the subsystem that reads a real-world PDF and turns it into an editable `DesignExportDto` the frontend canvas can open and modify.

The pipeline has two distinct jobs:

| Job | Entry point | Output |
|---|---|---|
| **PDF → Canvas import** | `CanvasImporterPdfImporter.ImportAsync` | `DesignExportDto` (pages + elements) |
| **Canvas → PDF regeneration** | `CanvasPdfGeneratorBridge.RegenerateAsync` | byte stream |

---

## Architecture

```
PDF bytes
  └─ Canvas.Importer.PdfImporter          (low-level tokenizer / parser)
       └─ PdfDocumentModel                (pages, resources, graphics objects)
            └─ SceneGraphEngine           (Phase 5 analysis)
                 ├─ PrimitiveBuilder      (PdfGraphicsElement → PrimitiveObject)
                 ├─ ObjectClassifier      (barcode / separator / decoration heuristics)
                 ├─ ReadingOrderEngine    (XY-cut text ordering)
                 ├─ GroupingEngine        (label-value, icon-text, contained groups)
                 └─ SemanticLayoutEngine  (header / footer / paragraph / figure)
                      └─ CanvasImporterPdfImporter   (maps to DesignExportDto)
```

Key source locations:

| Layer | Project | Path |
|---|---|---|
| Tokenizer / parser | `Canvas.Importer` | `src/Canvas.Importer/` |
| Scene graph / analysis | `Canvas.Importer` | `src/Canvas.Importer/Analysis/` |
| PDF → Canvas import | `Canvas.Infrastructure.Converters` | `src/Canvas.Infrastructure.Converters/CanvasImporterPdfImporter.cs` |
| Canvas → PDF regeneration | `Canvas.Infrastructure.Pdf` | `src/Canvas.Infrastructure.Pdf/CanvasPdfGeneratorBridge.cs` |
| API endpoint | `Canvas.WebApi` | `Canvas.WebApi/Controllers/DocumentOpsController.cs` |
| Tests | `Canvas.Importer.Tests` | `tests/Canvas.Importer.Tests/PdfImporterCoreTests.cs` |

---

## Completed Work

### Phase 3 — Text Fidelity, Image Codec Expansion, Barcode Support
- Font size scale fix (text matrix scale factor applied in importer)
- CCITT Fax and LZW image round-trip in the generator bridge (decode → re-encode as FlateDecode)
- Indexed color space palette expansion in bridge image handling
- Barcode round-trip verification (vector-path, CCITT/Indexed image, barcode-font text)

### Phase 4 — Adobe / Standard Font Recognition
- `/BaseFont` extraction from font dictionaries, subset prefix (`ABCDEF+`) stripped
- Bold / Italic detection from font name suffixes
- Font family mapping to CSS names (`Helvetica`, `Times New Roman`, `Courier New`, etc.) in the importer
- `FontFamily`, `Bold`, `Italic` passed to `PdfDrawTextOptions` in the generator bridge

### Phase 5 — Scene Graph Wiring
- All Phase 5 analysis engines (`SceneGraphEngine`, `ReadingOrderEngine`, `SemanticLayoutEngine`, etc.) built and tested
- `CanvasImporterPdfImporter` rewritten to consume `SceneGraphEngine.BuildPage` output
- Reading-order text emission (XY-cut algorithm, top-to-bottom, left-to-right)
- Semantic header/footer detection (top/bottom 8% zone) for multi-page shared elements
- `PrimitiveText`, `PrimitiveShape`, `PrimitivePath`, `PrimitiveImage`, nested `PrimitiveGroup` all mapped
- Rotation, bold/italic style keys, classification metadata passed in element style dict

### Test Fixes
- `MatrixEngine_ShouldTransformRotatedBoundsAndExtractRotation` — corrected expected `point.X` to `−50`
- Test suite: **84 tests, all passing**

---

## Known Gaps / Open Items

### QR Code / Complex Path Rendering (OPEN — not fixed)
**Symptom:** A QR code (or any PDF path consisting of many small rectangle subpaths) imports as a single solid black square instead of the individual module grid.

**Root cause:** `PrimitivePath` objects (multi-segment paths) reach `MapPath`, which uses `el.Bounds` — the path's overall bounding box — so every segment collapses to one filled rectangle.

**What was tried:**
1. `EmitComplexPath` — split `RectangleSegment` subpaths into individual shape elements, fell back to SVG for curve-only paths. Did not fix the visual result.
2. Relaxing the `onlyRects` guard from `All(s is RectangleSegment or ClosePathSegment)` to `!Any(s is CurveToSegment)` to allow `MoveToSegment` in the rect-split path. Also did not fix the visual result.

**Current state of the code:** Both attempts were reverted. `PrimitivePath` now goes through plain `MapPath` (bounding-box approach), same as `PrimitiveShape`.

**What to investigate next:**
- Confirm whether the QR code is actually a vector `PrimitivePath` or a raster `PrimitiveImage` XObject. If it is an image, the image codec path (CCITTFaxDecode / JBIG2) is the real bottleneck.
- If it is a vector path, add debug logging to `EmitPrimitive` to count how many segments are in the `PrimitivePath` that covers the QR area, and verify coordinates after `TransformBounds`.
- Check `LivePreview.tsx` shape rendering: the outer wrapper (`wrapperStyle`) sets position/size from `el.x/y/width/height`; the inner `<div>` fills 100%/100% with `backgroundColor`. Verify the element DTO fields are correct at the API boundary.

### JBIG2 and JPEG2000 decoders
Both are deferred — they require external library dependencies:
- `JBIG2Decode` → needs a JBIG2 library (e.g., `Docnet.Core` or native wrapper)
- `JPXDecode` (JPEG 2000) → needs `Grok`, `OpenJPEG`, or similar

PDFs that use only these codecs for images will produce empty `ImageBytes` and show no image.

### Image data URI format
`ImageBytesToDataUri` only sniffs JPEG (`FF D8 FF` header). Everything else defaults to `image/png`. Decoded bitmap pixel data (no container format) → browser cannot decode → broken image. A proper fix wraps raw pixel bytes in a lightweight PNG encoder before embedding.

### Complex path rendering (curves)
Curve-only `PrimitivePath` objects (circles, arcs, complex outlines) currently use `MapPath` (bounding box), which renders them as solid-filled rectangles. SVG fallback was attempted but reverted.

### Font size scale
The font size scale fix (Phase 3) was **reverted from the interpreter** because applying the text matrix scale to `FontSize` in `PdfGraphicsInterpreter` caused very large text on many real PDFs (the CTM can include a large scale factor). The scale is now applied only in `CanvasImporterPdfImporter.MapText` using `prim.Transform.ScaleY`.

### Decorations filtered
Primitives classified as `PrimitiveClassification.Decoration` are silently dropped during import. Intentional (decorative lines, background fills) but may occasionally drop meaningful content.

---

## Running the Stack

```bash
# Backend
dotnet run --project Canvas.WebApi/Canvas.WebApi.csproj --urls "http://localhost:5086"

# Frontend
cd ui-designer-v2 && npm run dev

# Tests
dotnet test tests/Canvas.Importer.Tests/Canvas.Importer.Tests.csproj
```

---

## Key Files to Know

| File | Why it matters |
|---|---|
| [CanvasImporterPdfImporter.cs](src/Canvas.Infrastructure.Converters/CanvasImporterPdfImporter.cs) | Main PDF→Canvas conversion: element mapping, coordinate flip, header/footer detection |
| [CanvasPdfGeneratorBridge.cs](src/Canvas.Infrastructure.Pdf/CanvasPdfGeneratorBridge.cs) | Canvas→PDF regeneration: image codec handling, shading, font mapping |
| [SceneGraphEngine.cs](src/Canvas.Importer/Analysis/SceneGraphEngine.cs) | Orchestrates all Phase 5 analysis engines per page |
| [PrimitiveModel.cs](src/Canvas.Importer/Analysis/PrimitiveModel.cs) | `PrimitiveText`, `PrimitivePath`, `PrimitiveShape`, `PrimitiveImage`, `PrimitiveGroup` |
| [BoundingBoxCalculator.cs](src/Canvas.Importer/Analysis/BoundingBoxCalculator.cs) | Computes global-space bounds for all primitive types by applying the CTM |
| [ReadingOrderEngine.cs](src/Canvas.Importer/Analysis/ReadingOrderEngine.cs) | XY-cut reading order, `Flatten` helper |
| [PdfGraphicsInterpreter.cs](src/Canvas.Importer/Graphics/PdfGraphicsInterpreter.cs) | Interprets PDF content stream operators into typed `PdfGraphicsElement` objects |
| [LivePreview.tsx](ui-designer-v2/src/components/Preview/LivePreview.tsx) | Frontend renderer for imported PDFs — `wrapperStyle` applies `el.x/y/width/height`; shape inner div fills 100%×100% with `backgroundColor` |
| [PdfImporterCoreTests.cs](tests/Canvas.Importer.Tests/PdfImporterCoreTests.cs) | 84 tests covering tokenizer, xref, content, text geometry, image codecs, barcodes, Phase 5 analysis |
| [Importer-New-Featuers.md](checklists/Importer-New-Featuers.md) | Phase-by-phase feature checklist |
