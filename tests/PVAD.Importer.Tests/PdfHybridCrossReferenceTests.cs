using System.Text;
using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Xref;

namespace PVAD.Importer.Tests;

/// <summary>
/// A hybrid-reference file keeps a classic table for old readers and an /XRefStm for
/// PDF 1.5+ readers. If PXA read only the classic table it could resolve different bytes
/// than the viewer showing the document.
/// </summary>
public sealed class PdfHybridCrossReferenceTests
{
    [Fact]
    public void Parse_ShouldResolveObjectsSuppliedOnlyByTheHybridCrossReferenceStream()
    {
        var pdf = BuildHybridPdf(conflicting: false);

        var (table, graph) = ParseGraph(pdf);

        Assert.True(table.Entries.ContainsKey(new PdfObjectId(4, 0)));
        var hidden = Assert.IsType<PdfDictionary>(graph.Resolve(new PdfObjectId(4, 0))!.Value);
        Assert.Equal("HybridOnly", Assert.IsType<PdfName>(hidden["Type"]).Value);
        Assert.DoesNotContain(table.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFXREF010");
    }

    [Fact]
    public void Parse_ShouldReportWhenTheClassicTableAndHybridStreamDisagree()
    {
        var pdf = BuildHybridPdf(conflicting: true);

        var (table, _) = ParseGraph(pdf);

        Assert.Contains(table.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFXREF010");
    }

    [Fact]
    public void Parse_ShouldNotScanForObjectsWhenCrossReferenceDataIsUsable()
    {
        var pdf = BuildHybridPdf(conflicting: false);
        var withAppendedObject = pdf
            .Concat(Encoding.ASCII.GetBytes("\n99 0 obj\n<</Type /Unreferenced>>\nendobj\n"))
            .ToArray();

        var (table, graph) = ParseGraph(withAppendedObject);

        Assert.False(table.Entries.ContainsKey(new PdfObjectId(99, 0)));
        Assert.Null(graph.Resolve(new PdfObjectId(99, 0)));
        Assert.DoesNotContain(table.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFXREF008");
    }

    [Fact]
    public void Parse_ShouldStillRecoverObjectsWhenCrossReferenceDataIsUnusable()
    {
        var pdf = Encoding.Latin1.GetBytes(
            "%PDF-1.7\n1 0 obj\n<</Type /Catalog>>\nendobj\ntrailer\n<</Root 1 0 R>>\n%%EOF\n");

        var (table, graph) = ParseGraph(pdf);

        Assert.True(table.Entries.ContainsKey(new PdfObjectId(1, 0)));
        Assert.NotNull(graph.Resolve(new PdfObjectId(1, 0)));
        Assert.Contains(table.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFXREF008");
    }

    private static (PdfCrossReferenceTable Table, PdfObjectGraph Graph) ParseGraph(byte[] pdf)
    {
        var context = new PdfParseContext(pdf, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        return (table, new PdfObjectParser(context, table).ParseDocumentGraph());
    }

    private static byte[] BuildHybridPdf(bool conflicting)
    {
        var body = new MemoryStream();
        void Write(string text) => body.Write(Encoding.ASCII.GetBytes(text));

        Write("%PDF-1.5\n");
        var offset1 = body.Length;
        Write("1 0 obj\n<</Type /Catalog /Pages 2 0 R>>\nendobj\n");
        var offset2 = body.Length;
        Write("2 0 obj\n<</Type /Pages /Kids [] /Count 0>>\nendobj\n");
        var offset4 = body.Length;
        Write("4 0 obj\n<</Type /HybridOnly>>\nendobj\n");

        // The cross-reference stream supplies object 4, which the classic table omits.
        var entries = new byte[7];
        entries[0] = 1;
        entries[1] = (byte)(offset4 >> 24);
        entries[2] = (byte)(offset4 >> 16);
        entries[3] = (byte)(offset4 >> 8);
        entries[4] = (byte)offset4;
        entries[5] = 0;
        entries[6] = 0;

        var streamOffset = body.Length;
        Write($"3 0 obj\n<</Type /XRef /W [1 4 2] /Index [4 1] /Size 5 /Root 1 0 R /Length {entries.Length}>>\nstream\n");
        body.Write(entries);
        Write("\nendstream\nendobj\n");

        var xrefOffset = body.Length;
        // When conflicting, the classic table claims object 4 lives somewhere else entirely.
        var classicFourth = conflicting
            ? $"{offset1:D10} 00000 n \n"
            : $"{streamOffset:D10} 00000 n \n";
        Write("xref\n0 4\n");
        Write("0000000000 65535 f \n");
        Write($"{offset1:D10} 00000 n \n");
        Write($"{offset2:D10} 00000 n \n");
        Write($"{streamOffset:D10} 00000 n \n");
        if (conflicting)
        {
            Write("4 1\n");
            Write(classicFourth);
        }

        Write($"trailer\n<</Size 5 /Root 1 0 R /XRefStm {streamOffset}>>\nstartxref\n{xrefOffset}\n%%EOF\n");
        return body.ToArray();
    }
}
