namespace PXA.WebApi.Security;

public static class PxaDesignerMigrationPermissions
{
    public static IReadOnlyDictionary<string, string> PdfProviders { get; } =
        CreateProviderMap("pdf",
        [
            ("active-pdf", new[] { "ActivePdf" }),
            ("apryse", new[] { "Apryse" }),
            ("aspose-pdf", new[] { "Aspose" }),
            ("devexpress-pdf", new[] { "DevExpress" }),
            ("dspdf", new[] { "DsPdf" }),
            ("foxit-pdf", new[] { "Foxit" }),
            ("gembox-pdf", new[] { "GemBox" }),
            ("iron-pdf", new[] { "IronPdf" }),
            ("itext7", new[] { "iText7" }),
            ("leadtools-pdf", new[] { "Leadtools" }),
            ("nutrient", new[] { "Nutrient", "GdPicture", "GdPicture.NET", "gdpicture" }),
            ("pdfkitnet", new[] { "PdfKitNet" }),
            ("pdftools", new[] { "PdfTools" }),
            ("pdftools-toolbox", new[] { "PdfToolsToolbox" }),
            ("spire-pdf", new[] { "Spire" }),
            ("syncfusion-pdf", new[] { "Syncfusion" }),
        ]);

    public static IReadOnlyDictionary<string, string> SpreadsheetProviders { get; } =
        CreateProviderMap("spreadsheet",
        [
            ("aspose-cells", new[] { "AsposeCells" }),
            ("closedxml", new[] { "ClosedXmlSpreadsheet" }),
            ("epplus", new[] { "EpplusSpreadsheet" }),
            ("gembox-spreadsheet", new[] { "GemBoxSpreadsheet" }),
            ("npoi", new[] { "Npoi" }),
            ("spire-xls", new[] { "SpireXls" }),
            ("spreadsheetlight", new[] { "SpreadsheetLight" }),
            ("syncfusion-xlsio", new[] { "SyncfusionXlsIo" }),
        ]);

    public static IReadOnlyDictionary<string, string> ViewerProviders { get; } =
        CreateProviderMap("pdf",
        [
            ("nutrient-viewer", new[] { "NutrientViewer", "PSPDFKit", "pspdfkit-web" }),
        ]);

    public static IReadOnlyDictionary<string, string> ReportProviders { get; } =
        CreateProviderMap("report",
        [
            ("activereports-js", new[] { "ActiveReportsJsReport" }),
            ("devexpress-report", new[] { "DevExpressReport" }),
            ("fastreport", new[] { "FrxReport" }),
            ("jasperreports", new[] { "JrxmlReport" }),
            ("rdl", new[] { "RdlReport" }),
            ("rpx", new[] { "RpxReport" }),
            ("stimulsoft", new[] { "MrtReport" }),
            ("telerik", new[] { "TrdxReport" }),
        ]);

    public static IReadOnlyList<string> All { get; } = PdfProviders.Values
        .Concat(SpreadsheetProviders.Values)
        .Concat(ReportProviders.Values)
        .Concat(ViewerProviders.Values)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    public static string? ResolveCodeProvider(string framework, string domain) =>
        string.Equals(domain, "spreadsheet", StringComparison.OrdinalIgnoreCase)
            ? Resolve(SpreadsheetProviders, framework)
            : Resolve(PdfProviders, framework);

    public static string? ResolveReportProvider(string provider) => Resolve(ReportProviders, provider);

    public static string? ResolveViewerProvider(string provider) => Resolve(ViewerProviders, provider);

    private static string? Resolve(IReadOnlyDictionary<string, string> providers, string value) =>
        providers.TryGetValue(value, out var permission) ? permission : null;

    private static IReadOnlyDictionary<string, string> CreateProviderMap(
        string domain,
        IEnumerable<(string Key, string[] Aliases)> definitions)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, aliases) in definitions)
        {
            var permission = $"designer.migration.{domain}.provider.{key}.execute";
            result[key] = permission;
            foreach (var alias in aliases)
                result[alias] = permission;
        }
        return result;
    }
}
