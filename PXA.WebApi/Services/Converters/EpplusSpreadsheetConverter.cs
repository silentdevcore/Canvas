using Canvas.Migration.EpplusSpreadsheet;

namespace PXA.WebApi.Services.Converters;

public sealed class EpplusSpreadsheetConverter : BaseSpreadsheetConverter
{
    public override string FrameworkId => "EpplusSpreadsheet";

    public override string FrameworkName => "EPPlus (spreadsheet)";

    public override string Status => "full";

    public override string Description =>
        "Spreadsheet code migration: EPPlus ExcelPackage → Canvas spreadsheet API (CanvasWorkbook). Roslyn-based: package/worksheet, Cells[..] indexer → Cell(..), value/formula/Merge/Bold-Italic-FontSize style/SaveAs mapping + 1-based→0-based index shift. Charts, pivots, conditional formatting, and data validation need manual review.";

    public override string ConvertCode(string sourceCode) =>
        new EpplusSpreadsheetMigration().Migrate(sourceCode).MigratedCode;

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode) =>
        new EpplusSpreadsheetMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static d => new MigrationDiagnostic(d.Id, d.Severity.ToString(), d.Message))
            .ToArray();
}
