# Canvas.Importer Architecture

`Canvas.Importer` is a modular .NET 10 PDF parsing and editing SDK foundation. It intentionally does not implement OCR or AI. The project focuses on low-level PDF parsing, graphics interpretation, editable object reconstruction, and delegation to an existing PDF generation DLL.

## Pipeline

```text
PDF file
 -> PdfTokenizer
 -> PdfCrossReferenceParser
 -> PdfObjectParser
 -> PdfObjectGraph
 -> PdfContentStreamParser
 -> PdfGraphicsInterpreter
 -> PdfDocumentModel / editable scene graph
 -> IPdfGeneratorBridge
 -> existing PDF generator DLL
```

## Namespaces

- `Canvas.Importer.Tokenizer`: binary-safe tokenization for numbers, names, strings, arrays, dictionaries, comments, keywords, and content operators.
- `Canvas.Importer.Xref`: classic xref table model, incremental revision metadata, and parser entry point. Xref streams plug in here.
- `Canvas.Importer.Objects`: strongly typed PDF primitive object graph with source spans and original object ids.
- `Canvas.Importer.Content`: standard content operator registry and content stream command parser.
- `Canvas.Importer.Graphics`: graphics state stack, matrix/color geometry, and editable scene graph elements.
- `Canvas.Importer.Fonts`: font resource, encoding, ToUnicode, and font parser contracts.
- `Canvas.Importer.Streams`: stream decoder registry with Flate, ASCIIHex, ASCII85, and extension points for LZW/JBIG2/CCITT/JPEG2000.
- `Canvas.Importer.Document`: editable document, catalog, page, resource, content stream, text, and graphics hierarchy.
- `Canvas.Importer.Editing`: mutation session for text replacement, transforms, deletion, insertion, and metadata updates.
- `Canvas.Importer.Generation`: `IPdfGeneratorBridge` abstraction for mapping parsed objects into the existing PDF generation DLL.

## Core Responsibilities

`PdfTokenizer` reads `ReadOnlySpan<byte>` and produces low-allocation tokens. It is binary safe, comment aware, and supports PDF delimiters without assuming text-only input.

`PdfCrossReferenceParser` builds a `PdfCrossReferenceTable` keyed by `PdfObjectId`. It currently handles classic xref tables and has an indirect-object fallback for early integration. Xref streams and incremental-update chain traversal belong behind this parser.

`PdfObjectParser` creates `PdfDictionary`, `PdfArray`, `PdfName`, `PdfString`, `PdfNumber`, `PdfBoolean`, `PdfNull`, `PdfReference`, and `PdfStreamObject` instances while preserving source offsets and original references.

`PdfContentStreamParser` converts graphics/content streams into `PdfContentCommand` records. `PdfOperatorRegistry` contains the standard operator surface for graphics state, paths, painting, clipping, text, colors, XObjects, inline images, marked content, and compatibility sections.

`PdfGraphicsInterpreter` maintains `GraphicsStateStack`, applies `q/Q/cm`, tracks text and path state, and creates editable `PdfTextElement`, `PdfPathElement`, `PdfImageElement`, and `PdfGroupElement` instances.

`PdfDocumentModel` is the editable DOM. Pages preserve original dictionaries, resources, content streams, graphics objects, text objects, source offsets, and object references.

`IPdfGeneratorBridge` is the only write integration point. The importer must map parsed/editable objects into the existing generator DLL instead of growing a second writer engine.

## Performance Strategy

- Parse from `ReadOnlyMemory<byte>` and `ReadOnlySpan<byte>`.
- Preserve encoded stream bytes and defer decoding until needed.
- Keep object ids and source spans for lazy object loading and incremental-update support.
- Use registries for decoders and parser extensions instead of reflection-heavy discovery.
- Make page interpretation independent so callers can add parallel page parsing.
- Prefer pooled buffers at file ingress and keep stream decoding isolated for future pooling.

## Expansion Points

- Add xref stream parser under `Canvas.Importer.Xref`.
- Add stream decoders by implementing `IPdfStreamDecoder`.
- Add font engines by implementing `IPdfFontParser`.
- Add renderer/SVG/HTML export layers from the editable scene graph.
- Add annotations, forms, signatures, OCR, or AI modules as separate layers over the parsed DOM.
