using CanvasReportPackageExtractor = Canvas.Migration.Abstractions.ReportPackageExtractor;

namespace PXA.Migration.Report;

public static class ReportPackageExtractor
{
    public static bool IsZip(ReadOnlySpan<byte> bytes) => CanvasReportPackageExtractor.IsZip(bytes);

    public static (string? Report, Dictionary<string, string> Resources) Extract(
        byte[] zipBytes,
        Func<string, bool> isReport) =>
        CanvasReportPackageExtractor.Extract(zipBytes, isReport);
}
