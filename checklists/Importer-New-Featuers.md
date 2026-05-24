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
