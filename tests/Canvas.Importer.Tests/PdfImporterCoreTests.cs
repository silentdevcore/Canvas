using System.IO.Compression;
using System.Text;
using Canvas.Importer.Document;
using Canvas.Importer.Content;
using Canvas.Importer.Graphics;
using Canvas.Importer.Editing;
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

        var inlineImageCommand = Assert.Single(commands.Where(command => command.Operator.Name == "BI"));
        var inlineStream = Assert.IsType<PdfStreamObject>(Assert.Single(inlineImageCommand.Operands));
        Assert.Equal(1, Assert.IsType<PdfInteger>(inlineStream.Dictionary["W"]).Value);
        Assert.Equal(1, Assert.IsType<PdfInteger>(inlineStream.Dictionary["H"]).Value);
        Assert.Equal(new byte[] { 0x7f }, inlineStream.EncodedBytes.ToArray());

        var image = Assert.IsType<PdfImageElement>(Assert.Single(interpreter.Interpret(commands)));
        Assert.Equal(new byte[] { 0x7f }, image.ImageBytes.ToArray());
        Assert.Equal(PdfMatrix.Identity.Multiply(new PdfMatrix(1, 0, 0, 1, 10, 20)), image.Transform);
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

    private static PdfArray Array(params long[] values)
    {
        return new PdfArray(values.Select(value => new PdfInteger(value)));
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
