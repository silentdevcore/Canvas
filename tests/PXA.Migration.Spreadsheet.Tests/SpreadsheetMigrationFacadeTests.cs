using PXA.Migration.Abstractions;

namespace PXA.Migration.Spreadsheet.Tests;

public sealed class SpreadsheetMigrationFacadeTests
{
    [Fact]
    public void ClosedXml_UsesPxaMigrationResult()
    {
        const string source = """
            using ClosedXML.Excel;
            var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Sheet1");
            ws.Cell("A1").Value = "Item";
            wb.SaveAs("out.xlsx");
            """;

        MigrationResult result = new ClosedXmlSpreadsheetMigration().Migrate(source);

        Assert.Contains("new CanvasWorkbook()", result.MigratedCode);
        Assert.Contains("wb.AddSheet(\"Sheet1\")", result.MigratedCode);
        Assert.Contains("ws.Cell(\"A1\").Value(\"Item\")", result.MigratedCode);
        Assert.Contains("wb.Save(\"out.xlsx\")", result.MigratedCode);
    }

    [Fact]
    public void Epplus_MapsCanvasDiagnosticsToPxaDiagnostics()
    {
        const string source = """
            using OfficeOpenXml;
            using var pkg = new ExcelPackage();
            var ws = pkg.Workbook.Worksheets.Add("S");
            ws.Cells[1, 2].Value = 10;
            pkg.SaveAs("o.xlsx");
            """;

        var result = new EpplusSpreadsheetMigration().Migrate(source);

        Assert.Contains("ws.Cell(0, 1).Value(10)", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGEPPL010" && diagnostic.Severity == MigrationDiagnosticSeverity.Info);
    }
}
