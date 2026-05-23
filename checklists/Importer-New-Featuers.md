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
- [ ] Expand the sample generator bridge beyond the current subset for shading regeneration and broader image/resource fidelity.
	- Covered so far: mixed group filtering so non-shading content is not duplicated during shading compatibility regeneration.
	- Covered so far: indirect shading resource graphs and inherited page-tree shading resources.
	- Covered so far: shading pages that also contain text and XObject image resources.
	- Covered so far: deleted shading elements no longer trigger compatibility regeneration.
	- Covered so far: shading-adjacent `/ColorSpace` resources are preserved with the shading resource bundle.
	- Covered so far: Flate XObject images can resolve named page `/ColorSpace` resources during regeneration.
	- Covered so far: ICCBased XObject images map back to the correct device color space based on the profile component count, including gray and CMYK cases.
	- Covered so far: named and indirect ICCBased image color-space resource definitions resolve correctly during regeneration.
	- Remaining likely slices: named ICCBased color-space resources, indirect ICCBased resource definitions, and other resource dependencies adjacent to shading/image regeneration.
	- Remaining likely slices: broader unsupported bridge cases where `Canvas.Pdf` still needs compatibility preservation instead of native emission.
- [ ] Add broader import-edit-regenerate end-to-end fixtures against real sample PDFs.
	- Target coverage: real PDFs that combine text, vector paths, images, inherited resources, incremental updates, and shading resources.
	- Goal: verify importer -> editable model -> bridge regeneration against less synthetic inputs than the focused unit-style object-graph tests.
- [ ] Implement actual deferred decoders for JBIG2, CCITT Fax, and JPEG2000.
	- This is a larger implementation item than the current bridge-fidelity slices.
	- Likely approach: add decoder implementations or vetted library-backed adapters, then extend `PdfStreamDecoderRegistry` coverage and focused decoder tests.
