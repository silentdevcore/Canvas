using Canvas.Migration.EpplusSpreadsheet;

namespace Canvas.Migration.EpplusSpreadsheet.Tests;

public sealed class EpplusSpreadsheetMigrationTests
{
    [Fact]
    public void RewritesCoreEpplusApi()
    {
        const string src = """
            using OfficeOpenXml;
            using var pkg = new ExcelPackage();
            var ws = pkg.Workbook.Worksheets.Add("Sheet1");
            ws.Cells["A1"].Value = "Item";
            ws.Cells["B1"].Formula = "SUM(B2:B3)";
            ws.Cells["A1"].Style.Font.Bold = true;
            ws.Cells["A1:B1"].Merge = true;
            pkg.SaveAs("out.xlsx");
            """;

        var code = new EpplusSpreadsheetMigration().Migrate(src).MigratedCode;

        Assert.Contains("new CanvasWorkbook()", code);
        Assert.Contains("pkg.AddSheet(\"Sheet1\")", code);
        Assert.Contains("ws.Cell(\"A1\").Value(\"Item\")", code);
        Assert.Contains("ws.Cell(\"B1\").Formula(\"SUM(B2:B3)\")", code);
        Assert.Contains("ws.Cell(\"A1\").Style(s => s.Bold(true))", code);
        Assert.Contains("ws.Range(\"A1:B1\").Merge()", code);
        Assert.Contains("pkg.Save(\"out.xlsx\")", code);
        Assert.DoesNotContain("OfficeOpenXml", code);
        Assert.Contains("using Canvas.Infrastructure.Spreadsheet;", code);
    }

    [Fact]
    public void ShiftsNumericCellsIndexer()
    {
        const string src = """
            using OfficeOpenXml;
            using var pkg = new ExcelPackage();
            var ws = pkg.Workbook.Worksheets.Add("S");
            ws.Cells[1, 2].Value = 10;
            pkg.SaveAs("o.xlsx");
            """;

        var result = new EpplusSpreadsheetMigration().Migrate(src);
        Assert.Contains("ws.Cell(0, 1).Value(10)", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGEPPL010");
    }

    [Fact]
    public void MapsHorizontalAlignment()
    {
        const string src = """
            using OfficeOpenXml;
            using var pkg = new ExcelPackage();
            var ws = pkg.Workbook.Worksheets.Add("S");
            ws.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            pkg.SaveAs("o.xlsx");
            """;

        var code = new EpplusSpreadsheetMigration().Migrate(src).MigratedCode;
        Assert.Contains("ws.Cell(\"A1\").Style(s => s.Align(\"center\"))", code);
    }

    [Fact]
    public void FlagsUnsupportedFeatures()
    {
        const string src = """
            using OfficeOpenXml;
            using var pkg = new ExcelPackage();
            var ws = pkg.Workbook.Worksheets.Add("S");
            var cf = ws.ConditionalFormatting;
            var pt = ws.PivotTables;
            pkg.SaveAs("o.xlsx");
            """;

        var diags = new EpplusSpreadsheetMigration().Migrate(src).Diagnostics;
        Assert.Contains(diags, d => d.Id == "CANMIGEPPL030");
        Assert.Contains(diags, d => d.Id == "CANMIGEPPL031");
    }
}
