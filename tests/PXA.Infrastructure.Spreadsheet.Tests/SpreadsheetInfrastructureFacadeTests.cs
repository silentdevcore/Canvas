using PXA.Core.Contracts;
using PXA.Infrastructure.Spreadsheet;

namespace PXA.Infrastructure.Spreadsheet.Tests;

public sealed class SpreadsheetInfrastructureFacadeTests
{
    [Fact]
    public void CanvasWorkbook_BuildsPxaWorkbookContract()
    {
        var workbook = new CanvasWorkbook("Budget");
        workbook.AddSheet("Data")
            .Cell("A1")
            .Value("Amount")
            .Style(style => style.Bold().Background("#e0f2fe"));
        workbook.Sheet("Data").Cell("B1").Formula("=1+2");
        workbook.DefineName("Amounts", "Data!$A$1:$A$1");

        var dto = workbook.ToWorkbook();

        Assert.IsType<SpreadsheetDto>(dto);
        Assert.Equal("Budget", dto.Name);
        Assert.Single(dto.Sheets);
        Assert.Equal("=1+2", dto.Sheets[0].Cells.Single(c => c.Col == 1).Formula);
        Assert.Single(dto.DefinedNames);
    }

    [Fact]
    public void ExcelWorkbookExporterImporter_RoundTripsPxaWorkbook()
    {
        var workbook = BuildWorkbook();

        var bytes = new ExcelWorkbookExporter().Export(workbook);
        using var stream = new MemoryStream(bytes);
        var imported = new ExcelWorkbookImporter().Import(stream, "budget.xlsx");

        Assert.Equal("budget", imported.Name);
        Assert.Equal("Data", imported.Sheets[0].Name);
        Assert.Contains(imported.Sheets[0].Cells, cell => cell.Type == "text" && cell.Value?.ToString() == "Amount");
    }

    [Fact]
    public void Calculator_AndValidator_UsePxaWorkbookContract()
    {
        var workbook = BuildWorkbook();

        var calculated = new SpreadsheetCalculator().Calculate(workbook);
        var validation = new SpreadsheetValidator().Validate(calculated);

        Assert.IsType<SpreadsheetDto>(calculated);
        Assert.True(validation.Valid);
        Assert.DoesNotContain(validation.Issues, issue => issue.Severity == "error");
    }

    [Fact]
    public void SpreadsheetToDesign_ReturnsPxaDesignContract()
    {
        var design = new SpreadsheetToDesignConverter().Convert(BuildWorkbook(), gridlines: true);

        Assert.IsType<DesignExportDto>(design);
        Assert.Equal("Data", design.Name);
        Assert.Equal("table", design.Pages[0].Elements[0].Type);
    }

    [Fact]
    public void ExcelDocumentExporter_ExportsTablesFromPxaDesign()
    {
        var exporter = new ExcelDocumentExporter();

        var bytes = exporter.Export(new DesignExportDto
        {
            Id = "design-1",
            Name = "Sheet Export",
            Pages =
            [
                new PageDto
                {
                    Id = "page-1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "table-1",
                            Type = "table",
                            Name = "Data",
                            CellData =
                            [
                                ["Name", "Amount"],
                                ["A", "42"],
                            ],
                        }
                    ],
                }
            ],
        });

        Assert.True(bytes.Length > 0);
        Assert.Equal("excel", exporter.FormatKey);
        Assert.Equal(".xlsx", exporter.FileExtension);
        Assert.False(exporter.Capabilities.SupportsRichText);
    }

    [Fact]
    public void SheetCapabilities_MirrorCanvasSpreadsheetCapabilities()
    {
        var capabilities = new SheetRendererCapabilities();

        Assert.Equal("sheet", capabilities.RendererKey);
        Assert.True(capabilities.SupportsInternalLinks);
        Assert.True(capabilities.SupportsExternalLinks);
        Assert.True(capabilities.SupportsHeaderFooter);
        Assert.False(capabilities.SupportsWatermarks);
    }

    private static SpreadsheetDto BuildWorkbook() => new()
    {
        Id = "workbook-1",
        Name = "Budget",
        Sheets =
        [
            new SheetDto
            {
                Id = "sheet-1",
                Name = "Data",
                Cells =
                [
                    new CellDto { Row = 0, Col = 0, Type = "text", Value = "Amount" },
                    new CellDto { Row = 1, Col = 0, Type = "number", Value = 1 },
                    new CellDto { Row = 2, Col = 0, Type = "number", Value = 2 },
                    new CellDto { Row = 3, Col = 0, Type = "formula", Formula = "=SUM(A2:A3)" },
                ],
            }
        ],
    };
}
