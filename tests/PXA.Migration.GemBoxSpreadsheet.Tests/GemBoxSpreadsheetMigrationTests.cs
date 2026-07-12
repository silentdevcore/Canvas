using PXA.Migration.GemBoxSpreadsheet;

namespace PXA.Migration.GemBoxSpreadsheet.Tests;

public sealed class GemBoxSpreadsheetMigrationTests
{
    [Fact]
    public void RewritesCoreGemBoxApi()
    {
        const string src = """
            using GemBox.Spreadsheet;
            SpreadsheetInfo.SetLicense("FREE-LIMITED-KEY");
            var wb = new ExcelFile();
            var ws = wb.Worksheets.Add("Sheet1");
            ws.Cells["A1"].Value = "Item";
            ws.Cells[0, 1].Value = 10;
            ws.Cells["B1"].Formula = "=SUM(B2:B3)";
            ws.Cells["A1"].Style.Font.Weight = ExcelFont.BoldWeight;
            wb.Save("out.xlsx");
            """;

        var code = new GemBoxSpreadsheetMigration().Migrate(src).MigratedCode;

        Assert.DoesNotContain("SetLicense", code);             // license dropped
        Assert.Contains("new CanvasWorkbook()", code);
        Assert.Contains("wb.AddSheet(\"Sheet1\")", code);
        Assert.Contains("ws.Cell(\"A1\").Value(\"Item\")", code);
        Assert.Contains("ws.Cell(0, 1).Value(10)", code);       // 0-based: unchanged
        Assert.Contains("ws.Cell(\"B1\").Formula(\"=SUM(B2:B3)\")", code);
        Assert.Contains("ws.Cell(\"A1\").Style(s => s.Bold())", code); // Weight → Bold()
        Assert.Contains("wb.Save(\"out.xlsx\")", code);
        Assert.DoesNotContain("GemBox", code);
        Assert.Contains("using Canvas.Infrastructure.Spreadsheet;", code);
    }

    [Fact]
    public void MapsHorizontalAlignment()
    {
        const string src = """
            using GemBox.Spreadsheet;
            var wb = new ExcelFile();
            var ws = wb.Worksheets.Add("S");
            ws.Cells["A1"].Style.HorizontalAlignment = HorizontalAlignmentStyle.Center;
            wb.Save("o.xlsx");
            """;

        var code = new GemBoxSpreadsheetMigration().Migrate(src).MigratedCode;
        Assert.Contains("ws.Cell(\"A1\").Style(s => s.Align(\"center\"))", code);
    }

    [Fact]
    public void FlagsMergeAndCharts()
    {
        const string src = """
            using GemBox.Spreadsheet;
            var wb = new ExcelFile();
            var ws = wb.Worksheets.Add("S");
            ws.Cells.GetSubrange("A1:B1").Merged = true;
            wb.Save("o.xlsx");
            """;

        var diags = new GemBoxSpreadsheetMigration().Migrate(src).Diagnostics;
        Assert.Contains(diags, d => d.Id == "CANMIGGBSS020");
    }
}
