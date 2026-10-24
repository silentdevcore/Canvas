namespace PVAD.Migration.Abstractions;

public static class MigrationDomains
{
    public const string Pdf = "pdf";
    public const string Spreadsheet = "spreadsheet";
    public const string Report = "report";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Pdf, Spreadsheet, Report
    };
}

public static class MigrationKinds
{
    public const string Code = "code";
    public const string Viewer = "viewer";
    public const string Designer = "designer";
    public const string Datasource = "datasource";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Code, Viewer, Designer, Datasource
    };
}

public sealed record MigrationInputDescriptor(
    string Id,
    string MediaType,
    IReadOnlyList<string> FileExtensions,
    bool SupportsText = true,
    bool SupportsBinary = false);

public sealed record MigrationArtifactDescriptor(
    string Id,
    string MediaType,
    string? FileExtension = null)
{
    private MigrationArtifactIdentity? Canonical => MigrationArtifactIdentities.Resolve(Id, MediaType, FileExtension);
    public string? CanonicalId => Canonical?.Id;
    public string? CanonicalMediaType => Canonical?.MediaType;
    public string? CanonicalFileExtension => Canonical?.FileExtension;
}

public sealed record MigrationProviderDescriptor(
    string ProviderKey,
    string ProviderFamily,
    string DisplayName,
    string Domain,
    string MigrationKind,
    string Status,
    string Description,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<MigrationInputDescriptor> Inputs,
    IReadOnlyList<MigrationArtifactDescriptor> Artifacts,
    bool SupportsPreview,
    bool HasExamples,
    string DocumentationId,
    string? FeatureId = null,
    long MaximumInputBytes = 10 * 1024 * 1024)
{
    public string PermissionKey =>
        $"designer.migration.{Domain}.{MigrationKind}.provider.{ProviderFamily}.execute";
}

public static class MigrationProviderCatalog
{
    private static readonly MigrationInputDescriptor CSharp =
        new("csharp", "text/x-csharp", [".cs"]);
    private static readonly MigrationArtifactDescriptor PdfCode =
        Artifact(MigrationArtifactIdentities.PdfCode);
    private static readonly MigrationArtifactDescriptor SpreadsheetCode =
        Artifact(MigrationArtifactIdentities.SpreadsheetCode);
    private static readonly MigrationArtifactDescriptor DesignJson =
        new("design", "application/json", ".json");

    public static IReadOnlyList<MigrationProviderDescriptor> All { get; } =
    [
        PdfCodeProvider("active-pdf", "activepdf", "ActivePDF", "pilot", ["ActivePdf"]),
        PdfCodeProvider("apryse", "apryse", "Apryse (PDFTron)", "full", ["Apryse", "pdftron"]),
        PdfCodeProvider("aspose-pdf", "aspose", "Aspose.PDF", "full", ["Aspose"]),
        PdfCodeProvider("devexpress-pdf", "devexpress", "DevExpress PDF", "full", ["DevExpress"]),
        PdfCodeProvider("dspdf", "dspdf", "DsPdf", "full", ["DsPdf"]),
        PdfCodeProvider("foxit-pdf", "foxit", "Foxit PDF SDK", "full", ["Foxit"]),
        PdfCodeProvider("gembox-pdf", "gembox", "GemBox.Pdf", "full", ["GemBox"]),
        PdfCodeProvider("iron-pdf", "ironpdf", "IronPDF", "pilot", ["IronPdf"]),
        PdfCodeProvider("itext7", "itext7", "iText 7", "full", ["iText7"]),
        PdfCodeProvider("leadtools-pdf", "leadtools", "LEADTOOLS PDF", "full", ["Leadtools"]),
        PdfCodeProvider("nutrient", "nutrient", "Nutrient .NET", "beta", ["Nutrient", "GdPicture", "GdPicture.NET", "gdpicture"]),
        PdfCodeProvider("pdfkitnet", "pdfkitnet", "PDFKit.NET", "full", ["PdfKitNet"]),
        PdfCodeProvider("pdftools", "pdftools", "Pdftools SDK", "pilot", ["PdfTools"]),
        PdfCodeProvider("pdftools-toolbox", "pdftools-toolbox", "Pdftools Toolbox", "pilot", ["PdfToolsToolbox"]),
        PdfCodeProvider("spire-pdf", "spire", "Spire.PDF", "full", ["Spire"]),
        PdfCodeProvider("syncfusion-pdf", "syncfusion", "Syncfusion PDF", "full", ["Syncfusion"]),

        new(
            "nutrient-viewer", "nutrient", "Nutrient Web SDK / PSPDFKit Viewer",
            MigrationDomains.Pdf, MigrationKinds.Viewer, "beta",
            "Migrates Viewer JavaScript, TypeScript, configuration, and Instant JSON into PVADE Viewer artifacts.",
            ["NutrientViewer", "PSPDFKit", "pspdfkit-web"],
            [
                new("javascript", "text/javascript", [".js", ".mjs"]),
                new("typescript", "text/typescript", [".ts", ".tsx"]),
                new("instantJson", "application/json", [".json"])
            ],
            [
                Artifact(MigrationArtifactIdentities.ViewerPackage)
            ],
            false, true, "nutrient-viewer-migration", "designer.nutrient-viewer-migration"),

        new(
            "nutrient-viewer", "nutrient", "Nutrient Viewer",
            MigrationDomains.Pdf, MigrationKinds.Designer, "beta",
            "Combines a source PDF with Nutrient Viewer configuration or Instant JSON and creates one editable PVAD design.",
            ["NutrientViewerDesign", "PSPDFKitViewerDesign"],
            [
                new("pdf", "application/pdf", [".pdf"], false, true),
                new("javascript", "text/javascript", [".js", ".mjs"]),
                new("typescript", "text/typescript", [".ts", ".tsx"]),
                new("instantJson", "application/json", [".json"])
            ],
            [DesignJson],
            true, true, "nutrient-viewer-designer-migration", "designer.nutrient-viewer-migration"),

        SpreadsheetCodeProvider("aspose-cells", "aspose", "Aspose.Cells", ["AsposeCells"]),
        SpreadsheetCodeProvider("closedxml", "closedxml", "ClosedXML", ["ClosedXmlSpreadsheet"]),
        SpreadsheetCodeProvider("epplus", "epplus", "EPPlus", ["EpplusSpreadsheet"]),
        SpreadsheetCodeProvider("gembox-spreadsheet", "gembox", "GemBox.Spreadsheet", ["GemBoxSpreadsheet"]),
        SpreadsheetCodeProvider("npoi", "npoi", "NPOI", ["Npoi"]),
        SpreadsheetCodeProvider("spire-xls", "spire", "Spire.XLS", ["SpireXls"]),
        SpreadsheetCodeProvider("spreadsheetlight", "spreadsheetlight", "SpreadsheetLight", ["SpreadsheetLight"]),
        SpreadsheetCodeProvider("syncfusion-xlsio", "syncfusion", "Syncfusion XlsIO", ["SyncfusionXlsIo"]),

        ReportProvider("activereports-js", "activereports-js", "ActiveReports JS", ["ActiveReportsJsReport"], [".json"]),
        ReportProvider("devexpress-report", "devexpress", "DevExpress XtraReports", ["DevExpressReport"], [".cs", ".repx"]),
        ReportProvider("fastreport", "fastreport", "FastReport", ["FrxReport"], [".frx"]),
        ReportProvider("jasperreports", "jasperreports", "JasperReports", ["JrxmlReport"], [".jrxml"]),
        ReportProvider("rdl", "rdl", "RDL / RDLC", ["RdlReport"], [".rdl", ".rdlc", ".rdlx"]),
        ReportProvider("rpx", "activereports-rpx", "ActiveReports RPX", ["RpxReport"], [".rpx"]),
        ReportProvider("stimulsoft", "stimulsoft", "Stimulsoft Reports", ["MrtReport"], [".mrt"]),
        ReportProvider("telerik", "telerik", "Telerik Reporting", ["TrdxReport"], [".trdx", ".trdp"]),
        new(
            "auto", "auto", "Automatic report detection", MigrationDomains.Report, MigrationKinds.Designer,
            "compatibility", "Detects the report provider from a supported source or package.", [],
            [new("report", "application/octet-stream", [".cs", ".xml", ".json", ".zip"], true, true)],
            [DesignJson], false, false, "report-designer-migration")
    ];

    private static readonly IReadOnlyDictionary<string, MigrationProviderDescriptor> Lookup = BuildLookup();

    private static MigrationArtifactDescriptor Artifact(MigrationArtifactIdentity identity) =>
        new(identity.Id, identity.MediaType, identity.FileExtension);

    public static MigrationProviderDescriptor Get(string domain, string kind, string provider)
    {
        if (TryGet(domain, kind, provider, out var descriptor)) return descriptor;
        throw new KeyNotFoundException($"Unknown migration provider '{domain}/{kind}/{provider}'.");
    }

    public static bool TryGet(
        string domain,
        string kind,
        string provider,
        out MigrationProviderDescriptor descriptor) =>
        Lookup.TryGetValue(Key(domain, kind, provider), out descriptor!);

    private static MigrationProviderDescriptor PdfCodeProvider(
        string key, string family, string name, string status, IReadOnlyList<string> aliases) =>
        new(key, family, name, MigrationDomains.Pdf, MigrationKinds.Code, status,
            $"Migrates {name} C# source to PVAD PDF code.", aliases, [CSharp], [PdfCode], true, key == "nutrient",
            $"pdf-code-migration-{key}");

    private static MigrationProviderDescriptor SpreadsheetCodeProvider(
        string key, string family, string name, IReadOnlyList<string> aliases) =>
        new(key, family, name, MigrationDomains.Spreadsheet, MigrationKinds.Code, "full",
            $"Migrates {name} C# source to the PVAD Spreadsheet API.", aliases, [CSharp], [SpreadsheetCode], true, false,
            $"spreadsheet-code-migration-{key}");

    private static MigrationProviderDescriptor ReportProvider(
        string key, string family, string name, IReadOnlyList<string> aliases, IReadOnlyList<string> extensions) =>
        new(key, family, name, MigrationDomains.Report, MigrationKinds.Designer, "full",
            $"Migrates {name} reports to editable PVAD design JSON.", aliases,
            [new("report", "application/octet-stream", extensions, true,
                extensions.Any(extension => extension is ".trdp" or ".rdlx"))], [DesignJson], false, false,
            $"report-designer-migration-{key}");

    private static IReadOnlyDictionary<string, MigrationProviderDescriptor> BuildLookup()
    {
        var result = new Dictionary<string, MigrationProviderDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in All)
        {
            Add(descriptor.ProviderKey, descriptor);
            foreach (var alias in descriptor.Aliases) Add(alias, descriptor);
        }
        return result;

        void Add(string provider, MigrationProviderDescriptor descriptor)
        {
            var key = Key(descriptor.Domain, descriptor.MigrationKind, provider);
            if (result.TryGetValue(key, out var existing))
            {
                if (existing == descriptor) return;
                throw new InvalidOperationException($"Duplicate migration provider or alias '{key}'.");
            }
            result.Add(key, descriptor);
        }
    }

    private static string Key(string domain, string kind, string provider) =>
        $"{domain.Trim()}/{kind.Trim()}/{provider.Trim()}";
}
