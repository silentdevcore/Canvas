using PXA.Infrastructure.Word;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace PXA.Export.Tests;

public sealed class WordIntegrationParityTests
{
    [Fact]
    public void Word_Integration_MultiPage_InsertsExpectedPageBreaks()
    {
        var design = new DesignExportDto
        {
            Id = "int-mp-1",
            Name = "Multi page",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto { Id = "t1", Type = "text", Content = "Page One", X = 10, Y = 10 }
                    ]
                },
                new PageDto
                {
                    Id = "p2",
                    Elements =
                    [
                        new ElementDto { Id = "t2", Type = "text", Content = "Page Two", X = 10, Y = 10 }
                    ]
                },
                new PageDto
                {
                    Id = "p3",
                    Elements =
                    [
                        new ElementDto { Id = "t3", Type = "text", Content = "Page Three", X = 10, Y = 10 }
                    ]
                }
            ]
        };

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var pageBreakCount = doc.MainDocumentPart!
            .Document!
            .Body!
            .Descendants<Break>()
            .Count(b => b.Type?.Value == BreakValues.Page);

        Assert.Equal(2, pageBreakCount); // pages-1
    }

    [Fact]
    public void Word_Integration_Table_UsesFixedLayoutAndExpectedGridWidths()
    {
        var design = new DesignExportDto
        {
            Id = "int-tbl-1",
            Name = "Table parity",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "tbl1",
                            Type = "table",
                            X = 12,
                            Y = 40,
                            Width = 200,
                            Height = 120,
                            HeaderRow = true,
                            ColumnWidths = [50, 150],
                            CellData =
                            [
                                ["H1", "H2"],
                                ["A", "B"]
                            ]
                        }
                    ]
                }
            ]
        };

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var table = doc.MainDocumentPart!.Document!.Body!.Descendants<Table>().Single();

        var layout = table.TableProperties?.GetFirstChild<TableLayout>();
        Assert.NotNull(layout);
        Assert.Equal(TableLayoutValues.Fixed, layout!.Type!.Value);

        var gridWidths = table.Descendants<GridColumn>()
            .Select(c => c.Width!.Value)
            .ToList();

        Assert.Equal(["1000", "3000"], gridWidths); // 50->1000 twips, 150->3000 twips
    }

    [Fact]
    public void Word_Integration_ImageDataUrl_IsEmbeddedAndDrawn()
    {
        const string tinyPng = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO6nN5QAAAAASUVORK5CYII=";

        var design = new DesignExportDto
        {
            Id = "int-img-1",
            Name = "Image inclusion",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "img1",
                            Type = "image",
                            X = 20,
                            Y = 30,
                            Width = 80,
                            Height = 60,
                            Content = tinyPng
                        }
                    ]
                }
            ]
        };

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);

        Assert.NotEmpty(doc.MainDocumentPart!.ImageParts);
        Assert.NotEmpty(doc.MainDocumentPart.Document!.Body!.Descendants<Drawing>());
    }
}