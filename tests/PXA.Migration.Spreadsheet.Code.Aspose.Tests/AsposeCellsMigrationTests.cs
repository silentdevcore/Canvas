using PXA.Migration.Spreadsheet.Code.Aspose;

namespace PXA.Migration.Spreadsheet.Code.Aspose.Tests;

public sealed class AsposeCellsMigrationTests
{
    [Fact]
    public void RewritesCoreAsposeApi()
    {
        const string src = """
            using Aspose.Cells;
            var wb = new Workbook();
            var ws = wb.Worksheets[0];
            ws.Cells["A1"].PutValue("Item");
            ws.Cells[0, 1].PutValue(10);
            ws.Cells["B1"].Formula = "=SUM(B2:B3)";
            ws.Cells.SetColumnWidth(0, 20);
            wb.Save("out.xlsx");
            """;

        var code = new AsposeCellsMigration().Migrate(src).MigratedCode;

        Assert.Contains("new PxaWorkbook()", code);
        Assert.Contains("wb.AddSheet(\"Sheet1\")", code);            // Worksheets[0] → AddSheet
        Assert.Contains("ws.Cell(\"A1\").Value(\"Item\")", code);    // Cells[..] + PutValue → Cell().Value()
        Assert.Contains("ws.Cell(0, 1).Value(10)", code);            // 0-based: unchanged
        Assert.Contains("ws.Cell(\"B1\").Formula(\"=SUM(B2:B3)\")", code);
        Assert.Contains("ws.Column(0).Width(20)", code);             // SetColumnWidth → Column().Width()
        Assert.Contains("wb.Save(\"out.xlsx\")", code);
        Assert.DoesNotContain("Aspose", code);
        Assert.Contains("using PXA.Infrastructure.Spreadsheet;", code);
    }

    [Fact]
    public void FlagsStyleAndChartPatterns()
    {
        const string src = """
            using Aspose.Cells;
            var wb = new Workbook();
            var ws = wb.Worksheets[0];
            var style = ws.Cells["A1"].GetStyle();
            style.Font.IsBold = true;
            ws.Cells["A1"].SetStyle(style);
            wb.Save("o.xlsx");
            """;

        var diags = new AsposeCellsMigration().Migrate(src).Diagnostics;
        Assert.Contains(diags, d => d.Id == "CANMIGASPC020"); // GetStyle/SetStyle
        Assert.Contains(diags, d => d.Id == "CANMIGASPC011"); // default-sheet note
    }
}
