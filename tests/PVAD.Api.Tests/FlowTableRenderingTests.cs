using System.Text.Json;
using PVAD.Importer;
using PVAD.Importer.Document;
using PVAD.Pdf;

namespace PVAD.Api.Tests;

public sealed class FlowTableRenderingTests
{
    private static PdfFlowOptions FlowOptions => new()
    {
        PageWidth = 300, PageHeight = 180, MarginLeft = 20, MarginRight = 20,
        MarginTop = 20, MarginBottom = 20, ParagraphSpacing = 0
    };

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    public async Task RowStylesHeightsAndFooterPolicySurvivePagination(bool header, bool footer, bool repeatFooter)
    {
        var rows = new List<IReadOnlyList<string>>();
        if (header) rows.Add(new[] { "Heading", "Units" });
        for (var index = 1; index <= 7; index++) rows.Add(new[] { $"Item{index}", index.ToString() });
        if (footer) rows.Add(new[] { "Summary", "28" });
        var document = new PdfDocument();
        var result = document.CreateFlow(FlowOptions).AddSimpleTable(rows, new PdfTableOptions
        {
            HasHeaderRow = header, HasFooterRow = footer, RepeatFooterOnEachChunk = repeatFooter,
            RowHeights = Enumerable.Range(0, rows.Count).Select(index => 28d + index * 2).ToArray(),
            RowLineHeights = Enumerable.Range(0, rows.Count).Select(index => 12d + index).ToArray(),
            CellStyles = Enumerable.Range(0, rows.Count).Select(index => new PdfTableCellStyle
            {
                Row = index, Column = 0, Italic = true, Bold = false, FontSize = 11 + index,
                Padding = index, BackgroundColor = PdfColor.FromRgb(220, 240, 255),
                BorderBottom = new() { Width = 2, Color = PdfColor.FromRgb(255, 0, 0) }
            }).ToArray()
        });
        var parsed = await Parse(document);
        Assert.True(parsed.Pages.Count > 1);
        Assert.Equal(rows.Count, result.RowCount);
        var expectedHeight = 0d;
        foreach (var page in parsed.Pages)
        {
            var labels = page.TextObjects.Select(text => text.Text).ToArray();
            Assert.Equal(header ? 1 : 0, labels.Count(label => label == "Heading"));
            Assert.Equal(footer && (repeatFooter || page == parsed.Pages[^1]) ? 1 : 0,
                labels.Count(label => label == "Summary"));
            foreach (var text in page.TextObjects.Where(text => rows.Any(row => row[0] == text.Text)))
            {
                var original = rows.FindIndex(row => row[0] == text.Text);
                Assert.True(text.Italic);
                Assert.False(text.Bold);
                Assert.Equal(11 + original, text.FontSize);
                Assert.Equal(20 + original, text.Transform.E, 3);
                Assert.InRange(text.Transform.F, 20, 160);
                expectedHeight += 28 + original * 2;
            }
        }
        for (var index = 1; index <= 7; index++)
            Assert.Single(parsed.Pages.SelectMany(page => page.TextObjects).Where(text => text.Text == $"Item{index}"));
        Assert.Equal(expectedHeight, result.Height, 3);
    }

    [Fact]
    public async Task AutomaticRowHeightUsesFontAndPaddingBeforeChoosingPageBreaks()
    {
        var document = new PdfDocument();
        var result = document.CreateFlow(FlowOptions).AddSimpleTable(
            [new[] { "Heading" }, new[] { "Large" }, new[] { "Small" }], new PdfTableOptions
            {
                AutoRowHeight = true,
                CellStyles = [new() { Row = 1, Column = 0, FontSize = 50, Padding = 20 }]
            });
        var parsed = await Parse(document);
        Assert.Equal(2, parsed.Pages.Count);
        Assert.Contains(parsed.Pages[0].TextObjects, text => text.Text == "Large" && text.FontSize == 50);
        Assert.Contains(parsed.Pages[1].TextObjects, text => text.Text == "Small");
        Assert.Equal(172, result.Height, 3); // 24 + 100, then repeated 24 + 24.
    }

    [Fact]
    public async Task PartiallyUsedPageMovesHeaderWithItsFirstDataRow()
    {
        var document = new PdfDocument();
        var flow = document.CreateFlow(FlowOptions);
        flow.AddTextLine("Previous", new PdfDrawTextOptions { FontSize = 100 });
        flow.AddSimpleTable([new[] { "Heading" }, new[] { "Item" }], new PdfTableOptions { RowHeight = 30 });
        var parsed = await Parse(document);
        Assert.Equal(2, parsed.Pages.Count);
        Assert.DoesNotContain(parsed.Pages[0].TextObjects, text => text.Text == "Heading");
        Assert.Contains(parsed.Pages[1].TextObjects, text => text.Text == "Heading");
    }

    [Fact]
    public async Task ContinuationMarkersReserveSpaceOnBothSidesOfTheBreak()
    {
        var document = new PdfDocument();
        document.CreateFlow(FlowOptions).AddSimpleTable(
            [new[] { "Heading" }, new[] { "Item1" }, new[] { "Item2" }, new[] { "Item3" }, new[] { "Item4" }],
            new PdfTableOptions
            {
                RowHeight = 50, ShowContinuationMarkers = true, ContinuationHeaderText = "Continued",
                ContinuationFooterText = "More", ContinuationFontSize = 9
            });
        var parsed = await Parse(document);
        Assert.Equal(4, parsed.Pages.Count);
        for (var index = 0; index < parsed.Pages.Count; index++)
        {
            var page = parsed.Pages[index];
            Assert.Equal(index > 0 ? 1 : 0, page.TextObjects.Count(text => text.Text == "Continued"));
            Assert.Equal(index < parsed.Pages.Count - 1 ? 1 : 0, page.TextObjects.Count(text => text.Text == "More"));
            Assert.All(page.TextObjects, text => Assert.InRange(text.Transform.F, 20, 160));
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task TablesWithoutDataStillRenderTheirHeaderOrFooter(bool header, bool footer)
    {
        var document = new PdfDocument();
        var rows = new List<IReadOnlyList<string>>();
        if (header) rows.Add(new[] { "Heading" });
        if (footer) rows.Add(new[] { "Summary" });
        document.CreateFlow(FlowOptions).AddSimpleTable(rows, new PdfTableOptions { HasHeaderRow = header, HasFooterRow = footer });
        var parsed = await Parse(document);
        Assert.Single(parsed.Pages);
        Assert.Equal(rows.Count, parsed.Pages[0].TextObjects.Count());
    }

    [Theory]
    [InlineData(200)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task OversizedOrNonFiniteRowsFailBeforeAnyTableIsPainted(double height)
    {
        var document = new PdfDocument();
        var flow = document.CreateFlow(FlowOptions);
        flow.CurrentPage.DrawText("Existing", 20, 160);
        var before = document.ToBytes();
        Assert.ThrowsAny<Exception>(() => flow.AddSimpleTable(
            [new[] { "Heading" }, new[] { "First" }, new[] { "Oversized" }],
            new PdfTableOptions { RowHeights = [24, 24, height] }));
        Assert.Single(document.Pages);
        Assert.Equal(before, document.ToBytes());
        var parsed = await Parse(document);
        Assert.DoesNotContain(parsed.Pages[0].TextObjects, text => text.Text == "First");
    }

    [Fact]
    public void InvalidSourceRowArraysAreRejectedBeforePagination()
    {
        var document = new PdfDocument();
        Assert.Throws<ArgumentException>(() => document.CreateFlow(FlowOptions).AddSimpleTable(
            [new[] { "Heading" }, new[] { "Data" }], new PdfTableOptions { RowHeights = [24] }));
    }

    [Fact]
    public async Task AlternatingRowFillsContinueAcrossOddSizedChunks()
    {
        var document = new PdfDocument();
        document.CreateFlow(FlowOptions).AddSimpleTable(
            Enumerable.Range(0, 5).Select(index => (IReadOnlyList<string>)new[] { $"Item{index}" }).ToArray(),
            new PdfTableOptions { HasHeaderRow = false, RowHeight = 40, AlternateRowFillColor = PdfColor.FromRgb(255, 0, 0) });
        var parsed = await Parse(document);
        Assert.Equal(2, parsed.Pages.Count);
        var fill = Assert.Single(parsed.Pages[1].GraphicsObjects.OfType<PVAD.Importer.Graphics.PdfPathElement>()
            .Where(path => path.FillColor.C1 == 1 && path.FillColor.C2 == 0 && path.FillColor.C3 == 0));
        var rectangle = Assert.IsType<PVAD.Importer.Graphics.RectangleSegment>(Assert.Single(fill.Segments));
        Assert.Equal(120, rectangle.Rectangle.Y);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidLineMetricsFailBeforePainting(double height)
    {
        var document = new PdfDocument();
        var flow = document.CreateFlow(FlowOptions);
        var before = document.ToBytes();
        Assert.Throws<ArgumentOutOfRangeException>(() => flow.AddSimpleTable(
            [new[] { "Heading" }, new[] { "Data" }], new PdfTableOptions { RowLineHeights = [12, height] }));
        Assert.Equal(before, document.ToBytes());
    }

    [Fact]
    public async Task StyledFlowMatchesIndependentlyPlannedExplicitPages_AndWritesVisualArtifacts()
    {
        var flowDocument = new PdfDocument { FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts")) };
        IReadOnlyList<IReadOnlyList<string>> rows = [new[] { "Heading", "Units" }, new[] { "Item1", "1" },
            new[] { "Item2", "2" }, new[] { "Item3", "3" }, new[] { "Item4", "4" },
            new[] { "Item5", "5" }, new[] { "Summary", "15" }];
        var heights = new double[] { 28, 36, 48, 32, 52, 40, 24 };
        var result = flowDocument.CreateFlow(FlowOptions).AddSimpleTable(rows, VisualOptions(heights,
            Enumerable.Range(0, rows.Count).Select(index => VisualStyle(index, index)).ToArray(),
            true, [12, 13, 14, 15, 16, 17, 18]));
        var expected = new PdfDocument { FontLoader = flowDocument.FontLoader };
        // Explicit expected grouping is independent of the flow planner.
        var pageRows = new[] { new[] { 0, 1, 2 }, new[] { 0, 3, 4 }, new[] { 0, 5, 6 } };
        for (var page = 0; page < pageRows.Length; page++)
        {
            var indices = pageRows[page];
            expected.AddPage(300, 180).DrawSimpleTable(20, 160, 260, indices.Select(index => rows[index]).ToArray(),
                VisualOptions(indices.Select(index => heights[index]).ToArray(),
                    indices.Select((index, local) => VisualStyle(index, local)).ToArray(), page == 2,
                    indices.Select(index => 12d + index).ToArray()));
        }
        Assert.Equal(316, result.Height);
        Assert.Equal(expected.ToBytes(), flowDocument.ToBytes());
        var parsed = await Parse(flowDocument);
        Assert.Equal(3, parsed.Pages.Count);
        var output = PVAD.Testing.PvadTestEnvironment.Read("PVAD_FLOW_TABLE_ARTIFACTS");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(output);
            await File.WriteAllBytesAsync(Path.Combine(output, "flow.pdf"), flowDocument.ToBytes());
            await File.WriteAllBytesAsync(Path.Combine(output, "explicit.pdf"), expected.ToBytes());
            await File.WriteAllTextAsync(Path.Combine(output, "pages.json"), JsonSerializer.Serialize(new
            {
                pages = parsed.Pages.Count, width = 300, height = 180, rows = pageRows,
                rowHeights = heights, totalTableHeight = result.Height
            }));
        }
    }

    private static PdfTableOptions VisualOptions(double[] heights, PdfTableCellStyle[] styles, bool footer, double[] lineHeights) => new()
    {
        HasHeaderRow = true, HasFooterRow = footer, RepeatFooterOnEachChunk = false,
        RowHeights = heights, RowLineHeights = lineHeights, CellStyles = styles, AlternateRowFillColor = null,
        ColumnWidths = [2, 1]
    };

    private static PdfTableCellStyle VisualStyle(int original, int local) => new()
    {
        Row = local, Column = 0, Bold = false, Italic = original % 2 == 1, Padding = 6,
        FontSize = original == 0 ? 13 : 11, TextColor = PdfColor.FromRgb(20, 30, 50),
        BackgroundColor = original == 0 ? PdfColor.FromRgb(200, 220, 255) : PdfColor.FromRgb(235, 250, 235),
        BorderWidth = 1, BorderColor = PdfColor.FromRgb(100, 130, 160),
        BorderBottom = new() { Width = original == 6 ? 2 : 1, Color = PdfColor.FromRgb(0, 100, 80) }
    };

    private static async Task<PdfDocumentModel> Parse(PdfDocument document)
        => await new PdfImporter().LoadAsync(new MemoryStream(document.ToBytes()));
}
