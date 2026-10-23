using System.IO.Compression;
using System.Text;
using PVAD.Importer;
using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Streams;
using PVAD.Importer.Tokenizer;
using PVAD.Importer.Trust;
using PVAD.Importer.Xref;

namespace PVAD.Importer.Tests;

/// <summary>
/// Each fixture here is hostile input that previously exhausted memory or the stack.
/// The assertions prove the parser is bounded, not that it recovers the content.
/// </summary>
public sealed class PdfParserHardeningTests
{
    [Fact]
    public void ParseDocumentGraph_ShouldBoundDeeplyNestedArrays()
    {
        var pdf = CreatePdfWithObject(string.Concat(Enumerable.Repeat("[", 20_000)) +
            string.Concat(Enumerable.Repeat("]", 20_000)));

        var (context, graph) = ParseGraph(pdf);

        Assert.Empty(graph.Objects);
        Assert.Contains(context.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFPARSE001");
    }

    [Fact]
    public void ParseDocumentGraph_ShouldBoundDeeplyNestedDictionaries()
    {
        var pdf = CreatePdfWithObject(string.Concat(Enumerable.Repeat("<</A ", 20_000)) +
            "null" + string.Concat(Enumerable.Repeat(">>", 20_000)));

        var (context, graph) = ParseGraph(pdf);

        Assert.Empty(graph.Objects);
        Assert.Contains(context.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFPARSE001");
    }

    [Fact]
    public void ParseDocumentGraph_ShouldBoundArrayElementCount()
    {
        var pdf = CreatePdfWithObject("[" + string.Join(' ', Enumerable.Repeat("0", 5_000)) + "]");
        var limits = new PdfParseLimits { MaxArrayItemCount = 100 };

        var (context, graph) = ParseGraph(pdf, limits);

        Assert.Empty(graph.Objects);
        Assert.Contains(context.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFPARSE002");
    }

    [Fact]
    public void ParseDocumentGraph_ShouldNotRecurseForeverOnCyclicLengthReferences()
    {
        var pdf = new StringBuilder()
            .Append("%PDF-1.7\n")
            .Append("1 0 obj\n<</Length 2 0 R>>\nstream\nabc\nendstream\nendobj\n")
            .Append("2 0 obj\n<</Length 1 0 R>>\nstream\ndef\nendstream\nendobj\n")
            .Append("trailer\n<</Root 1 0 R>>\n%%EOF\n")
            .ToString();

        var (context, graph) = ParseGraph(Encoding.Latin1.GetBytes(pdf));

        // The cycle is broken and reported; the objects themselves still parse.
        Assert.Contains(context.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFPARSE006");
        Assert.NotEmpty(graph.Objects);
    }

    [Fact]
    public void ParseDocumentGraph_ShouldRejectAnInflatedObjectStreamCount()
    {
        var pdf = CreatePdfWithObject("<</Type /ObjStm /N 2000000000 /First 10 /Length 4>>\nstream\nabcd\nendstream");
        var context = new PdfParseContext(pdf, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        table.Add(new PdfCrossReferenceEntry(new PdfObjectId(9, 0), Offset: -1, Generation: 0, IsFree: false, Revision: 0,
            PdfCrossReferenceEntryKind.Compressed, ObjectStreamNumber: 1, ObjectStreamIndex: 0));

        new PdfObjectParser(context, table).ParseDocumentGraph();

        Assert.Contains(context.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFPARSE004");
    }

    [Fact]
    public void FlateDecoder_ShouldRejectADecompressionBomb()
    {
        // 10 MB of zeros compresses to a few kilobytes; the default ratio and floor put the
        // budget far below the decoded size.
        var bomb = Deflate(new byte[10 * 1024 * 1024]);
        var decoder = new FlateDecodeStreamDecoder(new PdfParseLimits());

        var exception = Assert.Throws<PdfParseLimitExceededException>(
            () => decoder.Decode(bomb, new PdfDictionary()));

        Assert.Equal("PXAPDFPARSE008", exception.Code);
    }

    [Fact]
    public void FlateDecoder_ShouldStillDecodeStreamsWithinTheBudget()
    {
        var payload = Encoding.ASCII.GetBytes(new string('x', 4096));

        var decoded = new FlateDecodeStreamDecoder(new PdfParseLimits())
            .Decode(Deflate(payload), new PdfDictionary());

        Assert.Equal(payload, decoded.ToArray());
    }

    [Fact]
    public void FlateDecoder_ShouldAllowSmallStreamsToExpandAboveTheRatio()
    {
        // A 40-byte stream expanding to 512 KB exceeds the 200:1 ratio but stays under the
        // floor, so the ratio rule alone must not reject it.
        var payload = new byte[512 * 1024];
        var encoded = Deflate(payload);
        Assert.True(encoded.Length * 200 < payload.Length);

        var decoded = new FlateDecodeStreamDecoder(new PdfParseLimits())
            .Decode(encoded, new PdfDictionary());

        Assert.Equal(payload.Length, decoded.Length);
    }

    [Fact]
    public void LzwDecoder_ShouldRejectOutputAboveTheBudget()
    {
        var limits = new PdfParseLimits { MaxDecodedStreamBytes = 8, MinimumDecodedStreamBytes = 8 };
        // 64 bytes of 9-bit literal codes decode to ~56 bytes, above the 8-byte cap.
        var encoded = new byte[64];

        var exception = Assert.Throws<PdfParseLimitExceededException>(
            () => new LzwStreamDecoder(limits).Decode(encoded, new PdfDictionary()));

        Assert.Equal("PXAPDFPARSE009", exception.Code);
    }

    [Theory]
    [InlineData(2_000_000_000, 1)]
    [InlineData(1728, 2_000_000_000)]
    public void CcittDecoder_ShouldRejectHostileDimensionsBeforeAllocating(int columns, int rows)
    {
        var dictionary = new PdfDictionary
        {
            ["DecodeParms"] = new PdfDictionary
            {
                ["Columns"] = new PdfInteger(columns),
                ["Rows"] = new PdfInteger(rows)
            }
        };

        var exception = Assert.Throws<PdfParseLimitExceededException>(
            () => new CcittFaxStreamDecoder(new PdfParseLimits()).Decode(new byte[] { 0 }, dictionary));

        Assert.Equal("PXAPDFPARSE011", exception.Code);
    }

    [Fact]
    public void CcittDecoder_ShouldRejectDeclaredOutputAboveTheStreamBudget()
    {
        var limits = new PdfParseLimits
        {
            MaxDecodedStreamBytes = 8,
            MinimumDecodedStreamBytes = 8
        };
        var dictionary = new PdfDictionary
        {
            ["DecodeParms"] = new PdfDictionary
            {
                ["Columns"] = new PdfInteger(1728),
                ["Rows"] = new PdfInteger(1)
            }
        };

        var exception = Assert.Throws<PdfParseLimitExceededException>(
            () => new CcittFaxStreamDecoder(limits).Decode(new byte[] { 0 }, dictionary));

        Assert.Equal("PXAPDFPARSE011", exception.Code);
    }

    [Theory]
    [InlineData(2_000_000_000, 1, 8)]
    [InlineData(1, 0, 8)]
    [InlineData(1, 1, 0)]
    public void Predictor_ShouldRejectOutOfRangeParameters(int columns, int colors, int bitsPerComponent)
    {
        var dictionary = new PdfDictionary
        {
            ["DecodeParms"] = new PdfDictionary
            {
                ["Predictor"] = new PdfInteger(12),
                ["Columns"] = new PdfInteger(columns),
                ["Colors"] = new PdfInteger(colors),
                ["BitsPerComponent"] = new PdfInteger(bitsPerComponent)
            }
        };

        Assert.Throws<PdfParseLimitExceededException>(
            () => PdfPredictor.Decode(new byte[16], dictionary, new PdfParseLimits()));
    }

    [Fact]
    public async Task Inspect_ShouldReportDeepNestingWithoutCrashingTheProcess()
    {
        var pdf = CreatePdfWithObject(string.Concat(Enumerable.Repeat("[", 50_000)) +
            string.Concat(Enumerable.Repeat("]", 50_000)));

        await using var stream = new MemoryStream(pdf);
        var report = await new PdfTrustInspector().InspectAsync(stream);

        Assert.Equal(PdfTrustStatus.Invalid, report.StructureStatus);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFPARSE001");
    }

    [Fact]
    public void ParseDocumentGraph_ShouldObserveCancellation()
    {
        var pdf = CreatePdfWithObject("<</A 1>>");
        var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new PdfParseContext(pdf, new PdfImporterOptions(), cancellation.Token);
        var table = new PdfCrossReferenceParser().Parse(context);

        Assert.Throws<OperationCanceledException>(
            () => new PdfObjectParser(context, table).ParseDocumentGraph());
    }

    [Theory]
    [InlineData(">")]
    [InlineData(")")]
    [InlineData("{")]
    [InlineData("}")]
    public void Tokenizer_ShouldAdvancePastMalformedSingleDelimiters(string delimiter)
    {
        var tokenizer = new PdfTokenizer(Encoding.ASCII.GetBytes(delimiter));

        var token = tokenizer.ReadToken();

        Assert.Equal(1, token.Length);
        Assert.Equal(1, tokenizer.Position);
    }

    [Fact]
    public void Build_ShouldBoundPageCount()
    {
        var pages = string.Concat(Enumerable.Range(2, 12)
            .Select(number => $"{number} 0 obj\n<</Type /Page>>\nendobj\n"));
        var pdf = Encoding.Latin1.GetBytes(
            $"%PDF-1.7\n1 0 obj\n<</Type /Pages>>\nendobj\n{pages}trailer\n<</Root 1 0 R>>\n%%EOF\n");
        var options = new PdfImporterOptions { Limits = new PdfParseLimits { MaxPageCount = 5 } };
        var context = new PdfParseContext(pdf, options);
        var table = new PdfCrossReferenceParser().Parse(context);
        var graph = new PdfObjectParser(context, table).ParseDocumentGraph();

        var document = new Document.PdfDocumentBuilder(
                new Content.PdfContentStreamParser(),
                new Graphics.PdfGraphicsInterpreter(),
                limits: options.Limits)
            .Build(graph);

        Assert.Equal(5, document.Pages.Count);
    }

    [Fact]
    public void ParseDocumentGraph_ShouldLeaveWellFormedDocumentsUnchanged()
    {
        var pdf = CreatePdfWithObject("<</Type /Catalog /Count 3 /Kids [1 0 R 2 0 R]>>");

        var (context, graph) = ParseGraph(pdf);

        Assert.Empty(context.Diagnostics);
        var value = Assert.IsType<PdfDictionary>(Assert.Single(graph.Objects.Values).Value);
        Assert.Equal("Catalog", Assert.IsType<PdfName>(value["Type"]).Value);
        Assert.Equal(2, Assert.IsType<PdfArray>(value["Kids"]).Items.Count);
    }

    private static byte[] CreatePdfWithObject(string body) =>
        Encoding.Latin1.GetBytes($"%PDF-1.7\n1 0 obj\n{body}\nendobj\ntrailer\n<</Root 1 0 R>>\n%%EOF\n");

    private static (PdfParseContext Context, PdfObjectGraph Graph) ParseGraph(
        byte[] pdf,
        PdfParseLimits? limits = null)
    {
        var context = new PdfParseContext(pdf, new PdfImporterOptions { Limits = limits ?? new PdfParseLimits() });
        var table = new PdfCrossReferenceParser().Parse(context);
        return (context, new PdfObjectParser(context, table).ParseDocumentGraph());
    }

    private static byte[] Deflate(byte[] payload)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(payload);
        }

        return output.ToArray();
    }
}
