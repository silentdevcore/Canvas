using Canvas.Migration.Npoi;

namespace PXA.WebApi.Services.Converters;

public sealed class NpoiConverter : BaseSpreadsheetConverter
{
    public override string FrameworkId => "Npoi";

    public override string FrameworkName => "NPOI";

    public override string Status => "full";

    public override string Description =>
        "Spreadsheet code migration: NPOI (XSSF/HSSFWorkbook) → Canvas spreadsheet API (CanvasWorkbook). Roslyn-based: tracks the CreateRow/CreateCell variable model and inlines cell writes as sheet.Cell(r, c).Value(..)/Formula(..); CreateSheet → AddSheet, SetColumnWidth → Column().Width(), Write(stream) → Save(path). Column-width units and stream save are flagged for review.";

    public override string ConvertCode(string sourceCode) =>
        new NpoiMigration().Migrate(sourceCode).MigratedCode;

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode) =>
        new NpoiMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static d => new MigrationDiagnostic(d.Id, d.Severity.ToString(), d.Message))
            .ToArray();
}
