using System.Text.Json.Serialization;

namespace PXA.Core.ProjectFormats;

public enum PxaProjectKind
{
    Pdf,
    Spreadsheet,
}

public static class PxaProjectFormat
{
    public const string PxapFormatId = "pxap";
    public const string PxasFormatId = "pxas";
    public const string PxapExtension = ".pxap";
    public const string PxasExtension = ".pxas";
    public const string PxapDisplayName = "PXA PDF Project";
    public const string PxasDisplayName = "PXA Spreadsheet Project";
    public const string PxapMediaType = "application/vnd.powerdox.pxap+zip";
    public const string PxasMediaType = "application/vnd.powerdox.pxas+zip";
    public const string EnvelopeVersion = "1.0";
    public const string DocumentSchemaVersion = "1.0";
    public const string ManifestEntry = "manifest.json";
    public const string MimeTypeEntry = "mimetype";
    public const string DocumentEntry = "document.json";

    public static string CurrentProductVersion
    {
        get
        {
            var version = typeof(PxaProjectFormat).Assembly.GetName().Version;
            return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public static string FormatId(PxaProjectKind kind) =>
        kind == PxaProjectKind.Pdf ? PxapFormatId : PxasFormatId;

    public static string MediaType(PxaProjectKind kind) =>
        kind == PxaProjectKind.Pdf ? PxapMediaType : PxasMediaType;

    public static string Magic(PxaProjectKind kind) =>
        kind == PxaProjectKind.Pdf ? "PXA-PXAP-1" : "PXA-PXAS-1";
}

public static class PxaProjectAssetKinds
{
    public const string Image = "image";
    public const string Font = "font";
    public const string Preview = "preview";
    public const string Source = "source";
    public const string Data = "data";
}

public static class PxaProjectCapabilities
{
    public const string PdfDesigner = "pdf.designer";
    public const string PdfPerPageLayouts = "pdf.per-page-layouts";
    public const string PdfCharts = "pdf.charts";
    public const string PdfAccessibility = "pdf.accessibility";
    public const string PdfLocalization = "pdf.localization";
    public const string PdfBindings = "pdf.bindings";
    public const string PdfTrustSettings = "pdf.trust-settings";
    public const string SpreadsheetDesigner = "spreadsheet.designer";
    public const string SpreadsheetFormulas = "spreadsheet.formulas";
    public const string SpreadsheetImages = "spreadsheet.images";
    public const string SpreadsheetValidation = "spreadsheet.validation";
    public const string SpreadsheetConditionalFormatting = "spreadsheet.conditional-formatting";
    public const string SpreadsheetPrintLayout = "spreadsheet.print-layout";

    public static readonly IReadOnlySet<string> Supported = new HashSet<string>(StringComparer.Ordinal)
    {
        PdfDesigner, PdfPerPageLayouts, PdfCharts, PdfAccessibility, PdfLocalization,
        PdfBindings, PdfTrustSettings, SpreadsheetDesigner, SpreadsheetFormulas,
        SpreadsheetImages, SpreadsheetValidation, SpreadsheetConditionalFormatting,
        SpreadsheetPrintLayout,
    };
}

public sealed class PxaProjectDiagnostic
{
    public string Code { get; set; } = "";
    public string Severity { get; set; } = "warning";
    public string Message { get; set; } = "";
    public string? Source { get; set; }
}

public sealed class PxaProjectManifest
{
    public string Magic { get; set; } = "";
    public string FormatId { get; set; } = "";
    public string MediaType { get; set; } = "";
    public string EnvelopeVersion { get; set; } = PxaProjectFormat.EnvelopeVersion;
    public string DocumentSchemaVersion { get; set; } = PxaProjectFormat.DocumentSchemaVersion;
    public string MinimumPxaVersion { get; set; } = PxaProjectFormat.CurrentProductVersion;
    public string DocumentId { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ModifiedAtUtc { get; set; }
    public string Locale { get; set; } = "en";
    public string EntryDocument { get; set; } = PxaProjectFormat.DocumentEntry;
    public string ContentHash { get; set; } = "";
    public List<PxaProjectAssetDescriptor> Assets { get; set; } = [];
    public SortedDictionary<string, string> EntryChecksums { get; set; } = new(StringComparer.Ordinal);
    public List<string> RequiredFeatures { get; set; } = [];
    public List<PxaProjectDiagnostic> Diagnostics { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? Extensions { get; set; }
}

public sealed class PxaProjectAssetDescriptor
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Path { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Length { get; set; }
    public string Sha256 { get; set; } = "";
}

public sealed record PxaProjectAsset(
    string Id,
    string FileName,
    string ContentType,
    byte[] Content,
    string Kind = PxaProjectAssetKinds.Image);

public sealed class PxaProjectWriteOptions
{
    public string? DocumentId { get; init; }
    public string? Title { get; init; }
    public string Locale { get; init; } = "en";
    public string MinimumPxaVersion { get; init; } = PxaProjectFormat.CurrentProductVersion;
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UnixEpoch;
    public DateTimeOffset ModifiedAtUtc { get; init; } = DateTimeOffset.UnixEpoch;
    public IReadOnlyCollection<PxaProjectAsset> Assets { get; init; } = [];
    public IReadOnlyDictionary<string, System.Text.Json.JsonElement>? ManifestExtensions { get; init; }
}

public sealed class PxaProjectReadLimits
{
    public const long DefaultMaximumCompressedBytes = 25 * 1024 * 1024;
    public const long DefaultMaximumExpandedBytes = 100 * 1024 * 1024;
    public const long DefaultMaximumImagePixels = 100_000_000;

    public long MaximumCompressedBytes { get; init; } = DefaultMaximumCompressedBytes;
    public long MaximumExpandedBytes { get; init; } = DefaultMaximumExpandedBytes;
    public int MaximumEntries { get; init; } = 256;
    public long MaximumEntryBytes { get; init; } = 20 * 1024 * 1024;
    public double MaximumCompressionRatio { get; init; } = 100;
    public int MaximumJsonDepth { get; init; } = 128;
    public int MaximumPages { get; init; } = 500;
    public int MaximumElements { get; init; } = 100_000;
    public int MaximumSheets { get; init; } = 500;
    public int MaximumCells { get; init; } = 1_000_000;
    public int MaximumFormulas { get; init; } = 500_000;
    public long MaximumImagePixels { get; init; } = DefaultMaximumImagePixels;
    public TimeSpan MaximumProcessingDuration { get; init; } = TimeSpan.FromSeconds(15);
    public int MaximumExtensionProperties { get; init; } = 1_024;
    public long MaximumExtensionBytes { get; init; } = 1024 * 1024;
}

public sealed record PxaProjectPackage<TDocument>(
    PxaProjectManifest Manifest,
    TDocument Document,
    IReadOnlyList<PxaProjectAsset> Assets,
    string PackageSha256)
{
    public PxaProjectUpgradeReport? UpgradeReport { get; init; }
}

public sealed class PxaProjectFormatException : Exception
{
    public PxaProjectFormatException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
