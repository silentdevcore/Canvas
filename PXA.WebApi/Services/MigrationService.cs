using PXA.WebApi.Services.Converters;

namespace PXA.WebApi.Services;

public sealed class MigrationService
{
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
            new PdfToolsConverter(),
            new PdfToolsToolboxConverter(),
            new PdfKitNetConverter(),
            new FoxitPdfConverter(),
            new DevExpressPdfConverter(),
            // spreadsheet code migration (→ PXA spreadsheet API)
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

    public IEnumerable<FrameworkInfo> GetFrameworks() =>
        _converters.Values.Select(c =>
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

    /// <summary>Migration target kind for a framework ("pdf" | "spreadsheet"); defaults to "pdf".</summary>
    public string GetKind(string frameworkId) =>
        _converters.TryGetValue(frameworkId, out var c) ? c.Kind : "pdf";

    public MigrationResult Convert(string frameworkId, string sourceCode)
    {
        var converter = GetConverter(frameworkId);
        var pxaCode = converter.ConvertCode(sourceCode);
        var diagnostics = converter.GetDiagnostics(sourceCode);
        return new MigrationResult(pxaCode, diagnostics, CreateSummary(diagnostics));
    }

    public byte[] GeneratePreview(string frameworkId, string sourceCode)
    {
        var converter = GetConverter(frameworkId);
        return converter.GeneratePreview(sourceCode);
    }

    private ICodeConverter GetConverter(string frameworkId)
    {
        if (!_converters.TryGetValue(frameworkId, out var converter))
            throw new ArgumentException($"Unknown framework '{frameworkId}'. Supported: {string.Join(", ", _converters.Keys)}");
        return converter;
    }

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
