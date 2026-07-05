using Canvas.Pdf;
using PXA.Core.Contracts;
using PXA.Generator;

namespace PXA.Generator.Tests;

public sealed class PdfFacadeTests
{
    [Fact]
    public void CreateDocument_ReturnsCanvasPdfDocument()
    {
        var document = Pdf.CreateDocument();

        Assert.IsType<PdfDocument>(document);
        var page = document.AddPage(300, 180);
        page.DrawTextFromTop("PXA generator facade", 24, 24, 12);

        var bytes = document.ToBytes();

        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }

    [Fact]
    public void CreateDocument_PreservesDefaultFontOption()
    {
        var document = Pdf.CreateDocument(PdfStandardFont.Courier);

        Assert.Equal(PdfStandardFont.Courier, document.DefaultFont);
    }

    [Fact]
    public void CreateWorkbook_ReturnsSpreadsheetWorkbook()
    {
        var workbook = Spreadsheet.CreateWorkbook("Sales");
        var sheet = workbook.AddSheet("Q1");
        sheet.Cell("A1").Value("Revenue");
        sheet.Cell("B1").Value(42);

        var bytes = workbook.ToXlsx();

        Assert.Equal("Sales", workbook.Name);
        Assert.True(bytes.Length > 0);
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]
    public void ExportWord_ReturnsDocxBytes()
    {
        var design = new DesignExportDto
        {
            Name = "PXA Word",
            Pages =
            [
                new PageDto
                {
                    Elements =
                    [
                        new ElementDto
                        {
                            Type = "text",
                            X = 24,
                            Y = 24,
                            Width = 180,
                            Height = 32,
                            Content = "PXA generator facade",
                        }
                    ]
                }
            ]
        };

        var bytes = Word.Export(design);

        Assert.True(bytes.Length > 0);
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }
}
