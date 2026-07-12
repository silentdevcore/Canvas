using PXA.Migration.Spreadsheet;

namespace PXA.WebApi.Services.Converters;

public sealed class SpireXlsConverter : BaseSpreadsheetConverter
{
    public override string FrameworkId => "SpireXls";

    public override string FrameworkName => "Spire.XLS";

    public override string Status => "full";

    public override string Description =>
        "Spreadsheet code migration: Spire.XLS Workbook → PXA spreadsheet API (PxaWorkbook). Roslyn-based: Worksheets[0] → AddSheet, Range[\"A1\"] → Cell(..), Text/Value/Number/Formula → method, IsBold/IsItalic/Size style, Range[..].Merge(), SetColumnWidth → Column().Width(), SaveToFile → Save. Charts and complex styles need manual review.";

    public override string ConvertCode(string sourceCode) =>
        new SpireXlsMigration().Migrate(sourceCode).MigratedCode;

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode) =>
        new SpireXlsMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static d => new MigrationDiagnostic(d.Id, d.Severity.ToString(), d.Message))
            .ToArray();
}
