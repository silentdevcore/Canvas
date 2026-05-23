using System.IO.Compression;
using System.Text;
using Canvas.Importer;
using Canvas.Importer.Document;
using Canvas.Importer.Content;
using Canvas.Importer.Graphics;
using Canvas.Importer.Editing;
using Canvas.Importer.Fonts;
using Canvas.Importer.Generation;
using Canvas.Importer.Objects;
using Canvas.Importer.Parsing;
using Canvas.Importer.Streams;
using Canvas.Importer.Xref;

namespace Canvas.Importer.Tests;

public sealed class PdfImporterCoreTests
{
    [Fact]
    public void DocumentBuilder_ShouldTraversePageTree_AndInheritResourcesAndGeometry()
    {
        var graph = new PdfObjectGraph();
        var resourceDictionary = new PdfDictionary();
        resourceDictionary["Font"] = new PdfDictionary();

        var catalog = new PdfDictionary();
        catalog["Type"] = new PdfName("Catalog");
        catalog["Pages"] = new PdfReference(new PdfObjectId(2, 0));

        var pages = new PdfDictionary();
        pages["Type"] = new PdfName("Pages");
        pages["Resources"] = resourceDictionary;
        pages["MediaBox"] = Array(0, 0, 612, 792);
        pages["Rotate"] = new PdfInteger(90);
        pages["Kids"] = new PdfArray([new PdfReference(new PdfObjectId(3, 0))]);

        var page = new PdfDictionary();
        page["Type"] = new PdfName("Page");

        graph.Add(new PdfIndirectObject(new PdfObjectId(1, 0), catalog, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(2, 0), pages, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(3, 0), page, new PdfSourceSpan(0, 1)));

        var document = new PdfDocumentBuilder(new Content.PdfContentStreamParser(), new Graphics.PdfGraphicsInterpreter()).Build(graph);

        var builtPage = Assert.Single(document.Pages);
        Assert.Same(resourceDictionary, builtPage.Resources);
        Assert.Equal(new PdfRectangle(0, 0, 612, 792), builtPage.MediaBox);
        Assert.Equal(90, builtPage.Rotate);
    }

    [Fact]
    public void DocumentBuilder_ShouldInheritExtendedPageBoxesFromPageTree()
    {
        var graph = new PdfObjectGraph();

        var catalog = new PdfDictionary();
        catalog["Type"] = new PdfName("Catalog");
        catalog["Pages"] = new PdfReference(new PdfObjectId(2, 0));

        var pages = new PdfDictionary();
        pages["Type"] = new PdfName("Pages");
        pages["BleedBox"] = Array(0, 0, 620, 800);
        pages["TrimBox"] = Array(10, 20, 600, 780);
        pages["ArtBox"] = Array(30, 40, 580, 760);
        pages["Kids"] = new PdfArray([new PdfReference(new PdfObjectId(3, 0))]);

        var page = new PdfDictionary();
        page["Type"] = new PdfName("Page");

        graph.Add(new PdfIndirectObject(new PdfObjectId(1, 0), catalog, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(2, 0), pages, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(3, 0), page, new PdfSourceSpan(0, 1)));

        var document = new PdfDocumentBuilder(new Content.PdfContentStreamParser(), new Graphics.PdfGraphicsInterpreter()).Build(graph);

        var builtPage = Assert.Single(document.Pages);
        Assert.Equal(new PdfRectangle(0, 0, 620, 800), builtPage.BleedBox);
        Assert.Equal(new PdfRectangle(10, 20, 590, 760), builtPage.TrimBox);
        Assert.Equal(new PdfRectangle(30, 40, 550, 720), builtPage.ArtBox);
    }

    [Fact]
    public void XrefParser_ShouldFollowStartXrefAndPrevChain()
    {
        var bytes = BuildIncrementalXrefPdf();

        var context = new PdfParseContext(bytes, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);

        Assert.True(table.Entries.ContainsKey(new PdfObjectId(1, 0)));
        Assert.True(table.Entries.ContainsKey(new PdfObjectId(2, 0)));
        Assert.NotNull(table.Trailer);
    }

    [Fact]
    public void FlateDecoder_ShouldApplyPngSubPredictor()
    {
        var predicted = new byte[] { 1, 10, 2, 3 };
        var compressed = Compress(predicted);
        var dictionary = new PdfDictionary();
        dictionary["DecodeParms"] = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Predictor"] = new PdfInteger(12),
            ["Colors"] = new PdfInteger(1),
            ["BitsPerComponent"] = new PdfInteger(8),
            ["Columns"] = new PdfInteger(3)
        });

        var decoded = new FlateDecodeStreamDecoder().Decode(compressed, dictionary);

        Assert.Equal([10, 12, 15], decoded.ToArray());
    }

    [Fact]
    public void StreamDecoderRegistry_ShouldEvaluateDeferredFilters()
    {
        var registry = new PdfStreamDecoderRegistry();
        var filterArray = new PdfArray([
            new PdfName("FlateDecode"),
            new PdfName("JBIG2Decode"),
            new PdfName("CCITTFaxDecode"),
            new PdfName("JPXDecode")
        ]);

        var supports = registry.Evaluate(filterArray);

        Assert.Collection(
            supports,
            support => Assert.Equal(PdfStreamDecoderSupportStatus.Supported, support.Status),
            support => Assert.Equal(PdfStreamDecoderSupportStatus.Deferred, support.Status),
            support => Assert.Equal(PdfStreamDecoderSupportStatus.Deferred, support.Status),
            support => Assert.Equal(PdfStreamDecoderSupportStatus.Deferred, support.Status));
    }

    [Fact]
    public void StreamDecoderRegistry_ShouldThrowForDeferredRoadmapFilters()
    {
        var registry = new PdfStreamDecoderRegistry();
        var stream = new PdfStreamObject(new PdfDictionary
        {
            ["Filter"] = new PdfName("JPXDecode")
        }, new byte[] { 1, 2, 3 });

        var exception = Assert.Throws<NotSupportedException>(() => registry.Decode(stream));

        Assert.Contains("JPXDecode", exception.Message, StringComparison.Ordinal);
        Assert.Contains("deferred", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ObjectResolver_ShouldResolveReferencesFromGraph()
    {
        var graph = new PdfObjectGraph();
        var name = new PdfName("Resolved");
        var id = new PdfObjectId(8, 0);
        graph.Add(new PdfIndirectObject(id, name, new PdfSourceSpan(0, 1)));

        var resolver = new PdfObjectResolver(graph);

        Assert.True(resolver.TryResolve<PdfName>(new PdfReference(id), out var resolved));
        Assert.Equal("Resolved", resolved.Value);
    }

    [Fact]
    public void GraphicsInterpreter_ShouldApplyRgbColorsToPathElements()
    {
        var interpreter = new PdfGraphicsInterpreter();
        var commands = new PdfContentCommand[]
        {
            Command("rg", 1, new PdfNumber(0.25), new PdfNumber(0.5), new PdfNumber(0.75)),
            Command("RG", 2, new PdfNumber(0.1), new PdfNumber(0.2), new PdfNumber(0.3)),
            Command("re", 3, new PdfInteger(0), new PdfInteger(0), new PdfInteger(10), new PdfInteger(20)),
            Command("B", 4)
        };

        var path = Assert.IsType<PdfPathElement>(Assert.Single(interpreter.Interpret(commands)));

        Assert.Equal(new PdfColor(0.25, 0.5, 0.75, 1, PdfColorSpace.DeviceRgb), path.FillColor);
        Assert.Equal(new PdfColor(0.1, 0.2, 0.3, 1, PdfColorSpace.DeviceRgb), path.StrokeColor);
    }

    [Fact]
    public void GraphicsInterpreter_ShouldApplyGrayAndCmykColorsToTextElements()
    {
        var interpreter = new PdfGraphicsInterpreter();
        var commands = new PdfContentCommand[]
        {
            Command("g", 1, new PdfNumber(0.6)),
            Command("K", 2, new PdfNumber(0.1), new PdfNumber(0.2), new PdfNumber(0.3), new PdfNumber(0.4)),
            Command("Tm", 3, new PdfInteger(1), new PdfInteger(0), new PdfInteger(0), new PdfInteger(1), new PdfInteger(0), new PdfInteger(0)),
            Command("Tj", 4, new PdfString(Encoding.ASCII.GetBytes("Hello"), IsHex: false))
        };

        var text = Assert.IsType<PdfTextElement>(Assert.Single(interpreter.Interpret(commands)));

        Assert.Equal(new PdfColor(0.6, 0, 0, 1, PdfColorSpace.DeviceGray), text.FillColor);
        Assert.Equal(new PdfColor(0.1, 0.2, 0.3, 0.4, PdfColorSpace.DeviceCmyk), text.StrokeColor);
    }

    [Fact]
    public void GraphicsInterpreter_ShouldApplyGenericScAndScOperatorsForNumericColors()
    {
        var interpreter = new PdfGraphicsInterpreter();
        var commands = new PdfContentCommand[]
        {
            Command("sc", 1, new PdfNumber(0.2), new PdfNumber(0.4), new PdfNumber(0.6)),
            Command("SC", 2, new PdfNumber(0.7)),
            Command("Tm", 3, new PdfInteger(1), new PdfInteger(0), new PdfInteger(0), new PdfInteger(1), new PdfInteger(0), new PdfInteger(0)),
            Command("Tj", 4, new PdfString(Encoding.ASCII.GetBytes("Color"), IsHex: false))
        };

        var text = Assert.IsType<PdfTextElement>(Assert.Single(interpreter.Interpret(commands)));

        Assert.Equal(new PdfColor(0.2, 0.4, 0.6, 1, PdfColorSpace.DeviceRgb), text.FillColor);
        Assert.Equal(new PdfColor(0.7, 0, 0, 1, PdfColorSpace.DeviceGray), text.StrokeColor);
    }

    [Fact]
    public void GraphicsInterpreter_ShouldApplyColorSpacesToScnAndScnOperators()
    {
        var interpreter = new PdfGraphicsInterpreter();
        var commands = new PdfContentCommand[]
        {
            Command("cs", 1, new PdfName("DeviceCMYK")),
            Command("CS", 2, new PdfName("DeviceRGB")),
            Command("scn", 3, new PdfNumber(0.1), new PdfNumber(0.2), new PdfNumber(0.3), new PdfNumber(0.4)),
            Command("SCN", 4, new PdfNumber(0.8), new PdfNumber(0.6), new PdfNumber(0.4)),
            Command("Tm", 5, new PdfInteger(1), new PdfInteger(0), new PdfInteger(0), new PdfInteger(1), new PdfInteger(0), new PdfInteger(0)),
            Command("Tj", 6, new PdfString(Encoding.ASCII.GetBytes("Spaces"), IsHex: false))
        };

        var text = Assert.IsType<PdfTextElement>(Assert.Single(interpreter.Interpret(commands)));

        Assert.Equal(new PdfColor(0.1, 0.2, 0.3, 0.4, PdfColorSpace.DeviceCmyk), text.FillColor);
        Assert.Equal(new PdfColor(0.8, 0.6, 0.4, 1, PdfColorSpace.DeviceRgb), text.StrokeColor);
    }

    [Fact]
    public void GraphicsInterpreter_ShouldPreserveMarkedContentGroups()
    {
        var interpreter = new PdfGraphicsInterpreter();
        var commands = new PdfContentCommand[]
        {
            Command("BMC", 1, new PdfName("Span")),
            Command("Tm", 2, new PdfInteger(1), new PdfInteger(0), new PdfInteger(0), new PdfInteger(1), new PdfInteger(0), new PdfInteger(0)),
            Command("Tj", 3, new PdfString(Encoding.ASCII.GetBytes("Grouped"), IsHex: false)),
            Command("EMC", 4),
            Command("Tj", 5, new PdfString(Encoding.ASCII.GetBytes("Outside"), IsHex: false))
        };

        var elements = interpreter.Interpret(commands);

        Assert.Equal(2, elements.Count);

        var group = Assert.IsType<PdfGroupElement>(elements[0]);
        Assert.Equal("Span", group.MarkedContentTag);
        var groupedText = Assert.IsType<PdfTextElement>(Assert.Single(group.Children));
        Assert.Equal("Grouped", groupedText.Text);

        var outsideText = Assert.IsType<PdfTextElement>(elements[1]);
        Assert.Equal("Outside", outsideText.Text);
    }

    [Fact]
    public void GraphicsInterpreter_ShouldPreserveMarkedContentPropertiesForBdc()
    {
        var interpreter = new PdfGraphicsInterpreter();
        var properties = new PdfDictionary
        {
            ["MCID"] = new PdfInteger(7)
        };

        var commands = new PdfContentCommand[]
        {
            Command("BDC", 1, new PdfName("P"), properties),
            Command("Tm", 2, new PdfInteger(1), new PdfInteger(0), new PdfInteger(0), new PdfInteger(1), new PdfInteger(0), new PdfInteger(0)),
            Command("Tj", 3, new PdfString(Encoding.ASCII.GetBytes("Tagged"), IsHex: false)),
            Command("EMC", 4)
        };

        var group = Assert.IsType<PdfGroupElement>(Assert.Single(interpreter.Interpret(commands)));

        Assert.Equal("P", group.MarkedContentTag);
        Assert.Same(properties, group.Properties);
        Assert.IsType<PdfTextElement>(Assert.Single(group.Children));
    }

    [Fact]
    public void GraphicsInterpreter_ShouldPreserveMarkedContentMarkersForMpAndDp()
    {
        var interpreter = new PdfGraphicsInterpreter();
        var properties = new PdfDictionary
        {
            ["Lang"] = new PdfName("en-US")
        };

        var commands = new PdfContentCommand[]
        {
            Command("MP", 1, new PdfName("Artifact")),
            Command("DP", 2, new PdfName("Span"), properties)
        };

        var elements = interpreter.Interpret(commands);

        Assert.Equal(2, elements.Count);

        var marker = Assert.IsType<PdfGroupElement>(elements[0]);
        Assert.Equal("Artifact", marker.MarkedContentTag);
        Assert.Empty(marker.Children);
        Assert.Null(marker.Properties);

        var propertyMarker = Assert.IsType<PdfGroupElement>(elements[1]);
        Assert.Equal("Span", propertyMarker.MarkedContentTag);
        Assert.Same(properties, propertyMarker.Properties);
        Assert.Empty(propertyMarker.Children);
    }

    [Fact]
    public void ContentParser_AndGraphicsInterpreter_ShouldEmitInlineImageElements()
    {
        var parser = new PdfContentStreamParser();
        var interpreter = new PdfGraphicsInterpreter();
        var bytes = new byte[]
        {
            (byte)'q', (byte)' ',
            (byte)'1', (byte)' ', (byte)'0', (byte)' ', (byte)'0', (byte)' ', (byte)'1', (byte)' ', (byte)'1', (byte)'0', (byte)' ', (byte)'2', (byte)'0', (byte)' ', (byte)'c', (byte)'m', (byte)' ',
            (byte)'B', (byte)'I', (byte)' ',
            (byte)'/', (byte)'W', (byte)' ', (byte)'1', (byte)' ',
            (byte)'/', (byte)'H', (byte)' ', (byte)'1', (byte)' ',
            (byte)'/', (byte)'B', (byte)'P', (byte)'C', (byte)' ', (byte)'8', (byte)' ',
            (byte)'/', (byte)'C', (byte)'S', (byte)' ', (byte)'/', (byte)'D', (byte)'e', (byte)'v', (byte)'i', (byte)'c', (byte)'e', (byte)'G', (byte)'r', (byte)'a', (byte)'y', (byte)' ',
            (byte)'I', (byte)'D', (byte)' ',
            0x7f,
            (byte)' ', (byte)'E', (byte)'I', (byte)' ',
            (byte)'Q'
        };

        var commands = parser.Parse(bytes);

        var inlineImageCommand = Assert.Single(commands, command => command.Operator.Name == "BI");
        var inlineStream = Assert.IsType<PdfStreamObject>(Assert.Single(inlineImageCommand.Operands));
        Assert.Equal(1, Assert.IsType<PdfInteger>(inlineStream.Dictionary["W"]).Value);
        Assert.Equal(1, Assert.IsType<PdfInteger>(inlineStream.Dictionary["H"]).Value);
        Assert.Equal(new byte[] { 0x7f }, inlineStream.EncodedBytes.ToArray());

        var image = Assert.IsType<PdfImageElement>(Assert.Single(interpreter.Interpret(commands)));
        Assert.Equal(new byte[] { 0x7f }, image.ImageBytes.ToArray());
        Assert.Equal(PdfMatrix.Identity.Multiply(new PdfMatrix(1, 0, 0, 1, 10, 20)), image.Transform);
    }

    [Fact]
    public void GraphicsInterpreter_ShouldApplyClippingPathToSubsequentElements()
    {
        var interpreter = new PdfGraphicsInterpreter();
        var commands = new PdfContentCommand[]
        {
            Command("re", 1, new PdfInteger(10), new PdfInteger(20), new PdfInteger(30), new PdfInteger(40)),
            Command("W*", 2),
            Command("n", 3),
            Command("Tm", 4, new PdfInteger(1), new PdfInteger(0), new PdfInteger(0), new PdfInteger(1), new PdfInteger(5), new PdfInteger(6)),
            Command("Tj", 5, new PdfString(Encoding.ASCII.GetBytes("Clip"), IsHex: false)),
            Command("re", 6, new PdfInteger(0), new PdfInteger(0), new PdfInteger(5), new PdfInteger(5)),
            Command("S", 7)
        };

        var elements = interpreter.Interpret(commands);

        var text = Assert.IsType<PdfTextElement>(elements[0]);
        var textClip = Assert.IsType<PdfClippingPath>(text.ClippingPath);
        Assert.True(textClip.UsesEvenOddRule);
        var textRect = Assert.IsType<RectangleSegment>(Assert.Single(textClip.Segments));
        Assert.Equal(new PdfRectangle(10, 20, 30, 40), textRect.Rectangle);

        var path = Assert.IsType<PdfPathElement>(elements[1]);
        var pathClip = Assert.IsType<PdfClippingPath>(path.ClippingPath);
        Assert.True(pathClip.UsesEvenOddRule);
        Assert.IsType<RectangleSegment>(Assert.Single(pathClip.Segments));
    }

    [Fact]
    public void GraphicsInterpreter_ShouldApplyTextPositioningOperatorsToTextTransforms()
    {
        var interpreter = new PdfGraphicsInterpreter();
        var commands = new PdfContentCommand[]
        {
            Command("BT", 1),
            Command("Tm", 2, new PdfInteger(1), new PdfInteger(0), new PdfInteger(0), new PdfInteger(1), new PdfInteger(10), new PdfInteger(20)),
            Command("Td", 3, new PdfInteger(5), new PdfInteger(-7)),
            Command("Tj", 4, new PdfString(Encoding.ASCII.GetBytes("First"), IsHex: false)),
            Command("TD", 5, new PdfInteger(2), new PdfInteger(-11)),
            Command("Tj", 6, new PdfString(Encoding.ASCII.GetBytes("Second"), IsHex: false)),
            Command("T*", 7),
            Command("Tj", 8, new PdfString(Encoding.ASCII.GetBytes("Third"), IsHex: false))
        };

        var elements = interpreter.Interpret(commands).Cast<PdfTextElement>().ToArray();

        Assert.Equal(new PdfMatrix(1, 0, 0, 1, 15, 13), elements[0].Transform);
        Assert.Equal(new PdfMatrix(1, 0, 0, 1, 17, 2), elements[1].Transform);
        Assert.Equal(new PdfMatrix(1, 0, 0, 1, 17, -9), elements[2].Transform);
    }

    [Fact]
    public void GraphicsInterpreter_ShouldMoveToNextLineForQuoteOperators()
    {
        var interpreter = new PdfGraphicsInterpreter();
        var commands = new PdfContentCommand[]
        {
            Command("BT", 1),
            Command("TL", 2, new PdfInteger(14)),
            Command("Tm", 3, new PdfInteger(1), new PdfInteger(0), new PdfInteger(0), new PdfInteger(1), new PdfInteger(100), new PdfInteger(200)),
            Command("'", 4, new PdfString(Encoding.ASCII.GetBytes("Next"), IsHex: false)),
            Command("\"", 5, new PdfInteger(8), new PdfInteger(3), new PdfString(Encoding.ASCII.GetBytes("Again"), IsHex: false))
        };

        var elements = interpreter.Interpret(commands).Cast<PdfTextElement>().ToArray();

        Assert.Equal(new PdfMatrix(1, 0, 0, 1, 100, 186), elements[0].Transform);
        Assert.Equal(new PdfMatrix(1, 0, 0, 1, 100, 172), elements[1].Transform);
    }

    [Fact]
    public void ToUnicodeCMapParser_ShouldParseBfCharAndBfRangeMappings()
    {
        var parser = new PdfToUnicodeCMapParser();
        var cmap = Encoding.ASCII.GetBytes("""
        /CIDInit /ProcSet findresource begin
        12 dict begin
        begincmap
        1 begincodespacerange
        <00> <FF>
        endcodespacerange
        2 beginbfchar
        <01> <0041>
        <02> <0042>
        endbfchar
        2 beginbfrange
        <03> <04> <0043>
        <05> <06> [<0045> <0046>]
        endbfrange
        endcmap
        CMapName currentdict /CMap defineresource pop
        end
        end
        """);

        var map = parser.Parse(cmap);

        Assert.Equal("ABCDEF", map.Decode(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 }));
    }

    [Fact]
    public void ToUnicodeMap_ShouldDecodeMultiByteCodes()
    {
        var map = new PdfToUnicodeMap();
        map.Add(0x0001, 2, "A");
        map.Add(0x0002, 2, "B");

        Assert.Equal("AB", map.Decode(new byte[] { 0x00, 0x01, 0x00, 0x02 }));
    }

    [Fact]
    public void DocumentBuilder_ShouldResolveFontResources_AndAdvanceTextForTjAndTj()
    {
        var graph = new PdfObjectGraph();

        var fontDescriptor = new PdfDictionary
        {
            ["MissingWidth"] = new PdfInteger(400)
        };

        var toUnicodeDictionary = new PdfDictionary();
        var toUnicodeBytes = Encoding.ASCII.GetBytes("""
        1 beginbfchar
        <41> <03A9>
        endbfchar
        """);
        var toUnicodeStream = new PdfStreamObject(toUnicodeDictionary, toUnicodeBytes);

        var fontDictionary = new PdfDictionary
        {
            ["Type"] = new PdfName("Font"),
            ["Subtype"] = new PdfName("Type1"),
            ["FirstChar"] = new PdfInteger(65),
            ["Widths"] = new PdfArray([new PdfInteger(600), new PdfInteger(500)]),
            ["FontDescriptor"] = fontDescriptor,
            ["ToUnicode"] = toUnicodeStream
        };

        var resources = new PdfDictionary
        {
            ["Font"] = new PdfDictionary
            {
                ["F1"] = fontDictionary
            }
        };

        var contentBytes = Encoding.ASCII.GetBytes("BT /F1 12 Tf 1 0 0 1 0 0 Tm (A) Tj (B) Tj ET");
        var contentStream = new PdfStreamObject(new PdfDictionary(), contentBytes);

        var catalog = new PdfDictionary
        {
            ["Type"] = new PdfName("Catalog"),
            ["Pages"] = new PdfReference(new PdfObjectId(2, 0))
        };

        var pages = new PdfDictionary
        {
            ["Type"] = new PdfName("Pages"),
            ["Kids"] = new PdfArray([new PdfReference(new PdfObjectId(3, 0))])
        };

        var page = new PdfDictionary
        {
            ["Type"] = new PdfName("Page"),
            ["Resources"] = resources,
            ["Contents"] = contentStream
        };

        graph.Add(new PdfIndirectObject(new PdfObjectId(1, 0), catalog, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(2, 0), pages, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(3, 0), page, new PdfSourceSpan(0, 1)));

        var document = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter()).Build(graph);
        var builtPage = Assert.Single(document.Pages);
        var texts = builtPage.TextObjects.ToArray();

        Assert.Equal("F1", texts[0].FontResourceName);
        Assert.Equal("Ω", texts[0].Text);
        Assert.Equal(new PdfMatrix(1, 0, 0, 1, 0, 0), texts[0].Transform);
        Assert.Equal(7.2, texts[1].Transform.E, 10);
        Assert.Equal(0, texts[1].Transform.F, 10);
    }

    [Fact]
    public void GraphicsInterpreter_ShouldAdvanceTextForTjAndTjArray()
    {
        var interpreter = new PdfGraphicsInterpreter();
        var font = new PdfFontResource
        {
            ResourceName = "F1",
            Kind = PdfFontKind.Type1,
            Widths = new Dictionary<int, double>
            {
                [(byte)'A'] = 600,
                [(byte)'B'] = 500,
                [(byte)'C'] = 400
            }
        };

        var commands = new PdfContentCommand[]
        {
            Command("BT", 1),
            Command("Tf", 2, new PdfName("F1"), new PdfInteger(10)),
            Command("Tm", 3, new PdfInteger(1), new PdfInteger(0), new PdfInteger(0), new PdfInteger(1), new PdfInteger(0), new PdfInteger(0)),
            Command("Tj", 4, new PdfString(Encoding.ASCII.GetBytes("AB"), IsHex: false)),
            Command("TJ", 5, new PdfArray([new PdfString(Encoding.ASCII.GetBytes("C"), IsHex: false), new PdfInteger(100), new PdfString(Encoding.ASCII.GetBytes("A"), IsHex: false)]))
        };

        var elements = interpreter.Interpret(commands, new Dictionary<string, PdfFontResource> { ["F1"] = font }).Cast<PdfTextElement>().ToArray();

        Assert.Equal(new PdfMatrix(1, 0, 0, 1, 0, 0), elements[0].Transform);
        Assert.Equal(new PdfMatrix(1, 0, 0, 1, 11, 0), elements[1].Transform);
    }

    [Fact]
    public void GraphicsInterpreter_ShouldEmitShadingElements()
    {
        var interpreter = new PdfGraphicsInterpreter();
        var commands = new PdfContentCommand[]
        {
            Command("re", 1, new PdfInteger(0), new PdfInteger(0), new PdfInteger(50), new PdfInteger(60)),
            Command("W", 2),
            Command("n", 3),
            Command("sh", 4, new PdfName("Sh1"))
        };

        var shading = Assert.IsType<PdfShadingElement>(Assert.Single(interpreter.Interpret(commands)));

        Assert.Equal("Sh1", shading.ResourceName);
        var clip = Assert.IsType<PdfClippingPath>(shading.ClippingPath);
        Assert.False(clip.UsesEvenOddRule);
        Assert.IsType<RectangleSegment>(Assert.Single(clip.Segments));
    }

    [Fact]
    public void GraphicsInterpreter_ShouldPreserveCompatibilitySections()
    {
        var interpreter = new PdfGraphicsInterpreter();
        var commands = new PdfContentCommand[]
        {
            Command("BX", 1),
            Command("Tm", 2, new PdfInteger(1), new PdfInteger(0), new PdfInteger(0), new PdfInteger(1), new PdfInteger(10), new PdfInteger(20)),
            Command("Tj", 3, new PdfString(Encoding.ASCII.GetBytes("Compat"), IsHex: false)),
            Command("EX", 4),
            Command("Tj", 5, new PdfString(Encoding.ASCII.GetBytes("Outside"), IsHex: false))
        };

        var elements = interpreter.Interpret(commands);

        var compatibilityGroup = Assert.IsType<PdfGroupElement>(elements[0]);
        Assert.True(compatibilityGroup.IsCompatibilitySection);
        var innerText = Assert.IsType<PdfTextElement>(Assert.Single(compatibilityGroup.Children));
        Assert.Equal("Compat", innerText.Text);

        var outsideText = Assert.IsType<PdfTextElement>(elements[1]);
        Assert.Equal("Outside", outsideText.Text);
    }

    [Fact]
    public void DocumentBuilder_ShouldParseType0Fonts_AndAdvanceCompositeText()
    {
        var graph = new PdfObjectGraph();

        var descendantFont = new PdfDictionary
        {
            ["Type"] = new PdfName("Font"),
            ["Subtype"] = new PdfName("CIDFontType2"),
            ["DW"] = new PdfInteger(1000),
            ["W"] = new PdfArray([
                new PdfInteger(1),
                new PdfArray([new PdfInteger(500), new PdfInteger(600)])
            ])
        };

        var toUnicodeStream = new PdfStreamObject(new PdfDictionary(), Encoding.ASCII.GetBytes("""
        1 begincodespacerange
        <0000> <FFFF>
        endcodespacerange
        2 beginbfchar
        <0001> <0041>
        <0002> <0042>
        endbfchar
        """));

        var fontDictionary = new PdfDictionary
        {
            ["Type"] = new PdfName("Font"),
            ["Subtype"] = new PdfName("Type0"),
            ["DescendantFonts"] = new PdfArray([descendantFont]),
            ["ToUnicode"] = toUnicodeStream
        };

        var resources = new PdfDictionary
        {
            ["Font"] = new PdfDictionary
            {
                ["F1"] = fontDictionary
            }
        };

        var contentBytes = new byte[]
        {
            (byte)'B',(byte)'T',(byte)' ',
            (byte)'/',(byte)'F',(byte)'1',(byte)' ',(byte)'1',(byte)'0',(byte)' ',(byte)'T',(byte)'f',(byte)' ',
            (byte)'1',(byte)' ',(byte)'0',(byte)' ',(byte)'0',(byte)' ',(byte)'1',(byte)' ',(byte)'0',(byte)' ',(byte)'0',(byte)' ',(byte)'T',(byte)'m',(byte)' ',
            (byte)'<',(byte)'0',(byte)'0',(byte)'0',(byte)'1',(byte)'>',(byte)' ',(byte)'T',(byte)'j',(byte)' ',
            (byte)'<',(byte)'0',(byte)'0',(byte)'0',(byte)'2',(byte)'>',(byte)' ',(byte)'T',(byte)'j',(byte)' ',
            (byte)'E',(byte)'T'
        };

        var page = new PdfDictionary
        {
            ["Type"] = new PdfName("Page"),
            ["Resources"] = resources,
            ["Contents"] = new PdfStreamObject(new PdfDictionary(), contentBytes)
        };

        var pages = new PdfDictionary
        {
            ["Type"] = new PdfName("Pages"),
            ["Kids"] = new PdfArray([new PdfReference(new PdfObjectId(2, 0))])
        };

        var catalog = new PdfDictionary
        {
            ["Type"] = new PdfName("Catalog"),
            ["Pages"] = new PdfReference(new PdfObjectId(1, 0))
        };

        graph.Add(new PdfIndirectObject(new PdfObjectId(1, 0), pages, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(2, 0), page, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(3, 0), catalog, new PdfSourceSpan(0, 1)));

        var document = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter()).Build(graph);
        var texts = Assert.Single(document.Pages).TextObjects.ToArray();

        Assert.Equal("A", texts[0].Text);
        Assert.Equal("B", texts[1].Text);
        Assert.Equal(5, texts[1].Transform.E, 10);
    }

    [Fact]
    public void EditingSession_ShouldInsertGraphicsElementsIntoPage()
    {
        var document = new PdfDocumentModel();
        var page = new PdfPageModel(null, new PdfDictionary());
        document.AddPage(page);

        var session = new PdfEditingSession(document);
        var element = new PdfTextElement(1, PdfMatrix.Identity, Command("Tj", 1, new PdfString(Encoding.ASCII.GetBytes("New"), IsHex: false)), "New");

        session.Insert(page, element);

        Assert.Same(element, Assert.Single(page.GraphicsObjects));
    }

    [Fact]
    public void EditingSession_ShouldReplaceMoveDeleteAndUpdateMetadata()
    {
        var document = new PdfDocumentModel();
        var page = new PdfPageModel(null, new PdfDictionary());
        document.AddPage(page);

        var session = new PdfEditingSession(document);
        var element = new PdfTextElement(1, PdfMatrix.Identity, Command("Tj", 1, new PdfString(Encoding.ASCII.GetBytes("Old"), IsHex: false)), "Old");
        session.Insert(page, element);

        session.ReplaceText(element, "New");
        session.Move(element, new PdfMatrix(1, 0, 0, 1, 12, 34));
        session.SetMetadata("Author", new PdfString(Encoding.ASCII.GetBytes("Canvas"), IsHex: false));
        session.Delete(element);

        Assert.Equal("New", element.Text);
        Assert.Equal(new PdfMatrix(1, 0, 0, 1, 12, 34), element.Transform);
        Assert.True(element.IsDeleted);
        Assert.Equal("Canvas", Assert.IsType<PdfString>(document.Metadata["Author"]).ToLatin1String());
    }

    [Fact]
    public void ContentStreamRewriter_ShouldRoundTripEditedSceneGraph()
    {
        var font = new PdfFontResource
        {
            ResourceName = "F1",
            Kind = PdfFontKind.Type1,
            Widths = new Dictionary<int, double>
            {
                [(byte)'H'] = 600,
                [(byte)'i'] = 300
            }
        };

        var page = new PdfPageModel(null, new PdfDictionary())
        {
            FontResources = new Dictionary<string, PdfFontResource>
            {
                ["F1"] = font
            }
        };

        page.Insert(new PdfTextElement(2, new PdfMatrix(1, 0, 0, 1, 15, 25), Command("Tj", 2, new PdfString(Encoding.ASCII.GetBytes("Hi"), IsHex: false)), "Hi")
        {
            FontResourceName = "F1",
            FontSize = 12,
            FillColor = new PdfColor(0.2, 0.4, 0.6, 1, PdfColorSpace.DeviceRgb),
            StrokeColor = new PdfColor(0, 0, 0, 1, PdfColorSpace.DeviceGray)
        });

        page.Insert(new PdfPathElement(1, new PdfMatrix(1, 0, 0, 1, 5, 10), Command("S", 1),
        [
            new RectangleSegment(new PdfRectangle(0, 0, 40, 20))
        ])
        {
            StrokeColor = new PdfColor(0.1, 0.2, 0.3, 1, PdfColorSpace.DeviceRgb),
            FillColor = new PdfColor(0, 0, 0, 1, PdfColorSpace.DeviceGray),
            LineWidth = 2
        });

        var compatibilityGroup = new PdfGroupElement(3, PdfMatrix.Identity, Command("BX", 3))
        {
            IsCompatibilitySection = true
        };
        compatibilityGroup.Children.Add(new PdfTextElement(4, new PdfMatrix(1, 0, 0, 1, 30, 40), Command("Tj", 4, new PdfString(Encoding.ASCII.GetBytes("Compat"), IsHex: false)), "Compat")
        {
            FontResourceName = "F1",
            FontSize = 9,
            FillColor = new PdfColor(0, 0, 0, 1, PdfColorSpace.DeviceGray),
            StrokeColor = new PdfColor(0, 0, 0, 1, PdfColorSpace.DeviceGray)
        });
        page.Insert(compatibilityGroup);

        var rewriter = new PdfContentStreamRewriter();
        var bytes = rewriter.Rewrite(page);

        var parser = new PdfContentStreamParser();
        var commands = parser.Parse(bytes);
        var elements = new PdfGraphicsInterpreter().Interpret(commands, page.FontResources);

        Assert.Equal(3, elements.Count);

        var path = Assert.IsType<PdfPathElement>(elements[0]);
        Assert.Equal(new PdfMatrix(1, 0, 0, 1, 5, 10), path.Transform);
        Assert.IsType<RectangleSegment>(Assert.Single(path.Segments));

        var text = Assert.IsType<PdfTextElement>(elements[1]);
        Assert.Equal("Hi", text.Text);
        Assert.Equal("F1", text.FontResourceName);
        Assert.Equal(new PdfMatrix(1, 0, 0, 1, 15, 25), text.Transform);

        var group = Assert.IsType<PdfGroupElement>(elements[2]);
        Assert.True(group.IsCompatibilitySection);
        var groupedText = Assert.IsType<PdfTextElement>(Assert.Single(group.Children));
        Assert.Equal("Compat", groupedText.Text);
    }

    [Fact]
    public async Task CanvasPdfGeneratorBridge_ShouldRenderAndReimportSimpleDocument()
    {
        var document = new PdfDocumentModel();
        document.Metadata["Author"] = new PdfString(Encoding.ASCII.GetBytes("Canvas"), IsHex: false);

        var page = new PdfPageModel(null, new PdfDictionary())
        {
            MediaBox = new PdfRectangle(0, 0, 200, 120),
            CropBox = new PdfRectangle(0, 0, 180, 100)
        };

        page.Insert(new PdfPathElement(1, new PdfMatrix(1, 0, 0, 1, 5, 10), Command("S", 1),
        [
            new RectangleSegment(new PdfRectangle(0, 0, 40, 20))
        ])
        {
            StrokeColor = new PdfColor(0.1, 0.2, 0.3, 1, PdfColorSpace.DeviceRgb),
            FillColor = new PdfColor(0, 0, 0, 1, PdfColorSpace.DeviceGray),
            LineWidth = 2
        });

        page.Insert(new PdfTextElement(2, new PdfMatrix(1, 0, 0, 1, 25, 60), Command("Tj", 2, new PdfString(Encoding.ASCII.GetBytes("Hello bridge"), IsHex: false)), "Hello bridge")
        {
            FontSize = 12,
            FillColor = new PdfColor(0, 0, 0, 1, PdfColorSpace.DeviceGray),
            StrokeColor = new PdfColor(0, 0, 0, 1, PdfColorSpace.DeviceGray)
        });

        document.AddPage(page);

        var bridge = new CanvasPdfGeneratorBridge();
        await using var output = new MemoryStream();

        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedPage = Assert.Single(reimported.Pages);
        Assert.Equal(200, importedPage.MediaBox?.Width);
        Assert.Equal(120, importedPage.MediaBox?.Height);
        Assert.Equal(180, importedPage.CropBox?.Width);
        Assert.Equal(100, importedPage.CropBox?.Height);
        Assert.Equal("Hello bridge", Assert.Single(importedPage.TextObjects).Text);
        Assert.Contains(importedPage.GraphicsObjects, static element => element is PdfPathElement);
    }

    [Fact]
    public async Task CanvasPdfGeneratorBridge_ShouldRenderAndReimportFillOnlyRectanglePath()
    {
        var document = new PdfDocumentModel();
        var page = new PdfPageModel(null, new PdfDictionary())
        {
            MediaBox = new PdfRectangle(0, 0, 200, 120)
        };

        page.Insert(new PdfPathElement(1, new PdfMatrix(1, 0, 0, 1, 12, 18), Command("f", 1),
        [
            new RectangleSegment(new PdfRectangle(0, 0, 40, 20))
        ])
        {
            FillColor = new PdfColor(0.2, 0.4, 0.6, 1, PdfColorSpace.DeviceRgb),
            StrokeColor = new PdfColor(0, 0, 0, 1, PdfColorSpace.DeviceGray),
            LineWidth = 2
        });

        document.AddPage(page);

        var bridge = new CanvasPdfGeneratorBridge();
        await using var output = new MemoryStream();

        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedPath = Assert.IsType<PdfPathElement>(Assert.Single(Assert.Single(reimported.Pages).GraphicsObjects));
        var rectangle = Assert.IsType<RectangleSegment>(Assert.Single(importedPath.Segments));

        Assert.Equal(new PdfRectangle(12, 18, 40, 20), rectangle.Rectangle);
        Assert.Equal(PdfColorSpace.DeviceRgb, importedPath.FillColor.ColorSpace);
        Assert.Equal(0.2, importedPath.FillColor.C1, 3);
        Assert.Equal(0.4, importedPath.FillColor.C2, 3);
        Assert.Equal(0.6, importedPath.FillColor.C3, 3);
    }

    [Fact]
    public async Task DocumentBuilder_AndCanvasPdfGeneratorBridge_ShouldRoundTripJpegXObjectImages()
    {
        var graph = new PdfObjectGraph();

        var catalog = new PdfDictionary();
        catalog["Type"] = new PdfName("Catalog");
        catalog["Pages"] = new PdfReference(new PdfObjectId(2, 0));

        var pages = new PdfDictionary();
        pages["Type"] = new PdfName("Pages");
        pages["Count"] = new PdfInteger(1);
        pages["Kids"] = new PdfArray([new PdfReference(new PdfObjectId(3, 0))]);

        var xObjectResources = new PdfDictionary();
        xObjectResources["Im1"] = new PdfReference(new PdfObjectId(5, 0));

        var resources = new PdfDictionary();
        resources["XObject"] = xObjectResources;

        var page = new PdfDictionary();
        page["Type"] = new PdfName("Page");
        page["Parent"] = new PdfReference(new PdfObjectId(2, 0));
        page["Resources"] = resources;
        page["MediaBox"] = Array(0, 0, 200, 120);
        page["Contents"] = new PdfReference(new PdfObjectId(4, 0));

        var contentBytes = Encoding.ASCII.GetBytes("q 40 0 0 20 30 40 cm /Im1 Do Q");
        var contentStream = new PdfStreamObject(new PdfDictionary(), contentBytes);

        var imageDictionary = new PdfDictionary();
        imageDictionary["Type"] = new PdfName("XObject");
        imageDictionary["Subtype"] = new PdfName("Image");
        imageDictionary["Width"] = new PdfInteger(1);
        imageDictionary["Height"] = new PdfInteger(1);
        imageDictionary["ColorSpace"] = new PdfName("DeviceRGB");
        imageDictionary["BitsPerComponent"] = new PdfInteger(8);
        imageDictionary["Filter"] = new PdfName("DCTDecode");
        var imageStream = new PdfStreamObject(imageDictionary, TinyJpegBytes());

        graph.Add(new PdfIndirectObject(new PdfObjectId(1, 0), catalog, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(2, 0), pages, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(3, 0), page, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(4, 0), contentStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(5, 0), imageStream, new PdfSourceSpan(0, 1)));

        var builder = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter());
        var document = builder.Build(graph);

        var builtImage = Assert.IsType<PdfImageElement>(Assert.Single(Assert.Single(document.Pages).GraphicsObjects));
        Assert.False(builtImage.ImageBytes.IsEmpty);
        Assert.Equal(TinyJpegBytes(), builtImage.ImageBytes.ToArray());

        var bridge = new CanvasPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedImage = Assert.IsType<PdfImageElement>(Assert.Single(Assert.Single(reimported.Pages).GraphicsObjects));
        Assert.False(importedImage.ImageBytes.IsEmpty);
    }

    [Fact]
    public async Task DocumentBuilder_AndCanvasPdfGeneratorBridge_ShouldRoundTripFlateXObjectImages()
    {
        var graph = new PdfObjectGraph();

        var catalog = new PdfDictionary();
        catalog["Type"] = new PdfName("Catalog");
        catalog["Pages"] = new PdfReference(new PdfObjectId(2, 0));

        var pages = new PdfDictionary();
        pages["Type"] = new PdfName("Pages");
        pages["Count"] = new PdfInteger(1);
        pages["Kids"] = new PdfArray([new PdfReference(new PdfObjectId(3, 0))]);

        var xObjectResources = new PdfDictionary();
        xObjectResources["Im1"] = new PdfReference(new PdfObjectId(5, 0));

        var resources = new PdfDictionary();
        resources["XObject"] = xObjectResources;

        var page = new PdfDictionary();
        page["Type"] = new PdfName("Page");
        page["Parent"] = new PdfReference(new PdfObjectId(2, 0));
        page["Resources"] = resources;
        page["MediaBox"] = Array(0, 0, 200, 120);
        page["Contents"] = new PdfReference(new PdfObjectId(4, 0));

        var contentBytes = Encoding.ASCII.GetBytes("q 40 0 0 20 30 40 cm /Im1 Do Q");
        var contentStream = new PdfStreamObject(new PdfDictionary(), contentBytes);

        var imageDictionary = new PdfDictionary();
        imageDictionary["Type"] = new PdfName("XObject");
        imageDictionary["Subtype"] = new PdfName("Image");
        imageDictionary["Width"] = new PdfInteger(1);
        imageDictionary["Height"] = new PdfInteger(1);
        imageDictionary["ColorSpace"] = new PdfName("DeviceRGB");
        imageDictionary["BitsPerComponent"] = new PdfInteger(8);
        imageDictionary["Filter"] = new PdfName("FlateDecode");
        var encodedImageBytes = Compress([0x12, 0x34, 0x56]);
        var imageStream = new PdfStreamObject(imageDictionary, encodedImageBytes);

        graph.Add(new PdfIndirectObject(new PdfObjectId(1, 0), catalog, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(2, 0), pages, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(3, 0), page, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(4, 0), contentStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(5, 0), imageStream, new PdfSourceSpan(0, 1)));

        var builder = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter());
        var document = builder.Build(graph);

        var builtImage = Assert.IsType<PdfImageElement>(Assert.Single(Assert.Single(document.Pages).GraphicsObjects));
        Assert.Equal(encodedImageBytes, builtImage.ImageBytes.ToArray());

        var bridge = new CanvasPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedImage = Assert.IsType<PdfImageElement>(Assert.Single(Assert.Single(reimported.Pages).GraphicsObjects));
        Assert.Equal(encodedImageBytes, importedImage.ImageBytes.ToArray());
    }

    [Fact]
    public async Task DocumentBuilder_AndCanvasPdfGeneratorBridge_ShouldPreserveImageSoftMasks()
    {
        var graph = new PdfObjectGraph();

        var catalog = new PdfDictionary();
        catalog["Type"] = new PdfName("Catalog");
        catalog["Pages"] = new PdfReference(new PdfObjectId(2, 0));

        var pages = new PdfDictionary();
        pages["Type"] = new PdfName("Pages");
        pages["Count"] = new PdfInteger(1);
        pages["Kids"] = new PdfArray([new PdfReference(new PdfObjectId(3, 0))]);

        var xObjectResources = new PdfDictionary();
        xObjectResources["Im1"] = new PdfReference(new PdfObjectId(5, 0));

        var resources = new PdfDictionary();
        resources["XObject"] = xObjectResources;

        var page = new PdfDictionary();
        page["Type"] = new PdfName("Page");
        page["Parent"] = new PdfReference(new PdfObjectId(2, 0));
        page["Resources"] = resources;
        page["MediaBox"] = Array(0, 0, 200, 120);
        page["Contents"] = new PdfReference(new PdfObjectId(4, 0));

        var contentBytes = Encoding.ASCII.GetBytes("q 40 0 0 20 30 40 cm /Im1 Do Q");
        var contentStream = new PdfStreamObject(new PdfDictionary(), contentBytes);

        var softMaskDictionary = new PdfDictionary();
        softMaskDictionary["Type"] = new PdfName("XObject");
        softMaskDictionary["Subtype"] = new PdfName("Image");
        softMaskDictionary["Width"] = new PdfInteger(1);
        softMaskDictionary["Height"] = new PdfInteger(1);
        softMaskDictionary["ColorSpace"] = new PdfName("DeviceGray");
        softMaskDictionary["BitsPerComponent"] = new PdfInteger(8);
        softMaskDictionary["Filter"] = new PdfName("FlateDecode");
        var softMaskStream = new PdfStreamObject(softMaskDictionary, Compress([0x7f]));

        var imageDictionary = new PdfDictionary();
        imageDictionary["Type"] = new PdfName("XObject");
        imageDictionary["Subtype"] = new PdfName("Image");
        imageDictionary["Width"] = new PdfInteger(1);
        imageDictionary["Height"] = new PdfInteger(1);
        imageDictionary["ColorSpace"] = new PdfName("DeviceRGB");
        imageDictionary["BitsPerComponent"] = new PdfInteger(8);
        imageDictionary["Filter"] = new PdfName("FlateDecode");
        imageDictionary["SMask"] = new PdfReference(new PdfObjectId(6, 0));
        var imageStream = new PdfStreamObject(imageDictionary, Compress([0x12, 0x34, 0x56]));

        graph.Add(new PdfIndirectObject(new PdfObjectId(1, 0), catalog, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(2, 0), pages, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(3, 0), page, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(4, 0), contentStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(5, 0), imageStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(6, 0), softMaskStream, new PdfSourceSpan(0, 1)));

        var builder = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter());
        var document = builder.Build(graph);

        var bridge = new CanvasPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var regeneratedImageObject = Assert.Single(
            reimported.ObjectGraph.Objects.Values
                .Select(static indirect => indirect.Value)
                .OfType<PdfStreamObject>(),
            static stream => stream.Dictionary["Subtype"] is PdfName { Value: "Image" } && stream.Dictionary["SMask"] is not null);

        Assert.IsType<PdfReference>(regeneratedImageObject.Dictionary["SMask"]);
    }

    [Fact]
    public async Task CanvasPdfGeneratorBridge_ShouldPreserveDirectShadingResources()
    {
        var document = new PdfDocumentModel();
        var shadingDictionary = new PdfDictionary();
        shadingDictionary["ShadingType"] = new PdfInteger(2);
        shadingDictionary["ColorSpace"] = new PdfName("DeviceRGB");
        shadingDictionary["Coords"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(100), new PdfInteger(0)]);
        shadingDictionary["Function"] = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["FunctionType"] = new PdfInteger(2),
            ["Domain"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)]),
            ["C0"] = new PdfArray([new PdfInteger(1), new PdfInteger(0), new PdfInteger(0)]),
            ["C1"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(1)]),
            ["N"] = new PdfInteger(1)
        });
        shadingDictionary["Extend"] = new PdfArray([new PdfBoolean(true), new PdfBoolean(true)]);

        var pageResources = new PdfDictionary();
        pageResources["Shading"] = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Sh1"] = shadingDictionary
        });

        var page = new PdfPageModel(null, new PdfDictionary())
        {
            MediaBox = new PdfRectangle(0, 0, 200, 120),
            Resources = pageResources
        };

        page.Insert(new PdfShadingElement(1, PdfMatrix.Identity, Command("sh", 1, new PdfName("Sh1")), "Sh1"));
        document.AddPage(page);

        var bridge = new CanvasPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedPage = Assert.Single(reimported.Pages);
        var importedShading = Assert.IsType<PdfShadingElement>(Assert.Single(importedPage.GraphicsObjects));
        Assert.Equal("Sh1", importedShading.ResourceName);
        var shadingResources = Assert.IsType<PdfDictionary>(importedPage.Resources["Shading"]);
        Assert.IsType<PdfDictionary>(shadingResources["Sh1"]);
    }

    [Fact]
    public async Task CanvasPdfGeneratorBridge_ShouldNotDuplicateNonShadingContentFromMixedGroups()
    {
        var document = new PdfDocumentModel();
        var shadingDictionary = new PdfDictionary();
        shadingDictionary["ShadingType"] = new PdfInteger(2);
        shadingDictionary["ColorSpace"] = new PdfName("DeviceRGB");
        shadingDictionary["Coords"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(100), new PdfInteger(0)]);
        shadingDictionary["Function"] = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["FunctionType"] = new PdfInteger(2),
            ["Domain"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)]),
            ["C0"] = new PdfArray([new PdfInteger(1), new PdfInteger(0), new PdfInteger(0)]),
            ["C1"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(1)]),
            ["N"] = new PdfInteger(1)
        });
        shadingDictionary["Extend"] = new PdfArray([new PdfBoolean(true), new PdfBoolean(true)]);

        var pageResources = new PdfDictionary();
        pageResources["Shading"] = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Sh1"] = shadingDictionary
        });

        var page = new PdfPageModel(null, new PdfDictionary())
        {
            MediaBox = new PdfRectangle(0, 0, 200, 120),
            Resources = pageResources
        };

        var mixedGroup = new PdfGroupElement(1, PdfMatrix.Identity, Command("BMC", 1, new PdfName("Span")))
        {
            Children =
            {
                new PdfPathElement(
                    1,
                    PdfMatrix.Identity,
                    Command("f", 1),
                    new PdfPathSegment[]
                    {
                        new RectangleSegment(new PdfRectangle(10, 10, 40, 20))
                    })
                {
                    FillColor = new PdfColor(1, 0, 0, 1, PdfColorSpace.DeviceRgb)
                },
                new PdfShadingElement(2, PdfMatrix.Identity, Command("sh", 2, new PdfName("Sh1")), "Sh1")
            }
        };

        page.Insert(mixedGroup);
        document.AddPage(page);

        var bridge = new CanvasPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedPage = Assert.Single(reimported.Pages);
        Assert.Single(importedPage.GraphicsObjects.OfType<PdfPathElement>());
        Assert.Single(importedPage.GraphicsObjects.OfType<PdfShadingElement>());
        Assert.Equal(2, importedPage.GraphicsObjects.Count);
    }

    [Fact]
    public async Task CanvasPdfGeneratorBridge_ShouldPreserveIndirectShadingResources()
    {
        var graph = new PdfObjectGraph();

        var functionStreamId = new PdfObjectId(10, 0);
        var shadingId = new PdfObjectId(11, 0);
        var shadingResourcesId = new PdfObjectId(12, 0);

        var functionStream = new PdfStreamObject(new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["FunctionType"] = new PdfInteger(2),
            ["Domain"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)]),
            ["C0"] = new PdfArray([new PdfInteger(1), new PdfInteger(0), new PdfInteger(0)]),
            ["C1"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(1)]),
            ["N"] = new PdfInteger(1)
        }), ReadOnlyMemory<byte>.Empty);

        var shadingDictionary = new PdfDictionary();
        shadingDictionary["ShadingType"] = new PdfInteger(2);
        shadingDictionary["ColorSpace"] = new PdfName("DeviceRGB");
        shadingDictionary["Coords"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(100), new PdfInteger(0)]);
        shadingDictionary["Function"] = new PdfReference(functionStreamId);
        shadingDictionary["Extend"] = new PdfArray([new PdfBoolean(true), new PdfBoolean(true)]);

        var shadingResources = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Sh1"] = new PdfReference(shadingId)
        });

        graph.Add(new PdfIndirectObject(functionStreamId, functionStream, new PdfSourceSpan(0, 0)));
        graph.Add(new PdfIndirectObject(shadingId, shadingDictionary, new PdfSourceSpan(0, 0)));
        graph.Add(new PdfIndirectObject(shadingResourcesId, shadingResources, new PdfSourceSpan(0, 0)));

        var document = new PdfDocumentModel
        {
            ObjectGraph = graph
        };

        var pageResources = new PdfDictionary();
        pageResources["Shading"] = new PdfReference(shadingResourcesId);

        var page = new PdfPageModel(null, new PdfDictionary())
        {
            MediaBox = new PdfRectangle(0, 0, 200, 120),
            Resources = pageResources
        };

        page.Insert(new PdfShadingElement(1, PdfMatrix.Identity, Command("sh", 1, new PdfName("Sh1")), "Sh1"));
        document.AddPage(page);

        var bridge = new CanvasPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedPage = Assert.Single(reimported.Pages);
        var importedShading = Assert.IsType<PdfShadingElement>(Assert.Single(importedPage.GraphicsObjects));
        Assert.Equal("Sh1", importedShading.ResourceName);

        var shadingResourceReference = Assert.IsType<PdfReference>(importedPage.Resources["Shading"]);
        var importedShadingResources = Assert.IsType<PdfDictionary>(reimported.ObjectGraph.Resolve(shadingResourceReference.Id)!.Value);
        var shadingReference = Assert.IsType<PdfReference>(importedShadingResources["Sh1"]);
        var importedShadingDictionary = Assert.IsType<PdfDictionary>(reimported.ObjectGraph.Resolve(shadingReference.Id)!.Value);
        Assert.IsType<PdfReference>(importedShadingDictionary["Function"]);
    }

    [Fact]
    public async Task CanvasPdfGeneratorBridge_ShouldPreserveTextResourcesWhenShadingIsPresent()
    {
        var document = new PdfDocumentModel();
        var shadingDictionary = new PdfDictionary();
        shadingDictionary["ShadingType"] = new PdfInteger(2);
        shadingDictionary["ColorSpace"] = new PdfName("DeviceRGB");
        shadingDictionary["Coords"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(100), new PdfInteger(0)]);
        shadingDictionary["Function"] = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["FunctionType"] = new PdfInteger(2),
            ["Domain"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)]),
            ["C0"] = new PdfArray([new PdfInteger(1), new PdfInteger(0), new PdfInteger(0)]),
            ["C1"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(1)]),
            ["N"] = new PdfInteger(1)
        });
        shadingDictionary["Extend"] = new PdfArray([new PdfBoolean(true), new PdfBoolean(true)]);

        var pageResources = new PdfDictionary();
        pageResources["Shading"] = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Sh1"] = shadingDictionary
        });

        var page = new PdfPageModel(null, new PdfDictionary())
        {
            MediaBox = new PdfRectangle(0, 0, 200, 120),
            Resources = pageResources
        };

        page.Insert(new PdfTextElement(1, new PdfMatrix(1, 0, 0, 1, 24, 48), Command("Tj", 1, new PdfString(Encoding.ASCII.GetBytes("Shade Text"), IsHex: false)), "Shade Text")
        {
            FontResourceName = "F1",
            FontSize = 12,
            FillColor = new PdfColor(0, 0, 0, 1, PdfColorSpace.DeviceGray),
            StrokeColor = new PdfColor(0, 0, 0, 1, PdfColorSpace.DeviceGray)
        });
        page.Insert(new PdfShadingElement(2, PdfMatrix.Identity, Command("sh", 2, new PdfName("Sh1")), "Sh1"));
        document.AddPage(page);

        var bridge = new CanvasPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedPage = Assert.Single(reimported.Pages);
        var importedText = Assert.Single(importedPage.GraphicsObjects.OfType<PdfTextElement>());
        Assert.Equal("Shade Text", importedText.Text);
        Assert.Single(importedPage.GraphicsObjects.OfType<PdfShadingElement>());
    }

    [Fact]
    public async Task CanvasPdfGeneratorBridge_ShouldIgnoreDeletedShadingElements()
    {
        var document = new PdfDocumentModel();
        var page = new PdfPageModel(null, new PdfDictionary())
        {
            MediaBox = new PdfRectangle(0, 0, 200, 120)
        };

        var shading = new PdfShadingElement(1, PdfMatrix.Identity, Command("sh", 1, new PdfName("Sh1")), "Sh1");
        page.Insert(shading);
        page.Delete(shading);
        document.AddPage(page);

        var bridge = new CanvasPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedPage = Assert.Single(reimported.Pages);
        Assert.Empty(importedPage.GraphicsObjects);
        Assert.False(importedPage.Resources.Values.ContainsKey("Shading"));
    }

    private static PdfArray Array(params long[] values)
    {
        return new PdfArray(values.Select(value => new PdfInteger(value)));
    }

    private static byte[] TinyJpegBytes()
    {
        return Convert.FromBase64String("/9j/4AAQSkZJRgABAQAAAQABAAD/2wCEAAkGBxAQEBUQEBAVFRUVFRUVFRUVFRUVFRUVFRUWFhUVFRUYHSggGBolHRUVITEhJSkrLi4uFx8zODMsNygtLisBCgoKDg0OGxAQGy0mICYtLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLf/AABEIAAEAAgMBIgACEQEDEQH/xAAXAAADAQAAAAAAAAAAAAAAAAAAAQID/8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAwDAQACEAMQAAAB6A//xAAXEAEBAQEAAAAAAAAAAAAAAAABEQAh/9oACAEBAAEFAjLxX//EABQRAQAAAAAAAAAAAAAAAAAAABD/2gAIAQMBAT8BP//EABQRAQAAAAAAAAAAAAAAAAAAABD/2gAIAQIBAT8BP//Z");
    }

    private static PdfContentCommand Command(string operatorName, int sequence, params PdfObject[] operands)
    {
        if (!PdfOperatorRegistry.TryGet(operatorName, out var descriptor))
        {
            throw new InvalidOperationException($"Operator '{operatorName}' is not registered.");
        }

        return new PdfContentCommand(descriptor, operands, new PdfSourceSpan(0, 1), sequence);
    }

    private static byte[] Compress(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(bytes);
        }

        return output.ToArray();
    }

    private static byte[] BuildIncrementalXrefPdf()
    {
        var builder = new StringBuilder();
        builder.Append("%PDF-1.7\n");

        var objectOneOffset = builder.Length;
        builder.Append("1 0 obj\n<< /Type /Catalog >>\nendobj\n");

        var firstXrefOffset = builder.Length;
        builder.Append("xref\n");
        builder.Append("0 2\n");
        builder.Append("0000000000 65535 f\n");
        builder.Append($"{objectOneOffset:0000000000} 00000 n\n");
        builder.Append("trailer\n");
        builder.Append("<< /Size 2 >>\n");
        builder.Append("startxref\n");
        builder.Append(firstXrefOffset);
        builder.Append("\n%%EOF\n");

        var objectTwoOffset = builder.Length;
        builder.Append("2 0 obj\n<< /Type /Page >>\nendobj\n");

        var secondXrefOffset = builder.Length;
        builder.Append("xref\n");
        builder.Append("2 1\n");
        builder.Append($"{objectTwoOffset:0000000000} 00000 n\n");
        builder.Append("trailer\n");
        builder.Append($"<< /Size 3 /Prev {firstXrefOffset} >>\n");
        builder.Append("startxref\n");
        builder.Append(secondXrefOffset);
        builder.Append("\n%%EOF\n");

        return Encoding.ASCII.GetBytes(builder.ToString());
    }
}
