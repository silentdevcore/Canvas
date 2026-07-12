using System.IO.Compression;
using System.Globalization;
using System.Text;
using PXA.Core.Contracts;
using PXA.FileImporter;
using PXA.Importer;
using PXA.Importer.Analysis;
using PXA.Importer.Document;
using PXA.Importer.Content;
using PXA.Importer.Debugging;
using PXA.Importer.Graphics;
using PXA.Importer.Editing;
using PXA.Importer.Fonts;
using PXA.Importer.Generation;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Streams;
using PXA.Importer.Xref;

#pragma warning disable PXA0002 // This suite intentionally verifies the legacy Canvas importer engine.

namespace PXA.Importer.Tests;

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
            new PdfName("CCITTFaxDecode"),
            new PdfName("JBIG2Decode"),
            new PdfName("JPXDecode")
        ]);

        var supports = registry.Evaluate(filterArray);

        Assert.Collection(
            supports,
            support => Assert.Equal(PdfStreamDecoderSupportStatus.Supported, support.Status),
            support => Assert.Equal(PdfStreamDecoderSupportStatus.Supported, support.Status),
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

        var bridge = new PxaPdfGeneratorBridge();
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

        var bridge = new PxaPdfGeneratorBridge();
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

        var bridge = new PxaPdfGeneratorBridge();
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

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedImage = Assert.IsType<PdfImageElement>(Assert.Single(Assert.Single(reimported.Pages).GraphicsObjects));
        Assert.Equal(encodedImageBytes, importedImage.ImageBytes.ToArray());
    }

    [Fact]
    public async Task DocumentBuilder_AndCanvasPdfGeneratorBridge_ShouldNormalizeFlippedImageBounds()
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

        var contentBytes = Encoding.ASCII.GetBytes("q 40 0 0 -20 30 60 cm /Im1 Do Q");
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

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedImage = Assert.IsType<PdfImageElement>(Assert.Single(Assert.Single(reimported.Pages).GraphicsObjects));
        Assert.Equal(40, importedImage.Transform.A, 3);
        Assert.Equal(0, importedImage.Transform.B, 3);
        Assert.Equal(0, importedImage.Transform.C, 3);
        Assert.Equal(20, importedImage.Transform.D, 3);
        Assert.Equal(30, importedImage.Transform.E, 3);
        Assert.Equal(40, importedImage.Transform.F, 3);
    }

    [Fact]
    public async Task DocumentBuilder_AndCanvasPdfGeneratorBridge_ShouldRoundTripFlateXObjectImagesWithNamedColorSpace()
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

        var colorSpaceResources = new PdfDictionary();
        colorSpaceResources["CS1"] = new PdfName("DeviceRGB");

        var resources = new PdfDictionary();
        resources["XObject"] = xObjectResources;
        resources["ColorSpace"] = colorSpaceResources;

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
        imageDictionary["ColorSpace"] = new PdfName("CS1");
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

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedImage = Assert.IsType<PdfImageElement>(Assert.Single(Assert.Single(reimported.Pages).GraphicsObjects));
        Assert.Equal(encodedImageBytes, importedImage.ImageBytes.ToArray());
    }

    [Fact]
    public async Task DocumentBuilder_AndCanvasPdfGeneratorBridge_ShouldRoundTripFlateXObjectImagesWithIndirectNamedColorSpace()
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

        var colorSpaceResources = new PdfDictionary();
        colorSpaceResources["CS1"] = new PdfReference(new PdfObjectId(6, 0));

        var resources = new PdfDictionary();
        resources["XObject"] = xObjectResources;
        resources["ColorSpace"] = colorSpaceResources;

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
        imageDictionary["ColorSpace"] = new PdfName("CS1");
        imageDictionary["BitsPerComponent"] = new PdfInteger(8);
        imageDictionary["Filter"] = new PdfName("FlateDecode");
        var encodedImageBytes = Compress([0x12, 0x34, 0x56]);
        var imageStream = new PdfStreamObject(imageDictionary, encodedImageBytes);

        graph.Add(new PdfIndirectObject(new PdfObjectId(1, 0), catalog, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(2, 0), pages, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(3, 0), page, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(4, 0), contentStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(5, 0), imageStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(6, 0), new PdfName("DeviceRGB"), new PdfSourceSpan(0, 1)));

        var builder = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter());
        var document = builder.Build(graph);

        var builtImage = Assert.IsType<PdfImageElement>(Assert.Single(Assert.Single(document.Pages).GraphicsObjects));
        Assert.Equal(encodedImageBytes, builtImage.ImageBytes.ToArray());

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedImage = Assert.IsType<PdfImageElement>(Assert.Single(Assert.Single(reimported.Pages).GraphicsObjects));
        Assert.Equal(encodedImageBytes, importedImage.ImageBytes.ToArray());
    }

    [Fact]
    public async Task DocumentBuilder_AndCanvasPdfGeneratorBridge_ShouldPreserveIndirectDecodeParametersForFlateImages()
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

        var decodeParms = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Predictor"] = new PdfInteger(12),
            ["Colors"] = new PdfInteger(3),
            ["BitsPerComponent"] = new PdfInteger(8),
            ["Columns"] = new PdfInteger(1)
        });

        var imageDictionary = new PdfDictionary();
        imageDictionary["Type"] = new PdfName("XObject");
        imageDictionary["Subtype"] = new PdfName("Image");
        imageDictionary["Width"] = new PdfInteger(1);
        imageDictionary["Height"] = new PdfInteger(1);
        imageDictionary["ColorSpace"] = new PdfName("DeviceRGB");
        imageDictionary["BitsPerComponent"] = new PdfInteger(8);
        imageDictionary["Filter"] = new PdfName("FlateDecode");
        imageDictionary["DecodeParms"] = new PdfReference(new PdfObjectId(6, 0));
        var encodedImageBytes = Compress([0x12, 0x34, 0x56]);
        var imageStream = new PdfStreamObject(imageDictionary, encodedImageBytes);

        graph.Add(new PdfIndirectObject(new PdfObjectId(1, 0), catalog, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(2, 0), pages, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(3, 0), page, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(4, 0), contentStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(5, 0), imageStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(6, 0), decodeParms, new PdfSourceSpan(0, 1)));

        var builder = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter());
        var document = builder.Build(graph);

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var regeneratedImageObject = Assert.Single(
            reimported.ObjectGraph.Objects.Values
                .Select(static indirect => indirect.Value)
                .OfType<PdfStreamObject>(),
            static stream => stream.Dictionary["Subtype"] is PdfName { Value: "Image" });

        var importedDecodeParms = Assert.IsType<PdfDictionary>(regeneratedImageObject.Dictionary["DecodeParms"]);
        Assert.Equal(12, Assert.IsType<PdfInteger>(importedDecodeParms["Predictor"]).Value);
        Assert.Equal(3, Assert.IsType<PdfInteger>(importedDecodeParms["Colors"]).Value);
    }

    [Fact]
    public async Task DocumentBuilder_AndCanvasPdfGeneratorBridge_ShouldPreserveSingleEntryFilterArraysForFlateImages()
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

        var decodeParms = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Predictor"] = new PdfInteger(12),
            ["Colors"] = new PdfInteger(3),
            ["BitsPerComponent"] = new PdfInteger(8),
            ["Columns"] = new PdfInteger(1)
        });

        var imageDictionary = new PdfDictionary();
        imageDictionary["Type"] = new PdfName("XObject");
        imageDictionary["Subtype"] = new PdfName("Image");
        imageDictionary["Width"] = new PdfInteger(1);
        imageDictionary["Height"] = new PdfInteger(1);
        imageDictionary["ColorSpace"] = new PdfName("DeviceRGB");
        imageDictionary["BitsPerComponent"] = new PdfInteger(8);
        imageDictionary["Filter"] = new PdfArray([new PdfName("FlateDecode")]);
        imageDictionary["DecodeParms"] = new PdfArray([decodeParms]);
        var encodedImageBytes = Compress([0x12, 0x34, 0x56]);
        var imageStream = new PdfStreamObject(imageDictionary, encodedImageBytes);

        graph.Add(new PdfIndirectObject(new PdfObjectId(1, 0), catalog, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(2, 0), pages, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(3, 0), page, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(4, 0), contentStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(5, 0), imageStream, new PdfSourceSpan(0, 1)));

        var builder = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter());
        var document = builder.Build(graph);

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var regeneratedImageObject = Assert.Single(
            reimported.ObjectGraph.Objects.Values
                .Select(static indirect => indirect.Value)
                .OfType<PdfStreamObject>(),
            static stream => stream.Dictionary["Subtype"] is PdfName { Value: "Image" });

        Assert.Equal("FlateDecode", Assert.IsType<PdfName>(regeneratedImageObject.Dictionary["Filter"]).Value);
        var importedDecodeParms = Assert.IsType<PdfDictionary>(regeneratedImageObject.Dictionary["DecodeParms"]);
        Assert.Equal(12, Assert.IsType<PdfInteger>(importedDecodeParms["Predictor"]).Value);
        Assert.Equal(3, Assert.IsType<PdfInteger>(importedDecodeParms["Colors"]).Value);
    }

    [Fact]
    public async Task DocumentBuilder_AndCanvasPdfGeneratorBridge_ShouldPreserveIndirectFilterForFlateImages()
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

        var decodeParms = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Predictor"] = new PdfInteger(12),
            ["Colors"] = new PdfInteger(3),
            ["BitsPerComponent"] = new PdfInteger(8),
            ["Columns"] = new PdfInteger(1)
        });

        var imageDictionary = new PdfDictionary();
        imageDictionary["Type"] = new PdfName("XObject");
        imageDictionary["Subtype"] = new PdfName("Image");
        imageDictionary["Width"] = new PdfInteger(1);
        imageDictionary["Height"] = new PdfInteger(1);
        imageDictionary["ColorSpace"] = new PdfName("DeviceRGB");
        imageDictionary["BitsPerComponent"] = new PdfInteger(8);
        imageDictionary["Filter"] = new PdfReference(new PdfObjectId(6, 0));
        imageDictionary["DecodeParms"] = decodeParms;
        var encodedImageBytes = Compress([0x12, 0x34, 0x56]);
        var imageStream = new PdfStreamObject(imageDictionary, encodedImageBytes);

        graph.Add(new PdfIndirectObject(new PdfObjectId(1, 0), catalog, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(2, 0), pages, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(3, 0), page, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(4, 0), contentStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(5, 0), imageStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(6, 0), new PdfName("FlateDecode"), new PdfSourceSpan(0, 1)));

        var builder = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter());
        var document = builder.Build(graph);

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var regeneratedImageObject = Assert.Single(
            reimported.ObjectGraph.Objects.Values
                .Select(static indirect => indirect.Value)
                .OfType<PdfStreamObject>(),
            static stream => stream.Dictionary["Subtype"] is PdfName { Value: "Image" });

        Assert.Equal("FlateDecode", Assert.IsType<PdfName>(regeneratedImageObject.Dictionary["Filter"]).Value);
        var importedDecodeParms = Assert.IsType<PdfDictionary>(regeneratedImageObject.Dictionary["DecodeParms"]);
        Assert.Equal(12, Assert.IsType<PdfInteger>(importedDecodeParms["Predictor"]).Value);
    }

    [Fact]
    public async Task DocumentBuilder_AndCanvasPdfGeneratorBridge_ShouldRoundTripFlateXObjectImagesWithIccBasedGrayColorSpace()
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

        var iccProfileDictionary = new PdfDictionary();
        iccProfileDictionary["N"] = new PdfInteger(1);
        var iccProfileStream = new PdfStreamObject(iccProfileDictionary, ReadOnlyMemory<byte>.Empty);

        var imageDictionary = new PdfDictionary();
        imageDictionary["Type"] = new PdfName("XObject");
        imageDictionary["Subtype"] = new PdfName("Image");
        imageDictionary["Width"] = new PdfInteger(1);
        imageDictionary["Height"] = new PdfInteger(1);
        imageDictionary["ColorSpace"] = new PdfArray([new PdfName("ICCBased"), new PdfReference(new PdfObjectId(6, 0))]);
        imageDictionary["BitsPerComponent"] = new PdfInteger(8);
        imageDictionary["Filter"] = new PdfName("FlateDecode");
        var encodedImageBytes = Compress([0x7f]);
        var imageStream = new PdfStreamObject(imageDictionary, encodedImageBytes);

        graph.Add(new PdfIndirectObject(new PdfObjectId(1, 0), catalog, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(2, 0), pages, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(3, 0), page, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(4, 0), contentStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(5, 0), imageStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(6, 0), iccProfileStream, new PdfSourceSpan(0, 1)));

        var builder = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter());
        var document = builder.Build(graph);

        var builtImage = Assert.IsType<PdfImageElement>(Assert.Single(Assert.Single(document.Pages).GraphicsObjects));
        Assert.Equal(encodedImageBytes, builtImage.ImageBytes.ToArray());

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedImage = Assert.IsType<PdfImageElement>(Assert.Single(Assert.Single(reimported.Pages).GraphicsObjects));
        Assert.Equal(encodedImageBytes, importedImage.ImageBytes.ToArray());
    }

    [Fact]
    public async Task DocumentBuilder_AndCanvasPdfGeneratorBridge_ShouldMapIccBasedGrayImagesToDeviceGray()
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

        var iccProfileDictionary = new PdfDictionary();
        iccProfileDictionary["N"] = new PdfInteger(1);
        var iccProfileStream = new PdfStreamObject(iccProfileDictionary, ReadOnlyMemory<byte>.Empty);

        var imageDictionary = new PdfDictionary();
        imageDictionary["Type"] = new PdfName("XObject");
        imageDictionary["Subtype"] = new PdfName("Image");
        imageDictionary["Width"] = new PdfInteger(1);
        imageDictionary["Height"] = new PdfInteger(1);
        imageDictionary["ColorSpace"] = new PdfArray([new PdfName("ICCBased"), new PdfReference(new PdfObjectId(6, 0))]);
        imageDictionary["BitsPerComponent"] = new PdfInteger(8);
        imageDictionary["Filter"] = new PdfName("FlateDecode");
        var imageStream = new PdfStreamObject(imageDictionary, Compress([0x7f]));

        graph.Add(new PdfIndirectObject(new PdfObjectId(1, 0), catalog, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(2, 0), pages, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(3, 0), page, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(4, 0), contentStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(5, 0), imageStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(6, 0), iccProfileStream, new PdfSourceSpan(0, 1)));

        var builder = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter());
        var document = builder.Build(graph);

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var regeneratedImageObject = Assert.Single(
            reimported.ObjectGraph.Objects.Values
                .Select(static indirect => indirect.Value)
                .OfType<PdfStreamObject>(),
            static stream => stream.Dictionary["Subtype"] is PdfName { Value: "Image" });

        Assert.Equal("DeviceGray", Assert.IsType<PdfName>(regeneratedImageObject.Dictionary["ColorSpace"]).Value);
    }

    [Fact]
    public async Task DocumentBuilder_AndCanvasPdfGeneratorBridge_ShouldRoundTripFlateXObjectImagesWithIccBasedCmykColorSpace()
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

        var iccProfileDictionary = new PdfDictionary();
        iccProfileDictionary["N"] = new PdfInteger(4);
        var iccProfileStream = new PdfStreamObject(iccProfileDictionary, ReadOnlyMemory<byte>.Empty);

        var imageDictionary = new PdfDictionary();
        imageDictionary["Type"] = new PdfName("XObject");
        imageDictionary["Subtype"] = new PdfName("Image");
        imageDictionary["Width"] = new PdfInteger(1);
        imageDictionary["Height"] = new PdfInteger(1);
        imageDictionary["ColorSpace"] = new PdfArray([new PdfName("ICCBased"), new PdfReference(new PdfObjectId(6, 0))]);
        imageDictionary["BitsPerComponent"] = new PdfInteger(8);
        imageDictionary["Filter"] = new PdfName("FlateDecode");
        var encodedImageBytes = Compress([0x00, 0x40, 0x80, 0xC0]);
        var imageStream = new PdfStreamObject(imageDictionary, encodedImageBytes);

        graph.Add(new PdfIndirectObject(new PdfObjectId(1, 0), catalog, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(2, 0), pages, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(3, 0), page, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(4, 0), contentStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(5, 0), imageStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(6, 0), iccProfileStream, new PdfSourceSpan(0, 1)));

        var builder = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter());
        var document = builder.Build(graph);

        var builtImage = Assert.IsType<PdfImageElement>(Assert.Single(Assert.Single(document.Pages).GraphicsObjects));
        Assert.Equal(encodedImageBytes, builtImage.ImageBytes.ToArray());

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedImage = Assert.IsType<PdfImageElement>(Assert.Single(Assert.Single(reimported.Pages).GraphicsObjects));
        Assert.Equal(encodedImageBytes, importedImage.ImageBytes.ToArray());
    }

    [Fact]
    public async Task DocumentBuilder_AndCanvasPdfGeneratorBridge_ShouldMapIccBasedCmykImagesToDeviceCmyk()
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

        var iccProfileDictionary = new PdfDictionary();
        iccProfileDictionary["N"] = new PdfInteger(4);
        var iccProfileStream = new PdfStreamObject(iccProfileDictionary, ReadOnlyMemory<byte>.Empty);

        var imageDictionary = new PdfDictionary();
        imageDictionary["Type"] = new PdfName("XObject");
        imageDictionary["Subtype"] = new PdfName("Image");
        imageDictionary["Width"] = new PdfInteger(1);
        imageDictionary["Height"] = new PdfInteger(1);
        imageDictionary["ColorSpace"] = new PdfArray([new PdfName("ICCBased"), new PdfReference(new PdfObjectId(6, 0))]);
        imageDictionary["BitsPerComponent"] = new PdfInteger(8);
        imageDictionary["Filter"] = new PdfName("FlateDecode");
        var imageStream = new PdfStreamObject(imageDictionary, Compress([0x00, 0x40, 0x80, 0xC0]));

        graph.Add(new PdfIndirectObject(new PdfObjectId(1, 0), catalog, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(2, 0), pages, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(3, 0), page, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(4, 0), contentStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(5, 0), imageStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(6, 0), iccProfileStream, new PdfSourceSpan(0, 1)));

        var builder = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter());
        var document = builder.Build(graph);

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var regeneratedImageObject = Assert.Single(
            reimported.ObjectGraph.Objects.Values
                .Select(static indirect => indirect.Value)
                .OfType<PdfStreamObject>(),
            static stream => stream.Dictionary["Subtype"] is PdfName { Value: "Image" });

        Assert.Equal("DeviceCMYK", Assert.IsType<PdfName>(regeneratedImageObject.Dictionary["ColorSpace"]).Value);
    }

    [Fact]
    public async Task DocumentBuilder_AndCanvasPdfGeneratorBridge_ShouldMapNamedIccBasedGrayImagesToDeviceGray()
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

        var colorSpaceResources = new PdfDictionary();
        colorSpaceResources["CS1"] = new PdfArray([new PdfName("ICCBased"), new PdfReference(new PdfObjectId(6, 0))]);

        var resources = new PdfDictionary();
        resources["XObject"] = xObjectResources;
        resources["ColorSpace"] = colorSpaceResources;

        var page = new PdfDictionary();
        page["Type"] = new PdfName("Page");
        page["Parent"] = new PdfReference(new PdfObjectId(2, 0));
        page["Resources"] = resources;
        page["MediaBox"] = Array(0, 0, 200, 120);
        page["Contents"] = new PdfReference(new PdfObjectId(4, 0));

        var contentBytes = Encoding.ASCII.GetBytes("q 40 0 0 20 30 40 cm /Im1 Do Q");
        var contentStream = new PdfStreamObject(new PdfDictionary(), contentBytes);

        var iccProfileDictionary = new PdfDictionary();
        iccProfileDictionary["N"] = new PdfInteger(1);
        var iccProfileStream = new PdfStreamObject(iccProfileDictionary, ReadOnlyMemory<byte>.Empty);

        var imageDictionary = new PdfDictionary();
        imageDictionary["Type"] = new PdfName("XObject");
        imageDictionary["Subtype"] = new PdfName("Image");
        imageDictionary["Width"] = new PdfInteger(1);
        imageDictionary["Height"] = new PdfInteger(1);
        imageDictionary["ColorSpace"] = new PdfName("CS1");
        imageDictionary["BitsPerComponent"] = new PdfInteger(8);
        imageDictionary["Filter"] = new PdfName("FlateDecode");
        var imageStream = new PdfStreamObject(imageDictionary, Compress([0x7f]));

        graph.Add(new PdfIndirectObject(new PdfObjectId(1, 0), catalog, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(2, 0), pages, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(3, 0), page, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(4, 0), contentStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(5, 0), imageStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(6, 0), iccProfileStream, new PdfSourceSpan(0, 1)));

        var builder = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter());
        var document = builder.Build(graph);

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var regeneratedImageObject = Assert.Single(
            reimported.ObjectGraph.Objects.Values
                .Select(static indirect => indirect.Value)
                .OfType<PdfStreamObject>(),
            static stream => stream.Dictionary["Subtype"] is PdfName { Value: "Image" });

        Assert.Equal("DeviceGray", Assert.IsType<PdfName>(regeneratedImageObject.Dictionary["ColorSpace"]).Value);
    }

    [Fact]
    public async Task DocumentBuilder_AndCanvasPdfGeneratorBridge_ShouldMapIndirectNamedIccBasedGrayImagesToDeviceGray()
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

        var colorSpaceResources = new PdfDictionary();
        colorSpaceResources["CS1"] = new PdfReference(new PdfObjectId(6, 0));

        var resources = new PdfDictionary();
        resources["XObject"] = xObjectResources;
        resources["ColorSpace"] = colorSpaceResources;

        var page = new PdfDictionary();
        page["Type"] = new PdfName("Page");
        page["Parent"] = new PdfReference(new PdfObjectId(2, 0));
        page["Resources"] = resources;
        page["MediaBox"] = Array(0, 0, 200, 120);
        page["Contents"] = new PdfReference(new PdfObjectId(4, 0));

        var contentBytes = Encoding.ASCII.GetBytes("q 40 0 0 20 30 40 cm /Im1 Do Q");
        var contentStream = new PdfStreamObject(new PdfDictionary(), contentBytes);

        var iccProfileDictionary = new PdfDictionary();
        iccProfileDictionary["N"] = new PdfInteger(1);
        var iccProfileStream = new PdfStreamObject(iccProfileDictionary, ReadOnlyMemory<byte>.Empty);

        var imageDictionary = new PdfDictionary();
        imageDictionary["Type"] = new PdfName("XObject");
        imageDictionary["Subtype"] = new PdfName("Image");
        imageDictionary["Width"] = new PdfInteger(1);
        imageDictionary["Height"] = new PdfInteger(1);
        imageDictionary["ColorSpace"] = new PdfName("CS1");
        imageDictionary["BitsPerComponent"] = new PdfInteger(8);
        imageDictionary["Filter"] = new PdfName("FlateDecode");
        var imageStream = new PdfStreamObject(imageDictionary, Compress([0x7f]));

        var namedColorSpace = new PdfArray([new PdfName("ICCBased"), new PdfReference(new PdfObjectId(7, 0))]);

        graph.Add(new PdfIndirectObject(new PdfObjectId(1, 0), catalog, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(2, 0), pages, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(3, 0), page, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(4, 0), contentStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(5, 0), imageStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(6, 0), namedColorSpace, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(7, 0), iccProfileStream, new PdfSourceSpan(0, 1)));

        var builder = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter());
        var document = builder.Build(graph);

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var regeneratedImageObject = Assert.Single(
            reimported.ObjectGraph.Objects.Values
                .Select(static indirect => indirect.Value)
                .OfType<PdfStreamObject>(),
            static stream => stream.Dictionary["Subtype"] is PdfName { Value: "Image" });

        Assert.Equal("DeviceGray", Assert.IsType<PdfName>(regeneratedImageObject.Dictionary["ColorSpace"]).Value);
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

        var bridge = new PxaPdfGeneratorBridge();
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

        var bridge = new PxaPdfGeneratorBridge();
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

        var bridge = new PxaPdfGeneratorBridge();
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

        var bridge = new PxaPdfGeneratorBridge();
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

        var bridge = new PxaPdfGeneratorBridge();
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

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedPage = Assert.Single(reimported.Pages);
        Assert.Empty(importedPage.GraphicsObjects);
        Assert.False(importedPage.Resources.Values.ContainsKey("Shading"));
    }

    [Fact]
    public async Task DocumentBuilder_AndCanvasPdfGeneratorBridge_ShouldPreserveImagesWhenShadingIsPresent()
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

        var shadingResources = new PdfDictionary();
        shadingResources["Sh1"] = shadingDictionary;

        var resources = new PdfDictionary();
        resources["XObject"] = xObjectResources;
        resources["Shading"] = shadingResources;

        var page = new PdfDictionary();
        page["Type"] = new PdfName("Page");
        page["Parent"] = new PdfReference(new PdfObjectId(2, 0));
        page["Resources"] = resources;
        page["MediaBox"] = Array(0, 0, 200, 120);
        page["Contents"] = new PdfReference(new PdfObjectId(4, 0));

        var contentBytes = Encoding.ASCII.GetBytes("q 40 0 0 20 30 40 cm /Im1 Do Q /Sh1 sh");
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

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedPage = Assert.Single(reimported.Pages);
        var importedImage = Assert.Single(importedPage.GraphicsObjects.OfType<PdfImageElement>());
        Assert.Equal(TinyJpegBytes(), importedImage.ImageBytes.ToArray());
        var importedShading = Assert.Single(importedPage.GraphicsObjects.OfType<PdfShadingElement>());
        Assert.Equal("Sh1", importedShading.ResourceName);
    }

    [Fact]
    public async Task DocumentBuilder_AndCanvasPdfGeneratorBridge_ShouldPreserveInheritedShadingResources()
    {
        var graph = new PdfObjectGraph();

        var catalog = new PdfDictionary();
        catalog["Type"] = new PdfName("Catalog");
        catalog["Pages"] = new PdfReference(new PdfObjectId(2, 0));

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

        var inheritedResources = new PdfDictionary();
        inheritedResources["Shading"] = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Sh1"] = shadingDictionary
        });

        var pages = new PdfDictionary();
        pages["Type"] = new PdfName("Pages");
        pages["Count"] = new PdfInteger(1);
        pages["Kids"] = new PdfArray([new PdfReference(new PdfObjectId(3, 0))]);
        pages["Resources"] = inheritedResources;

        var page = new PdfDictionary();
        page["Type"] = new PdfName("Page");
        page["Parent"] = new PdfReference(new PdfObjectId(2, 0));
        page["MediaBox"] = Array(0, 0, 200, 120);
        page["Contents"] = new PdfReference(new PdfObjectId(4, 0));

        var contentBytes = Encoding.ASCII.GetBytes("/Sh1 sh");
        var contentStream = new PdfStreamObject(new PdfDictionary(), contentBytes);

        graph.Add(new PdfIndirectObject(new PdfObjectId(1, 0), catalog, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(2, 0), pages, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(3, 0), page, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(4, 0), contentStream, new PdfSourceSpan(0, 1)));

        var builder = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter());
        var document = builder.Build(graph);

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedPage = Assert.Single(reimported.Pages);
        var importedShading = Assert.Single(importedPage.GraphicsObjects.OfType<PdfShadingElement>());
        Assert.Equal("Sh1", importedShading.ResourceName);
        var importedResources = Assert.IsType<PdfDictionary>(importedPage.Resources["Shading"]);
        Assert.IsType<PdfDictionary>(importedResources["Sh1"]);
    }

    [Fact]
    public async Task CanvasPdfGeneratorBridge_ShouldPreserveColorSpaceResourcesRequiredByShading()
    {
        var document = new PdfDocumentModel();

        var colorSpaces = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["CS1"] = new PdfName("DeviceRGB")
        });

        var shadingDictionary = new PdfDictionary();
        shadingDictionary["ShadingType"] = new PdfInteger(2);
        shadingDictionary["ColorSpace"] = new PdfName("CS1");
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
        pageResources["ColorSpace"] = colorSpaces;
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

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedPage = Assert.Single(reimported.Pages);
        var importedColorSpaces = Assert.IsType<PdfDictionary>(importedPage.Resources["ColorSpace"]);
        Assert.Equal("DeviceRGB", Assert.IsType<PdfName>(importedColorSpaces["CS1"]).Value);
        var importedShading = Assert.Single(importedPage.GraphicsObjects.OfType<PdfShadingElement>());
        Assert.Equal("Sh1", importedShading.ResourceName);
    }

    // ── CCITTFaxDecode decoder ───────────────────────────────────────────────

    [Fact]
    public void CcittFaxDecoder_ShouldDecodeGroup3_1D_AllWhiteRow()
    {
        // 4-column row of all-white pixels.
        // White run=4 code: "1011" (4 bits) → 0xB0 padded to byte.
        // Rows=1, EndOfLine=false, EndOfBlock=false → no EOL/RTC needed.
        byte[] encoded = [0xB0];
        var dictionary = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["DecodeParms"] = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                ["Columns"] = new PdfInteger(4),
                ["Rows"] = new PdfInteger(1),
                ["EndOfLine"] = new PdfBoolean(false),
                ["EndOfBlock"] = new PdfBoolean(false)
            })
        });

        var decoded = new CcittFaxStreamDecoder().Decode(encoded, dictionary);

        // BlackIs1=false default: white=1, black=0. 4 white pixels → 1111xxxx = 0xF0.
        Assert.Equal([0xF0], decoded.ToArray());
    }

    [Fact]
    public void CcittFaxDecoder_ShouldDecodeGroup3_1D_AllBlackRow()
    {
        // 4-column row of all-black pixels.
        // White run=0: "00110101" (8 bits). Black run=4: "011" (3 bits).
        // Bits: 00110101 011 → 0x35, 0x60.
        byte[] encoded = [0x35, 0x60];
        var dictionary = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["DecodeParms"] = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                ["Columns"] = new PdfInteger(4),
                ["Rows"] = new PdfInteger(1),
                ["EndOfLine"] = new PdfBoolean(false),
                ["EndOfBlock"] = new PdfBoolean(false)
            })
        });

        var decoded = new CcittFaxStreamDecoder().Decode(encoded, dictionary);

        // 4 black pixels → 0000xxxx = 0x00.
        Assert.Equal([0x00], decoded.ToArray());
    }

    [Fact]
    public void CcittFaxDecoder_ShouldDecodeGroup3_1D_MixedBlackWhiteRow()
    {
        // 4-column row: B W B W (starting black, so white run=0 first).
        // White run=0: "00110101" (8 bits). Black run=1: "010" (3 bits).
        // White run=1: "000111" (6 bits). Black run=1: "010" (3 bits).
        // White run=1: "000111" (6 bits) — trailing white after last black.
        // Bits: 00110101 010 000111 010 000111
        // = 00110101 010000111 010000111
        // Total: 8+3+6+3+6 = 26 bits
        // Byte 0: 00110101 = 0x35
        // Byte 1: 01000011 = 0x43
        // Byte 2: 10100001 = 0xA1 (bits 16-23: 10100001 with remaining bits padded)
        // Let me recalculate:
        // Bits 0-7:  00110101
        // Bits 8-10: 010
        // Bits 11-16: 000111
        // Bits 17-19: 010
        // Bits 20-25: 000111
        // Bit grouping into bytes:
        // Byte 0 (0-7):  0 0 1 1 0 1 0 1 = 0x35
        // Byte 1 (8-15): 0 1 0 0 0 0 1 1 = 0x43
        // Byte 2 (16-23):1 0 1 0 0 0 0 1 = 0xA1
        // Byte 3 (24-25):1 1 (padded)   = 0xC0
        byte[] encoded = [0x35, 0x43, 0xA1, 0xC0];
        var dictionary = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["DecodeParms"] = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                ["Columns"] = new PdfInteger(4),
                ["Rows"] = new PdfInteger(1),
                ["EndOfLine"] = new PdfBoolean(false),
                ["EndOfBlock"] = new PdfBoolean(false)
            })
        });

        var decoded = new CcittFaxStreamDecoder().Decode(encoded, dictionary);

        // B W B W → 0 1 0 1 → 0101xxxx = 0x50.
        Assert.Equal([0x50], decoded.ToArray());
    }

    [Fact]
    public void CcittFaxDecoder_ShouldDecodeGroup4_AllWhiteRow()
    {
        // 4-column row, all white, Group 4 (K=-1).
        // Reference row = all white. Current row = all white.
        // V0 mode code: "1" (1 bit). b1 = sentinel (no black in refRow) = 4. a1 = 4 = columns. Done.
        // EOFB = two EOL codes ("000000000001" each, 12 bits each).
        // Bits: 1 | 000000000001 | 000000000001 (25 bits total)
        // Byte 0: 10000000 = 0x80
        // Byte 1: 00000010 = wait — let me pack carefully:
        // Bit  0: 1  (V0)
        // Bits 1-12: 000000000001 (first EOL)
        // Bits 13-24: 000000000001 (second EOL)
        // Byte 0 (0-7):  1 0 0 0 0 0 0 0 = 0x80
        // Byte 1 (8-15): 0 0 0 1 0 0 0 0 = 0x10
        // Byte 2 (16-23):0 0 0 0 0 0 0 1 = 0x01
        // Byte 3 (24):   1 (padded 7 zeros) = 0x80
        byte[] encoded = [0x80, 0x10, 0x01, 0x80];
        var dictionary = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["DecodeParms"] = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                ["K"] = new PdfInteger(-1),
                ["Columns"] = new PdfInteger(4),
                ["Rows"] = new PdfInteger(1),
                ["EndOfBlock"] = new PdfBoolean(true)
            })
        });

        var decoded = new CcittFaxStreamDecoder().Decode(encoded, dictionary);

        // 4 white pixels → 0xF0.
        Assert.Equal([0xF0], decoded.ToArray());
    }

    [Fact]
    public void CcittFaxDecoder_ShouldDecodeGroup3_1D_TwoRows()
    {
        // Two 4-column rows: first all-white, second all-black.
        // Row 1: white run=4 → "1011" (4 bits)
        // EOL: "000000000001" (12 bits)
        // Row 2: white run=0 → "00110101" (8 bits), black run=4 → "011" (3 bits)
        // RTC (EndOfBlock): 6 × "000000000001" (72 bits)
        // Row1 + EOL: 1011_000000000001 (16 bits)
        // Row2: 00110101_011 (11 bits)
        // RTC: 6 × 000000000001 (72 bits)
        // Total: 99 bits → 13 bytes
        // Byte 0: 1011_0000 = 0xB0
        // Byte 1: 0000_0001 = 0x01
        // Byte 2: 0011_0101 = 0x35
        // Byte 3: 011_00000 = 0x60
        // Bytes 4-12: RTC = 6 × 000000000001 (72 bits = 9 bytes)
        // But for this test we use EndOfBlock=false, Rows=2 to avoid encoding RTC
        // Row1 bits: 1011 (4 bits)
        // EOL bits: 000000000001 (12 bits) → total 16 bits after row 1
        // Row2 bits: 00110101 011 (11 bits)
        // With Rows=2, EndOfLine=true, EndOfBlock=false:
        // 16 + 11 = 27 bits → 4 bytes
        // Byte 0: 1011_0000 = 0xB0
        // Byte 1: 0000_0001 = 0x01
        // Byte 2: 0011_0101 = 0x35
        // Byte 3: 011_00000 = 0x60
        byte[] encoded = [0xB0, 0x01, 0x35, 0x60];
        var dictionary = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["DecodeParms"] = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                ["Columns"] = new PdfInteger(4),
                ["Rows"] = new PdfInteger(2),
                ["EndOfLine"] = new PdfBoolean(true),
                ["EndOfBlock"] = new PdfBoolean(false)
            })
        });

        var decoded = new CcittFaxStreamDecoder().Decode(encoded, dictionary);

        // Row1: 4 white → 0xF0. Row2: 4 black → 0x00.
        Assert.Equal([0xF0, 0x00], decoded.ToArray());
    }

    [Fact]
    public void CcittFaxDecoder_ShouldEvaluateAsSupported()
    {
        var registry = new PdfStreamDecoderRegistry();
        var support = Assert.Single(registry.Evaluate(new PdfName("CCITTFaxDecode")));
        Assert.Equal(PdfStreamDecoderSupportStatus.Supported, support.Status);
        var supportCcf = Assert.Single(registry.Evaluate(new PdfName("CCF")));
        Assert.Equal(PdfStreamDecoderSupportStatus.Supported, supportCcf.Status);
    }

    // ── Shading + ICCBased color space remaining slices ─────────────────────

    [Fact]
    public async Task CanvasPdfGeneratorBridge_ShouldPreserveShadingWithNamedIccBasedColorSpace()
    {
        var graph = new PdfObjectGraph();

        var iccProfileDictionary = new PdfDictionary();
        iccProfileDictionary["N"] = new PdfInteger(3);
        var iccProfileStream = new PdfStreamObject(iccProfileDictionary, ReadOnlyMemory<byte>.Empty);

        var iccBasedArray = new PdfArray([new PdfName("ICCBased"), new PdfReference(new PdfObjectId(10, 0))]);
        var colorSpaceResources = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["CS1"] = iccBasedArray
        });

        var shadingDictionary = new PdfDictionary();
        shadingDictionary["ShadingType"] = new PdfInteger(2);
        shadingDictionary["ColorSpace"] = new PdfName("CS1");
        shadingDictionary["Coords"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(100), new PdfInteger(0)]);
        shadingDictionary["Function"] = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["FunctionType"] = new PdfInteger(2),
            ["Domain"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)]),
            ["C0"] = new PdfArray([new PdfInteger(1), new PdfInteger(0), new PdfInteger(0)]),
            ["C1"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(1)]),
            ["N"] = new PdfInteger(1)
        });

        graph.Add(new PdfIndirectObject(new PdfObjectId(10, 0), iccProfileStream, new PdfSourceSpan(0, 0)));

        var document = new PdfDocumentModel { ObjectGraph = graph };

        var pageResources = new PdfDictionary();
        pageResources["ColorSpace"] = colorSpaceResources;
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

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedPage = Assert.Single(reimported.Pages);
        var importedShading = Assert.IsType<PdfShadingElement>(Assert.Single(importedPage.GraphicsObjects));
        Assert.Equal("Sh1", importedShading.ResourceName);
        var importedColorSpaces = Assert.IsType<PdfDictionary>(importedPage.Resources["ColorSpace"]);
        Assert.True(importedColorSpaces.Values.ContainsKey("CS1"));
    }

    [Fact]
    public async Task CanvasPdfGeneratorBridge_ShouldPreserveShadingWithIndirectIccBasedColorSpace()
    {
        var graph = new PdfObjectGraph();

        var iccProfileDictionary = new PdfDictionary();
        iccProfileDictionary["N"] = new PdfInteger(3);
        var iccProfileStream = new PdfStreamObject(iccProfileDictionary, ReadOnlyMemory<byte>.Empty);

        var iccBasedArray = new PdfArray([new PdfName("ICCBased"), new PdfReference(new PdfObjectId(11, 0))]);

        // CS1 is an indirect reference to the ICCBased array
        graph.Add(new PdfIndirectObject(new PdfObjectId(10, 0), iccBasedArray, new PdfSourceSpan(0, 0)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(11, 0), iccProfileStream, new PdfSourceSpan(0, 0)));

        var colorSpaceResources = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["CS1"] = new PdfReference(new PdfObjectId(10, 0))
        });

        var shadingDictionary = new PdfDictionary();
        shadingDictionary["ShadingType"] = new PdfInteger(2);
        shadingDictionary["ColorSpace"] = new PdfName("CS1");
        shadingDictionary["Coords"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(100), new PdfInteger(0)]);
        shadingDictionary["Function"] = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["FunctionType"] = new PdfInteger(2),
            ["Domain"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)]),
            ["C0"] = new PdfArray([new PdfInteger(1), new PdfInteger(0), new PdfInteger(0)]),
            ["C1"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(1)]),
            ["N"] = new PdfInteger(1)
        });

        var document = new PdfDocumentModel { ObjectGraph = graph };

        var pageResources = new PdfDictionary();
        pageResources["ColorSpace"] = colorSpaceResources;
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

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedPage = Assert.Single(reimported.Pages);
        Assert.IsType<PdfShadingElement>(Assert.Single(importedPage.GraphicsObjects));
        var importedColorSpaces = Assert.IsType<PdfDictionary>(importedPage.Resources["ColorSpace"]);
        Assert.True(importedColorSpaces.Values.ContainsKey("CS1"));
    }

    // ── End-to-end multi-content fixtures ───────────────────────────────────

    [Fact]
    public async Task DocumentBuilder_AndCanvasPdfGeneratorBridge_ShouldRoundTripDocumentWithTextPathsAndImages()
    {
        // Combines text + vector path + JPEG image on a single page — verifies
        // all three element types survive the full importer→model→bridge cycle.
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
        page["MediaBox"] = Array(0, 0, 300, 200);
        page["Contents"] = new PdfReference(new PdfObjectId(4, 0));

        // Content: a filled rectangle, then a text object, then an image
        var contentBytes = Encoding.ASCII.GetBytes(
            "q 1 0 0 1 10 10 cm 0 0 50 30 re f Q " +
            "BT /F1 12 Tf 1 0 0 1 20 100 Tm (Hello) Tj ET " +
            "q 40 0 0 20 80 50 cm /Im1 Do Q");
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

        var importedPage = Assert.Single(document.Pages);
        Assert.Equal(300, importedPage.MediaBox?.Width);
        Assert.Equal(200, importedPage.MediaBox?.Height);
        Assert.Single(importedPage.TextObjects);
        Assert.Contains(importedPage.GraphicsObjects, static e => e is PdfPathElement);
        Assert.Contains(importedPage.GraphicsObjects, static e => e is PdfImageElement);

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var reimportedPage = Assert.Single(reimported.Pages);
        Assert.Equal("Hello", Assert.Single(reimportedPage.TextObjects).Text);
        Assert.Contains(reimportedPage.GraphicsObjects, static e => e is PdfPathElement);
        var reimportedImage = Assert.Single(reimportedPage.GraphicsObjects.OfType<PdfImageElement>());
        Assert.False(reimportedImage.ImageBytes.IsEmpty);
    }

    [Fact]
    public async Task DocumentBuilder_AndCanvasPdfGeneratorBridge_ShouldRoundTripDocumentWithInheritedResourcesAndIncremental()
    {
        // Two-page document where page-tree inherits resources and the xref
        // has an incremental update — verifies page-tree traversal, inherited
        // resources, and multi-page bridge regeneration end-to-end.
        var graph = new PdfObjectGraph();

        var sharedColorSpaces = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["CS1"] = new PdfName("DeviceRGB")
        });
        var inheritedResources = new PdfDictionary();
        inheritedResources["ColorSpace"] = sharedColorSpaces;

        var catalog = new PdfDictionary();
        catalog["Type"] = new PdfName("Catalog");
        catalog["Pages"] = new PdfReference(new PdfObjectId(2, 0));

        var pages = new PdfDictionary();
        pages["Type"] = new PdfName("Pages");
        pages["Count"] = new PdfInteger(2);
        pages["Kids"] = new PdfArray([
            new PdfReference(new PdfObjectId(3, 0)),
            new PdfReference(new PdfObjectId(6, 0))
        ]);
        pages["Resources"] = inheritedResources;
        pages["MediaBox"] = Array(0, 0, 200, 150);

        var page1 = new PdfDictionary();
        page1["Type"] = new PdfName("Page");
        page1["Parent"] = new PdfReference(new PdfObjectId(2, 0));
        page1["Contents"] = new PdfReference(new PdfObjectId(4, 0));

        var content1 = Encoding.ASCII.GetBytes("BT /F1 10 Tf 1 0 0 1 10 100 Tm (Page one) Tj ET");
        var contentStream1 = new PdfStreamObject(new PdfDictionary(), content1);

        var page2 = new PdfDictionary();
        page2["Type"] = new PdfName("Page");
        page2["Parent"] = new PdfReference(new PdfObjectId(2, 0));
        page2["Contents"] = new PdfReference(new PdfObjectId(7, 0));

        var content2 = Encoding.ASCII.GetBytes("BT /F1 10 Tf 1 0 0 1 10 100 Tm (Page two) Tj ET");
        var contentStream2 = new PdfStreamObject(new PdfDictionary(), content2);

        graph.Add(new PdfIndirectObject(new PdfObjectId(1, 0), catalog, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(2, 0), pages, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(3, 0), page1, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(4, 0), contentStream1, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(6, 0), page2, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(7, 0), contentStream2, new PdfSourceSpan(0, 1)));

        var builder = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter());
        var document = builder.Build(graph);

        Assert.Equal(2, document.Pages.Count);

        // Both pages should have inherited resources (ColorSpace CS1)
        foreach (var p in document.Pages)
        {
            var cs = Assert.IsType<PdfDictionary>(p.Resources["ColorSpace"]);
            Assert.True(cs.Values.ContainsKey("CS1"));
        }

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        Assert.Equal(2, reimported.Pages.Count);
        Assert.Equal("Page one", Assert.Single(reimported.Pages[0].TextObjects).Text);
        Assert.Equal("Page two", Assert.Single(reimported.Pages[1].TextObjects).Text);
    }

    [Fact]
    public async Task DocumentBuilder_AndCanvasPdfGeneratorBridge_ShouldRoundTripDocumentCombiningTextPathsImagesAndShading()
    {
        // Most complex fixture: text + path + JPEG image + shading on one page.
        // Validates that the shading compatibility update does not erase other content.
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

        var shadingDictionary = new PdfDictionary();
        shadingDictionary["ShadingType"] = new PdfInteger(2);
        shadingDictionary["ColorSpace"] = new PdfName("DeviceRGB");
        shadingDictionary["Coords"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(200), new PdfInteger(0)]);
        shadingDictionary["Function"] = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["FunctionType"] = new PdfInteger(2),
            ["Domain"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)]),
            ["C0"] = new PdfArray([new PdfInteger(1), new PdfInteger(0), new PdfInteger(0)]),
            ["C1"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(1)]),
            ["N"] = new PdfInteger(1)
        });

        var resources = new PdfDictionary();
        resources["XObject"] = xObjectResources;
        resources["Shading"] = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Sh1"] = shadingDictionary
        });

        var page = new PdfDictionary();
        page["Type"] = new PdfName("Page");
        page["Parent"] = new PdfReference(new PdfObjectId(2, 0));
        page["Resources"] = resources;
        page["MediaBox"] = Array(0, 0, 300, 200);
        page["Contents"] = new PdfReference(new PdfObjectId(4, 0));

        var contentBytes = Encoding.ASCII.GetBytes(
            "q 1 0 0 1 5 5 cm 0 0 30 20 re f Q " +
            "BT /F1 11 Tf 1 0 0 1 10 150 Tm (Shade) Tj ET " +
            "q 40 0 0 20 60 60 cm /Im1 Do Q " +
            "/Sh1 sh");
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

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedPage = Assert.Single(reimported.Pages);
        Assert.Equal("Shade", Assert.Single(importedPage.TextObjects).Text);
        Assert.Single(importedPage.GraphicsObjects.OfType<PdfPathElement>());
        Assert.Single(importedPage.GraphicsObjects.OfType<PdfImageElement>());
        Assert.Single(importedPage.GraphicsObjects.OfType<PdfShadingElement>());
    }

    [Fact]
    public async Task CanvasImporterPdfImporter_ShouldUseSceneGraphBoundsAndRotationForText()
    {
        var design = await ImportDesignFromSinglePageContentAsync(
            "BT /F1 12 Tf 0 1 -1 0 120 80 Tm (Rotated) Tj ET");

        var text = Assert.Single(Assert.Single(design.Pages).Elements, static element => element.Type == "text");

        Assert.Equal("Rotated", text.Content);
        Assert.True(text.Width > 0);
        Assert.True(text.Height > 0);
        Assert.True(text.Width > text.Height);
        var rotation = StyleValue<double>(text, "rotation");
        Assert.InRange(rotation, -91d, -89d);
    }

    [Fact]
    public async Task CanvasImporterPdfImporter_ShouldNotDoubleScaleTextMatrixBounds()
    {
        var design = await ImportDesignFromSinglePageContentAsync(
            "BT /F1 1 Tf 12 0 0 12 20 120 Tm (Scaled) Tj ET");

        var text = Assert.Single(Assert.Single(design.Pages).Elements, static element => element.Type == "text");

        Assert.Equal("Scaled", text.Content);
        Assert.InRange(text.X, 18d, 21d);
        Assert.InRange(text.Y, 69d, 72d);
        Assert.InRange(text.Width, 37d, 39d);
        Assert.InRange(text.Height, 13d, 15d);
        Assert.Equal(1.05d, StyleValue<double>(text, "lineHeight"));
        Assert.Equal("pre", StyleValue<string>(text, "whiteSpace"));
        Assert.True(text.Height >= StyleValue<double>(text, "fontSize") * 1.1d);
    }

    [Fact]
    public async Task CanvasImporterPdfImporter_ShouldUseReadingOrderInsteadOfDrawOrderForText()
    {
        var design = await ImportDesignFromSinglePageContentAsync(
            "BT /F1 10 Tf 1 0 0 1 20 40 Tm (Second) Tj 1 0 0 1 20 160 Tm (First) Tj ET");

        var texts = Assert.Single(design.Pages)
            .Elements
            .Where(static element => element.Type == "text")
            .Select(static element => element.Content ?? string.Empty)
            .ToArray();

        Assert.Equal(["First", "Second"], texts);
    }

    [Fact]
    public async Task CanvasImporterPdfImporter_ShouldNormalizePdfFontFamilyAndStyle()
    {
        const string resources = "<< /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /ABCDEF+TimesNewRomanPS-BoldItalicMT >> >> >>";
        var design = await ImportDesignFromSinglePageContentAsync(
            "BT /F1 12 Tf 1 0 0 1 20 120 Tm (Styled) Tj ET",
            resources);

        var text = Assert.Single(Assert.Single(design.Pages).Elements, static element => element.Type == "text");

        Assert.Equal("Times New Roman", StyleValue<string>(text, "fontFamily"));
        Assert.Equal("bold", StyleValue<string>(text, "fontWeight"));
        Assert.Equal("italic", StyleValue<string>(text, "fontStyle"));
        Assert.Equal("TimesNewRomanPS-BoldItalicMT", StyleValue<string>(text, "pdfFontName"));
    }

    [Fact]
    public async Task CanvasImporterPdfImporter_ShouldUseFontDescriptorNameWhenBaseFontIsMissing()
    {
        const string resources = "<< /Font << /F1 << /Type /Font /Subtype /Type1 /FontDescriptor << /FontName /ABCDEF+Arial-BoldMT >> >> >> >>";
        var design = await ImportDesignFromSinglePageContentAsync(
            "BT /F1 12 Tf 1 0 0 1 20 120 Tm (Descriptor) Tj ET",
            resources);

        var text = Assert.Single(Assert.Single(design.Pages).Elements, static element => element.Type == "text");

        Assert.Equal("Arial", StyleValue<string>(text, "fontFamily"));
        Assert.Equal("bold", StyleValue<string>(text, "fontWeight"));
        Assert.Equal("normal", StyleValue<string>(text, "fontStyle"));
        Assert.Equal("Arial-BoldMT", StyleValue<string>(text, "pdfFontName"));
    }

    [Fact]
    public async Task CanvasImporterPdfImporter_ShouldUseFontDescriptorStyleMetadata()
    {
        const string resources = "<< /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /ABCDEF+MinionPro /FontDescriptor << /FontName /ABCDEF+MinionPro /FontWeight 700 /ItalicAngle -12 >> >> >> >>";
        var design = await ImportDesignFromSinglePageContentAsync(
            "BT /F1 12 Tf 1 0 0 1 20 120 Tm (Descriptor style) Tj ET",
            resources);

        var text = Assert.Single(Assert.Single(design.Pages).Elements, static element => element.Type == "text");

        Assert.Equal("Minion Pro", StyleValue<string>(text, "fontFamily"));
        Assert.Equal("bold", StyleValue<string>(text, "fontWeight"));
        Assert.Equal("italic", StyleValue<string>(text, "fontStyle"));
    }

    [Fact]
    public async Task CanvasImporterPdfImporter_ShouldExposeEmbeddedTrueTypeFontAssetForNonSubsetFont()
    {
        var fontBytes = new byte[] { 0x00, 0x01, 0x00, 0x00, 0x41, 0x42, 0x43, 0x44 };
        var fontFile = new SyntheticPdfObject(5, BuildStreamObjectBody(string.Empty, fontBytes));
        const string resources = "<< /Font << /F1 << /Type /Font /Subtype /TrueType /BaseFont /ArialMT /FontDescriptor << /FontName /ArialMT /FontFile2 5 0 R >> >> >> >>";

        var design = await ImportDesignFromSinglePageContentAsync(
            "BT /F1 12 Tf 1 0 0 1 20 120 Tm (Embedded) Tj ET",
            resources,
            [fontFile]);

        var text = Assert.Single(Assert.Single(design.Pages).Elements, static element => element.Type == "text");

        Assert.StartsWith("PxaPdf_ArialMT", StyleValue<string>(text, "fontFamily"));
        Assert.Equal("Arial", StyleValue<string>(text, "fontDisplayName"));
        Assert.Equal("truetype", StyleValue<string>(text, "fontFormat"));
        Assert.Equal($"data:font/ttf;base64,{Convert.ToBase64String(fontBytes)}", StyleValue<string>(text, "fontDataUri"));
    }

    [Fact]
    public async Task CanvasImporterPdfImporter_ShouldNotUseEmbeddedSubsetFontForEditableText()
    {
        var fontBytes = new byte[] { 0x00, 0x01, 0x00, 0x00, 0x41, 0x42, 0x43, 0x44 };
        var fontFile = new SyntheticPdfObject(5, BuildStreamObjectBody(string.Empty, fontBytes));
        var toUnicode = new SyntheticPdfObject(6, BuildStreamObjectBody(string.Empty, Ascii("""
        1 beginbfchar
        <41> <0048>
        endbfchar
        """)));
        const string resources = "<< /Font << /F1 << /Type /Font /Subtype /TrueType /BaseFont /ABCDEF+ArialMT /ToUnicode 6 0 R /FontDescriptor << /FontName /ABCDEF+ArialMT /FontFile2 5 0 R >> >> >> >>";

        var design = await ImportDesignFromSinglePageContentAsync(
            "BT /F1 12 Tf 1 0 0 1 20 120 Tm (A) Tj ET",
            resources,
            [fontFile, toUnicode]);

        var text = Assert.Single(Assert.Single(design.Pages).Elements, static element => element.Type == "text");

        Assert.Equal("H", text.Content);
        Assert.Equal("Arial", StyleValue<string>(text, "fontFamily"));
        Assert.DoesNotContain("fontDataUri", text.Style!.Keys);
        Assert.Contains("subset", StyleValue<string>(text, "pdfEmbeddedFontSkippedReason"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CanvasImporterPdfImporter_ShouldMapVectorRectanglesThroughPrimitiveShapes()
    {
        var design = await ImportDesignFromSinglePageContentAsync("0 0 0 rg 20 30 60 40 re f");

        var shape = Assert.Single(Assert.Single(design.Pages).Elements, static element => element.Type == "shape");

        Assert.Equal(20, shape.X, precision: 1);
        Assert.Equal(60, shape.Width, precision: 1);
        Assert.Equal(40, shape.Height, precision: 1);
        Assert.Equal("Unknown", StyleValue<string>(shape, "pdfClassification"));
    }

    [Fact]
    public async Task CanvasImporterPdfImporter_ShouldPreserveFillOnlyCurvePathsWithoutDefaultStroke()
    {
        var design = await ImportDesignFromSinglePageContentAsync(
            "0.7 0 0.25 rg 10.25 10.5 m 30.75 10.5 30.75 30.25 10.25 30.25 c f");

        var image = Assert.Single(Assert.Single(design.Pages).Elements, static element => element.Type == "image");
        var svg = DecodeDataUriText(image.Content);

        Assert.Equal("fill", image.FitMode);
        Assert.Contains("fill=\"#B20040\"", svg, StringComparison.Ordinal);
        Assert.Contains("stroke=\"none\"", svg, StringComparison.Ordinal);
        Assert.Contains("M 0 19.75", svg, StringComparison.Ordinal);
        Assert.Contains("fill-rule=\"nonzero\"", svg, StringComparison.Ordinal);
        Assert.Contains("preserveAspectRatio=\"none\"", svg, StringComparison.Ordinal);
        Assert.Equal("svg-vector-path", StyleValue<string>(image, "pdfVisualFallback"));
        Assert.Equal(1, StyleValue<int>(image, "pdfPrimitiveCount"));
    }

    [Fact]
    public async Task CanvasImporterPdfImporter_ShouldPreserveVCurveAndYCurveOperatorsInSvg()
    {
        var design = await ImportDesignFromSinglePageContentAsync(
            "0.7 0 0.25 rg 10 10 m 30 10 30 30 v 10 30 10 10 y f");

        var image = Assert.Single(Assert.Single(design.Pages).Elements, static element => element.Type == "image");
        var svg = DecodeDataUriText(image.Content);

        Assert.Contains("C", svg, StringComparison.Ordinal);
        Assert.Contains("fill-rule=\"nonzero\"", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("stroke=\"#000000\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CanvasImporterPdfImporter_ShouldPreserveLineOnlyComplexPathsAsSvg()
    {
        var design = await ImportDesignFromSinglePageContentAsync(
            "0.7 0 0.25 rg 10 10 m 24 10 l 24 30 l 16 22 l 10 30 l h f");

        var image = Assert.Single(Assert.Single(design.Pages).Elements, static element => element.Type == "image");
        var svg = DecodeDataUriText(image.Content);

        Assert.Equal("fill", image.FitMode);
        Assert.Contains("L", svg, StringComparison.Ordinal);
        Assert.Contains("stroke=\"none\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CanvasImporterPdfImporter_ShouldGroupAdjacentVectorGlyphOutlinesIntoOneSvg()
    {
        var design = await ImportDesignFromSinglePageContentAsync(
            "0.7 0 0.25 rg " +
            "10 10 m 24 10 l 24 30 l 16 22 l 10 30 l h f " +
            "28 10 m 42 10 l 42 30 l 34 22 l 28 30 l h f");

        var image = Assert.Single(Assert.Single(design.Pages).Elements, static element => element.Type == "image");
        var svg = DecodeDataUriText(image.Content);

        Assert.Equal("fill", image.FitMode);
        Assert.Equal("VectorArtworkGroup", StyleValue<string>(image, "pdfClassification"));
        Assert.Equal("svg-vector-cluster", StyleValue<string>(image, "pdfVisualFallback"));
        Assert.Equal(2, StyleValue<int>(image, "pdfPrimitiveCount"));
        Assert.Equal(2, CountOccurrences(svg, "<path "));
    }

    [Fact]
    public async Task CanvasImporterPdfImporter_ShouldUseInvariantSvgNumbersUnderGermanCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var german = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentCulture = german;
            CultureInfo.CurrentUICulture = german;

            var design = await ImportDesignFromSinglePageContentAsync(
                "0.7 0 0.25 rg 10.25 10.5 m 24.75 10.5 l 24.75 30.125 l 16.5 22.25 l 10.25 30.125 l h f");

            var image = Assert.Single(Assert.Single(design.Pages).Elements, static element => element.Type == "image");
            var svg = DecodeDataUriText(image.Content);

            Assert.Equal("fill", image.FitMode);
            Assert.Contains("viewBox=\"0 0 14.5 19.625\"", svg, StringComparison.Ordinal);
            Assert.Contains("M 0 19.625", svg, StringComparison.Ordinal);
            Assert.Contains("L 14.5 19.625", svg, StringComparison.Ordinal);
            Assert.DoesNotContain("14,5", svg, StringComparison.Ordinal);
            Assert.DoesNotContain("19,625", svg, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public async Task CanvasImporterPdfImporter_ShouldUseInvariantSvgNumbersForVectorClustersUnderGermanCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var german = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentCulture = german;
            CultureInfo.CurrentUICulture = german;

            var design = await ImportDesignFromSinglePageContentAsync(
                "0.7 0 0.25 rg " +
                "10.25 10.5 m 24.75 10.5 l 24.75 30.125 l 16.5 22.25 l 10.25 30.125 l h f " +
                "28.5 10.5 m 42.75 10.5 l 42.75 30.125 l 34.25 22.25 l 28.5 30.125 l h f");

            var image = Assert.Single(Assert.Single(design.Pages).Elements, static element => element.Type == "image");
            var svg = DecodeDataUriText(image.Content);

            Assert.Equal("fill", image.FitMode);
            Assert.Equal("svg-vector-cluster", StyleValue<string>(image, "pdfVisualFallback"));
            Assert.Equal(2, CountOccurrences(svg, "<path "));
            Assert.Contains("viewBox=\"0 0 32.5 19.625\"", svg, StringComparison.Ordinal);
            Assert.Contains("M 0 19.625", svg, StringComparison.Ordinal);
            Assert.Contains("M 18.25 19.625", svg, StringComparison.Ordinal);
            Assert.DoesNotContain("32,5", svg, StringComparison.Ordinal);
            Assert.DoesNotContain("18,25", svg, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public async Task CanvasImporterPdfImporter_ShouldEmitNonTextElementsBeforeTextElements()
    {
        var design = await ImportDesignFromSinglePageContentAsync(
            "BT /F1 10 Tf 1 0 0 1 25 55 Tm (Inside box) Tj ET 0.9 0.9 0.9 rg 20 40 80 30 re f");

        var elements = Assert.Single(design.Pages).Elements;

        Assert.Equal("shape", elements[0].Type);
        Assert.Equal("text", elements[^1].Type);
        Assert.Equal("Inside box", elements[^1].Content);
    }

    [Fact]
    public async Task CanvasImporterPdfImporter_ShouldMapImageXObjectsThroughPrimitiveImages()
    {
        var imageObject = new SyntheticPdfObject(5, BuildStreamObjectBody(
            " /Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode",
            TinyJpegBytes()));

        var design = await ImportDesignFromSinglePageContentAsync(
            "q 40 0 0 20 30 60 cm /Im1 Do Q",
            "<< /XObject << /Im1 5 0 R >> >>",
            [imageObject]);

        var image = Assert.Single(Assert.Single(design.Pages).Elements, static element => element.Type == "image");

        Assert.Equal(30, image.X, precision: 1);
        Assert.Equal(40, image.Width, precision: 1);
        Assert.StartsWith("data:image/jpeg;base64,", image.Content ?? string.Empty);
        Assert.Equal("Image", StyleValue<string>(image, "pdfClassification"));
    }

    [Fact]
    public async Task CanvasImporterPdfImporter_ShouldClassifyBarcodeBarsAndKeepThemEditable()
    {
        var bars = string.Join(' ', Enumerable.Range(0, 10).Select(static i => $"{10 + i * 3} 20 1 80 re f"));
        var design = await ImportDesignFromSinglePageContentAsync($"0 0 0 rg {bars}");

        var barElements = Assert.Single(design.Pages)
            .Elements
            .Where(static element => element.Type is "rect" or "shape")
            .ToArray();

        Assert.True(barElements.Length >= 8);
        Assert.Contains(barElements, element => StyleValue<string>(element, "pdfClassification") == "LinearBarcode");
    }

    [Fact]
    public async Task CanvasImporterPdfImporter_ShouldPromoteRepeatedHeaderTextToSharedElements()
    {
        var pageOne = "BT /F1 10 Tf 1 0 0 1 20 185 Tm (Header) Tj 1 0 0 1 20 90 Tm (Page one body) Tj ET";
        var pageTwo = "BT /F1 10 Tf 1 0 0 1 20 185 Tm (Header) Tj 1 0 0 1 20 90 Tm (Page two body) Tj ET";

        using var stream = new MemoryStream(BuildTwoPagePdf(pageOne, pageTwo));
        var design = await PdfFileImporter.DoImportAsync(stream, "Shared header test");

        var shared = Assert.Single(design.SharedElements);
        Assert.Equal("Header", shared.Content);
        Assert.Equal(2, design.Pages.Count);
        Assert.DoesNotContain(design.Pages[0].Elements, static element => element.Content == "Header");
        Assert.DoesNotContain(design.Pages[1].Elements, static element => element.Content == "Header");
    }

    private static PdfArray Array(params long[] values)
    {
        return new PdfArray(values.Select(value => new PdfInteger(value)));
    }

    private static async Task<DesignExportDto> ImportDesignFromSinglePageContentAsync(
        string content,
        string resources = "<< /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >>",
        IReadOnlyList<SyntheticPdfObject>? extraObjects = null)
    {
        using var stream = new MemoryStream(BuildSinglePagePdf(content, resources, extraObjects));
        return await PdfFileImporter.DoImportAsync(stream, "Scene graph import test");
    }

    private static byte[] BuildSinglePagePdf(
        string content,
        string resources,
        IReadOnlyList<SyntheticPdfObject>? extraObjects)
    {
        var objects = new List<SyntheticPdfObject>
        {
            new(1, Ascii("<< /Type /Catalog /Pages 2 0 R >>")),
            new(2, Ascii("<< /Type /Pages /Kids [3 0 R] /Count 1 >>")),
            new(3, Ascii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 200] /Resources {resources} /Contents 4 0 R >>")),
            new(4, BuildStreamObjectBody(string.Empty, Ascii(content))),
        };

        if (extraObjects is not null)
        {
            objects.AddRange(extraObjects);
        }

        return BuildPdf(objects);
    }

    private static byte[] BuildTwoPagePdf(string pageOneContent, string pageTwoContent)
    {
        const string resources = "<< /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >>";
        var objects = new List<SyntheticPdfObject>
        {
            new(1, Ascii("<< /Type /Catalog /Pages 2 0 R >>")),
            new(2, Ascii("<< /Type /Pages /Kids [3 0 R 5 0 R] /Count 2 >>")),
            new(3, Ascii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 200] /Resources {resources} /Contents 4 0 R >>")),
            new(4, BuildStreamObjectBody(string.Empty, Ascii(pageOneContent))),
            new(5, Ascii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 200] /Resources {resources} /Contents 6 0 R >>")),
            new(6, BuildStreamObjectBody(string.Empty, Ascii(pageTwoContent))),
        };

        return BuildPdf(objects);
    }

    private static byte[] BuildPdf(IReadOnlyList<SyntheticPdfObject> objects)
    {
        using var output = new MemoryStream();
        AppendAscii(output, "%PDF-1.7\n");

        var offsets = new Dictionary<int, long>();
        foreach (var obj in objects.OrderBy(static obj => obj.Number))
        {
            offsets[obj.Number] = output.Position;
            AppendAscii(output, $"{obj.Number} 0 obj\n");
            output.Write(obj.Body.Span);
            AppendAscii(output, "\nendobj\n");
        }

        var xrefOffset = output.Position;
        var maxObjectNumber = objects.Max(static obj => obj.Number);
        AppendAscii(output, "xref\n");
        AppendAscii(output, $"0 {maxObjectNumber + 1}\n");
        AppendAscii(output, "0000000000 65535 f \n");

        for (var number = 1; number <= maxObjectNumber; number++)
        {
            if (offsets.TryGetValue(number, out var offset))
            {
                AppendAscii(output, $"{offset:0000000000} 00000 n \n");
            }
            else
            {
                AppendAscii(output, "0000000000 65535 f \n");
            }
        }

        AppendAscii(output, "trailer\n");
        AppendAscii(output, $"<< /Size {maxObjectNumber + 1} /Root 1 0 R >>\n");
        AppendAscii(output, "startxref\n");
        AppendAscii(output, $"{xrefOffset}\n");
        AppendAscii(output, "%%EOF\n");
        return output.ToArray();
    }

    private static byte[] BuildStreamObjectBody(string dictionaryEntries, ReadOnlySpan<byte> streamBytes)
    {
        using var output = new MemoryStream();
        AppendAscii(output, $"<< /Length {streamBytes.Length}{dictionaryEntries} >>\nstream\n");
        output.Write(streamBytes);
        AppendAscii(output, "\nendstream");
        return output.ToArray();
    }

    private static byte[] Ascii(string value)
    {
        return Encoding.ASCII.GetBytes(value);
    }

    private static void AppendAscii(Stream stream, string value)
    {
        stream.Write(Encoding.ASCII.GetBytes(value));
    }

    private static T StyleValue<T>(ElementDto element, string key)
    {
        Assert.NotNull(element.Style);
        Assert.True(element.Style.TryGetValue(key, out var value), $"Missing style key '{key}'.");
        return Assert.IsType<T>(value);
    }

    private static string DecodeDataUriText(string? dataUri)
    {
        Assert.NotNull(dataUri);
        var comma = dataUri.IndexOf(',');
        Assert.True(comma > 0, "Expected a data URI with base64 payload.");
        return Encoding.UTF8.GetString(Convert.FromBase64String(dataUri[(comma + 1)..]));
    }

    private static int CountOccurrences(string value, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }

    private sealed record SyntheticPdfObject(int Number, ReadOnlyMemory<byte> Body);

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

    [Fact]
    public void GraphicsInterpreter_ShouldPreserveTfFontSizeRegardlessOfTextMatrixScale()
    {
        var interpreter = new PdfGraphicsInterpreter();
        var commands = new PdfContentCommand[]
        {
            Command("BT", 1),
            Command("Tf", 2, new PdfName("F1"), new PdfInteger(10)),
            Command("Tm", 3, new PdfNumber(2), new PdfInteger(0), new PdfInteger(0), new PdfNumber(2), new PdfInteger(50), new PdfInteger(100)),
            Command("Tj", 4, new PdfString(Encoding.ASCII.GetBytes("Hi"), IsHex: false))
        };

        var elements = interpreter.Interpret(commands).Cast<PdfTextElement>().ToArray();

        Assert.Single(elements);
        Assert.Equal(10.0, elements[0].FontSize, 6);
    }

    [Fact]
    public void GraphicsInterpreter_ShouldApplyTextMatrixRotation90()
    {
        var interpreter = new PdfGraphicsInterpreter();
        var commands = new PdfContentCommand[]
        {
            Command("BT", 1),
            Command("Tf", 2, new PdfName("F1"), new PdfInteger(12)),
            Command("Tm", 3, new PdfInteger(0), new PdfInteger(1), new PdfInteger(-1), new PdfInteger(0), new PdfInteger(100), new PdfInteger(200)),
            Command("Tj", 4, new PdfString(Encoding.ASCII.GetBytes("R"), IsHex: false))
        };

        var elements = interpreter.Interpret(commands).Cast<PdfTextElement>().ToArray();

        Assert.Single(elements);
        Assert.Equal(12.0, elements[0].FontSize, 6);
        Assert.Equal(90.0, Math.Atan2(elements[0].Transform.B, elements[0].Transform.A) * 180d / Math.PI, 6);
    }

    [Fact]
    public void GraphicsInterpreter_ShouldApplyTextMatrixRotation45WithScale()
    {
        var sqrt2Over2 = Math.Sqrt(2) / 2;
        var interpreter = new PdfGraphicsInterpreter();
        var commands = new PdfContentCommand[]
        {
            Command("BT", 1),
            Command("Tf", 2, new PdfName("F1"), new PdfInteger(10)),
            Command("Tm", 3,
                new PdfNumber(sqrt2Over2 * 3), new PdfNumber(sqrt2Over2 * 3),
                new PdfNumber(-sqrt2Over2 * 3), new PdfNumber(sqrt2Over2 * 3),
                new PdfInteger(50), new PdfInteger(50)),
            Command("Tj", 4, new PdfString(Encoding.ASCII.GetBytes("D"), IsHex: false))
        };

        var elements = interpreter.Interpret(commands).Cast<PdfTextElement>().ToArray();

        Assert.Single(elements);
        Assert.Equal(10.0, elements[0].FontSize, 4);
        Assert.Equal(45.0, Math.Atan2(elements[0].Transform.B, elements[0].Transform.A) * 180d / Math.PI, 4);
    }

    [Fact]
    public async Task DocumentBuilder_AndCanvasPdfGeneratorBridge_ShouldRoundTripCcittFaxXObjectImages()
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

        var decodeParms = new PdfDictionary();
        decodeParms["K"] = new PdfInteger(0);
        decodeParms["Columns"] = new PdfInteger(4);
        decodeParms["Rows"] = new PdfInteger(1);
        decodeParms["EndOfLine"] = new PdfBoolean(false);
        decodeParms["EndOfBlock"] = new PdfBoolean(false);

        var imageDictionary = new PdfDictionary();
        imageDictionary["Type"] = new PdfName("XObject");
        imageDictionary["Subtype"] = new PdfName("Image");
        imageDictionary["Width"] = new PdfInteger(4);
        imageDictionary["Height"] = new PdfInteger(1);
        imageDictionary["ColorSpace"] = new PdfName("DeviceGray");
        imageDictionary["BitsPerComponent"] = new PdfInteger(1);
        imageDictionary["Filter"] = new PdfName("CCITTFaxDecode");
        imageDictionary["DecodeParms"] = decodeParms;
        // 4 white pixels (Group 3 1D): white run=4 → code "1011" (4 bits) → 0xB0
        byte[] ccittData = [0xB0];
        var imageStream = new PdfStreamObject(imageDictionary, ccittData);

        graph.Add(new PdfIndirectObject(new PdfObjectId(1, 0), catalog, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(2, 0), pages, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(3, 0), page, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(4, 0), contentStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(5, 0), imageStream, new PdfSourceSpan(0, 1)));

        var builder = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter());
        var document = builder.Build(graph);

        Assert.IsType<PdfImageElement>(Assert.Single(Assert.Single(document.Pages).GraphicsObjects));

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedImage = Assert.IsType<PdfImageElement>(Assert.Single(Assert.Single(reimported.Pages).GraphicsObjects));
        Assert.False(importedImage.ImageBytes.IsEmpty);
    }

    [Fact]
    public async Task DocumentBuilder_AndCanvasPdfGeneratorBridge_ShouldRoundTripIndexedColorSpaceImages()
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

        // Indexed/DeviceGray: index 0 → gray 0x00, index 1 → gray 0xFF
        var palette = new PdfString(new byte[] { 0x00, 0xFF }, IsHex: false);
        var colorSpace = new PdfArray([new PdfName("Indexed"), new PdfName("DeviceGray"), new PdfInteger(1), palette]);

        var imageDictionary = new PdfDictionary();
        imageDictionary["Type"] = new PdfName("XObject");
        imageDictionary["Subtype"] = new PdfName("Image");
        imageDictionary["Width"] = new PdfInteger(2);
        imageDictionary["Height"] = new PdfInteger(1);
        imageDictionary["ColorSpace"] = colorSpace;
        imageDictionary["BitsPerComponent"] = new PdfInteger(8);
        imageDictionary["Filter"] = new PdfName("FlateDecode");
        // Pixel 0 → index 0 (black), Pixel 1 → index 1 (white)
        var encodedImageBytes = Compress([0x00, 0x01]);
        var imageStream = new PdfStreamObject(imageDictionary, encodedImageBytes);

        graph.Add(new PdfIndirectObject(new PdfObjectId(1, 0), catalog, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(2, 0), pages, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(3, 0), page, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(4, 0), contentStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(5, 0), imageStream, new PdfSourceSpan(0, 1)));

        var builder = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter());
        var document = builder.Build(graph);

        Assert.IsType<PdfImageElement>(Assert.Single(Assert.Single(document.Pages).GraphicsObjects));

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedImage = Assert.IsType<PdfImageElement>(Assert.Single(Assert.Single(reimported.Pages).GraphicsObjects));
        Assert.False(importedImage.ImageBytes.IsEmpty);
    }

    [Fact]
    public async Task BarcodeRoundTrip_ShouldPreserveVectorPath_CcittImage_AndScaledText()
    {
        // Builds a synthetic "barcode page" covering all three barcode representations:
        // 1. Vector-path bars (filled rectangles)
        // 2. CCITT-encoded 1-bit image barcode
        // 3. Text rendered with a scaled text matrix (barcode font character)

        var graph = new PdfObjectGraph();

        var catalog = new PdfDictionary();
        catalog["Type"] = new PdfName("Catalog");
        catalog["Pages"] = new PdfReference(new PdfObjectId(2, 0));

        var pages = new PdfDictionary();
        pages["Type"] = new PdfName("Pages");
        pages["Count"] = new PdfInteger(1);
        pages["Kids"] = new PdfArray([new PdfReference(new PdfObjectId(3, 0))]);

        var xObjectResources = new PdfDictionary();
        xObjectResources["ImBarcode"] = new PdfReference(new PdfObjectId(5, 0));

        var resources = new PdfDictionary();
        resources["XObject"] = xObjectResources;

        var page = new PdfDictionary();
        page["Type"] = new PdfName("Page");
        page["Parent"] = new PdfReference(new PdfObjectId(2, 0));
        page["Resources"] = resources;
        page["MediaBox"] = Array(0, 0, 400, 200);
        page["Contents"] = new PdfReference(new PdfObjectId(4, 0));

        // Content stream: vector bars + CCITT image + scaled text
        var contentBytes = Encoding.ASCII.GetBytes(
            "0 g " +                                     // fill black
            "10 10 5 80 re f " +                         // bar 1
            "20 10 5 80 re f " +                         // bar 2
            "q 40 0 0 20 50 80 cm /ImBarcode Do Q " +    // CCITT image
            "BT /F1 8 Tf 2 0 0 2 100 50 Tm (X) Tj ET"); // scaled barcode-font text
        var contentStream = new PdfStreamObject(new PdfDictionary(), contentBytes);

        // CCITT 1-bit image: 4 white pixels, Group 3 1D, no EOL/EOB
        var decodeParms = new PdfDictionary();
        decodeParms["K"] = new PdfInteger(0);
        decodeParms["Columns"] = new PdfInteger(4);
        decodeParms["Rows"] = new PdfInteger(1);
        decodeParms["EndOfLine"] = new PdfBoolean(false);
        decodeParms["EndOfBlock"] = new PdfBoolean(false);

        var imageDictionary = new PdfDictionary();
        imageDictionary["Type"] = new PdfName("XObject");
        imageDictionary["Subtype"] = new PdfName("Image");
        imageDictionary["Width"] = new PdfInteger(4);
        imageDictionary["Height"] = new PdfInteger(1);
        imageDictionary["ColorSpace"] = new PdfName("DeviceGray");
        imageDictionary["BitsPerComponent"] = new PdfInteger(1);
        imageDictionary["Filter"] = new PdfName("CCITTFaxDecode");
        imageDictionary["DecodeParms"] = decodeParms;
        byte[] ccittData = [0xB0]; // 4 white pixels
        var imageStream = new PdfStreamObject(imageDictionary, ccittData);

        graph.Add(new PdfIndirectObject(new PdfObjectId(1, 0), catalog, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(2, 0), pages, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(3, 0), page, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(4, 0), contentStream, new PdfSourceSpan(0, 1)));
        graph.Add(new PdfIndirectObject(new PdfObjectId(5, 0), imageStream, new PdfSourceSpan(0, 1)));

        var builder = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter());
        var document = builder.Build(graph);

        var builtPage = Assert.Single(document.Pages);
        var paths = builtPage.GraphicsObjects.OfType<PdfPathElement>().ToList();
        var images = builtPage.GraphicsObjects.OfType<PdfImageElement>().ToList();
        var texts = builtPage.TextObjects.ToList();
        Assert.Equal(2, paths.Count);
        Assert.Single(images);
        Assert.Single(texts);

        // Scaled text: Tf=8, transform carries scale in matrix A/D; FontSize stores raw Tf value
        Assert.Equal(8.0, texts[0].FontSize, 4);

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();
        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedPage = Assert.Single(reimported.Pages);
        Assert.Equal(2, importedPage.GraphicsObjects.OfType<PdfPathElement>().Count());
        Assert.Single(importedPage.GraphicsObjects.OfType<PdfImageElement>());
        Assert.Equal("X", Assert.Single(importedPage.TextObjects).Text);
        Assert.False(importedPage.GraphicsObjects.OfType<PdfImageElement>().Single().ImageBytes.IsEmpty);
    }
    [Fact]
    public void MatrixEngine_ShouldTransformRotatedBoundsAndExtractRotation()
    {
        var matrix = MatrixEngine.Translate(100, 50).Multiply(MatrixEngine.Rotate(90));

        var point = MatrixEngine.TransformPoint(new PdfPoint(10, 0), matrix);
        var bounds = MatrixEngine.TransformBounds(new PdfRectangle(0, 0, 10, 20), matrix);

        Assert.Equal(-50, point.X, precision: 6);
        Assert.Equal(110, point.Y, precision: 6);
        Assert.Equal(90, MatrixEngine.ExtractRotationDegrees(matrix), precision: 6);
        Assert.Equal(20, bounds.Width, precision: 6);
        Assert.Equal(10, bounds.Height, precision: 6);
    }

    [Fact]
    public async Task CanvasPdfGeneratorBridge_ShouldNormalizeFlippedRectangleBounds()
    {
        var document = new PdfDocumentModel();
        var page = new PdfPageModel(null, new PdfDictionary())
        {
            MediaBox = new PdfRectangle(0, 0, 200, 120)
        };

        page.Insert(new PdfPathElement(1, new PdfMatrix(1, 0, 0, -1, 12, 38), Command("f", 1),
        [
            new RectangleSegment(new PdfRectangle(0, 0, 40, 20))
        ])
        {
            FillColor = new PdfColor(0.2, 0.4, 0.6, 1, PdfColorSpace.DeviceRgb),
            StrokeColor = new PdfColor(0, 0, 0, 1, PdfColorSpace.DeviceGray),
            LineWidth = 2
        });

        document.AddPage(page);

        var bridge = new PxaPdfGeneratorBridge();
        await using var output = new MemoryStream();

        await bridge.RegenerateAsync(document, output);

        output.Position = 0;
        var reimported = await new PdfImporter().LoadAsync(output);

        var importedPath = Assert.IsType<PdfPathElement>(Assert.Single(Assert.Single(reimported.Pages).GraphicsObjects));
        var rectangle = Assert.IsType<RectangleSegment>(Assert.Single(importedPath.Segments));

        Assert.Equal(new PdfRectangle(12, 18, 40, 20), rectangle.Rectangle);
    }

    [Fact]
    public void PrimitiveBuilder_ShouldComputeRotatedTextGeometry()
    {
        var text = new PdfTextElement(
            1,
            MatrixEngine.Translate(100, 100).Multiply(MatrixEngine.Rotate(90)),
            Command("Tj", 1, new PdfString(Encoding.ASCII.GetBytes("Rotated"), IsHex: false)),
            "Rotated")
        {
            FontSize = 12,
            FillColor = PdfColor.Black,
            StrokeColor = PdfColor.Black
        };

        var primitive = Assert.IsType<PrimitiveText>(Assert.Single(new PrimitiveBuilder().Build([text])));

        Assert.Equal(90, primitive.Geometry.RotationDegrees, precision: 6);
        Assert.True(primitive.Bounds.Width > 0);
        Assert.True(primitive.Bounds.Height > 0);
        Assert.True(Math.Abs(primitive.Geometry.Baseline.Length - 1) < 0.0001);
    }

    [Fact]
    public void ReadingOrderEngine_ShouldSortByGeometryInsteadOfDrawOrder()
    {
        var lower = BuildPrimitiveText("Second", 1, 10, 20);
        var upper = BuildPrimitiveText("First", 2, 10, 80);

        var result = new ReadingOrderEngine().Analyze([lower, upper]);

        Assert.Equal(["First", "Second"], result.Lines.Select(static line => line.Text).ToArray());
    }

    [Fact]
    public void ObjectClassifier_ShouldDetectLinearBarcodeFromRepeatedBars()
    {
        var bars = Enumerable.Range(0, 10)
            .Select(i => BuildPrimitiveShape(i, i * 4, 0, 1, 50))
            .Cast<PrimitiveObject>()
            .ToArray();

        new ObjectClassifier().Classify(bars);

        Assert.All(bars, primitive => Assert.Equal(PrimitiveClassification.LinearBarcode, primitive.Classification));
    }

    [Fact]
    public void SceneGraphEngine_ShouldBuildSemanticLayoutGroupsAndDebugOverlays()
    {
        var page = new PdfPageModel(null, new PdfDictionary())
        {
            MediaBox = new PdfRectangle(0, 0, 300, 200)
        };

        page.GraphicsObjects.Add(new PdfTextElement(1, MatrixEngine.Translate(20, 160), Command("Tj", 1, new PdfString(Encoding.ASCII.GetBytes("Name:"), false)), "Name:")
        {
            FontSize = 12,
            FillColor = PdfColor.Black,
            StrokeColor = PdfColor.Black
        });
        page.GraphicsObjects.Add(new PdfTextElement(2, MatrixEngine.Translate(70, 160), Command("Tj", 2, new PdfString(Encoding.ASCII.GetBytes("Ada"), false)), "Ada")
        {
            FontSize = 12,
            FillColor = PdfColor.Black,
            StrokeColor = PdfColor.Black
        });

        var scenePage = new SceneGraphEngine().BuildPage(0, page);
        var overlays = new PdfDebugOverlayBuilder().Build(scenePage);

        Assert.NotNull(scenePage.ReadingOrder);
        Assert.Contains(scenePage.VisualGroups, group => group.Kind == "LabelValue");
        Assert.NotNull(scenePage.Layout);
        Assert.Contains(overlays, overlay => overlay.Kind == PdfDebugOverlayKind.Bounds);
    }

    private static PrimitiveText BuildPrimitiveText(string text, int zOrder, double x, double y)
    {
        var element = new PdfTextElement(zOrder, MatrixEngine.Translate(x, y), Command("Tj", zOrder, new PdfString(Encoding.ASCII.GetBytes(text), false)), text)
        {
            FontSize = 10,
            FillColor = PdfColor.Black,
            StrokeColor = PdfColor.Black
        };

        return Assert.IsType<PrimitiveText>(Assert.Single(new PrimitiveBuilder().Build([element])));
    }

    private static PrimitiveShape BuildPrimitiveShape(int zOrder, double x, double y, double width, double height)
    {
        var element = new PdfPathElement(
            zOrder,
            PdfMatrix.Identity,
            Command("f", zOrder),
            [new RectangleSegment(new PdfRectangle(x, y, width, height))])
        {
            FillColor = PdfColor.Black,
            StrokeColor = PdfColor.Black,
            LineWidth = 1
        };

        return Assert.IsType<PrimitiveShape>(Assert.Single(new PrimitiveBuilder().Build([element])));
    }
}

#pragma warning restore PXA0002
