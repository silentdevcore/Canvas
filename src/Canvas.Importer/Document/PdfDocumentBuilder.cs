using Canvas.Importer.Content;
using Canvas.Importer.Fonts;
using Canvas.Importer.Graphics;
using Canvas.Importer.Objects;
using Canvas.Importer.Parsing;
using Canvas.Importer.Streams;

namespace Canvas.Importer.Document;

public sealed class PdfDocumentBuilder
{
    private readonly PdfContentStreamParser _contentParser;
    private readonly PdfGraphicsInterpreter _graphicsInterpreter;
    private readonly PdfStreamDecoderRegistry _streamDecoders;
    private readonly IPdfFontParser _fontParser;

    public PdfDocumentBuilder(PdfContentStreamParser contentParser, PdfGraphicsInterpreter graphicsInterpreter, PdfStreamDecoderRegistry? streamDecoders = null, IPdfFontParser? fontParser = null)
    {
        _contentParser = contentParser;
        _graphicsInterpreter = graphicsInterpreter;
        _streamDecoders = streamDecoders ?? new PdfStreamDecoderRegistry();
        _fontParser = fontParser ?? new PdfSimpleFontParser();
    }

    public PdfDocumentModel Build(PdfObjectGraph graph)
    {
        var document = new PdfDocumentModel { ObjectGraph = graph };
        var pagesFromTree = false;

        foreach (var indirectObject in graph.Objects.Values)
        {
            if (indirectObject.Value is not PdfDictionary dictionary)
            {
                continue;
            }

            if (dictionary["Type"] is PdfName { Value: "Catalog" })
            {
                document.Catalog = new PdfCatalog
                {
                    OriginalReference = indirectObject.Id,
                    Dictionary = dictionary
                };

                pagesFromTree = AddPagesFromCatalog(document, dictionary, graph);
            }
        }

        if (!pagesFromTree)
        {
            foreach (var indirectObject in graph.Objects.Values)
            {
                if (indirectObject.Value is not PdfDictionary dictionary)
                {
                    continue;
                }

                if (dictionary["Type"] is PdfName { Value: "Page" })
                {
                    document.AddPage(BuildPage(indirectObject, graph, PdfInheritedPageAttributes.Empty));
                }
            }
        }

        return document;
    }

    private bool AddPagesFromCatalog(PdfDocumentModel document, PdfDictionary catalog, PdfObjectGraph graph)
    {
        var pagesRoot = ResolveIndirectObject(catalog["Pages"], graph);
        if (pagesRoot?.Value is not PdfDictionary pagesDictionary)
        {
            return false;
        }

        var added = 0;
        foreach (var page in WalkPageTree(pagesRoot, pagesDictionary, graph, PdfInheritedPageAttributes.Empty, visited: []))
        {
            document.AddPage(page);
            added++;
        }

        return added > 0;
    }

    private IEnumerable<PdfPageModel> WalkPageTree(
        PdfIndirectObject currentObject,
        PdfDictionary currentDictionary,
        PdfObjectGraph graph,
        PdfInheritedPageAttributes inherited,
        HashSet<PdfObjectId> visited)
    {
        if (!visited.Add(currentObject.Id))
        {
            yield break;
        }

        var current = inherited.Merge(currentDictionary, graph);

        if (currentDictionary["Type"] is PdfName { Value: "Page" })
        {
            yield return BuildPage(currentObject, graph, current);
            yield break;
        }

        if (currentDictionary["Kids"] is not PdfArray kids)
        {
            yield break;
        }

        foreach (var kid in kids.Items)
        {
            var kidObject = ResolveIndirectObject(kid, graph);
            if (kidObject?.Value is not PdfDictionary kidDictionary)
            {
                continue;
            }

            foreach (var page in WalkPageTree(kidObject, kidDictionary, graph, current, visited))
            {
                yield return page;
            }
        }
    }

    private PdfPageModel BuildPage(PdfIndirectObject pageObject, PdfObjectGraph graph, PdfInheritedPageAttributes inherited)
    {
        var dictionary = (PdfDictionary)pageObject.Value;
        var resources = ResolveDictionary(dictionary["Resources"], graph) ?? inherited.Resources ?? new PdfDictionary();
        var fontResources = ResolveFontResources(resources, graph);
        var page = new PdfPageModel(pageObject.Id, dictionary)
        {
            Resources = resources,
            MediaBox = ResolveRectangle(dictionary["MediaBox"], graph) ?? inherited.MediaBox,
            CropBox = ResolveRectangle(dictionary["CropBox"], graph) ?? inherited.CropBox,
            BleedBox = ResolveRectangle(dictionary["BleedBox"], graph) ?? inherited.BleedBox,
            TrimBox = ResolveRectangle(dictionary["TrimBox"], graph) ?? inherited.TrimBox,
            ArtBox = ResolveRectangle(dictionary["ArtBox"], graph) ?? inherited.ArtBox,
            Rotate = ResolveInteger(dictionary["Rotate"], graph) ?? inherited.Rotate,
            FontResources = fontResources
        };

        foreach (var stream in ResolveContentStreams(dictionary["Contents"], graph))
        {
            page.ContentStreams.Add(stream);
            DecodeIfPossible(stream);
            var commands = _contentParser.Parse(stream.IsDecoded ? stream.DecodedBytes : stream.EncodedBytes);
            page.GraphicsObjects.AddRange(_graphicsInterpreter.Interpret(commands, fontResources));
        }

        return page;
    }

    private PdfPageModel BuildPage(PdfIndirectObject pageObject, PdfObjectGraph graph)
    {
        return BuildPage(pageObject, graph, PdfInheritedPageAttributes.Empty);
    }

    private static PdfIndirectObject? ResolveIndirectObject(PdfObject? value, PdfObjectGraph graph)
    {
        return value switch
        {
            PdfReference reference => graph.Resolve(reference.Id),
            _ => null
        };
    }

    private static IEnumerable<PdfStreamObject> ResolveContentStreams(PdfObject? contents, PdfObjectGraph graph)
    {
        switch (contents)
        {
            case PdfStreamObject stream:
                yield return stream;
                break;
            case PdfReference reference when graph.Resolve(reference.Id)?.Value is PdfStreamObject stream:
                yield return stream;
                break;
            case PdfArray array:
                foreach (var item in array.Items)
                {
                    foreach (var streamItem in ResolveContentStreams(item, graph))
                    {
                        yield return streamItem;
                    }
                }

                break;
        }
    }

    private static PdfDictionary? ResolveDictionary(PdfObject? value, PdfObjectGraph graph)
    {
        return value switch
        {
            PdfDictionary dictionary => dictionary,
            PdfReference reference when graph.Resolve(reference.Id)?.Value is PdfDictionary dictionary => dictionary,
            _ => null
        };
    }

    private static PdfRectangle? ResolveRectangle(PdfObject? value, PdfObjectGraph graph)
    {
        var resolved = ResolveObject(value, graph);
        if (resolved is not PdfArray { Items.Count: >= 4 } array)
        {
            return null;
        }

        var x1 = ResolveNumber(array.Items[0], graph);
        var y1 = ResolveNumber(array.Items[1], graph);
        var x2 = ResolveNumber(array.Items[2], graph);
        var y2 = ResolveNumber(array.Items[3], graph);
        if (x1 is null || y1 is null || x2 is null || y2 is null)
        {
            return null;
        }

        return new PdfRectangle(x1.Value, y1.Value, x2.Value - x1.Value, y2.Value - y1.Value);
    }

    private static int? ResolveInteger(PdfObject? value, PdfObjectGraph graph)
    {
        return ResolveNumber(value, graph) is { } number ? (int)number : null;
    }

    private static double? ResolveNumber(PdfObject? value, PdfObjectGraph graph)
    {
        return ResolveObject(value, graph) switch
        {
            PdfInteger integer => integer.Value,
            PdfNumber number => number.Value,
            _ => null
        };
    }

    private static PdfObject? ResolveObject(PdfObject? value, PdfObjectGraph graph)
    {
        return value switch
        {
            PdfReference reference => graph.Resolve(reference.Id)?.Value,
            _ => value
        };
    }

    private void DecodeIfPossible(PdfStreamObject stream)
    {
        if (stream.IsDecoded)
        {
            return;
        }

        try
        {
            stream.SetDecodedBytes(_streamDecoders.Decode(stream));
        }
        catch (NotSupportedException)
        {
            // Keep encoded bytes available; specialized decoders can be registered later.
        }
        catch (InvalidDataException)
        {
            // Malformed streams should not prevent object graph construction.
        }
    }

    private IReadOnlyDictionary<string, PdfFontResource> ResolveFontResources(PdfDictionary resources, PdfObjectGraph graph)
    {
        if (ResolveObject(resources["Font"], graph) is not PdfDictionary fontDictionary)
        {
            return new Dictionary<string, PdfFontResource>();
        }

        var resolver = new PdfObjectResolver(graph);
        var fonts = new Dictionary<string, PdfFontResource>(StringComparer.Ordinal);
        foreach (var entry in fontDictionary.Values)
        {
            if (ResolveObject(entry.Value, graph) is not PdfDictionary resolvedFont)
            {
                continue;
            }

            var font = _fontParser.Parse(entry.Key, resolvedFont, resolver);
            fonts[entry.Key] = AttachToUnicode(font, resolvedFont, resolver);
        }

        return fonts;
    }

    private PdfFontResource AttachToUnicode(PdfFontResource font, PdfDictionary fontDictionary, PdfObjectResolver resolver)
    {
        if (fontDictionary["ToUnicode"] is not { } toUnicodeValue || resolver.Resolve(toUnicodeValue) is not PdfStreamObject stream)
        {
            return font;
        }

        DecodeIfPossible(stream);
        var bytes = stream.IsDecoded ? stream.DecodedBytes : stream.EncodedBytes;
        return new PdfFontResource
        {
            ResourceName = font.ResourceName,
            Kind = font.Kind,
            Dictionary = font.Dictionary,
            Encoding = font.Encoding,
            Widths = font.Widths,
            MissingWidth = font.MissingWidth,
            ToUnicode = new PdfToUnicodeCMapParser().Parse(bytes)
        };
    }

    private sealed record PdfInheritedPageAttributes(
        PdfDictionary? Resources,
        PdfRectangle? MediaBox,
        PdfRectangle? CropBox,
        PdfRectangle? BleedBox,
        PdfRectangle? TrimBox,
        PdfRectangle? ArtBox,
        int Rotate)
    {
        public static PdfInheritedPageAttributes Empty { get; } = new(null, null, null, null, null, null, 0);

        public PdfInheritedPageAttributes Merge(PdfDictionary dictionary, PdfObjectGraph graph)
        {
            return this with
            {
                Resources = ResolveDictionary(dictionary["Resources"], graph) ?? Resources,
                MediaBox = ResolveRectangle(dictionary["MediaBox"], graph) ?? MediaBox,
                CropBox = ResolveRectangle(dictionary["CropBox"], graph) ?? CropBox,
                BleedBox = ResolveRectangle(dictionary["BleedBox"], graph) ?? BleedBox,
                TrimBox = ResolveRectangle(dictionary["TrimBox"], graph) ?? TrimBox,
                ArtBox = ResolveRectangle(dictionary["ArtBox"], graph) ?? ArtBox,
                Rotate = ResolveInteger(dictionary["Rotate"], graph) ?? Rotate
            };
        }
    }
}
