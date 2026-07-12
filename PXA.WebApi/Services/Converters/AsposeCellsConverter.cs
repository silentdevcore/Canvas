using PXA.Migration.Spreadsheet;

namespace PXA.WebApi.Services.Converters;

public sealed class AsposeCellsConverter : BaseSpreadsheetConverter
{
    public override string FrameworkId => "AsposeCells";

    public override string FrameworkName => "Aspose.Cells";

    public override string Status => "full";

    public override string Description =>
        "Spreadsheet code migration: Aspose.Cells Workbook → PXA spreadsheet API (PxaWorkbook). Roslyn-based: worksheet, Cells[..] indexer → Cell(..), PutValue → Value, Formula, SetColumnWidth → Column().Width(), save (indexes already 0-based). GetStyle/SetStyle styling, charts, and pivots need manual review; ClosedXML covers fewer functions than Aspose's ~450.";

    public override string ConvertCode(string sourceCode) =>
        new AsposeCellsMigration().Migrate(sourceCode).MigratedCode;

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode) =>
        new AsposeCellsMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static d => new MigrationDiagnostic(d.Id, d.Severity.ToString(), d.Message))
            .ToArray();
}
