using Canvas.WebApi.Services.Converters;

namespace Canvas.WebApi.Services;

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
            // spreadsheet code migration (→ Canvas spreadsheet API)
            new ClosedXmlSpreadsheetConverter(),
            new EpplusSpreadsheetConverter(),
            new GemBoxSpreadsheetConverter(),
        };
        _converters = all.ToDictionary(c => c.FrameworkId, StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<FrameworkInfo> GetFrameworks() =>
        _converters.Values.Select(c => new FrameworkInfo(c.FrameworkId, c.FrameworkName, c.Status, c.Description));

    public MigrationResult Convert(string frameworkId, string sourceCode)
    {
        var converter = GetConverter(frameworkId);
        var canvasCode = converter.ConvertCode(sourceCode);
        var diagnostics = converter.GetDiagnostics(sourceCode);
        return new MigrationResult(canvasCode, diagnostics, CreateSummary(diagnostics));
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

public sealed record FrameworkInfo(string Id, string Name, string Status, string Description);
