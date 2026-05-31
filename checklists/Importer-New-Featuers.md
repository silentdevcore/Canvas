# Importer New Featuers Checklist

Scope: this checklist tracks core Canvas.Importer PDF parsing, interpretation, editing, and regeneration work. Annotations, forms, signatures, OCR, and AI remain separate layers over the parsed DOM and are intentionally out of scope here.

## Parser Core

- [x] Create `Canvas.Importer` .NET 10 project.
- [x] Add low-level tokenizer, object parser, object graph, content parser, graphics scene graph, and generator bridge contracts.
- [x] Replace parser rewind behavior with explicit token lookahead.
- [x] Resolve indirect `/Length` values while parsing streams.
- [x] Preserve `endstream` spans for more accurate regeneration metadata.
- [x] Preserve `endobj` spans for more accurate regeneration metadata.

## Cross Reference Support

- [x] Parse classic xref entries.
- [x] Parse classic trailer dictionaries.
- [x] Parse `startxref` chains and `/Prev` incremental updates.
- [x] Parse xref streams.
- [x] Parse compressed object streams.

## Document Model

- [x] Build editable document/page model.
- [x] Traverse the page tree in document order.
- [x] Apply inherited page resources.
- [x] Inherit media, crop, bleed, trim, and art boxes, plus page rotation.
- [x] Add object resolver service for shared lazy loading.

## Content And Graphics

- [x] Register standard PDF content operators.
- [x] Convert text, path, and image commands into editable scene elements.
- [x] Apply color operators to graphics state and scene elements.
- [x] Parse inline images as image elements.
- [x] Track clipping paths.
- [x] Add full text positioning advancement for `Tj`, `TJ`, `'`, `"`, `Td`, `TD`, and `T*`.
- [x] Track marked content operators (`MP`, `DP`, `BMC`, `BDC`, `EMC`) and preserve their structure in the scene graph.
- [x] Support shading operators such as `sh`.
- [x] Decide whether compatibility operators (`BX`, `EX`) require explicit parsing or regeneration handling.

## Streams And Fonts

- [x] Add Flate, ASCIIHex, ASCII85, and baseline LZW stream decoders.
- [x] Add PNG predictor handling for Flate/LZW streams.
- [x] Add ToUnicode CMap parser.
- [x] Add Type1, TrueType, Type3, Type0, and CIDFont resource parsers.
- [x] Evaluate deferred decoder roadmap items such as JBIG2, CCITT, and JPEG2000 support.

## Editing And Regeneration

- [x] Add editing session primitives for replace, move, delete, and metadata update.
- [x] Add insertion editing primitives.
- [x] Add `IPdfGeneratorBridge` contract.
- [x] Add generator bridge sample adapter against `Canvas.Infrastructure.Pdf` / `Canvas.Pdf`.
- [x] Add round-trip content stream rewrite model.
- [x] Add focused fixture tests for tokenizer, objects, xref, page tree, and content parsing.
- [x] Add focused fixture tests for marked content, color operators, extended page boxes, and regeneration/editing edge cases.

## Next Phase

- [x] Expand the sample generator bridge to support fill-only vector path regeneration.
- [x] Expand the sample generator bridge to round-trip JPEG-backed XObject image resources.
- [x] Expand the sample generator bridge to round-trip simple Flate-backed XObject image resources.
- [x] Expand the sample generator bridge to preserve XObject image soft masks.
- [x] Expand the sample generator bridge to preserve direct page shading resources via compatibility regeneration.
- [x] Expand the sample generator bridge beyond the current subset for shading regeneration and broader image/resource fidelity.
	- Covered so far: mixed group filtering so non-shading content is not duplicated during shading compatibility regeneration.
	- Covered so far: indirect shading resource graphs and inherited page-tree shading resources.
	- Covered so far: shading pages that also contain text and XObject image resources.
	- Covered so far: deleted shading elements no longer trigger compatibility regeneration.
	- Covered so far: shading-adjacent `/ColorSpace` resources are preserved with the shading resource bundle.
	- Covered so far: Flate XObject images can resolve named page `/ColorSpace` resources during regeneration.
	- Covered so far: ICCBased XObject images map back to the correct device color space based on the profile component count, including gray and CMYK cases.
	- Covered so far: named and indirect ICCBased image color-space resource definitions resolve correctly during regeneration.
	- Covered so far: Flate XObject images preserve indirect `/DecodeParms` metadata during regeneration.
	- Covered so far: single-entry `/Filter` and `/DecodeParms` arrays for Flate XObject images regenerate correctly.
	- Covered so far: Flate XObject images preserve indirect `/Filter` objects during regeneration.
	- Covered so far: shading with named ICCBased `/ColorSpace` resource (array defined inline in page resource dict).
	- Covered so far: shading with indirect ICCBased `/ColorSpace` resource definition (CS1 → indirect ref → ICCBased array).
- [x] Add broader import-edit-regenerate end-to-end fixtures against real sample PDFs.
	- Covered: multi-element page round-trip — text + fill-path + JPEG image through the full importer → model → bridge cycle.
	- Covered: two-page document with page-tree inherited resources (shared `/ColorSpace`) and multi-page bridge regeneration.
	- Covered: combined text + path + JPEG image + shading on one page; verifies shading compatibility update does not erase other content.
- [x] Implement actual deferred decoders for JBIG2, CCITT Fax, and JPEG2000.
	- Implemented: CCITTFaxDecode — pure C# decoder for Group 3 1D and Group 4 with full Huffman tables, 2D pass/horizontal/vertical modes, and configurable K/Columns/Rows/EndOfLine/EndOfBlock/BlackIs1 parameters.
	- Remaining: JBIG2Decode — deferred; requires an external JBIG2 library dependency.
	- Remaining: JPXDecode — deferred; requires an external JPEG2000 library dependency.

## Phase 3 — Text Fidelity, Image Codec Expansion, Barcode Support

- [x] Fix effective font size calculation to include text matrix scale factor (FontSize × sqrt(A²+B²)).
- [x] Add round-trip tests for rotated text (90°, 45°) and scaled text (Tm scale factor).
- [x] Expand generator bridge to round-trip CCITTFaxDecode-encoded XObject images (decode then re-encode as FlateDecode).
- [x] Expand generator bridge to round-trip LZWDecode-encoded XObject images.
- [x] Add Indexed color space support to bridge image handling (expand palette to device color space before regeneration).
- [x] Verify barcode round-trip: vector-path barcodes, CCITT/Indexed image barcodes, and barcode-font text barcodes.

## Phase 4 — Adobe / Standard Font Recognition

- [x] Extract `/BaseFont` name from font dictionaries during parsing (strip embedded subset prefix `ABCDEF+`).
- [x] Propagate `FontName`, `Bold`, and `Italic` flags from parsed font resource to `PdfTextElement`.
- [x] Add `ResolveStandardFontFamily` mapping in bridge: Helvetica/Arial → Helvetica, Times/Garamond → Times, Courier/Mono → Courier.
- [x] Pass `FontFamily`, `Bold`, and `Italic` to `PdfDrawTextOptions` when regenerating text elements.

## Phase 5 — Graphics Scene And Semantic Reconstruction

- [x] Add matrix transformation engine for translate/scale/rotate/skew, local-to-world conversion, transformed bounds, and orientation extraction.
- [x] Add text geometry engine for rotated/arbitrary-angle text bounds, baseline vectors, and reading orientation.
- [x] Add bounding box engine for text, paths, images, groups, clipping-aware intersections, and transformed Bézier/path geometry.
- [x] Add primitive object model (`PrimitiveText`, `PrimitivePath`, `PrimitiveImage`, `PrimitiveShape`, `PrimitiveGroup`) preserving source operators, transforms, bounds, z-order, resources, and graphics style snapshots.
- [x] Add XObject resolution engine for image XObjects, Form XObjects, nested XObjects, inherited transforms, and reusable graphics instances.
- [x] Add scene graph engine with layers, groups, editable graphics nodes, resources, traversal, and export-ready structure.
- [x] Add reading order engine for line building, paragraph reconstruction, column detection, XY-cut style block ordering, and draw-order-independent flow.
- [x] Add text reconstruction engine for glyph/word/line/paragraph heuristics using spacing, baseline alignment, font continuity, and transform continuity.
- [x] Add object classification engine for vector icons, symbol-font icons, images, barcodes, separators, decorations, and table lines.
- [x] Add barcode detection heuristics for linear, QR/DataMatrix-like, PDF417-like, and high-frequency geometry regions.
- [x] Add grouping engine for visual containment, proximity, shared transforms/colors, alignment, labels + values, icon + text, buttons, and table-cell groups.
- [x] Add semantic layout tree for headers, footers, paragraphs, tables, figures, lists, labels, and form-like regions without OCR/AI.
- [x] Add debug overlay model for bounds, baselines, matrices, z-order, object ids, grouping, reading order, and classifications.
- [x] Add focused Phase 5 tests for matrix math, bounding boxes, reading order, grouping, and classification heuristics.
- [x] Wire `CanvasImporterPdfImporter` to import from `SceneGraphEngine` primitives while preserving the `DesignExportDto` API response shape.
- [x] Map `PrimitiveText`, `PrimitiveShape`, `PrimitivePath`, `PrimitiveImage`, and nested `PrimitiveGroup` nodes into editable Canvas elements.
- [x] Preserve repeated multi-page header/footer promotion while using Phase 5 reading order, bounds, rotation, and classification metadata.
- [x] Extend `debug-pdf-engine` diagnostics with Phase 5 scene graph metrics, classification counts, reading order, groups, layout nodes, and debug overlays.
- [x] Add backend importer coverage for rotated text, draw-order-independent text import, vector shapes, image XObjects, barcode-like bars, and shared headers.

## SVG Import

Goal: convert `.svg` files into editable Canvas designs with full vector fidelity. No new NuGet dependencies — uses `System.Xml.Linq` (already used in `OdtImporter`). New static class `SvgImporter` in `Canvas.Infrastructure.Converters`.

### Page dimensions
- [ ] Read `viewBox` attribute from `<svg>` root for canvas width/height.
- [ ] Fall back to `width`/`height` attributes; fall back to bounding box of all elements if both absent.

### Transform handling
- [ ] Parse `transform` attribute on `<g>` and individual elements: `matrix()`, `translate()`, `rotate()`, `scale()`, `skewX()`, `skewY()`.
- [ ] Compose transforms down the element tree (multiply matrices) before emitting final canvas coordinates.

### Element mapping
- [ ] Map `<rect>` → `shape` element (fill + stroke from attributes/inline style).
- [ ] Map `<circle>` and `<ellipse>` → inline SVG data-URI `image` element (Canvas has no circle primitive).
- [ ] Map `<line>`, `<polyline>`, `<polygon>`, `<path>` → inline SVG data-URI `image` element (same technique as `CanvasImporterPdfImporter.EmitSvgPath`).
- [ ] Map `<text>` / `<tspan>` → `text` element (x/y, font-size, fill color, font-family, font-weight).
- [ ] Map `<image>` → `image` element; resolve `href` / `xlink:href`; embed referenced file as base64 data-URI if relative path.
- [ ] Recurse into `<g>` groups, accumulating the composed transform.
- [ ] Register `<symbol>` definitions and resolve `<use href>` by cloning the referenced subtree with the `<use>` transform applied.
- [ ] Skip `<defs>` content during direct emit (only used for `<use>` resolution).

### Color resolution
- [ ] Parse `fill`, `stroke`, `fill-opacity`, `stroke-opacity`, `opacity` from both XML attributes and inline `style=""`.
- [ ] Resolve `currentColor` and `inherit` keywords by walking the element ancestry.

### API endpoint
- [ ] Add `POST /api/document/import-svg` to `DocumentOpsController` — accepts `.svg` / `image/svg+xml`, delegates to `SvgImporter.Import(stream, fileName)`.

### Frontend wiring
- [ ] Add `importSvg(file)` to `ExportService.ts` calling `_importFile(file, 'import-svg')`.
- [ ] Add `.svg` routing case in `useTemplateLoader.ts` → `loadFromFile()`.
- [ ] Add `.svg` to the file-input `accept` attribute in `IndexPage.tsx`.

---

## PowerPoint Import (.pptx)

Goal: convert `.pptx` files into editable Canvas designs with full slide fidelity (text, images, shapes, backgrounds, slide dimensions). Uses `DocumentFormat.OpenXml` — already a dependency of `Canvas.Infrastructure.Word` (same project as `DocxImporter`). New class `PptxImporter` in `Canvas.Infrastructure.Word`.

### Slide dimensions and page settings
- [ ] Read `PresentationPart.Presentation.SlideSize` (cx/cy in EMU); convert EMU → px (÷ 9144 × 96).
- [ ] Set `PageSettings.Width`, `PageSettings.Height`, and `Orientation` from slide size.

### Slide → page mapping
- [ ] Iterate `PresentationPart.SlideParts` in presentation slide-id order; emit one `PageDto` per slide.

### Shape tree traversal
- [ ] For each `sp` (shape) with a `txBody`: extract text runs with font size, bold, italic, color, alignment → `text` element. Position/size from `spPr.xfrm` (EMU → px).
- [ ] For each `sp` without `txBody`: emit `shape` element; resolve fill color from `solidFill` → `srgbClr` / `schemeClr` (walk theme for scheme colors).
- [ ] For each `pic` (picture): extract blip relationship bytes, encode as base64 data-URI → `image` element. Position/size from `spPr.xfrm`.
- [ ] Resolve inherited styles: walk `sp.ShapeStyle` → slide layout shape → slide master shape for unset font size, color, and fill values.

### Background
- [ ] Read slide background fill (`CSld.Background` → `BgPr.solidFill` or `gradFill`).
- [ ] Emit as a full-page `shape` element behind all other elements, or set `PageSettings` background color if solid.
- [ ] Fall back to slide layout / slide master background if the slide itself has no explicit background.

### Theme color resolution
- [ ] Load `ThemePart` from slide master; build a scheme-color map (`dk1`, `lt1`, `acc1`–`acc6`, etc.) for resolving `schemeClr` references.
- [ ] Apply shade/tint/lum modifiers from `<a:lumMod>` / `<a:lumOff>` when present.

### API endpoint
- [ ] Add `POST /api/document/import-pptx` to `DocumentOpsController` — accepts `.pptx`, delegates to `PptxImporter.Import(stream, fileName)`.

### Frontend wiring
- [ ] Add `importPptx(file)` to `ExportService.ts` calling `_importFile(file, 'import-pptx')`.
- [ ] Add `.pptx` routing case in `useTemplateLoader.ts` → `loadFromFile()`.
- [ ] Add `.pptx` to the file-input `accept` attribute in `IndexPage.tsx`.
