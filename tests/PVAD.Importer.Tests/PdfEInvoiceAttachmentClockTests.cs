using System.Globalization;
using System.Text;
using PVAD.Importer.EInvoicing;
using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Trust;
using PVAD.Importer.Xref;
using PVAD.Pdf;

namespace PVAD.Importer.Tests;

public sealed class PdfEInvoiceAttachmentClockTests
{
    private static readonly PdfEInvoiceAttachmentPayload Payload = new()
    {
        Xml = "<invoice id=\"synthetic-clock\"/>"u8.ToArray(),
        FileName = "factur-x.xml"
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AttachAsync_FixedClockPreservesExactBytesAndAdvancingClockChangesMetadata(bool archival)
    {
        var source = Source(archival);
        var clock = new AttachmentClock(new(2026, 1, 2, 8, 34, 5, TimeSpan.FromHours(5.5)));
        var attacher = new PdfEInvoiceAttacher(new PdfA2bConverter(new PdfTrustInspector()), clock);
        var options = new PdfEInvoiceAttachmentOptions { AllowArchiveConversion = !archival };

        using var firstInput = new MemoryStream(source);
        var first = await attacher.AttachAsync(firstInput, Payload, options);
        using var secondInput = new MemoryStream(source);
        var second = await attacher.AttachAsync(secondInput, Payload, options);

        Assert.Equal(first.Bytes, second.Bytes);
        Assert.Equal(first.ResultSha256, second.ResultSha256);
        Assert.Equal("D:20260102030405Z", ModificationDate(first.Bytes));
        Assert.Equal(2, clock.Reads);
        Assert.Equal(archival ? PdfEInvoiceAttachmentStrategy.IncrementalUpdate : PdfEInvoiceAttachmentStrategy.ArchiveConversion, first.Strategy);

        clock.Now = clock.Now.AddSeconds(2);
        using var thirdInput = new MemoryStream(source);
        var third = await attacher.AttachAsync(thirdInput, Payload, options);

        Assert.False(first.Bytes.SequenceEqual(third.Bytes));
        Assert.NotEqual(first.ResultSha256, third.ResultSha256);
        Assert.Equal("D:20260102030407Z", ModificationDate(third.Bytes));
        Assert.Equal(first.AttachmentSha256, third.AttachmentSha256);
        Assert.Equal(first.SourceSha256, third.SourceSha256);
        Assert.Equal(first.Strategy, third.Strategy);
        Assert.True(third.PagesUnchanged);
        Assert.Equal(3, clock.Reads);
        if (archival)
        {
            Assert.Equal(source, third.Bytes[..source.Length]);
        }

        using var output = new MemoryStream(third.Bytes);
        var invoice = Assert.Single(await new PdfAssociatedFileExtractor().ExtractAsync(output));
        Assert.Equal(Payload.Xml.ToArray(), invoice.Content.ToArray());
        Assert.True(invoice.ChecksumMatches && invoice.SizeMatches);
    }

    [Fact]
    public async Task PreflightAndBlockedAttachment_DoNotReadTheMetadataClock()
    {
        var clock = new AttachmentClock(DateTimeOffset.UnixEpoch);
        var attacher = new PdfEInvoiceAttacher(new PdfA2bConverter(new PdfTrustInspector()), clock);
        var source = Source(archival: false);
        using var preflightInput = new MemoryStream(source);
        var preflight = await attacher.PreflightAsync(preflightInput, Payload);
        Assert.True(preflight.RequiresArchiveConversion);
        using var attachInput = new MemoryStream(source);
        await Assert.ThrowsAsync<PdfEInvoiceAttachmentBlockedException>(() => attacher.AttachAsync(attachInput, Payload));
        Assert.Equal(0, clock.Reads);
    }

    [Fact]
    public async Task LegacyConstructor_UsesTheSystemUtcClock()
    {
        var source = Source(archival: true);
        var attacher = new PdfEInvoiceAttacher(new PdfA2bConverter(new PdfTrustInspector()));
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        using var input = new MemoryStream(source);
        var result = await attacher.AttachAsync(input, Payload);
        var after = DateTimeOffset.UtcNow;
        var modificationTime = DateTimeOffset.ParseExact(ModificationDate(result.Bytes), "'D:'yyyyMMddHHmmss'Z'",
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        Assert.InRange(modificationTime, before, after);
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        var converter = new PdfA2bConverter(new PdfTrustInspector());
        Assert.Equal("archiveConverter", Assert.Throws<ArgumentNullException>(() => new PdfEInvoiceAttacher(null!)).ParamName);
        Assert.Equal("archiveConverter", Assert.Throws<ArgumentNullException>(() => new PdfEInvoiceAttacher(null!, TimeProvider.System)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new PdfEInvoiceAttacher(converter, null!)).ParamName);
    }

    private static byte[] Source(bool archival)
    {
        var document = PVAD.Generator.Pdf.CreateDocument();
        document.FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"));
        document.AddPage(300, 180).DrawText("Synthetic attachment clock", 30, 40, new() { Language = "en" });
        return archival ? document.ToBytes(new() { ComplianceProfile = PdfComplianceProfile.PdfA3B }) : document.ToBytes();
    }

    private static string ModificationDate(byte[] pdf)
    {
        var context = new PdfParseContext(pdf, new());
        var xref = new PdfCrossReferenceParser().Parse(context);
        var graph = new PdfObjectParser(context, xref).ParseDocumentGraph();
        var embedded = Assert.Single(graph.Objects.Values.Select(item => item.Value).OfType<PdfStreamObject>(),
            stream => stream.Dictionary["Type"] is PdfName { Value: "EmbeddedFile" });
        var parameters = Assert.IsType<PdfDictionary>(embedded.Dictionary["Params"]);
        return Encoding.ASCII.GetString(Assert.IsType<PdfString>(parameters["ModDate"]).GetDecodedBytes().Span);
    }

    private sealed class AttachmentClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public int Reads { get; private set; }

        public override DateTimeOffset GetUtcNow()
        {
            Reads++;
            return Now;
        }
    }
}
