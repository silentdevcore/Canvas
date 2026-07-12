using PXA.Migration.SpireXls;

namespace PXA.Migration.SpireXls.Tests;

public sealed class SpireXlsMigrationTests
{
    [Fact]
    public void RewritesCoreSpireApi()
    {
        const string src = """
            using Spire.Xls;
            var workbook = new Workbook();
            var sheet = workbook.Worksheets[0];
            sheet.Range["A1"].Text = "Item";
            sheet.Range["B1"].NumberValue = 3;
            sheet.Range["B2"].Formula = "=SUM(B1:B1)";
            sheet.Range["A1"].Style.Font.IsBold = true;
            sheet.Range["A1:B1"].Merge();
            workbook.SaveToFile("out.xlsx", ExcelVersion.Version2013);
            """;

        var code = new SpireXlsMigration().Migrate(src).MigratedCode;

        Assert.Contains("new PxaWorkbook()", code);
        Assert.Contains("workbook.AddSheet(\"Sheet1\")", code);
        Assert.Contains("sheet.Cell(\"A1\").Value(\"Item\")", code);
        Assert.Contains("sheet.Cell(\"B1\").Value(3)", code);
        Assert.Contains("sheet.Cell(\"B2\").Formula(\"=SUM(B1:B1)\")", code);
        Assert.Contains("sheet.Cell(\"A1\").Style(s => s.Bold(true))", code);
        Assert.Contains("sheet.Range(\"A1:B1\").Merge()", code);
        Assert.Contains("workbook.Save(\"out.xlsx\")", code);
        Assert.DoesNotContain("Spire", code);
        Assert.Contains("using PXA.Infrastructure.Spreadsheet;", code);
    }

    [Fact]
    public void MapsSetColumnWidthOneBased()
    {
        const string src = """
            using Spire.Xls;
            var workbook = new Workbook();
            var sheet = workbook.Worksheets[0];
            sheet.SetColumnWidth(1, 20);
            workbook.SaveToFile("o.xlsx");
            """;
        var code = new SpireXlsMigration().Migrate(src).MigratedCode;
        Assert.Contains("sheet.Column(0).Width(20)", code);
    }
}
