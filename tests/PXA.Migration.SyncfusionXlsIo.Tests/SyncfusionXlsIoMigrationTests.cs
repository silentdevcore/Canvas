using PXA.Migration.SyncfusionXlsIo;

namespace PXA.Migration.SyncfusionXlsIo.Tests;

public sealed class SyncfusionXlsIoMigrationTests
{
    [Fact]
    public void RewritesCoreXlsIoApi()
    {
        const string src = """
            using Syncfusion.XlsIO;
            var excelEngine = new ExcelEngine();
            var application = excelEngine.Excel;
            var workbook = application.Workbooks.Create(1);
            var worksheet = workbook.Worksheets[0];
            worksheet.Range["A1"].Text = "Item";
            worksheet.Range["B1"].Number = 3;
            worksheet.Range["B2"].Formula = "=SUM(B1:B1)";
            worksheet.Range["A1"].CellStyle.Font.Bold = true;
            worksheet.Range["A1:B1"].Merge();
            workbook.SaveAs("out.xlsx");
            excelEngine.Dispose();
            """;

        var code = new SyncfusionXlsIoMigration().Migrate(src).MigratedCode;

        Assert.Contains("new CanvasWorkbook()", code);
        Assert.Contains("workbook.AddSheet(\"Sheet1\")", code);
        Assert.Contains("worksheet.Cell(\"A1\").Value(\"Item\")", code);
        Assert.Contains("worksheet.Cell(\"B1\").Value(3)", code);
        Assert.Contains("worksheet.Cell(\"B2\").Formula(\"=SUM(B1:B1)\")", code);
        Assert.Contains("worksheet.Cell(\"A1\").Style(s => s.Bold(true))", code);
        Assert.Contains("worksheet.Range(\"A1:B1\").Merge()", code);
        Assert.Contains("workbook.Save(\"out.xlsx\")", code);
        // engine scaffolding removed
        Assert.DoesNotContain("ExcelEngine", code);
        Assert.DoesNotContain("Dispose", code);
        Assert.DoesNotContain("Syncfusion", code);
        Assert.Contains("using PXA.Infrastructure.Spreadsheet;", code);
    }
}
