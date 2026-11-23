using System.IO.Compression;
using System.Text;
using PVAD.Importer.Content;
using PVAD.Importer.Graphics;
using PVAD.Importer.Objects;
using PVAD.Importer.Streams;

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
}
