using PvadReportPackageExtractor = PVAD.Migration.Abstractions.ReportPackageExtractor;

namespace PVAD.Migration.Report;

public static class ReportPackageExtractor
{
    public static bool IsZip(ReadOnlySpan<byte> bytes) => PvadReportPackageExtractor.IsZip(bytes);

    public static (string? Report, Dictionary<string, string> Resources) Extract(
        byte[] zipBytes,
        Func<string, bool> isReport) =>
        PvadReportPackageExtractor.Extract(zipBytes, isReport);
}
