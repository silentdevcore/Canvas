using PvadReportPackageExtractor = PVAD.Migration.Abstractions.ReportPackageExtractor;

namespace PVAD.Migration.Report;

/// <summary>Unpacks textual report definitions and resources from report ZIP packages.</summary>
/// <remarks>This helper does not convert, execute or render a report, and does not grant native
/// migration rights. Binary entries are skipped and extracted texts remain in memory.</remarks>
public static class ReportPackageExtractor
{
    /// <summary>Checks for a leading ZIP marker; this does not validate the archive.</summary>
    /// <param name="bytes">Input bytes to inspect.</param>
    /// <returns>True when the first two bytes are the ZIP marker.</returns>
    public static bool IsZip(ReadOnlySpan<byte> bytes) => PvadReportPackageExtractor.IsZip(bytes);

    /// <summary>Selects the first recognized report text and collects other supported text resources.</summary>
    /// <param name="zipBytes">The complete ZIP archive bytes.</param>
    /// <param name="isReport">Caller-supplied report-text recognizer.</param>
    /// <returns>The selected report, or null, and resources keyed by entry file name.</returns>
    /// <remarks>Dedicated report extensions are considered before generic XML/text. This is not
    /// an upload security boundary; callers must enforce their input/resource limits separately.</remarks>
    public static (string? Report, Dictionary<string, string> Resources) Extract(
        byte[] zipBytes,
        Func<string, bool> isReport) =>
        PvadReportPackageExtractor.Extract(zipBytes, isReport);
}
