using PXA.Migration.Spreadsheet;

namespace PXA.WebApi.Services.Converters;

public sealed class SyncfusionXlsIoConverter : BaseSpreadsheetConverter
{
    public override string FrameworkId => "SyncfusionXlsIo";

    public override string FrameworkName => "Syncfusion XlsIO";

    public override string Status => "full";

    public override string Description =>
        "Spreadsheet code migration: Syncfusion XlsIO (ExcelEngine/IWorkbook) → PXA spreadsheet API (PxaWorkbook). Roslyn-based: drops the ExcelEngine/IApplication scaffolding, Workbooks.Create → new PxaWorkbook(), Worksheets[0] → AddSheet, Range[\"A1\"] → Cell(..), Text/Value/Number/Formula → method, CellStyle.Font.Bold style, SetColumnWidth → Column().Width(), SaveAs → Save. Charts and complex styles need manual review.";

    public override string ConvertCode(string sourceCode) =>
        new SyncfusionXlsIoMigration().Migrate(sourceCode).MigratedCode;

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode) =>
        new SyncfusionXlsIoMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static d => new MigrationDiagnostic(d.Id, d.Severity.ToString(), d.Message))
            .ToArray();
}
