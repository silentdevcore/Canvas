using System.Text.Json.Serialization;
using System.Text.Json;

namespace PVAD.Core.ProjectFormats;

public enum PvadProjectKind
{
    Pdf,
    Spreadsheet,
}

public static class PvadProjectFormat
{
    public const string PvadpFormatId = "pvadp";
    public const string PvadsFormatId = "pvads";
    public const string PvadpExtension = ".pvadp";
    public const string PvadsExtension = ".pvads";
    public const string PvadpDisplayName = "PVADP PDF Project";
    public const string PvadsDisplayName = "PVADS Spreadsheet Project";
    public const string PvadpMediaType = "application/vnd.pvad.pvadp+zip";
    public const string PvadsMediaType = "application/vnd.pvad.pvads+zip";
    public const string EnvelopeVersion = "1.0";
    public const string DocumentSchemaVersion = "1.0";
    public const string ManifestEntry = "manifest.json";
    public const string MimeTypeEntry = "mimetype";
    public const string DocumentEntry = "document.json";
    public const string CodeWorkspaceEntry = "components/code-workspace.json";

    public static string CurrentProductVersion
    {
        get
        {
            var version = typeof(PvadProjectFormat).Assembly.GetName().Version;
            return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public static string FormatId(PvadProjectKind kind) =>
        kind == PvadProjectKind.Pdf ? PvadpFormatId : PvadsFormatId;

    public static string MediaType(PvadProjectKind kind) =>
        kind == PvadProjectKind.Pdf ? PvadpMediaType : PvadsMediaType;

    public static string Magic(PvadProjectKind kind) =>
        kind == PvadProjectKind.Pdf ? "PVAD-PVADP-1" : "PVAD-PVADS-1";

    public static string LegacyMagic(PvadProjectKind kind) =>
        kind == PvadProjectKind.Pdf ? "PXA-PXAP-1" : "PXA-PXAS-1";

    public static string LegacyFormatId(PvadProjectKind kind) => kind == PvadProjectKind.Pdf ? "pxap" : "pxas";
    public static string LegacyMediaType(PvadProjectKind kind) =>
        kind == PvadProjectKind.Pdf ? "application/vnd.powerdox.pxap+zip" : "application/vnd.powerdox.pxas+zip";

    public static bool AcceptsMediaType(PvadProjectKind kind, string value) =>
        value == MediaType(kind) || value == LegacyMediaType(kind);

    public static bool AcceptsExtension(string canonicalExtension, string extension) =>
        extension.Equals(canonicalExtension, StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(canonicalExtension == PvadpExtension ? ".pxap" : ".pxas", StringComparison.OrdinalIgnoreCase);
}

public static class PvadProjectAssetKinds
{
    public const string Image = "image";
    public const string Font = "font";
    public const string Preview = "preview";
    public const string Source = "source";
    public const string Data = "data";
}

public static class PvadProjectCapabilities
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

public sealed class PvadProjectDiagnostic
{
    public string Code { get; set; } = "";
    public string Severity { get; set; } = "warning";
    public string Message { get; set; } = "";
    public string? Source { get; set; }
}

public sealed class PvadProjectManifest
{
    public string Magic { get; set; } = "";
    public string FormatId { get; set; } = "";
    public string MediaType { get; set; } = "";
    public string EnvelopeVersion { get; set; } = PvadProjectFormat.EnvelopeVersion;
    public string DocumentSchemaVersion { get; set; } = PvadProjectFormat.DocumentSchemaVersion;
    private string? minimumPvadVersion;
    private string? minimumPxaVersion;

    public string MinimumPvadVersion
    {
        get
        {
            if (minimumPvadVersion is not null && minimumPxaVersion is not null && minimumPvadVersion != minimumPxaVersion)
                throw new PvadProjectFormatException("PXAPROJECT008", "The project contains conflicting minimum product versions.");
            return minimumPvadVersion ?? minimumPxaVersion ?? PvadProjectFormat.CurrentProductVersion;
        }
        set => minimumPvadVersion = value ?? throw new JsonException("Minimum product version cannot be null.");
    }

    [JsonPropertyName("minimumPxaVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyMinimumPxaVersion
    {
        get => null;
        set => minimumPxaVersion = value ?? throw new JsonException("Minimum product version cannot be null.");
    }
    public string DocumentId { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ModifiedAtUtc { get; set; }
    public string Locale { get; set; } = "en";
    public string EntryDocument { get; set; } = PvadProjectFormat.DocumentEntry;
    public string ContentHash { get; set; } = "";
    public List<PvadProjectAssetDescriptor> Assets { get; set; } = [];
    public List<PvadProjectComponentDescriptor> Components { get; set; } = [];
    public SortedDictionary<string, string> EntryChecksums { get; set; } = new(StringComparer.Ordinal);
    public List<string> RequiredFeatures { get; set; } = [];
    public List<PvadProjectDiagnostic> Diagnostics { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? Extensions { get; set; }
}

public static class PvadProjectComponentKinds
{
    public const string CodeWorkspace = "code-workspace";
    public const string ConfidentialSource = "confidential-source";
}

public sealed class PvadProjectComponentDescriptor
{
    public string Kind { get; set; } = "";
    public string Path { get; set; } = "";
    public string SchemaVersion { get; set; } = "";
    public string SecurityClassification { get; set; } = "";
    public long Length { get; set; }
    public string Sha256 { get; set; } = "";
}

public sealed class PvadCodeWorkspaceProjectComponent
{
    public const string CurrentSchemaVersion = "1.0";

    public string SchemaVersion { get; set; } = CurrentSchemaVersion;
    public long Revision { get; set; }
    public long BaseTemplateRevision { get; set; }
    public string JsonDraft { get; set; } = "";
    public string CSharpModelDraft { get; set; } = "";
    public string CSharpPdfDraft { get; set; } = "";
    public string CSharpBase64Draft { get; set; } = "";
    public string CanonicalDesignJson { get; set; } = "{}";
    public System.Text.Json.JsonElement? SourceMap { get; set; }
}

public sealed class PvadProjectAssetDescriptor
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Path { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Length { get; set; }
    public string Sha256 { get; set; } = "";
}

public sealed record PvadProjectAsset(
    string Id,
    string FileName,
    string ContentType,
    byte[] Content,
    string Kind = PvadProjectAssetKinds.Image);

public sealed class PvadProjectWriteOptions
{
    public string? DocumentId { get; init; }
    public string? Title { get; init; }
    public string Locale { get; init; } = "en";
    public string MinimumPvadVersion { get; init; } = PvadProjectFormat.CurrentProductVersion;
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UnixEpoch;
    public DateTimeOffset ModifiedAtUtc { get; init; } = DateTimeOffset.UnixEpoch;
    public IReadOnlyCollection<PvadProjectAsset> Assets { get; init; } = [];
    public PvadCodeWorkspaceProjectComponent? CodeWorkspace { get; init; }
    public IReadOnlyDictionary<string, System.Text.Json.JsonElement>? ManifestExtensions { get; init; }
}

public sealed class PvadProjectReadLimits
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

public sealed record PvadProjectPackage<TDocument>(
    PvadProjectManifest Manifest,
    TDocument Document,
    IReadOnlyList<PvadProjectAsset> Assets,
    string PackageSha256)
{
    public PvadProjectUpgradeReport? UpgradeReport { get; init; }
    public PvadCodeWorkspaceProjectComponent? CodeWorkspace { get; init; }
}

public sealed class PvadProjectFormatException : Exception
{
    public PvadProjectFormatException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
