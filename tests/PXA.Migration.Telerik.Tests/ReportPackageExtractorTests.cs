using System.IO.Compression;
using PXA.Migration.Abstractions;
using PXA.Migration.Telerik;

namespace PXA.Migration.Telerik.Tests;

public sealed class ReportPackageExtractorTests
{
    [Fact]
    public void IsZip_DetectsPkMagic()
    {
        Assert.True(ReportPackageExtractor.IsZip(new byte[] { 0x50, 0x4B, 0x03, 0x04 }));
        Assert.False(ReportPackageExtractor.IsZip("<Report/>"u8.ToArray()));
    }

    [Fact]
    public void Extract_TrdpZip_FindsReportAndKeepsOtherEntriesAsResources()
    {
        const string trdx = """<Report Name="T" xmlns="http://schemas.telerik.com/reporting/2012/3.6"><Items /></Report>""";
        var zip = MakeZip(
            ("logo.png", "PNG-binary"),     // binary-ish entry, skipped (non-text extension)
            ("definition.trdx", trdx),
            ("shared/notes.txt", "hello"));

        var (report, resources) = ReportPackageExtractor.Extract(zip, TrdxToDesignConverter.LooksLikeTrdx);

        Assert.Equal(trdx, report);                       // the .trdx is recognized as the main report
        Assert.False(resources.ContainsKey("logo.png"));  // binary entry skipped
        Assert.Equal("hello", resources["notes.txt"]);    // text entry kept as a resource (keyed by file name)
    }

    [Fact]
    public void Extract_NoRecognizedReport_ReturnsNullReport()
    {
        var zip = MakeZip(("readme.txt", "just text"));
        var (report, _) = ReportPackageExtractor.Extract(zip, TrdxToDesignConverter.LooksLikeTrdx);
        Assert.Null(report);
    }

    private static byte[] MakeZip(params (string Name, string Content)[] entries)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, content) in entries)
            {
                var entry = zip.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
        return ms.ToArray();
    }
}
