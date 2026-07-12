using PXA.Migration.SpreadsheetLight;

namespace PXA.Migration.SpreadsheetLight.Tests;

public sealed class SpreadsheetLightMigrationTests
{
    [Fact]
    public void InjectsWorksheetAndRetargetsCells()
    {
        const string src = """
            using SpreadsheetLight;
            var doc = new SLDocument();
            doc.SetCellValue("A1", "Item");
            doc.SetCellValue("B1", 3);
            doc.SetCellValue(2, 1, "Coffee");
            doc.SetCellValue("B4", "=SUM(B1:B3)");
            doc.SaveAs("out.xlsx");
            """;

        var code = new SpreadsheetLightMigration().Migrate(src).MigratedCode;

        Assert.Contains("new CanvasWorkbook()", code);
        Assert.Contains("var sheet = doc.AddSheet(\"Sheet1\");", code);   // injected worksheet
        Assert.Contains("sheet.Cell(\"A1\").Value(\"Item\")", code);
        Assert.Contains("sheet.Cell(\"B1\").Value(3)", code);
        Assert.Contains("sheet.Cell(1, 0).Value(\"Coffee\")", code);      // 1-based → 0-based
        Assert.Contains("sheet.Cell(\"B4\").Formula(\"=SUM(B1:B3)\")", code); // "=" → Formula
        Assert.Contains("doc.Save(\"out.xlsx\")", code);
        Assert.DoesNotContain("SpreadsheetLight", code);
        Assert.Contains("using PXA.Infrastructure.Spreadsheet;", code);
    }
}
