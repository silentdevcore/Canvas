using PXA.Migration.Npoi;

namespace PXA.Migration.Npoi.Tests;

public sealed class NpoiMigrationTests
{
    [Fact]
    public void InlinesRowCellModelToPxaCells()
    {
        const string src = """
            using NPOI.XSSF.UserModel;
            using NPOI.SS.UserModel;
            IWorkbook wb = new XSSFWorkbook();
            ISheet sheet = wb.CreateSheet("Sales");
            IRow row0 = sheet.CreateRow(0);
            ICell cell = row0.CreateCell(0);
            cell.SetCellValue("Item");
            row0.CreateCell(1).SetCellValue("Qty");
            IRow row1 = sheet.CreateRow(1);
            row1.CreateCell(0).SetCellValue("Coffee");
            row1.CreateCell(1).SetCellValue(3);
            sheet.CreateRow(2).CreateCell(1).SetCellFormula("SUM(B1:B2)");
            """;

        var code = new NpoiMigration().Migrate(src).MigratedCode;

        Assert.Contains("new PxaWorkbook()", code);
        Assert.Contains("wb.AddSheet(\"Sales\")", code);
        Assert.Contains("sheet.Cell(0, 0).Value(\"Item\")", code);   // via cell var
        Assert.Contains("sheet.Cell(0, 1).Value(\"Qty\")", code);    // via row var chain
        Assert.Contains("sheet.Cell(1, 0).Value(\"Coffee\")", code);
        Assert.Contains("sheet.Cell(1, 1).Value(3)", code);
        Assert.Contains("sheet.Cell(2, 1).Formula(\"SUM(B1:B2)\")", code); // full chain
        // addressing-only declarations dropped
        Assert.DoesNotContain("CreateRow", code);
        Assert.DoesNotContain("CreateCell", code);
        Assert.DoesNotContain("NPOI", code);
        Assert.Contains("using PXA.Infrastructure.Spreadsheet;", code);
    }

    [Fact]
    public void MapsColumnWidthAndWriteWithDiagnostics()
    {
        const string src = """
            using NPOI.XSSF.UserModel;
            var wb = new XSSFWorkbook();
            var sheet = wb.CreateSheet("S");
            sheet.SetColumnWidth(0, 5120);
            var fs = new FileStream("o.xlsx", FileMode.Create);
            wb.Write(fs);
            """;
        var result = new NpoiMigration().Migrate(src);
        Assert.Contains("sheet.Column(0).Width(5120)", result.MigratedCode);
        Assert.Contains("wb.Save(\"output.xlsx\")", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGNPOI012");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGNPOI013");
    }
}
