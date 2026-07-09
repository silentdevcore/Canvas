using Canvas.Migration.ClosedXmlSpreadsheet;

namespace PXA.WebApi.Services.Converters;

public sealed class ClosedXmlSpreadsheetConverter : BaseSpreadsheetConverter
{
    public override string FrameworkId => "ClosedXmlSpreadsheet";

    public override string FrameworkName => "ClosedXML (spreadsheet)";

    public override string Status => "full";

    public override string Description =>
        "Spreadsheet code migration: ClosedXML XLWorkbook → Canvas spreadsheet API (CanvasWorkbook). Roslyn-based: workbook/worksheet/cell/value/formula/Bold-Italic-FontSize style/save mapping + 1-based→0-based index shift. Charts, pivots, conditional formatting, data validation, and auto-filter need manual review.";

    public override string ConvertCode(string sourceCode) =>
        new ClosedXmlSpreadsheetMigration().Migrate(sourceCode).MigratedCode;

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode) =>
        new ClosedXmlSpreadsheetMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static d => new MigrationDiagnostic(d.Id, d.Severity.ToString(), d.Message))
            .ToArray();
}
