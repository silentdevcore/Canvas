using Canvas.Migration.SpreadsheetLight;

namespace Canvas.WebApi.Services.Converters;

public sealed class SpreadsheetLightConverter : BaseSpreadsheetConverter
{
    public override string FrameworkId => "SpreadsheetLight";

    public override string FrameworkName => "SpreadsheetLight";

    public override string Status => "full";

    public override string Description =>
        "Spreadsheet code migration: SpreadsheetLight SLDocument → Canvas spreadsheet API (CanvasWorkbook). The SLDocument doubles as the active worksheet, so the converter maps it to a CanvasWorkbook and injects `var sheet = doc.AddSheet(\"Sheet1\");`, retargeting SetCellValue (address or row/col, formulas via \"=\") at the worksheet; RenameWorksheet → sheet.Name; SaveAs → Save.";

    public override string ConvertCode(string sourceCode) =>
        new SpreadsheetLightMigration().Migrate(sourceCode).MigratedCode;

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode) =>
        new SpreadsheetLightMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static d => new MigrationDiagnostic(d.Id, d.Severity.ToString(), d.Message))
            .ToArray();
}
