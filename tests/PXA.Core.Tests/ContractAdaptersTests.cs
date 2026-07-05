using PXA.Core.Contracts;

namespace PXA.Core.Tests;

public sealed class ContractAdaptersTests
{
    [Fact]
    public void DesignExportDto_RoundTripsThroughCanvasContract()
    {
        var pxaDesign = new DesignExportDto
        {
            Id = "design-1",
            Name = "PXA Design",
            PageSettings = new PageSettingsDto
            {
                Width = 595,
                Height = 842,
                Orientation = "portrait",
                Margins = new MarginsDto { Top = 10, Right = 20, Bottom = 30, Left = 40 },
                Encryption = new PdfEncryptionDto
                {
                    Enabled = true,
                    UserPassword = "user",
                    OwnerPassword = "owner",
                    Permissions = new PdfEncryptionPermissionsDto { Copy = false },
                },
            },
            Pages =
            [
                new PageDto
                {
                    Id = "page-1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "text-1",
                            Type = "text",
                            Content = "Hello PXA",
                            X = 12,
                            Y = 34,
                            Width = 200,
                            Height = 40,
                            Style = new Dictionary<string, object> { ["fontSize"] = 14 },
                            CellStyles =
                            [
                                new CellStyleDto
                                {
                                    Row = 0,
                                    Col = 1,
                                    BorderBottom = new CellBorderSideDto { Color = "#000000", Width = 1 },
                                }
                            ],
                        }
                    ],
                }
            ],
        };

        var canvasDesign = pxaDesign.ToCanvas();
        var roundTrip = canvasDesign.ToPxa();

        Assert.Equal("PXA Design", canvasDesign.Name);
        Assert.Equal("Hello PXA", roundTrip.Pages[0].Elements[0].Content);
        Assert.False(roundTrip.PageSettings!.Encryption!.Permissions!.Copy);
        Assert.Equal("#000000", roundTrip.Pages[0].Elements[0].CellStyles![0].BorderBottom!.Color);
    }

    [Fact]
    public void SpreadsheetDto_RoundTripsThroughCanvasContract()
    {
        var workbook = new SpreadsheetDto
        {
            Id = "book-1",
            Name = "Budget",
            Sheets =
            [
                new SheetDto
                {
                    Id = "sheet-1",
                    Name = "Sheet1",
                    Cells =
                    [
                        new CellDto
                        {
                            Row = 0,
                            Col = 0,
                            Type = "formula",
                            Formula = "=SUM(B1:B2)",
                            Style = new CellStyleDto { FontSize = 12, Bold = true },
                        }
                    ],
                    PageSetup = new PageSetupDto { Orientation = "landscape" },
                }
            ],
        };

        var canvasWorkbook = workbook.ToCanvas();
        var roundTrip = canvasWorkbook.ToPxa();

        Assert.Equal("Budget", canvasWorkbook.Name);
        Assert.Equal("=SUM(B1:B2)", roundTrip.Sheets[0].Cells[0].Formula);
        Assert.True(roundTrip.Sheets[0].Cells[0].Style!.Bold);
        Assert.Equal("landscape", roundTrip.Sheets[0].PageSetup!.Orientation);
    }

    [Fact]
    public void ExportOptions_MapsWithoutJsonRoundTrip()
    {
        var options = new ExportOptions(Dpi: 144, Quality: 90, WordFidelityV2: false);

        var canvasOptions = options.ToCanvas();
        var roundTrip = canvasOptions.ToPxa();

        Assert.Equal(144, canvasOptions.Dpi);
        Assert.Equal(90, roundTrip.Quality);
        Assert.False(roundTrip.WordFidelityV2);
    }
}
