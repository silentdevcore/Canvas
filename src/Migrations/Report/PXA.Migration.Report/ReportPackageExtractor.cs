using PxaReportPackageExtractor = PXA.Migration.Abstractions.ReportPackageExtractor;

namespace PXA.Migration.Report;

public static class ReportPackageExtractor
{
    public static bool IsZip(ReadOnlySpan<byte> bytes) => PxaReportPackageExtractor.IsZip(bytes);

    public static (string? Report, Dictionary<string, string> Resources) Extract(
        byte[] zipBytes,
        Func<string, bool> isReport) =>
        PxaReportPackageExtractor.Extract(zipBytes, isReport);
}
