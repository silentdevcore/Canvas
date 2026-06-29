using Canvas.Migration.ClosedXmlSpreadsheet;

namespace Canvas.Migration.ClosedXmlSpreadsheet.Tests;

public sealed class ClosedXmlSpreadsheetMigrationTests
{
    private static string Migrate(string src) => new ClosedXmlSpreadsheetMigration().Migrate(src).MigratedCode;

    [Fact]
    public void RewritesCoreWorkbookApi()
    {
        const string src = """
            using ClosedXML.Excel;
            var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Sheet1");
            ws.Cell("A1").Value = "Item";
            ws.Cell("B1").FormulaA1 = "SUM(B2:B3)";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Range("A1:B1").Merge();
            wb.SaveAs("out.xlsx");
            """;

        var code = Migrate(src);

        Assert.Contains("new CanvasWorkbook()", code);
        Assert.Contains("wb.AddSheet(\"Sheet1\")", code);
        Assert.Contains("ws.Cell(\"A1\").Value(\"Item\")", code);
        Assert.Contains("ws.Cell(\"B1\").Formula(\"SUM(B2:B3)\")", code);
        Assert.Contains("ws.Cell(\"A1\").Style(s => s.Bold(true))", code);
        Assert.Contains("ws.Range(\"A1:B1\").Merge()", code);
        Assert.Contains("wb.Save(\"out.xlsx\")", code);
        // ClosedXML using removed, Canvas using added
        Assert.DoesNotContain("ClosedXML", code);
        Assert.Contains("using Canvas.Infrastructure.Spreadsheet;", code);
    }

    [Fact]
    public void ShiftsOneBasedNumericIndexesToZeroBased()
    {
        const string src = """
            using ClosedXML.Excel;
            var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("S");
            ws.Cell(1, 2).Value = 10;
            ws.Column(3).Width = 20;
            wb.SaveAs("o.xlsx");
            """;

        var result = new ClosedXmlSpreadsheetMigration().Migrate(src);
        Assert.Contains("ws.Cell(0, 1).Value(10)", result.MigratedCode);
        Assert.Contains("ws.Column(2).Width(20)", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGCLXL010");
    }

    [Fact]
    public void FlagsUnsupportedFeatures()
    {
        const string src = """
            using ClosedXML.Excel;
            var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("S");
            ws.Range("A1:B2").SetAutoFilter();
            var pt = ws.PivotTables;
            wb.SaveAs("o.xlsx");
            """;

        var diags = new ClosedXmlSpreadsheetMigration().Migrate(src).Diagnostics;
        Assert.Contains(diags, d => d.Id == "CANMIGCLXL030"); // pivot
        Assert.Contains(diags, d => d.Id == "CANMIGCLXL031"); // auto-filter / validation
    }
}
