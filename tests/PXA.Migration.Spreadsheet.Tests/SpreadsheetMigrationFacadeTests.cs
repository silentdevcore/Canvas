using PXA.Migration.Abstractions;

namespace PXA.Migration.Spreadsheet.Tests;

public sealed class SpreadsheetMigrationFacadeTests
{
    [Fact]
    public void AsposeCells_UsesPxaMigrationResult()
    {
        const string source = """
            using Aspose.Cells;
            var wb = new Workbook();
            var ws = wb.Worksheets[0];
            ws.Cells["A1"].PutValue("Item");
            ws.Cells[0, 1].PutValue(10);
            ws.Cells["B1"].Formula = "=SUM(B2:B3)";
            wb.Save("out.xlsx");
            """;

        MigrationResult result = new AsposeCellsMigration().Migrate(source);

        Assert.Contains("new PxaWorkbook()", result.MigratedCode);
        Assert.Contains("wb.AddSheet(\"Sheet1\")", result.MigratedCode);
        Assert.Contains("ws.Cell(\"A1\").Value(\"Item\")", result.MigratedCode);
        Assert.Contains("ws.Cell(0, 1).Value(10)", result.MigratedCode);
        Assert.Contains("ws.Cell(\"B1\").Formula(\"=SUM(B2:B3)\")", result.MigratedCode);
        Assert.Contains("wb.Save(\"out.xlsx\")", result.MigratedCode);
    }

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

        Assert.Contains("new PxaWorkbook()", result.MigratedCode);
        Assert.Contains("wb.AddSheet(\"Sheet1\")", result.MigratedCode);
        Assert.Contains("ws.Cell(\"A1\").Value(\"Item\")", result.MigratedCode);
        Assert.Contains("wb.Save(\"out.xlsx\")", result.MigratedCode);
    }

    [Fact]
    public void Epplus_MapsPxaDiagnosticsToPxaDiagnostics()
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

    [Fact]
    public void GemBoxSpreadsheet_UsesPxaMigrationResult()
    {
        const string source = """
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

        MigrationResult result = new GemBoxSpreadsheetMigration().Migrate(source);

        Assert.DoesNotContain("SetLicense", result.MigratedCode);
        Assert.Contains("new PxaWorkbook()", result.MigratedCode);
        Assert.Contains("wb.AddSheet(\"Sheet1\")", result.MigratedCode);
        Assert.Contains("ws.Cell(\"A1\").Value(\"Item\")", result.MigratedCode);
        Assert.Contains("ws.Cell(0, 1).Value(10)", result.MigratedCode);
        Assert.Contains("ws.Cell(\"B1\").Formula(\"=SUM(B2:B3)\")", result.MigratedCode);
        Assert.Contains("ws.Cell(\"A1\").Style(s => s.Bold())", result.MigratedCode);
        Assert.Contains("wb.Save(\"out.xlsx\")", result.MigratedCode);
    }

    [Fact]
    public void Npoi_UsesPxaMigrationResult()
    {
        const string source = """
            using NPOI.XSSF.UserModel;
            using NPOI.SS.UserModel;
            IWorkbook wb = new XSSFWorkbook();
            ISheet sheet = wb.CreateSheet("Sales");
            IRow row0 = sheet.CreateRow(0);
            ICell cell = row0.CreateCell(0);
            cell.SetCellValue("Item");
            row0.CreateCell(1).SetCellValue("Qty");
            """;

        MigrationResult result = new NpoiMigration().Migrate(source);

        Assert.Contains("new PxaWorkbook()", result.MigratedCode);
        Assert.Contains("wb.AddSheet(\"Sales\")", result.MigratedCode);
        Assert.Contains("sheet.Cell(0, 0).Value(\"Item\")", result.MigratedCode);
        Assert.Contains("sheet.Cell(0, 1).Value(\"Qty\")", result.MigratedCode);
        Assert.DoesNotContain("CreateRow", result.MigratedCode);
        Assert.DoesNotContain("CreateCell", result.MigratedCode);
    }

    [Fact]
    public void SpireXls_UsesPxaMigrationResult()
    {
        const string source = """
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

        MigrationResult result = new SpireXlsMigration().Migrate(source);

        Assert.Contains("new PxaWorkbook()", result.MigratedCode);
        Assert.Contains("workbook.AddSheet(\"Sheet1\")", result.MigratedCode);
        Assert.Contains("sheet.Cell(\"A1\").Value(\"Item\")", result.MigratedCode);
        Assert.Contains("sheet.Cell(\"B1\").Value(3)", result.MigratedCode);
        Assert.Contains("sheet.Cell(\"B2\").Formula(\"=SUM(B1:B1)\")", result.MigratedCode);
        Assert.Contains("sheet.Cell(\"A1\").Style(s => s.Bold(true))", result.MigratedCode);
        Assert.Contains("sheet.Range(\"A1:B1\").Merge()", result.MigratedCode);
        Assert.Contains("workbook.Save(\"out.xlsx\")", result.MigratedCode);
    }

    [Fact]
    public void SpreadsheetLight_UsesPxaMigrationResult()
    {
        const string source = """
            using SpreadsheetLight;
            var doc = new SLDocument();
            doc.SetCellValue("A1", "Item");
            doc.SetCellValue("B1", 3);
            doc.SetCellValue(2, 1, "Coffee");
            doc.SetCellValue("B4", "=SUM(B1:B3)");
            doc.SaveAs("out.xlsx");
            """;

        MigrationResult result = new SpreadsheetLightMigration().Migrate(source);

        Assert.Contains("new PxaWorkbook()", result.MigratedCode);
        Assert.Contains("var sheet = doc.AddSheet(\"Sheet1\");", result.MigratedCode);
        Assert.Contains("sheet.Cell(\"A1\").Value(\"Item\")", result.MigratedCode);
        Assert.Contains("sheet.Cell(\"B1\").Value(3)", result.MigratedCode);
        Assert.Contains("sheet.Cell(1, 0).Value(\"Coffee\")", result.MigratedCode);
        Assert.Contains("sheet.Cell(\"B4\").Formula(\"=SUM(B1:B3)\")", result.MigratedCode);
        Assert.Contains("doc.Save(\"out.xlsx\")", result.MigratedCode);
    }

    [Fact]
    public void SyncfusionXlsIo_UsesPxaMigrationResult()
    {
        const string source = """
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

        MigrationResult result = new SyncfusionXlsIoMigration().Migrate(source);

        Assert.Contains("new PxaWorkbook()", result.MigratedCode);
        Assert.Contains("workbook.AddSheet(\"Sheet1\")", result.MigratedCode);
        Assert.Contains("worksheet.Cell(\"A1\").Value(\"Item\")", result.MigratedCode);
        Assert.Contains("worksheet.Cell(\"B1\").Value(3)", result.MigratedCode);
        Assert.Contains("worksheet.Cell(\"B2\").Formula(\"=SUM(B1:B1)\")", result.MigratedCode);
        Assert.Contains("worksheet.Cell(\"A1\").Style(s => s.Bold(true))", result.MigratedCode);
        Assert.Contains("worksheet.Range(\"A1:B1\").Merge()", result.MigratedCode);
        Assert.Contains("workbook.Save(\"out.xlsx\")", result.MigratedCode);
        Assert.DoesNotContain("ExcelEngine", result.MigratedCode);
        Assert.DoesNotContain("Dispose", result.MigratedCode);
    }
}
