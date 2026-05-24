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
        _fontParser = fontParser ?? new PdfSimpleFontParser(_streamDecoders);
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
        var imageResources = ResolveImageResources(resources, graph);
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
            var graphicsObjects = _graphicsInterpreter.Interpret(commands, fontResources);
            AttachImageResources(graphicsObjects, imageResources);
            page.GraphicsObjects.AddRange(graphicsObjects);
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

    private IReadOnlyDictionary<string, ReadOnlyMemory<byte>> ResolveImageResources(PdfDictionary resources, PdfObjectGraph graph)
    {
        if (ResolveObject(resources["XObject"], graph) is not PdfDictionary xObjectDictionary)
        {
            return new Dictionary<string, ReadOnlyMemory<byte>>();
        }

        var images = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal);
        foreach (var entry in xObjectDictionary.Values)
        {
            if (ResolveObject(entry.Value, graph) is not PdfStreamObject stream)
            {
                continue;
            }

            if (ResolveObject(stream.Dictionary["Subtype"], graph) is not PdfName { Value: "Image" })
            {
                continue;
            }

            DecodeIfPossible(stream);
            var imageBytes = GetRegenerableImageBytes(stream, graph);
            if (!imageBytes.IsEmpty)
            {
                images[entry.Key] = imageBytes;
            }
        }

        return images;
    }

    private static void AttachImageResources(IReadOnlyList<PdfGraphicsElement> elements, IReadOnlyDictionary<string, ReadOnlyMemory<byte>> imageResources)
    {
        foreach (var element in elements)
        {
            switch (element)
            {
                case PdfImageElement image when image.ImageBytes.IsEmpty && imageResources.TryGetValue(image.ResourceName, out var imageBytes):
                    image.ImageBytes = imageBytes;
                    break;
                case PdfGroupElement group:
                    AttachImageResources(group.Children, imageResources);
                    break;
            }
        }
    }

    private static ReadOnlyMemory<byte> GetRegenerableImageBytes(PdfStreamObject stream, PdfObjectGraph graph)
    {
        // DCTDecode and FlateDecode: keep encoded bytes intact (bridge needs them for re-emission)
        if (HasSingleSupportedImageFilter(stream.Dictionary["Filter"], graph))
            return stream.EncodedBytes;

        // Encoded bytes already carry a recognized image header (PNG or JPEG in stream)
        if (LooksLikeSupportedImage(stream.EncodedBytes.Span))
            return stream.EncodedBytes;

        // Decoded bytes carry a recognized image format
        if (stream.IsDecoded && LooksLikeSupportedImage(stream.DecodedBytes.Span))
            return stream.DecodedBytes;

        // Decoded raw pixels (CCITT, LZW, RunLength) → wrap as PNG for browser display
        if (stream.IsDecoded)
        {
            var png = TryWrapRawPixelsAsPng(stream.DecodedBytes.Span, stream.Dictionary);
            if (png.HasValue) return png.Value;
        }

        return ReadOnlyMemory<byte>.Empty;
    }

    private static bool HasSingleSupportedImageFilter(PdfObject? filter, PdfObjectGraph graph)
    {
        var resolvedFilter = ResolveObject(filter, graph) ?? filter;
        return resolvedFilter switch
        {
            PdfName { Value: "DCTDecode" or "FlateDecode" } => true,
            PdfArray { Items.Count: 1 } array => HasSingleSupportedImageFilter(array.Items[0], graph),
            _ => false
        };
    }

    /// <summary>
    /// Wraps raw decoded pixel bytes in a minimal PNG so browsers can display them.
    /// Supports 1-bit mono (CCITT output), 8-bit gray, and 8-bit RGB only.
    /// Returns null for unsupported formats (e.g. CMYK, missing metadata).
    /// </summary>
    private static ReadOnlyMemory<byte>? TryWrapRawPixelsAsPng(ReadOnlySpan<byte> pixels, PdfDictionary dict)
    {
        if (!TryGetInteger(dict["Width"],  out var width)  || width  <= 0) return null;
        if (!TryGetInteger(dict["Height"], out var height) || height <= 0) return null;
        if (!TryGetInteger(dict["BitsPerComponent"], out var bpc)) bpc = 8;

        var components = ResolveComponentCount(dict["ColorSpace"]);
        if (components <= 0 || components == 4) return null; // skip CMYK

        // Normalize to 8-bit per channel
        byte[] norm;
        if (bpc == 1 && components == 1)
        {
            // 1-bit packed mono → 8-bit grayscale (white=0xFF, black=0x00 in PDF convention)
            int rowBytes = ((int)width + 7) / 8;
            norm = new byte[(int)width * (int)height];
            int dst = 0;
            for (int row = 0; row < (int)height; row++)
            {
                int src = row * rowBytes;
                for (int col = 0; col < (int)width; col++)
                {
                    int byteIdx = src + col / 8;
                    int bit = (byteIdx < pixels.Length ? pixels[byteIdx] : 0) >> (7 - col % 8) & 1;
                    norm[dst++] = bit == 0 ? (byte)0x00 : (byte)0xFF; // 0=black,1=white in PDF default
                }
            }
        }
        else if (bpc == 8)
        {
            norm = pixels.Length >= (int)width * (int)height * components
                ? pixels[..(int)(width * height * components)].ToArray()
                : pixels.ToArray();
        }
        else
        {
            return null; // 2/4/16-bit not handled
        }

        byte colorType = components == 1 ? (byte)0 : (byte)2; // 0=gray, 2=RGB
        return EncodePng(norm, (int)width, (int)height, components, colorType);
    }

    private static int ResolveComponentCount(PdfObject? colorSpace)
    {
        return colorSpace switch
        {
            PdfName { Value: "DeviceGray" or "CalGray" } => 1,
            PdfName { Value: "DeviceRGB" or "CalRGB" or "sRGB" } => 3,
            PdfName { Value: "DeviceCMYK" } => 4,
            PdfArray arr when arr.Items.FirstOrDefault() is PdfName { Value: "ICCBased" } => -1, // skip
            _ => 1 // assume gray for unknown
        };
    }

    private static ReadOnlyMemory<byte> EncodePng(byte[] pixels, int width, int height, int components, byte colorType)
    {
        using var idatRaw = new System.IO.MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(idatRaw, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
        {
            int rowStride = width * components;
            for (int y = 0; y < height; y++)
            {
                zlib.WriteByte(0); // filter = None
                int rowStart = y * rowStride;
                int rowEnd = Math.Min(rowStart + rowStride, pixels.Length);
                if (rowEnd > rowStart)
                    zlib.Write(pixels, rowStart, rowEnd - rowStart);
            }
        }
        var idatData = idatRaw.ToArray();

        using var png = new System.IO.MemoryStream();
        // PNG signature
        png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        // IHDR
        var ihdr = new byte[]
        {
            (byte)(width >> 24), (byte)(width >> 16), (byte)(width >> 8), (byte)width,
            (byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height,
            8,          // bit depth
            colorType,  // color type
            0, 0, 0     // compression, filter, interlace
        };
        WritePngChunk(png, "IHDR", ihdr);

        // IDAT
        WritePngChunk(png, "IDAT", idatData);

        // IEND
        WritePngChunk(png, "IEND", Array.Empty<byte>());

        return png.ToArray();
    }

    private static void WritePngChunk(System.IO.Stream output, string type, byte[] data)
    {
        var lenBytes = BitConverter.GetBytes(data.Length);
        if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
        output.Write(lenBytes);

        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);

        uint crc = PngCrc32(typeBytes);
        crc = PngCrc32Update(crc, data);
        crc ^= 0xFFFFFFFF;
        var crcBytes = BitConverter.GetBytes(crc);
        if (BitConverter.IsLittleEndian) Array.Reverse(crcBytes);
        output.Write(crcBytes);
    }

    private static uint PngCrc32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        return PngCrc32Update(crc, data);
    }

    private static uint PngCrc32Update(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return crc;
    }

    private static bool TryGetInteger(PdfObject? obj, out long value)
    {
        switch (obj)
        {
            case PdfInteger i: value = i.Value; return true;
            case PdfNumber n:  value = (long)n.Value; return true;
            default: value = 0; return false;
        }
    }

    private static bool LooksLikeSupportedImage(ReadOnlySpan<byte> bytes)
    {
        return LooksLikePng(bytes) || LooksLikeJpeg(bytes);
    }

    private static bool LooksLikePng(ReadOnlySpan<byte> bytes)
    {
        return bytes.Length >= 8
            && bytes[0] == 0x89
            && bytes[1] == 0x50
            && bytes[2] == 0x4E
            && bytes[3] == 0x47
            && bytes[4] == 0x0D
            && bytes[5] == 0x0A
            && bytes[6] == 0x1A
            && bytes[7] == 0x0A;
    }

    private static bool LooksLikeJpeg(ReadOnlySpan<byte> bytes)
    {
        return bytes.Length >= 4
            && bytes[0] == 0xFF
            && bytes[1] == 0xD8
            && bytes[^2] == 0xFF
            && bytes[^1] == 0xD9;
    }

    private PdfFontResource AttachToUnicode(PdfFontResource font, PdfDictionary fontDictionary, PdfObjectResolver resolver)
    {
        if (fontDictionary["ToUnicode"] is not { } toUnicodeValue || resolver.Resolve(toUnicodeValue) is not PdfStreamObject stream)
        {
            return font;
        }

        DecodeIfPossible(stream);
        var bytes = stream.IsDecoded ? stream.DecodedBytes : stream.EncodedBytes;
        var toUnicode = new PdfToUnicodeCMapParser().Parse(bytes);
        return new PdfFontResource
        {
            ResourceName = font.ResourceName,
            Kind = font.Kind,
            Dictionary = font.Dictionary,
            Encoding = font.Encoding,
            Widths = font.Widths,
            MissingWidth = font.MissingWidth,
            CodeByteLength = Math.Max(font.CodeByteLength, toUnicode.MaxCodeLength),
            ToUnicode = toUnicode
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
