using Canvas.Infrastructure.Word;
using A = DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace Canvas.Export.Tests;

public sealed class WordPositioningTests
{
    [Fact]
    public void Word_Export_UsesElementXY_ForParagraphIndentAndVerticalSpacing()
    {
        var design = new DesignExportDto
        {
            Id = "pos-1",
            Name = "Positioned",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "t1",
                            Type = "text",
                            X = 50,
                            Y = 100,
                            Width = 200,
                            Height = 20,
                            Content = "First"
                        },
                        new ElementDto
                        {
                            Id = "t2",
                            Type = "text",
                            X = 80,
                            Y = 160,
                            Width = 200,
                            Height = 20,
                            Content = "Second"
                        }
                    ]
                }
            ]
        };

        var bytes = new WordDocumentExporter().Export(design, new ExportOptions(WordFidelityV2: false));

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var body = doc.MainDocumentPart!.Document!.Body!;

        var paragraphs = body.Elements<Paragraph>()
            .Where(p => p.InnerText is "First" or "Second")
            .ToList();

        Assert.Equal(2, paragraphs.Count);

        var firstProps = paragraphs[0].ParagraphProperties!;
        Assert.Equal("1000", firstProps.Indentation?.Left?.Value); // x=50 => 1000 twips
        Assert.Equal("2000", firstProps.SpacingBetweenLines?.Before?.Value); // y=100 => 2000 twips from top

        var secondProps = paragraphs[1].ParagraphProperties!;
        Assert.Equal("1600", secondProps.Indentation?.Left?.Value); // x=80 => 1600 twips
        Assert.Equal("800", secondProps.SpacingBetweenLines?.Before?.Value); // y gap after previous bottom (160-120)=40 => 800
    }

    [Fact]
    public void Word_Export_AppliesTableIndentation_FromElementX()
    {
        var design = new DesignExportDto
        {
            Id = "pos-2",
            Name = "TablePos",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "tbl",
                            Type = "table",
                            X = 60,
                            Y = 120,
                            Width = 300,
                            Height = 120,
                            HeaderRow = true,
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

        // V2 (default): the table floats at absolute page coordinates, matching anchored text/shapes.
        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var table = doc.MainDocumentPart!.Document!.Body!.Elements<Table>().First();
        var pos = table.TableProperties?.GetFirstChild<TablePositionProperties>();

        Assert.NotNull(pos);
        Assert.Equal(HorizontalAnchorValues.Page, pos!.HorizontalAnchor!.Value);
        Assert.Equal(VerticalAnchorValues.Page, pos.VerticalAnchor!.Value);
        Assert.Equal(1200, pos.TablePositionX!.Value); // x=60 => 1200 twips
        Assert.Equal(2400, pos.TablePositionY!.Value); // y=120 => 2400 twips
        Assert.Null(table.TableProperties?.GetFirstChild<TableIndentation>()); // no flow indentation in V2
    }

    [Fact]
    public void Word_Export_LegacyMode_UsesTableIndentation_FromElementX()
    {
        var design = new DesignExportDto
        {
            Id = "pos-2b",
            Name = "TablePosLegacy",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "tbl",
                            Type = "table",
                            X = 60,
                            Y = 120,
                            Width = 300,
                            Height = 120,
                            HeaderRow = true,
                            CellData = [["H1", "H2"], ["A", "B"]],
                        },
                    ],
                },
            ],
        };

        var bytes = new WordDocumentExporter().Export(design, new ExportOptions(WordFidelityV2: false));

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var table = doc.MainDocumentPart!.Document!.Body!.Elements<Table>().First();

        var indentation = table.TableProperties?.GetFirstChild<TableIndentation>();
        Assert.NotNull(indentation);
        Assert.Equal(1200, indentation!.Width!.Value); // x=60 => 1200 twips
        Assert.Null(table.TableProperties?.GetFirstChild<TablePositionProperties>()); // no floating in legacy mode
    }

    [Fact]
    public void Word_Export_UsesAnchoredTextBox_ForPositionedText_WhenFidelityV2Enabled()
    {
        // In v2 mode, text elements use WPS anchored text boxes instead of legacy framePr.
        // The anchor carries absolute X/Y in EMU (1 canvas unit = 12700 EMU).
        var design = new DesignExportDto
        {
            Id = "txt-anchor-1",
            Name = "Text anchor",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "t1",
                            Type = "text",
                            X = 50,
                            Y = 100,
                            Width = 200,
                            Height = 40,
                            Content = "Anchored"
                        }
                    ]
                }
            ]
        };

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var anchor = doc.MainDocumentPart!.Document!.Body!
            .Descendants<DW.Anchor>()
            .FirstOrDefault();

        Assert.NotNull(anchor);
        var hOffset = anchor!.Descendants<DW.PositionOffset>().First();
        var vOffset = anchor.Descendants<DW.PositionOffset>().ElementAt(1);
        Assert.Equal("635000", hOffset.Text);   // x=50 * 12700 EMU
        Assert.Equal("1270000", vOffset.Text);  // y=100 * 12700 EMU
    }

    [Fact]
    public void Word_Export_DoesNotUseFrameProperties_ForPositionedText_WhenFidelityV2Disabled()
    {
        var design = new DesignExportDto
        {
            Id = "txt-frame-2",
            Name = "Text frame off",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "t1",
                            Type = "text",
                            X = 50,
                            Y = 100,
                            Width = 200,
                            Height = 40,
                            Content = "Flow"
                        }
                    ]
                }
            ]
        };

        var bytes = new WordDocumentExporter().Export(design, new ExportOptions(WordFidelityV2: false));

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var paragraph = doc.MainDocumentPart!.Document!.Body!
            .Elements<Paragraph>()
            .First(p => p.InnerText == "Flow");

        var ppr = paragraph.ParagraphProperties;
        Assert.NotNull(ppr);
        Assert.Null(ppr!.GetFirstChild<FrameProperties>());
        Assert.Equal("1000", ppr.Indentation?.Left?.Value);
    }

    [Fact]
    public void Word_Export_UsesAnchors_ForPositionedImagesWithAbsoluteOffsets()
    {
        const string tinyPng = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO6nN5QAAAAASUVORK5CYII=";

        var design = new DesignExportDto
        {
            Id = "img-abs-1",
            Name = "Anchored image",
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
                            X = 50,
                            Y = 100,
                            Width = 80,
                            Height = 40,
                            Content = tinyPng
                        }
                    ]
                }
            ]
        };

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var anchor = doc.MainDocumentPart!.Document!.Body!.Descendants<DW.Anchor>().Single();

        Assert.Equal("635000", anchor.HorizontalPosition?.PositionOffset?.Text); // x=50 => 635000 EMU
        Assert.Equal("1270000", anchor.VerticalPosition?.PositionOffset?.Text); // y=100 => 1270000 EMU
        Assert.Equal(1016000L, anchor.Extent!.Cx!.Value); // width=80 => 1016000 EMU
        Assert.Equal(508000L, anchor.Extent!.Cy!.Value); // height=40 => 508000 EMU
    }

    [Fact]
    public void Word_Export_MapsImageZIndex_ToAnchorRelativeHeight()
    {
        const string tinyPng = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO6nN5QAAAAASUVORK5CYII=";

        var design = new DesignExportDto
        {
            Id = "img-z-1",
            Name = "Z order",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "img-low",
                            Type = "image",
                            X = 40,
                            Y = 40,
                            Width = 60,
                            Height = 40,
                            Content = tinyPng,
                            Style = new() { ["zIndex"] = 1 }
                        },
                        new ElementDto
                        {
                            Id = "img-high",
                            Type = "image",
                            X = 40,
                            Y = 40,
                            Width = 60,
                            Height = 40,
                            Content = tinyPng,
                            Style = new() { ["zIndex"] = 5 }
                        }
                    ]
                }
            ]
        };

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var anchors = doc.MainDocumentPart!.Document!.Body!.Descendants<DW.Anchor>().ToList();

        Assert.Equal(2, anchors.Count);
        Assert.True(anchors[1].RelativeHeight!.Value > anchors[0].RelativeHeight!.Value);
    }

    [Fact]
    public void Word_Export_AssignsDeterministicSequentialDrawingIds_ForImages()
    {
        const string tinyPng = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO6nN5QAAAAASUVORK5CYII=";

        var design = new DesignExportDto
        {
            Id = "img-id-1",
            Name = "Image IDs",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "img-a",
                            Type = "image",
                            X = 10,
                            Y = 10,
                            Width = 30,
                            Height = 20,
                            Content = tinyPng
                        },
                        new ElementDto
                        {
                            Id = "img-b",
                            Type = "image",
                            X = 20,
                            Y = 20,
                            Width = 30,
                            Height = 20,
                            Content = tinyPng
                        }
                    ]
                }
            ]
        };

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var drawingIds = doc.MainDocumentPart!
            .Document!
            .Body!
            .Descendants<DW.DocProperties>()
            .Select(p => p.Id!.Value)
            .ToList();

        Assert.Equal([1U, 2U], drawingIds);
    }

    [Theory]
    [InlineData("fill", false)]
    [InlineData("contain", true)]
    [InlineData("cover", true)]
    public void Word_Export_MapsImageFitMode_ToAspectLock(string fitMode, bool expectedNoChangeAspect)
    {
        const string tinyPng = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO6nN5QAAAAASUVORK5CYII=";

        var design = new DesignExportDto
        {
            Id = "img-fit-1",
            Name = "Fit mode",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "img-fit",
                            Type = "image",
                            X = 15,
                            Y = 25,
                            Width = 90,
                            Height = 40,
                            FitMode = fitMode,
                            Content = tinyPng
                        }
                    ]
                }
            ]
        };

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var locks = doc.MainDocumentPart!
            .Document!
            .Body!
            .Descendants<A.GraphicFrameLocks>()
            .Single();

        Assert.Equal(expectedNoChangeAspect, locks.NoChangeAspect?.Value ?? false);
    }
}
