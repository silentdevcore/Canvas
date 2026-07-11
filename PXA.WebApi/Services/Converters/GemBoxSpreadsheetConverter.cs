using PXA.Migration.Spreadsheet;

namespace PXA.WebApi.Services.Converters;

public sealed class GemBoxSpreadsheetConverter : BaseSpreadsheetConverter
{
    public override string FrameworkId => "GemBoxSpreadsheet";

    public override string FrameworkName => "GemBox.Spreadsheet";

    public override string Status => "full";

    public override string Description =>
        "Spreadsheet code migration: GemBox.Spreadsheet ExcelFile → Canvas spreadsheet API (CanvasWorkbook). Roslyn-based: drops SetLicense, rewrites worksheet/Cells[..] indexer/value/formula/font-weight style/save (indexes already 0-based). Charts, pivots, and range merges need manual review.";

    public override string ConvertCode(string sourceCode) =>
        new GemBoxSpreadsheetMigration().Migrate(sourceCode).MigratedCode;

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode) =>
        new GemBoxSpreadsheetMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static d => new MigrationDiagnostic(d.Id, d.Severity.ToString(), d.Message))
            .ToArray();
}
