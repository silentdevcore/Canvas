using System.IO.Compression;
using System.Text;
using PVAD.Importer.Content;
using PVAD.Importer.Fonts;
using PVAD.Importer.Graphics;
using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Streams;
using PVAD.Importer.Tokenizer;
using PVAD.Importer.Xref;

namespace PVAD.Importer.Tests;

public sealed class PdfMalformedContentTests
{
    [Theory]
    [InlineData("Tj")]
    [InlineData("'")]
    [InlineData("\"")]
    [InlineData("0 \"")]
    [InlineData("0 0 \"")]
    [InlineData("TJ")]
    [InlineData("42 Tj")]
    [InlineData("/Name '")]
    [InlineData("(text) TJ")]
    [InlineData("[(text) /Name] TJ")]
    [InlineData("/Name 0 (text) \"")]
    public void TextOperators_RejectMissingOrInvalidOperands(string content)
    {
        var commands = new PdfContentStreamParser().Parse(Encoding.ASCII.GetBytes(content));

        Assert.Throws<FormatException>(() => new PdfGraphicsInterpreter().Interpret(commands));
    }

    [Theory]
    [InlineData("() Tj")]
    [InlineData("() '")]
    [InlineData("0 0 () \"")]
    [InlineData("[] TJ")]
    [InlineData("[(a) -20 (b)] TJ")]
    public void TextOperators_StillAcceptValidEmptyStringsAndTextArrays(string content)
    {
        var commands = new PdfContentStreamParser().Parse(Encoding.ASCII.GetBytes(content));

        Assert.IsType<PdfTextElement>(Assert.Single(new PdfGraphicsInterpreter().Interpret(commands)));
    }

    [Theory]
    [InlineData(0x20)]
    [InlineData(0xbb)]
    public void FlateDecoder_RejectsPresetDictionaryFlagAsInvalidInput(byte flags)
    {
        byte[] encoded = [0x78, flags, 0, 0, 0, 1, 0x03, 0];

        Assert.Throws<InvalidDataException>(() => new FlateDecodeStreamDecoder().Decode(encoded, new PdfDictionary()));
    }

    [Fact]
    public void FlateDecoder_StillDecodesOrdinaryZlibData()
    {
        byte[] expected = Encoding.ASCII.GetBytes("BT (valid text) Tj ET");
        using var output = new MemoryStream();
        using (var compressor = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            compressor.Write(expected);

        Assert.Equal(expected, new FlateDecodeStreamDecoder().Decode(output.ToArray(), new PdfDictionary()).ToArray());
    }

    [Theory]
    [InlineData("123455555555555555555555555556")]
    [InlineData("9223372036854775807")]
    [InlineData("-9223372036854775808")]
    public void StreamLength_BeyondTheSourceFallsBackToEndStreamWithoutOverflow(string length)
    {
        var pdf = Encoding.Latin1.GetBytes(
            $"%PDF-1.7\n1 0 obj\n<</Length {length}>>\nstream\nabc\nendstream\nendobj\ntrailer\n<</Root 1 0 R>>\n%%EOF\n");
        var context = new PdfParseContext(pdf, new PdfImporterOptions());
        var graph = new PdfObjectParser(context, new PdfCrossReferenceParser().Parse(context)).ParseDocumentGraph();

        var stream = Assert.IsType<PdfStreamObject>(Assert.Single(graph.Objects.Values).Value);
        Assert.Equal("abc\n", Encoding.Latin1.GetString(stream.EncodedBytes.Span));
    }

    [Theory]
    [InlineData("<0000> <FFFFFFFF> <0041>")]
    [InlineData("<00000000> <7FFFFFFF> <0041>")]
    [InlineData("<80000000> <FFFFFFFF> <0041>")]
    public void ToUnicodeCMap_RejectsRangeExpansionBeyondTheMappingBudget(string range)
    {
        var exception = Assert.Throws<PdfParseLimitExceededException>(
            () => new PdfToUnicodeCMapParser().Parse(CreateCMap($"1 beginbfrange\n{range}\nendbfrange")));

        Assert.Equal("PVADPDFPARSE012", exception.Code);
    }

    [Fact]
    public void ToUnicodeCMap_RejectsCumulativeRangesBeyondTheMappingBudget()
    {
        var ranges = string.Join('\n', Enumerable.Range(0, 5).Select(index => $"<{index:X2}0000> <{index:X2}FFFF> <0041>"));

        var exception = Assert.Throws<PdfParseLimitExceededException>(
            () => new PdfToUnicodeCMapParser().Parse(CreateCMap($"5 beginbfrange\n{ranges}\nendbfrange")));

        Assert.Equal("PVADPDFPARSE012", exception.Code);
    }

    [Theory]
    [InlineData("1 beginbfchar\n<0000000001> <0041>\nendbfchar")]
    [InlineData("1 beginbfchar\n<> <0041>\nendbfchar")]
    [InlineData("1 beginbfrange\n<00> <0000000001> <0041>\nendbfrange")]
    [InlineData("1 beginbfchar\n<0G> <0041>\nendbfchar")]
    [InlineData("1 beginbfrange\n<00> <02> <S0>\nendbfrange")]
    public void ToUnicodeCMap_RejectsInvalidCodes(string section)
    {
        Assert.Throws<FormatException>(() => new PdfToUnicodeCMapParser().Parse(CreateCMap(section)));
    }

    [Fact]
    public void ToUnicodeCMap_StillMapsCompleteTwoByteRangeAndHighFourByteCodes()
    {
        var map = new PdfToUnicodeCMapParser().Parse(CreateCMap(
            "2 beginbfrange\n<0000> <FFFF> <4E00>\n<FFFFFFFE> <FFFFFFFF> <0061>\nendbfrange"));

        Assert.Equal("\u4E00", map.Decode([0x00, 0x00]));
        Assert.Equal("\u4E01", map.Decode([0x00, 0x01]));
        Assert.Equal("b", map.Decode([0xFF, 0xFF, 0xFF, 0xFF]));
    }

    [Theory]
    [InlineData("<")]
    [InlineData("(")]
    [InlineData("<414")]
    [InlineData("(abc")]
    [InlineData("(a(b)")]
    [InlineData("(abc\\)")]
    public void UnterminatedStringsAtEndOfAnObject_AreRejectedInsteadOfTruncated(string tail)
    {
        var pdf = Encoding.Latin1.GetBytes($"%PDF-1.7\n1 0 obj\n[{tail}");
        var context = new PdfParseContext(pdf, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);

        Assert.Throws<FormatException>(() => new PdfObjectParser(context, table).ParseDocumentGraph());
    }

    [Theory]
    [InlineData("<", "")]
    [InlineData("(", "")]
    [InlineData("<414", "414")]
    [InlineData("(abc", "abc")]
    [InlineData("(a(b)", "a(b)")]
    [InlineData("(abc\\)", "abc\\)")]
    public void UnterminatedFinalContentStrings_KeepEveryAvailableByte(string tail, string expected)
    {
        var tokenizer = new PdfTokenizer(Encoding.Latin1.GetBytes(tail));
        Assert.Equal(expected, Encoding.Latin1.GetString(tokenizer.ReadToken().AvailableStringContent().Span));

        var command = Assert.Single(new PdfContentStreamParser().Parse(Encoding.Latin1.GetBytes("BT " + tail)));
        Assert.Equal("BT", command.Operator.Name);
    }

    [Fact]
    public void TerminatedContentStrings_KeepTheirExistingContent()
    {
        var tokenizer = new PdfTokenizer("(a(b)c) <4142>"u8);

        Assert.Equal("a(b)c", Encoding.Latin1.GetString(tokenizer.ReadToken().AvailableStringContent().Span));
        Assert.Equal("4142", Encoding.Latin1.GetString(tokenizer.ReadToken().AvailableStringContent().Span));
    }

    [Theory]
    [InlineData("<>", "")]
    [InlineData("()", "")]
    [InlineData("<4142>", "AB")]
    [InlineData("(a(b)c)", "a(b)c")]
    [InlineData("(a\\)b)", "a\\)b")]
    public void TerminatedStrings_StillParseAtEndOfInput(string operand, string expected)
    {
        var command = Assert.Single(new PdfContentStreamParser().Parse(Encoding.Latin1.GetBytes(operand + " Tj")));

        var value = Assert.IsType<PdfString>(Assert.Single(command.Operands));
        Assert.Equal(expected, Encoding.Latin1.GetString(value.GetDecodedBytes().Span));
    }

    [Theory]
    [InlineData(0, int.MaxValue)]
    [InlineData(int.MaxValue - 1, int.MaxValue)]
    [InlineData(-2_000_000_000, 2_000_000_000)]
    public void CidWidths_RejectRangeExpansionBeyondTheWidthBudget(int first, int last)
    {
        var start = DateTime.UtcNow;
        PdfObject[] widths = first == int.MaxValue - 1
            ? [.. Enumerable.Range(0, 3).SelectMany(_ => new PdfObject[] { new PdfInteger(0), new PdfInteger(99_999), new PdfInteger(500) }),
                new PdfInteger(first), new PdfInteger(last), new PdfInteger(500)]
            : [new PdfInteger(first), new PdfInteger(last), new PdfInteger(500)];

        var exception = Assert.Throws<PdfParseLimitExceededException>(() => ParseCidFont(widths));

        Assert.Equal("PVADPDFPARSE013", exception.Code);
        Assert.True(DateTime.UtcNow - start < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void CidWidths_EndingAtIntMaxValueTerminates()
    {
        var font = ParseCidFont([new PdfInteger(int.MaxValue - 1), new PdfInteger(int.MaxValue), new PdfInteger(700)]);

        Assert.Equal(700, font.Widths[int.MaxValue - 1]);
        Assert.Equal(700, font.Widths[int.MaxValue]);
    }

    [Fact]
    public void CidWidths_StillExpandCompleteCidRangeAndExplicitWidths()
    {
        var font = ParseCidFont([
            new PdfInteger(0), new PdfInteger(65_535), new PdfInteger(1000),
            new PdfInteger(3), new PdfArray([new PdfInteger(250), new PdfInteger(300)])
        ]);

        Assert.Equal(65_536, font.Widths.Count);
        Assert.Equal(1000, font.Widths[65_535]);
        Assert.Equal(250, font.Widths[3]);
        Assert.Equal(300, font.Widths[4]);
    }

    private static PdfFontResource ParseCidFont(PdfObject[] widths) => new PdfSimpleFontParser().Parse(
        "F1",
        new PdfDictionary
        {
            ["Type"] = new PdfName("Font"),
            ["Subtype"] = new PdfName("Type0"),
            ["DescendantFonts"] = new PdfArray([new PdfDictionary
            {
                ["Type"] = new PdfName("Font"),
                ["Subtype"] = new PdfName("CIDFontType2"),
                ["W"] = new PdfArray(widths)
            }])
        },
        new IdentityResolver());

    private sealed class IdentityResolver : IPdfObjectResolver
    {
        public PdfObject? Resolve(PdfObject value) => value;
    }

    [Theory]
    [InlineData("%PDF-1.7\ntrailer\n<</Root (")]
    [InlineData("%PDF-1.7\nxref\n0 1\n0000000000 65535 f \ntrailer\n<</Size 1 /ID [<")]
    public async Task TrustInspector_ReportsInvalidStructureWhenTheCrossReferenceCannotBeRead(string text)
    {
        var inspection = await new PVAD.Importer.Trust.PdfTrustInspector().InspectAsync(
            new MemoryStream(Encoding.Latin1.GetBytes(text), writable: false));

        Assert.Equal(PVAD.Importer.Trust.PdfTrustStatus.Invalid, inspection.StructureStatus);
    }

    private static byte[] CreateCMap(string sections) => Encoding.ASCII.GetBytes(
        $"/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n{sections}\nendcmap\nend\nend\n");
}
