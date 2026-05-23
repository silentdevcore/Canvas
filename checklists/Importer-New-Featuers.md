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
- [ ] Apply color operators to graphics state and scene elements.
- [ ] Parse inline images as image elements.
- [ ] Track clipping paths.
- [ ] Add full text positioning advancement for `Tj`, `TJ`, `'`, `"`, `Td`, `TD`, and `T*`.
- [x] Track marked content operators (`MP`, `DP`, `BMC`, `BDC`, `EMC`) and preserve their structure in the scene graph.
- [ ] Support shading operators such as `sh`.
- [ ] Decide whether compatibility operators (`BX`, `EX`) require explicit parsing or regeneration handling.

## Streams And Fonts

- [x] Add Flate, ASCIIHex, ASCII85, and baseline LZW stream decoders.
- [x] Add PNG predictor handling for Flate/LZW streams.
- [ ] Add ToUnicode CMap parser.
- [ ] Add Type1, TrueType, Type3, Type0, and CIDFont resource parsers.
- [ ] Evaluate deferred decoder roadmap items such as JBIG2, CCITT, and JPEG2000 support.

## Editing And Regeneration

- [x] Add editing session primitives for replace, move, delete, and metadata update.
- [x] Add insertion editing primitives.
- [x] Add `IPdfGeneratorBridge` contract.
- [ ] Add generator bridge sample adapter once the existing generator DLL API is selected.
- [ ] Add round-trip content stream rewrite model.
- [x] Add focused fixture tests for tokenizer, objects, xref, page tree, and content parsing.
- [ ] Add focused fixture tests for marked content, color operators, extended page boxes, and regeneration/editing edge cases.
