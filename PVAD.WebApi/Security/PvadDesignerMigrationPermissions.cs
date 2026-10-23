using PVAD.Migration.Abstractions;

namespace PVAD.WebApi.Security;

public static class PvadDesignerMigrationPermissions
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

    public static IReadOnlyList<string> LegacyAll { get; } = PdfProviders.Values
        .Concat(SpreadsheetProviders.Values)
        .Concat(ReportProviders.Values)
        .Concat(ViewerProviders.Values)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    public static IReadOnlyList<string> CanonicalAll { get; } = MigrationProviderCatalog.All
        .Select(static descriptor => descriptor.PermissionKey)
        .Concat(MigrationProviderCatalog.All
            .SelectMany(static descriptor => new[]
            {
                KindPermission(descriptor.Domain, descriptor.MigrationKind, "read"),
                KindPermission(descriptor.Domain, descriptor.MigrationKind, "execute")
            }))
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    public static IReadOnlyList<string> All { get; } = LegacyAll
        .Concat(CanonicalAll)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    public static string KindPermission(string domain, string kind, string action) =>
        $"designer.migration.{domain}.{kind}.{action}";

    public static string ProviderPermission(string domain, string kind, string providerFamily) =>
        $"designer.migration.{domain}.{kind}.provider.{providerFamily}.execute";

    public static string? ResolveCanonicalProvider(string domain, string kind, string provider)
    {
        return MigrationProviderCatalog.TryGet(domain, kind, provider, out var descriptor)
            ? descriptor.PermissionKey
            : null;
    }

    public static IReadOnlyList<string> CompatibilityPermissions(MigrationProviderDescriptor descriptor)
    {
        var legacyProvider = descriptor.MigrationKind switch
        {
            MigrationKinds.Viewer => ResolveViewerProvider(descriptor.ProviderKey),
            MigrationKinds.Designer when descriptor.Domain == MigrationDomains.Pdf => ResolveViewerProvider(descriptor.ProviderKey),
            MigrationKinds.Designer => ResolveReportProvider(descriptor.ProviderKey),
            MigrationKinds.Code => ResolveCodeProvider(descriptor.ProviderKey, descriptor.Domain),
            _ => null
        };

        var domainPermission = descriptor.Domain switch
        {
            MigrationDomains.Pdf => PvadDesignerPermissions.MigrationPdfExecute,
            MigrationDomains.Spreadsheet => PvadDesignerPermissions.MigrationSpreadsheetExecute,
            MigrationDomains.Report => PvadDesignerPermissions.MigrationReportExecute,
            _ => PvadDesignerPermissions.MigrationExecute
        };

        return new[] { descriptor.PermissionKey, legacyProvider, domainPermission }
            .Where(static permission => !string.IsNullOrWhiteSpace(permission))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

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
