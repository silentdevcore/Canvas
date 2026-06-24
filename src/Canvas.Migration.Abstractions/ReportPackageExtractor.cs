using System.IO.Compression;

namespace Canvas.Migration.Abstractions;

/// <summary>
/// Unpacks zipped report packages (Telerik <c>.trdp</c>, packaged/OPC <c>.rdlx</c>, …) so the existing
/// string-based converters can run on the inner report document. The package's main report is the first
/// text entry a caller-supplied recognizer accepts; the remaining text entries are returned as resources
/// (keyed by file name) for converters that inline sub-reports.
/// </summary>
public static class ReportPackageExtractor
{
    // Text entries worth extracting as a report or a resource; binary entries (images, fonts) are skipped.
    private static readonly string[] TextExtensions =
        [".xml", ".rdl", ".rdlc", ".rdlx", ".trdx", ".jrxml", ".frx", ".mrt", ".repx", ".json", ".txt", ".resx"];

    /// <summary>True when the bytes start with the ZIP local-file-header magic (<c>PK</c>).</summary>
    public static bool IsZip(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 2 && bytes[0] == 0x50 && bytes[1] == 0x4B;

    /// <summary>
    /// Extracts a zipped report package. Returns the main report document (first text entry for which
    /// <paramref name="isReport"/> returns true, preferring report-like extensions) and the remaining
    /// text entries as resources keyed by file name. <c>Report</c> is null when nothing is recognized.
    /// </summary>
    public static (string? Report, Dictionary<string, string> Resources) Extract(byte[] zipBytes, Func<string, bool> isReport)
    {
        var resources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var entries = new List<(string Name, string Content)>();
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;           // directory entry
            if (!IsTextEntry(entry.Name)) continue;                   // skip binary resources
            using var reader = new StreamReader(entry.Open());
            entries.Add((entry.Name, reader.ReadToEnd()));
        }

        string? report = null;
        // Try report-like extensions first so the recognizer matches the definition, not a stray .xml.
        foreach (var (name, content) in entries.OrderBy(e => ExtensionPriority(e.Name)))
        {
            if (report is null && isReport(content))
                report = content;
            else
                resources[name] = content;
        }

        return (report, resources);
    }

    private static bool IsTextEntry(string name) =>
        TextExtensions.Any(ext => name.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    // Lower sorts first: prefer dedicated report formats over generic .xml when choosing the definition.
    private static int ExtensionPriority(string name)
    {
        foreach (var ext in new[] { ".trdx", ".rdlx", ".rdl", ".rdlc", ".jrxml", ".frx", ".mrt", ".repx" })
            if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) return 0;
        return name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? 1 : 2;   // .xml/.txt/.resx last
    }
}
