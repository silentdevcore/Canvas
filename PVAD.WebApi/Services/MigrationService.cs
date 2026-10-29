using PVAD.WebApi.Services.Converters;
using PVAD.Migration.Pdf;
using PVAD.Migration.Spreadsheet;

namespace PVAD.WebApi.Services;

public sealed class MigrationService
{
    private static readonly IReadOnlyDictionary<string, string> PdfProviderKeysByFrameworkId =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ActivePdf"] = PdfMigrationProviderKeys.ActivePdf,
            ["Apryse"] = PdfMigrationProviderKeys.Apryse,
            ["Aspose"] = PdfMigrationProviderKeys.AsposePdf,
            ["DevExpress"] = PdfMigrationProviderKeys.DevExpressPdf,
            ["DsPdf"] = PdfMigrationProviderKeys.DsPdf,
            ["Foxit"] = PdfMigrationProviderKeys.FoxitPdf,
            ["GemBox"] = PdfMigrationProviderKeys.GemBoxPdf,
            ["IronPdf"] = PdfMigrationProviderKeys.IronPdf,
            ["iText7"] = PdfMigrationProviderKeys.IText7,
            ["Leadtools"] = PdfMigrationProviderKeys.LeadtoolsPdf,
            ["Nutrient"] = PdfMigrationProviderKeys.Nutrient,
            ["PdfKitNet"] = PdfMigrationProviderKeys.PdfKitNet,
            ["PdfTools"] = PdfMigrationProviderKeys.PdfTools,
            ["PdfToolsToolbox"] = PdfMigrationProviderKeys.PdfToolsToolbox,
            ["Spire"] = PdfMigrationProviderKeys.SpirePdf,
            ["Syncfusion"] = PdfMigrationProviderKeys.SyncfusionPdf,
        };

    private static readonly IReadOnlyDictionary<string, string> FrameworkAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PdfMigrationProviderKeys.ActivePdf] = "ActivePdf",
            [PdfMigrationProviderKeys.Apryse] = "Apryse",
            [PdfMigrationProviderKeys.AsposePdf] = "Aspose",
            [PdfMigrationProviderKeys.DevExpressPdf] = "DevExpress",
            [PdfMigrationProviderKeys.DsPdf] = "DsPdf",
            [PdfMigrationProviderKeys.FoxitPdf] = "Foxit",
            [PdfMigrationProviderKeys.GemBoxPdf] = "GemBox",
            [PdfMigrationProviderKeys.IronPdf] = "IronPdf",
            [PdfMigrationProviderKeys.IText7] = "iText7",
            [PdfMigrationProviderKeys.LeadtoolsPdf] = "Leadtools",
            [PdfMigrationProviderKeys.Nutrient] = "Nutrient",
            ["GdPicture"] = "Nutrient",
            ["GdPicture.NET"] = "Nutrient",
            [PdfMigrationProviderKeys.PdfKitNet] = "PdfKitNet",
            [PdfMigrationProviderKeys.PdfTools] = "PdfTools",
            [PdfMigrationProviderKeys.PdfToolsToolbox] = "PdfToolsToolbox",
            [PdfMigrationProviderKeys.SpirePdf] = "Spire",
            [PdfMigrationProviderKeys.SyncfusionPdf] = "Syncfusion",
            [SpreadsheetMigrationProviderKeys.AsposeCells] = "AsposeCells",
            [SpreadsheetMigrationProviderKeys.ClosedXml] = "ClosedXmlSpreadsheet",
            [SpreadsheetMigrationProviderKeys.Epplus] = "EpplusSpreadsheet",
            [SpreadsheetMigrationProviderKeys.GemBoxSpreadsheet] = "GemBoxSpreadsheet",
            [SpreadsheetMigrationProviderKeys.Npoi] = "Npoi",
            [SpreadsheetMigrationProviderKeys.SpireXls] = "SpireXls",
            [SpreadsheetMigrationProviderKeys.SpreadsheetLight] = "SpreadsheetLight",
            [SpreadsheetMigrationProviderKeys.SyncfusionXlsIo] = "SyncfusionXlsIo",
        };

    private readonly IReadOnlyDictionary<string, ICodeConverter> _converters;

    public MigrationService()
    {
        var all = new ICodeConverter[]
        {
            new SyncfusionPdfConverter(),
            new AprysePdfConverter(),
            new AsposePdfConverter(),
            new DsPdfConverter(),
            new SpirePdfConverter(),
            new GemBoxPdfConverter(),
            new IText7PdfConverter(),
            new IronPdfConverter(),
            new ActivePdfConverter(),
            new LeadtoolsPdfConverter(),
            new NutrientPdfConverter(),
            new PdfToolsConverter(),
            new PdfToolsToolboxConverter(),
            new PdfKitNetConverter(),
            new FoxitPdfConverter(),
            new DevExpressPdfConverter(),
            // spreadsheet code migration (→ PVADS spreadsheet API)
            new ClosedXmlSpreadsheetConverter(),
            new EpplusSpreadsheetConverter(),
            new GemBoxSpreadsheetConverter(),
            new AsposeCellsConverter(),
            new SpireXlsConverter(),
            new SyncfusionXlsIoConverter(),
            new NpoiConverter(),
            new SpreadsheetLightConverter(),
        };
        _converters = all.ToDictionary(c => c.FrameworkId, StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<FrameworkInfo> GetFrameworks()
    {
        var codeFrameworks = _converters.Values.Select(c =>
        {
            var taxonomy = GetTaxonomy(c);
            return new FrameworkInfo(
                c.FrameworkId,
                c.FrameworkName,
                c.Status,
                c.Description,
                c.Kind,
                taxonomy.Domain,
                taxonomy.MigrationKind,
                taxonomy.Provider);
        });

        return codeFrameworks.Append(new FrameworkInfo(
            "nutrient-viewer",
            "Nutrient Web SDK / PSPDFKit Viewer",
            "beta",
            "Migrates JavaScript, TypeScript, Viewer configuration, and Instant JSON to PVADE Viewer artifacts.",
            "pdf",
            "pdf",
            "viewer",
            "Nutrient"));
    }

    /// <summary>Migration target kind for a framework ("pdf" | "spreadsheet"); defaults to "pdf".</summary>
    public string GetKind(string frameworkId) =>
        _converters.TryGetValue(NormalizeFrameworkId(frameworkId), out var c) ? c.Kind : "pdf";

    public MigrationResult Convert(string frameworkId, string sourceCode)
    {
        var converter = GetConverter(frameworkId);
        if (PdfProviderKeysByFrameworkId.TryGetValue(converter.FrameworkId, out var providerKey))
        {
            var nativeResult = PdfMigrationProviders.Migrate(providerKey, sourceCode);
            var nativeDiagnostics = nativeResult.Diagnostics
                .Select(static diagnostic => new MigrationDiagnostic(
                    diagnostic.Id,
                    diagnostic.Severity.ToString(),
                    diagnostic.Message))
                .ToArray();
            return new MigrationResult(
                nativeResult.MigratedCode,
                nativeDiagnostics,
                CreateSummary(nativeDiagnostics))
            {
                PdfTrust = nativeResult.PdfTrust
            };
        }

        var pvadCode = converter.ConvertCode(sourceCode);
        var diagnostics = converter.GetDiagnostics(sourceCode);
        return new MigrationResult(pvadCode, diagnostics, CreateSummary(diagnostics));
    }

    public byte[] GeneratePreview(string frameworkId, string sourceCode)
    {
        var converter = GetConverter(frameworkId);
        return converter.GeneratePreview(sourceCode);
    }

    private ICodeConverter GetConverter(string frameworkId)
    {
        var normalizedFrameworkId = NormalizeFrameworkId(frameworkId);
        if (!_converters.TryGetValue(normalizedFrameworkId, out var converter))
            throw new ArgumentException($"Unknown framework '{frameworkId}'. Supported: {string.Join(", ", _converters.Keys)}");
        return converter;
    }

    private static string NormalizeFrameworkId(string frameworkId) =>
        FrameworkAliases.TryGetValue(frameworkId, out var normalized)
            ? normalized
            : frameworkId;

    private static MigrationTaxonomy GetTaxonomy(ICodeConverter converter)
    {
        var domain = string.Equals(converter.Kind, "spreadsheet", StringComparison.OrdinalIgnoreCase)
            ? "spreadsheet"
            : "pdf";

        return new MigrationTaxonomy(domain, "code", GetProviderName(converter.FrameworkId));
    }

    private static string GetProviderName(string frameworkId) =>
        frameworkId switch
        {
            "Aspose" or "AsposeCells" => "Aspose",
            "ClosedXmlSpreadsheet" => "ClosedXml",
            "DevExpress" => "DevExpress",
            "EpplusSpreadsheet" => "Epplus",
            "GemBox" or "GemBoxSpreadsheet" => "GemBox",
            "iText7" => "IText7",
            "Leadtools" => "Leadtools",
            "Nutrient" => "Nutrient",
            "Spire" or "SpireXls" => "Spire",
            "Syncfusion" or "SyncfusionXlsIo" => "Syncfusion",
            _ => frameworkId
        };

    private static MigrationSummary CreateSummary(IReadOnlyList<MigrationDiagnostic> diagnostics)
    {
        var warningCount = diagnostics.Count(static diagnostic =>
            string.Equals(diagnostic.Severity, "Warning", StringComparison.OrdinalIgnoreCase));
        var errorCount = diagnostics.Count(static diagnostic =>
            string.Equals(diagnostic.Severity, "Error", StringComparison.OrdinalIgnoreCase));

        return new MigrationSummary(
            ConvertedCount: diagnostics.Count - warningCount - errorCount,
            WarningCount: warningCount,
            ErrorCount: errorCount,
            TotalDiagnostics: diagnostics.Count);
    }
}

public sealed record FrameworkInfo(
    string Id,
    string Name,
    string Status,
    string Description,
    string Kind,
    string Domain,
    string MigrationKind,
    string Provider);

public sealed record MigrationTaxonomy(string Domain, string MigrationKind, string Provider);
