using Canvas.Pdf;
using System.Globalization;

var document = new PdfDocument(defaultFont: PdfStandardFonts.FromStyle(PdfFontFamily.Times));
document.Info.Title = "Canvas Minimal PDF Demo";
document.Info.Author = "Canvas.Pdf";
document.Info.Subject = "Vector drawing and text rendering";
document.Info.Keywords = "canvas,pdf,demo";
document.Info.Creator = "Console Demo";
document.Info.Producer = ".NET 10 Minimal PDF Engine";

var firstPage = document.AddPage();
var secondPage = document.AddPage();
var rotatedPage = document.AddPageRotated(90, PdfPageSizes.LetterWidth, PdfPageSizes.LetterHeight);
var presetLandscapePage = document.AddPage(PdfPagePreset.A4, landscape: true);

document.SetViewerPreferences(new PdfViewerPreferencesOptions
{
    PageMode = PdfPageMode.UseOutlines,
    PageLayout = PdfPageLayoutMode.OneColumn,
    DisplayDocTitle = true,
    ReadingDirection = PdfReadingDirection.LeftToRight,
    DisablePrintScaling = true,
    DuplexFlipLongEdge = true,
    FitWindow = true,
    OpenPageNumber = 1,
    OpenZoomPercent = 110
});

rotatedPage.SetPageBoundary(PdfPageBoundary.CropBox, new PdfPoint(20, 20), new PdfPoint(rotatedPage.Width - 20, rotatedPage.Height - 20));

document.Info.CreationDate = DateTimeOffset.UtcNow;
document.Info.ModificationDate = DateTimeOffset.UtcNow;

document.SetPageBackground(new PdfColor(1, 1, 1));

firstPage.DrawRectangle(x: 80, y: 640, width: 430, height: 90, lineWidth: 1.5, fill: true, strokeColor: new PdfColor(0.1, 0.2, 0.5), fillColor: new PdfColor(0.92, 0.95, 1));
firstPage.DrawRoundedRectangle(x: 80, y: 590, width: 200, height: 40, cornerRadius: 10, fill: true, strokeColor: new PdfColor(0.1, 0.2, 0.5), fillColor: new PdfGrayColor(0.95));
firstPage.DrawCircle(centerX: 340, centerY: 610, radius: 20, fill: true, strokeColor: PdfColor.Gray, fillColor: new PdfCmykColor(0.2, 0, 0, 0));
firstPage.DrawPolygon(
    new[]
    {
        new PdfPoint(420, 630),
        new PdfPoint(500, 610),
        new PdfPoint(480, 560),
        new PdfPoint(410, 570)
    },
    fill: true,
    strokeColor: new PdfColor(0.1, 0.2, 0.5),
    fillColor: new PdfGrayColor(0.9),
    strokeStyle: new PdfStrokeStyle { LineWidth = 1.1, DashArray = new[] { 4d, 2d } });
firstPage.DrawBezierCurve(
    start: new PdfPoint(90, 560),
    control1: new PdfPoint(180, 630),
    control2: new PdfPoint(280, 500),
    end: new PdfPoint(370, 560),
    strokeColor: new PdfColor(0.1, 0.2, 0.5),
    strokeStyle: new PdfStrokeStyle { LineWidth = 1.2, DashArray = new[] { 8d, 3d } });
firstPage.DrawLine(x1: 80, y1: 684, x2: 510, y2: 684, lineWidth: 0.8, strokeColor: new PdfColor(0.1, 0.2, 0.5));
firstPage.DrawLine(
    x1: 80,
    y1: 640,
    x2: 510,
    y2: 640,
    strokeColor: new PdfGrayColor(0.35),
    strokeStyle: new PdfStrokeStyle
    {
        LineWidth = 1.2,
        LineCap = PdfLineCapStyle.Round,
        LineJoin = PdfLineJoinStyle.Round,
        DashArray = new[] { 6d, 3d }
    });

document.AddSection("Intro", 1);
document.AddSection("Flow", 3);

firstPage.DrawText("Hello World from a minimal PDF engine", x: 100, y: 700, new PdfDrawTextOptions { FontSize = 18, FontFamily = PdfFontFamily.Helvetica, Bold = true, FillColor = new PdfColor(0.1, 0.2, 0.5) });
firstPage.AddWebLink(x: 100, y: 696, width: 260, height: 20, url: "https://learn.microsoft.com/dotnet/");
firstPage.DrawText("Go to page 2", x: 380, y: 700, fontSize: 11, fontFamily: PdfFontFamily.Helvetica);
firstPage.AddPageLink(x: 380, y: 696, width: 90, height: 16, targetPageNumber: 2);
firstPage.DrawText("Jump to Flow Section", x: 100, y: 648, fontSize: 11, fontFamily: PdfFontFamily.Helvetica);
firstPage.AddNamedDestinationLink(x: 100, y: 644, width: 150, height: 16, destinationName: "flow-section");
firstPage.DrawText("This line uses grayscale color.", x: 100, y: 670, new PdfDrawTextOptions { FontSize = 12, FillColor = new PdfGrayColor(0.2) });

secondPage.DrawText("Second page", x: 100, y: 700, fontSize: 16, fontFamily: PdfFontFamily.Courier, bold: true);
rotatedPage.DrawText("Rotated page example (90°)", x: 120, y: 620, new PdfDrawTextOptions
{
    FontSize = 14,
    FontFamily = PdfFontFamily.Helvetica,
    Bold = true,
    Underline = true,
    CharacterSpacing = 0.4,
    HorizontalScalingPercent = 98
});
presetLandscapePage.DrawText("A4 landscape page via preset helper", x: 80, y: 520, new PdfDrawTextOptions
{
    FontSize = 13,
    FontFamily = PdfFontFamily.Helvetica,
    Bold = true,
    FillColor = new PdfGrayColor(0.25)
});
secondPage.DrawText("Back to page 1", x: 380, y: 700, fontSize: 11, fontFamily: PdfFontFamily.Helvetica);
secondPage.AddPageLink(x: 380, y: 696, width: 100, height: 16, targetPageNumber: 1);
secondPage.DrawText("Italic sample", x: 100, y: 670, new PdfDrawTextOptions { FontSize = 12, FontFamily = PdfFontFamily.Times, Italic = true });
secondPage.DrawParagraph(
    "This is a wrapped paragraph rendered by the minimal layout layer. It demonstrates left alignment and line breaking inside a fixed width box.",
    x: 100,
    y: 630,
    maxWidth: 380,
    new PdfParagraphOptions
    {
        FontSize = 12,
        FontFamily = PdfFontFamily.Helvetica,
        Alignment = PdfTextAlignment.Left,
        LineHeight = 16
    });
var centeredParagraphLayout = secondPage.DrawParagraph(
    "Centered paragraph sample for the same PDF page.",
    x: 100,
    y: 530,
    maxWidth: 380,
    new PdfParagraphOptions
    {
        FontSize = 12,
        FontFamily = PdfFontFamily.Times,
        Italic = true,
        Alignment = PdfTextAlignment.Center,
        FillColor = new PdfColor(0.25, 0.25, 0.25)
    });
var justifiedParagraphLayout = secondPage.DrawParagraph(
    "Justified paragraph sample. The first lines are stretched using extra word spacing so both left and right edges align to the same width.",
    x: 100,
    y: centeredParagraphLayout.BottomY - 28,
    maxWidth: 380,
    new PdfParagraphOptions
    {
        FontSize = 12,
        FontFamily = PdfFontFamily.Helvetica,
        Alignment = PdfTextAlignment.Justify,
        LineHeight = 16
    });

secondPage.DrawText($"Paragraph lines: {justifiedParagraphLayout.LineCount}", x: 100, y: justifiedParagraphLayout.BottomY - 24, fontSize: 10, fontFamily: PdfFontFamily.Courier);
secondPage.DrawRectangle(
    x: 95,
    y: justifiedParagraphLayout.BottomY - 32,
    width: 250,
    height: 18,
    lineWidth: 0.8,
    strokeColor: PdfColor.Gray,
    fill: true,
    fillColor: new PdfCmykColor(0.2, 0, 0, 0),
    strokeStyle: new PdfStrokeStyle
    {
        LineWidth = 0.8,
        DashArray = new[] { 4d, 2d }
    });

var tableTop = justifiedParagraphLayout.BottomY - 70;
secondPage.DrawSimpleTable(
    x: 95,
    y: tableTop,
    width: 360,
    rows: new List<IReadOnlyList<string>>
    {
        new[] { "Item", "Qty", "Price" },
        new[] { "Notebook with extended hard-cover title", "2", "$12.00" },
        new[] { "Pen Set", "1", "$8.50" }
    },
    options: new PdfTableOptions
    {
        FontFamily = PdfFontFamily.Helvetica,
        FontSize = 10,
        CellLineHeight = 12,
        AutoRowHeight = true,
        RowHeights = new[] { 26d, 28d, 24d },
        CellPaddingLeft = 6,
        CellPaddingRight = 6,
        CellPaddingTop = 4,
        CellPaddingBottom = 4,
        WrapCellText = true,
        ColumnWidths = new[] { 2.4, 1, 1.2 },
        ColumnAlignments = new[] { PdfTextAlignment.Left, PdfTextAlignment.Right, PdfTextAlignment.Right },
        ColumnVerticalAlignments = new[] { PdfVerticalAlignment.Top, PdfVerticalAlignment.Middle, PdfVerticalAlignment.Bottom },
        ColumnValueFormatters = new Func<string, string>[]
        {
            value => value,
            value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var qty)
                ? qty.ToString("N0", CultureInfo.InvariantCulture)
                : value,
            value => decimal.TryParse(value.TrimStart('$'), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
                ? amount.ToString("C2", CultureInfo.GetCultureInfo("en-US"))
                : value
        },
        CellAlignment = PdfTextAlignment.Left,
        HeaderFillColor = new PdfGrayColor(0.9),
        AlternateRowFillColor = new PdfGrayColor(0.97),
        CellCornerRadius = 3,
        BorderColor = PdfColor.Gray,
        BorderLineWidth = 0.7,
        DrawInnerVerticalBorders = true,
        DrawInnerHorizontalBorders = true,
        DrawOuterBorder = true,
        OuterBorderLineWidth = 1.1,
        InnerBorderLineWidth = 0.6,
        OuterBorderStrokeStyle = new PdfStrokeStyle { DashArray = new[] { 4d, 2d }, LineWidth = 1.1 },
        InnerBorderStrokeStyle = new PdfStrokeStyle { DashArray = new[] { 2d, 2d }, LineWidth = 0.6 }
    });

var flow = document.CreateFlow(new PdfFlowOptions
{
    MarginLeft = 60,
    MarginRight = 60,
    MarginTop = 80,
    MarginBottom = 60,
    ParagraphSpacing = 14,
    KeepHeadingsWithNextBlock = true,
    KeepParagraphsWithNextBlock = true
});

flow.AddTextLine("Flow layout page", new PdfDrawTextOptions
{
    FontSize = 16,
    FontFamily = PdfFontFamily.Helvetica,
    Bold = true,
    FillColor = new PdfColor(0.1, 0.2, 0.5)
});
flow.AddBookmark("Flow layout section");
document.AddBookmark("Flow layout section - Details", 3, level: 2);
flow.AddNamedDestination("flow-section");

flow.AddTextLine("Section heading that stays with next block", new PdfDrawTextOptions
{
    FontSize = 16,
    FontFamily = PdfFontFamily.Helvetica,
    Bold = true,
    FillColor = new PdfGrayColor(0.2)
});

flow.AddParagraph("This paragraph is added using the document flow helper. It provides margin-based placement and automatic page transitions when space runs low.",
    new PdfParagraphOptions
    {
        FontSize = 11,
        FontFamily = PdfFontFamily.Times,
        Alignment = PdfTextAlignment.Justify,
        LineHeight = 15
    });

flow.AddList(
    new[]
    {
        "First checklist item rendered as bullet list.",
        "Second item demonstrating wrapped list text within flow content width."
    },
    new PdfListOptions
    {
        Ordered = false,
        FontFamily = PdfFontFamily.Helvetica,
        FontSize = 10,
        MarkerFontFamily = PdfFontFamily.Courier,
        MarkerBold = true,
        MarkerFontSize = 11,
        MarkerColor = new PdfColor(0.1, 0.2, 0.5),
        ItemAlignment = PdfTextAlignment.Justify,
        ItemSpacing = 6
    });

flow.AddList(
    new[]
    {
        "Ordered list item one.",
        "Ordered list item two."
    },
    new PdfListOptions
    {
        Ordered = true,
        StartIndex = 3,
        NumberFormat = "({0})",
        MarkerGap = 8,
        AlignMarkersRight = true,
        FontFamily = PdfFontFamily.Times,
        FontSize = 10,
        ItemSpacing = 6
    });

var flowRows = new List<IReadOnlyList<string>>
{
    new[] { "Feature", "Status" }
};

for (var i = 1; i <= 24; i++)
{
    flowRows.Add(new[] { $"Flow row {i}", "Enabled" });
}

flowRows.Add(new[] { "Summary", "24 rows" });

flow.AddSimpleTable(
    flowRows,
    new PdfTableOptions
    {
        FontFamily = PdfFontFamily.Helvetica,
        FontSize = 10,
        CellLineHeight = 12,
        AutoRowHeight = true,
        WrapCellText = true,
        HasFooterRow = true,
        RepeatFooterOnEachChunk = false,
        ColumnWidths = new[] { 2.5, 1.2 },
        MinColumnWidth = 120,
        ColumnAlignments = new[] { PdfTextAlignment.Left, PdfTextAlignment.Center },
        ColumnVerticalAlignments = new[] { PdfVerticalAlignment.Middle, PdfVerticalAlignment.Top },
        ColumnValueFormatters = new Func<string, string>[]
        {
            value => value,
            value => value.Equals("enabled", StringComparison.OrdinalIgnoreCase) ? "✅ Enabled" : value
        },
        HeaderFillColor = new PdfGrayColor(0.9),
        FooterFillColor = new PdfGrayColor(0.92),
        AlternateRowFillColor = new PdfGrayColor(0.97),
        CellCornerRadius = 2,
        BorderColor = PdfColor.Gray,
        DrawOuterBorder = true,
        DrawInnerVerticalBorders = false,
        DrawInnerHorizontalBorders = true,
        OuterBorderLineWidth = 1,
        InnerBorderLineWidth = 0.5,
        InnerBorderStrokeStyle = new PdfStrokeStyle { DashArray = new[] { 3d, 2d }, LineWidth = 0.5 },
        ShowContinuationMarkers = true
    });

var sampleImagePath = Path.Combine(AppContext.BaseDirectory, "assets", "logo.png");

if (File.Exists(sampleImagePath))
{
    flow.AddImageFit(sampleImagePath, maxHeight: 120, fitMode: PdfImageFitMode.Contain);
    flow.AddImageFit(sampleImagePath, maxHeight: 90, fitMode: PdfImageFitMode.Cover);
    flow.AddImage(sampleImagePath, width: 80, height: 80, opacity: 0.55);
    secondPage.DrawImageClipped(sampleImagePath, x: 360, y: 530, width: 120, height: 120, clipX: 360, clipY: 530, clipWidth: 90, clipHeight: 90, opacity: 0.85);
}

document.AddBookmark("Page 1 - Shapes", 1);
document.AddBookmark("Page 2 - Paragraphs & Table", 2);
document.AddBookmark("Page 3 - Flow Layout", 3);

document.AddTableOfContents(new PdfTableOfContentsOptions
{
    Title = "Contents",
    FontFamily = PdfFontFamily.Helvetica,
    EntryFontSize = 11,
    Placement = PdfTableOfContentsPlacement.Beginning,
    TitleAlignment = PdfTextAlignment.Center,
    TitleColor = new PdfColor(0.1, 0.2, 0.5),
    TitleBottomSpacing = 24,
    ShowHierarchyIndent = true,
    EntryIndentPerLevel = 12,
    MinimumBookmarkLevel = 1,
    MaximumBookmarkLevel = 3,
    ShowLeaderDots = true,
    LeaderPattern = ".",
    LeaderGap = 8,
    ShowPageNumbers = true,
    MaximumEntries = 100
});

document.AddTextWatermark("CONFIDENTIAL", new PdfWatermarkOptions
{
    FontFamily = PdfFontFamily.Helvetica,
    Bold = true,
    FontSize = 42,
    RotationDegrees = 35,
    CharacterSpacing = 0.5,
    HorizontalScalingPercent = 95,
    Underline = false,
    FillColor = new PdfGrayColor(0.9),
    PageParity = PdfPageParity.Even,
    ExcludeTableOfContentsPages = true,
    SkipFirstPage = true
});

document.AddPageNumbers(new PdfPageNumberOptions
{
    Y = 18,
    Alignment = PdfTextAlignment.Center,
    FontFamily = PdfFontFamily.Helvetica,
    FontSize = 10,
    FillColor = new PdfGrayColor(0.35),
    StartPageNumber = 2,
    ShowTotalPages = false,
    MaximumNumber = 200,
    MinimumNumber = 2,
    NumberFormat = "{0:000}",
    UseRomanNumerals = false,
    PageParity = PdfPageParity.Both,
    ExcludeTableOfContentsPages = true,
    UseFilteredPageSequence = true,
    SkipFirstPage = true,
    UseSectionNumbering = true
});

document.AddHeadersAndFooters(new PdfHeaderFooterOptions
{
    HeaderTemplate = "Canvas.Pdf Demo - Section {sectionindex}/{sections}: {section} ({sectionpage}/{sectiontotal}, pages {sectionstart}-{sectionend})\n{date} {time}",
    FooterTemplate = "Page {page} of {total} | Section {sectionpage}/{sectiontotal}\n{title} | {author} | {creationdate} | {keywords}",
    HeaderAlignment = PdfTextAlignment.Left,
    FooterAlignment = PdfTextAlignment.Right,
    HeaderTopMargin = 20,
    FooterY = 18,
    HeaderLineSpacing = 11,
    FooterLineSpacing = 11,
    MarginX = 40,
    FontFamily = PdfFontFamily.Helvetica,
    FontSize = 10,
    FillColor = new PdfGrayColor(0.45),
    MinimumNumber = 2,
    MaximumNumber = 999,
    UseRomanNumerals = false,
    PageParity = PdfPageParity.Both,
    ExcludeTableOfContentsPages = true,
    UseFilteredPageSequence = true,
    SkipFirstPage = true,
    SuppressHeaderOnFirstPage = true,
    SuppressFooterOnLastPage = false
});

var outputPath = Path.Combine(AppContext.BaseDirectory, "output.pdf");

try
{
    document.Save(outputPath, new PdfSaveOptions
    {
        CompressContentStreams = true,
        CollectDiagnostics = true
    });
}
catch (IOException)
{
    outputPath = Path.Combine(AppContext.BaseDirectory, $"output_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
    document.Save(outputPath, new PdfSaveOptions
    {
        CompressContentStreams = true,
        CollectDiagnostics = true
    });
}

Console.WriteLine($"PDF generated at: {outputPath}");

if (document.LastDiagnostics is { } diagnostics)
{
    Console.WriteLine($"Diagnostics -> Pages: {diagnostics.PageCount}, Objects: {diagnostics.ObjectCount}, Bytes: {diagnostics.ByteSize}, Compressed: {diagnostics.ContentStreamsCompressed}, Bookmarks: {diagnostics.BookmarkCount}, Nested bookmarks: {diagnostics.NestedBookmarkCount}, TOC pages: {diagnostics.TableOfContentsPageCount}, TOC entries: {diagnostics.TocEntryCount}, TOC links: {diagnostics.TocPageLinkCount}, Named destinations: {diagnostics.NamedDestinationCount}, Links: {diagnostics.LinkAnnotationCount}, Web links: {diagnostics.WebLinkAnnotationCount}, Page links: {diagnostics.PageLinkAnnotationCount}, Named links: {diagnostics.NamedDestinationLinkAnnotationCount}, Sections: {diagnostics.SectionCount}, Images: {diagnostics.ImageResourceCount}, Opacity states: {diagnostics.ImageOpacityResourceCount}, Image draws: {diagnostics.ImageDrawCallCount}, Cache hits: {diagnostics.ImageCacheHitCount}, Watermarked pages: {diagnostics.WatermarkPageCount}, Header pages: {diagnostics.HeaderRenderedPageCount}, Footer pages: {diagnostics.FooterRenderedPageCount}, Numbered pages: {diagnostics.PageNumberRenderedPageCount}, Empty pages: {diagnostics.EmptyPageCount}, Rotated pages: {diagnostics.RotatedPageCount}, Pages with text: {diagnostics.PagesWithTextCount}, Pages with images: {diagnostics.PagesWithImageCount}, Pages with links: {diagnostics.PagesWithLinkCount}, Pages with shapes: {diagnostics.PagesWithShapeCount}, Bookmark target pages: {diagnostics.PagesWithBookmarkTargetCount}, Named destination pages: {diagnostics.PagesWithNamedDestinationCount}, Unique watermarked: {diagnostics.WatermarkedUniquePageCount}, Unique header: {diagnostics.HeaderRenderedUniquePageCount}, Unique footer: {diagnostics.FooterRenderedUniquePageCount}, Unique numbered: {diagnostics.PageNumberRenderedUniquePageCount}, CropBox pages: {diagnostics.PagesWithCropBoxCount}, BleedBox pages: {diagnostics.PagesWithBleedBoxCount}, TrimBox pages: {diagnostics.PagesWithTrimBoxCount}, ArtBox pages: {diagnostics.PagesWithArtBoxCount}, Boundary pages: {diagnostics.PagesWithAnyBoundaryBoxCount}, Any transparency pages: {diagnostics.PagesWithAnyTransparencyCount}, Image transparency pages: {diagnostics.PagesWithImageTransparencyCount}, Text decoration pages: {diagnostics.PagesWithTextDecorationsCount}, Flow-content pages: {diagnostics.PagesWithFlowContentCount}, Pages with web links: {diagnostics.PagesWithWebLinkCount}, Pages with page links: {diagnostics.PagesWithPageLinkCount}, Pages with named links: {diagnostics.PagesWithNamedDestinationLinkCount}, Pages with bookmarks: {diagnostics.PagesWithBookmarkCount}, Pages with named destinations by page: {diagnostics.PagesWithNamedDestinationCountByPage}, Pages with mixed content: {diagnostics.PagesWithMixedContentCount}, Pages with lines: {diagnostics.PagesWithLineCount}, Pages with rectangles: {diagnostics.PagesWithRectangleCount}, Pages with rounded rectangles: {diagnostics.PagesWithRoundedRectangleCount}, Pages with circles: {diagnostics.PagesWithCircleCount}, Pages with polygons: {diagnostics.PagesWithPolygonCount}, Pages with curves: {diagnostics.PagesWithBezierCurveCount}, Pages with underlined text: {diagnostics.PagesWithUnderlinedTextCount}, Pages with strikethrough text: {diagnostics.PagesWithStrikethroughTextCount}, Pages with rotated text: {diagnostics.PagesWithRotatedTextCount}, Pages with character spaced text: {diagnostics.PagesWithCharacterSpacedTextCount}, Pages with horizontally scaled text: {diagnostics.PagesWithHorizontallyScaledTextCount}, Pages with justified text: {diagnostics.PagesWithJustifiedTextCount}, Pages with opaque images only: {diagnostics.PagesWithOpaqueImagesOnlyCount}, Pages without links: {diagnostics.PagesWithoutLinksCount}, Pages without images: {diagnostics.PagesWithoutImagesCount}, Pages without text: {diagnostics.PagesWithoutTextCount}, Pages without shapes: {diagnostics.PagesWithoutShapesCount}, Pages with only text: {diagnostics.PagesWithOnlyTextCount}, Pages with only images: {diagnostics.PagesWithOnlyImagesCount}, Pages with any elements: {diagnostics.PagesWithAnyElementsCount}, Pages with multiple links: {diagnostics.PagesWithMultipleLinksCount}, Pages with multiple images: {diagnostics.PagesWithMultipleImagesCount}, Pages with multiple text elements: {diagnostics.PagesWithMultipleTextElementsCount}, Pages with multiple shapes: {diagnostics.PagesWithMultipleShapesCount}, Pages with >=5 elements: {diagnostics.PagesWithAtLeastFiveElementsCount}, Pages with <=1 element: {diagnostics.PagesWithAtMostOneElementCount}, Pages with exactly one link: {diagnostics.PagesWithExactlyOneLinkCount}, Pages with exactly one image: {diagnostics.PagesWithExactlyOneImageCount}, Pages with exactly one text element: {diagnostics.PagesWithExactlyOneTextElementCount}, Pages with exactly one shape: {diagnostics.PagesWithExactlyOneShapeCount}, Pages with exactly one line: {diagnostics.PagesWithExactlyOneLineCount}, Pages with exactly one rectangle: {diagnostics.PagesWithExactlyOneRectangleCount}, Pages with exactly one rounded rectangle: {diagnostics.PagesWithExactlyOneRoundedRectangleCount}, Pages with exactly one circle: {diagnostics.PagesWithExactlyOneCircleCount}, Pages with exactly one polygon: {diagnostics.PagesWithExactlyOnePolygonCount}, Pages with exactly one curve: {diagnostics.PagesWithExactlyOneBezierCurveCount}, Pages with text spacing adjustments: {diagnostics.PagesWithAnyTextSpacingAdjustmentsCount}, Pages with only vectors: {diagnostics.PagesWithOnlyVectorShapesCount}, Pages with only links: {diagnostics.PagesWithOnlyLinksCount}, Pages with elements and links: {diagnostics.PagesWithElementsAndLinksCount}, Pages without elements but with links: {diagnostics.PagesWithoutElementsButWithLinksCount}, Landscape pages: {diagnostics.PagesWithLandscapeOrientationCount}, Portrait pages: {diagnostics.PagesWithPortraitOrientationCount}, Square pages: {diagnostics.PagesWithSquareOrientationCount}, A4 pages: {diagnostics.PagesUsingA4SizeCount}, Non-A4 pages: {diagnostics.PagesUsingNonA4SizeCount}, Letter pages: {diagnostics.PagesUsingLetterSizeCount}, A3 pages: {diagnostics.PagesUsingA3SizeCount}, Rotation 0 pages: {diagnostics.PagesWithPageRotation0Count}, Rotation 90 pages: {diagnostics.PagesWithPageRotation90Count}, Rotation 180 pages: {diagnostics.PagesWithPageRotation180Count}, Rotation 270 pages: {diagnostics.PagesWithPageRotation270Count}, Any rotation pages: {diagnostics.PagesWithAnyPageRotationCount}");
}

foreach (var destination in document.GetNamedDestinations())
{
    Console.WriteLine($"Named destination -> {destination.Name} @ page {destination.PageNumber}{(destination.Y is { } y ? $", y={y:0.##}" : string.Empty)}");
}

foreach (var section in document.GetSections())
{
    Console.WriteLine($"Section -> {section.Name} starts at page {section.StartPageNumber}");
}

foreach (var sectionRange in document.GetSectionRanges())
{
    Console.WriteLine($"Section range -> {sectionRange.Name}: {sectionRange.StartPageNumber}-{sectionRange.EndPageNumber} ({sectionRange.PageCount} pages)");
}

foreach (var bookmark in document.GetBookmarks())
{
    Console.WriteLine($"Bookmark -> {bookmark.Title} @ page {bookmark.PageNumber}, level {bookmark.Level}");
}

var tocPages = document.GetTableOfContentsPageNumbers();
if (tocPages.Count > 0)
{
    Console.WriteLine($"TOC pages -> {string.Join(", ", tocPages)}");
}

var emptyPages = document.GetPagesWithoutContent();
if (emptyPages.Count > 0)
{
    Console.WriteLine($"Empty pages -> {string.Join(", ", emptyPages)}");
}

var rotatedPages = document.GetRotatedPageNumbers();
if (rotatedPages.Count > 0)
{
    Console.WriteLine($"Rotated pages -> {string.Join(", ", rotatedPages)}");
}

var linkPages = document.GetPagesWithLinks();
if (linkPages.Count > 0)
{
    Console.WriteLine($"Pages with links -> {string.Join(", ", linkPages)}");
}

var imagePages = document.GetPagesWithImages();
if (imagePages.Count > 0)
{
    Console.WriteLine($"Pages with images -> {string.Join(", ", imagePages)}");
}

var textPages = document.GetPagesWithText();
if (textPages.Count > 0)
{
    Console.WriteLine($"Pages with text -> {string.Join(", ", textPages)}");
}

var webLinkPages = document.GetPagesWithWebLinks();
if (webLinkPages.Count > 0)
{
    Console.WriteLine($"Pages with web links -> {string.Join(", ", webLinkPages)}");
}

var pageLinkPages = document.GetPagesWithPageLinks();
if (pageLinkPages.Count > 0)
{
    Console.WriteLine($"Pages with page links -> {string.Join(", ", pageLinkPages)}");
}

var namedLinkPages = document.GetPagesWithNamedDestinationLinks();
if (namedLinkPages.Count > 0)
{
    Console.WriteLine($"Pages with named destination links -> {string.Join(", ", namedLinkPages)}");
}

var bookmarkPages = document.GetPagesWithBookmarks();
if (bookmarkPages.Count > 0)
{
    Console.WriteLine($"Pages with bookmarks -> {string.Join(", ", bookmarkPages)}");
}

var namedDestinationPages = document.GetPagesWithNamedDestinations();
if (namedDestinationPages.Count > 0)
{
    Console.WriteLine($"Pages with named destinations -> {string.Join(", ", namedDestinationPages)}");
}

var mixedContentPages = document.GetPagesWithMixedContent();
if (mixedContentPages.Count > 0)
{
    Console.WriteLine($"Pages with mixed content -> {string.Join(", ", mixedContentPages)}");
}

var shapePages = document.GetPagesWithShapes();
if (shapePages.Count > 0)
{
    Console.WriteLine($"Pages with shapes -> {string.Join(", ", shapePages)}");
}

var linePages = document.GetPagesWithLines();
if (linePages.Count > 0)
{
    Console.WriteLine($"Pages with lines -> {string.Join(", ", linePages)}");
}

var rectanglePages = document.GetPagesWithRectangles();
if (rectanglePages.Count > 0)
{
    Console.WriteLine($"Pages with rectangles -> {string.Join(", ", rectanglePages)}");
}

var roundedRectanglePages = document.GetPagesWithRoundedRectangles();
if (roundedRectanglePages.Count > 0)
{
    Console.WriteLine($"Pages with rounded rectangles -> {string.Join(", ", roundedRectanglePages)}");
}

var circlePages = document.GetPagesWithCircles();
if (circlePages.Count > 0)
{
    Console.WriteLine($"Pages with circles -> {string.Join(", ", circlePages)}");
}

var polygonPages = document.GetPagesWithPolygons();
if (polygonPages.Count > 0)
{
    Console.WriteLine($"Pages with polygons -> {string.Join(", ", polygonPages)}");
}

var curvePages = document.GetPagesWithBezierCurves();
if (curvePages.Count > 0)
{
    Console.WriteLine($"Pages with Bezier curves -> {string.Join(", ", curvePages)}");
}

var underlinedPages = document.GetPagesWithUnderlinedText();
if (underlinedPages.Count > 0)
{
    Console.WriteLine($"Pages with underlined text -> {string.Join(", ", underlinedPages)}");
}

var strikethroughPages = document.GetPagesWithStrikethroughText();
if (strikethroughPages.Count > 0)
{
    Console.WriteLine($"Pages with strikethrough text -> {string.Join(", ", strikethroughPages)}");
}

var rotatedTextPages = document.GetPagesWithRotatedText();
if (rotatedTextPages.Count > 0)
{
    Console.WriteLine($"Pages with rotated text -> {string.Join(", ", rotatedTextPages)}");
}

var characterSpacedPages = document.GetPagesWithCharacterSpacedText();
if (characterSpacedPages.Count > 0)
{
    Console.WriteLine($"Pages with character-spaced text -> {string.Join(", ", characterSpacedPages)}");
}

var scaledTextPages = document.GetPagesWithHorizontallyScaledText();
if (scaledTextPages.Count > 0)
{
    Console.WriteLine($"Pages with horizontally scaled text -> {string.Join(", ", scaledTextPages)}");
}

var justifiedTextPages = document.GetPagesWithJustifiedText();
if (justifiedTextPages.Count > 0)
{
    Console.WriteLine($"Pages with justified text -> {string.Join(", ", justifiedTextPages)}");
}

var opaqueOnlyImagePages = document.GetPagesWithOpaqueImagesOnly();
if (opaqueOnlyImagePages.Count > 0)
{
    Console.WriteLine($"Pages with only opaque images -> {string.Join(", ", opaqueOnlyImagePages)}");
}

var withoutLinkPages = document.GetPagesWithoutLinks();
if (withoutLinkPages.Count > 0)
{
    Console.WriteLine($"Pages without links -> {string.Join(", ", withoutLinkPages)}");
}

var withoutImagePages = document.GetPagesWithoutImages();
if (withoutImagePages.Count > 0)
{
    Console.WriteLine($"Pages without images -> {string.Join(", ", withoutImagePages)}");
}

var withoutTextPages = document.GetPagesWithoutText();
if (withoutTextPages.Count > 0)
{
    Console.WriteLine($"Pages without text -> {string.Join(", ", withoutTextPages)}");
}

var withoutShapePages = document.GetPagesWithoutShapes();
if (withoutShapePages.Count > 0)
{
    Console.WriteLine($"Pages without shapes -> {string.Join(", ", withoutShapePages)}");
}

var onlyTextPages = document.GetPagesWithOnlyText();
if (onlyTextPages.Count > 0)
{
    Console.WriteLine($"Pages with only text -> {string.Join(", ", onlyTextPages)}");
}

var onlyImagePages = document.GetPagesWithOnlyImages();
if (onlyImagePages.Count > 0)
{
    Console.WriteLine($"Pages with only images -> {string.Join(", ", onlyImagePages)}");
}

var anyElementPages = document.GetPagesWithAnyElements();
if (anyElementPages.Count > 0)
{
    Console.WriteLine($"Pages with any elements -> {string.Join(", ", anyElementPages)}");
}

var multipleLinkPages = document.GetPagesWithMultipleLinks();
if (multipleLinkPages.Count > 0)
{
    Console.WriteLine($"Pages with multiple links -> {string.Join(", ", multipleLinkPages)}");
}

var multipleImagePages = document.GetPagesWithMultipleImages();
if (multipleImagePages.Count > 0)
{
    Console.WriteLine($"Pages with multiple images -> {string.Join(", ", multipleImagePages)}");
}

var multipleTextPages = document.GetPagesWithMultipleTextElements();
if (multipleTextPages.Count > 0)
{
    Console.WriteLine($"Pages with multiple text elements -> {string.Join(", ", multipleTextPages)}");
}

var multipleShapePages = document.GetPagesWithMultipleShapes();
if (multipleShapePages.Count > 0)
{
    Console.WriteLine($"Pages with multiple shapes -> {string.Join(", ", multipleShapePages)}");
}

var densePages = document.GetPagesWithAtLeastElementCount(5);
if (densePages.Count > 0)
{
    Console.WriteLine($"Pages with at least 5 elements -> {string.Join(", ", densePages)}");
}

var sparsePages = document.GetPagesWithAtMostElementCount(1);
if (sparsePages.Count > 0)
{
    Console.WriteLine($"Pages with at most 1 element -> {string.Join(", ", sparsePages)}");
}

var exactlyOneLinkPages = document.GetPagesWithExactlyOneLink();
if (exactlyOneLinkPages.Count > 0)
{
    Console.WriteLine($"Pages with exactly one link -> {string.Join(", ", exactlyOneLinkPages)}");
}

var exactlyOneImagePages = document.GetPagesWithExactlyOneImage();
if (exactlyOneImagePages.Count > 0)
{
    Console.WriteLine($"Pages with exactly one image -> {string.Join(", ", exactlyOneImagePages)}");
}

var exactlyOneTextPages = document.GetPagesWithExactlyOneTextElement();
if (exactlyOneTextPages.Count > 0)
{
    Console.WriteLine($"Pages with exactly one text element -> {string.Join(", ", exactlyOneTextPages)}");
}

var exactlyOneShapePages = document.GetPagesWithExactlyOneShape();
if (exactlyOneShapePages.Count > 0)
{
    Console.WriteLine($"Pages with exactly one shape -> {string.Join(", ", exactlyOneShapePages)}");
}

var exactlyOneLinePages = document.GetPagesWithExactlyOneLine();
if (exactlyOneLinePages.Count > 0)
{
    Console.WriteLine($"Pages with exactly one line -> {string.Join(", ", exactlyOneLinePages)}");
}

var exactlyOneRectanglePages = document.GetPagesWithExactlyOneRectangle();
if (exactlyOneRectanglePages.Count > 0)
{
    Console.WriteLine($"Pages with exactly one rectangle -> {string.Join(", ", exactlyOneRectanglePages)}");
}

var exactlyOneRoundedRectanglePages = document.GetPagesWithExactlyOneRoundedRectangle();
if (exactlyOneRoundedRectanglePages.Count > 0)
{
    Console.WriteLine($"Pages with exactly one rounded rectangle -> {string.Join(", ", exactlyOneRoundedRectanglePages)}");
}

var exactlyOneCirclePages = document.GetPagesWithExactlyOneCircle();
if (exactlyOneCirclePages.Count > 0)
{
    Console.WriteLine($"Pages with exactly one circle -> {string.Join(", ", exactlyOneCirclePages)}");
}

var exactlyOnePolygonPages = document.GetPagesWithExactlyOnePolygon();
if (exactlyOnePolygonPages.Count > 0)
{
    Console.WriteLine($"Pages with exactly one polygon -> {string.Join(", ", exactlyOnePolygonPages)}");
}

var exactlyOneCurvePages = document.GetPagesWithExactlyOneBezierCurve();
if (exactlyOneCurvePages.Count > 0)
{
    Console.WriteLine($"Pages with exactly one Bezier curve -> {string.Join(", ", exactlyOneCurvePages)}");
}

var spacingAdjustedPages = document.GetPagesWithAnyTextSpacingAdjustments();
if (spacingAdjustedPages.Count > 0)
{
    Console.WriteLine($"Pages with text spacing adjustments -> {string.Join(", ", spacingAdjustedPages)}");
}

var vectorOnlyPages = document.GetPagesWithOnlyVectorShapes();
if (vectorOnlyPages.Count > 0)
{
    Console.WriteLine($"Pages with only vector shapes -> {string.Join(", ", vectorOnlyPages)}");
}

var linksOnlyPages = document.GetPagesWithOnlyLinks();
if (linksOnlyPages.Count > 0)
{
    Console.WriteLine($"Pages with only links -> {string.Join(", ", linksOnlyPages)}");
}

var elementAndLinkPages = document.GetPagesWithElementsAndLinks();
if (elementAndLinkPages.Count > 0)
{
    Console.WriteLine($"Pages with elements and links -> {string.Join(", ", elementAndLinkPages)}");
}

var noElementButLinksPages = document.GetPagesWithoutElementsButWithLinks();
if (noElementButLinksPages.Count > 0)
{
    Console.WriteLine($"Pages without elements but with links -> {string.Join(", ", noElementButLinksPages)}");
}

var landscapePages = document.GetPagesWithLandscapeOrientation();
if (landscapePages.Count > 0)
{
    Console.WriteLine($"Landscape pages -> {string.Join(", ", landscapePages)}");
}

var portraitPages = document.GetPagesWithPortraitOrientation();
if (portraitPages.Count > 0)
{
    Console.WriteLine($"Portrait pages -> {string.Join(", ", portraitPages)}");
}

var squarePages = document.GetPagesWithSquareOrientation();
if (squarePages.Count > 0)
{
    Console.WriteLine($"Square pages -> {string.Join(", ", squarePages)}");
}

var a4Pages = document.GetPagesUsingA4Size();
if (a4Pages.Count > 0)
{
    Console.WriteLine($"A4 pages -> {string.Join(", ", a4Pages)}");
}

var nonA4Pages = document.GetPagesUsingNonA4Size();
if (nonA4Pages.Count > 0)
{
    Console.WriteLine($"Non-A4 pages -> {string.Join(", ", nonA4Pages)}");
}

var letterPages = document.GetPagesUsingLetterSize();
if (letterPages.Count > 0)
{
    Console.WriteLine($"Letter pages -> {string.Join(", ", letterPages)}");
}

var a3Pages = document.GetPagesUsingA3Size();
if (a3Pages.Count > 0)
{
    Console.WriteLine($"A3 pages -> {string.Join(", ", a3Pages)}");
}

var rotation0Pages = document.GetPagesWithPageRotation0();
if (rotation0Pages.Count > 0)
{
    Console.WriteLine($"Rotation 0 pages -> {string.Join(", ", rotation0Pages)}");
}

var rotation90Pages = document.GetPagesWithPageRotation90();
if (rotation90Pages.Count > 0)
{
    Console.WriteLine($"Rotation 90 pages -> {string.Join(", ", rotation90Pages)}");
}

var rotation180Pages = document.GetPagesWithPageRotation180();
if (rotation180Pages.Count > 0)
{
    Console.WriteLine($"Rotation 180 pages -> {string.Join(", ", rotation180Pages)}");
}

var rotation270Pages = document.GetPagesWithPageRotation270();
if (rotation270Pages.Count > 0)
{
    Console.WriteLine($"Rotation 270 pages -> {string.Join(", ", rotation270Pages)}");
}

var anyRotationPages = document.GetPagesWithAnyPageRotation();
if (anyRotationPages.Count > 0)
{
    Console.WriteLine($"Any rotation pages -> {string.Join(", ", anyRotationPages)}");
}

var lastWatermarkedPages = document.GetLastWatermarkedPageNumbers();
if (lastWatermarkedPages.Count > 0)
{
    Console.WriteLine($"Last watermarked pages -> {string.Join(", ", lastWatermarkedPages)}");
}

var lastHeaderPages = document.GetLastHeaderRenderedPageNumbers();
if (lastHeaderPages.Count > 0)
{
    Console.WriteLine($"Last header-rendered pages -> {string.Join(", ", lastHeaderPages)}");
}

var lastFooterPages = document.GetLastFooterRenderedPageNumbers();
if (lastFooterPages.Count > 0)
{
    Console.WriteLine($"Last footer-rendered pages -> {string.Join(", ", lastFooterPages)}");
}

var lastNumberedPages = document.GetLastPageNumberRenderedPageNumbers();
if (lastNumberedPages.Count > 0)
{
    Console.WriteLine($"Last page-number-rendered pages -> {string.Join(", ", lastNumberedPages)}");
}

// // ── Generated sample ──────────────────────────────────────────────────────────
// var sampleDoc = new PdfDocument();
// sampleDoc.Info.Title = "Untitled document";

// var page = sampleDoc.AddPage(595, 842);

// page.DrawParagraph("Click to edit text", x: 58.00, y: 769.48, maxWidth: 478.00, new PdfParagraphOptions { FontSize = 16, FontFamily = PdfFontFamily.Helvetica, FillColor = new PdfColor(0.067, 0.094, 0.153), Alignment = PdfTextAlignment.Left });

// page.DrawRectangle(x: 55.00, y: 655.00, width: 260.00, height: 44.00, lineWidth: 1, fill: false, strokeColor: new PdfColor(0.820, 0.835, 0.859), fillColor: PdfColor.White);
// page.DrawText("Full name", x: 55.00, y: 711.08, new PdfDrawTextOptions { FontSize = 11, FillColor = new PdfColor(0.216, 0.255, 0.318) });
// page.DrawText("full_name", x: 61.00, y: 675.68, new PdfDrawTextOptions { FontSize = 11, FillColor = new PdfColor(0.612, 0.639, 0.659), Italic = true });

// // chart (bar): Jan, Feb, Mar, Apr
// page.DrawLine(x1: 106.00, y1: 383.00, x2: 402.00, y2: 383.00, lineWidth: 0.75, strokeColor: new PdfGrayColor(0.6));
// page.DrawLine(x1: 106.00, y1: 383.00, x2: 106.00, y2: 613.00, lineWidth: 0.75, strokeColor: new PdfGrayColor(0.6));
// page.DrawRectangle(x: 117.10, y: 383.00, width: 49.80, height: 125.45, lineWidth: 0.5, fill: true, strokeColor: new PdfColor(0.11, 0.42, 1.0), fillColor: new PdfColor(0.11, 0.42, 1.0)); // Jan: 12
// page.DrawRectangle(x: 191.10, y: 383.00, width: 49.80, height: 198.64, lineWidth: 0.5, fill: true, strokeColor: new PdfColor(0.11, 0.42, 1.0), fillColor: new PdfColor(0.11, 0.42, 1.0)); // Feb: 19
// page.DrawRectangle(x: 265.10, y: 383.00, width: 49.80, height: 146.36, lineWidth: 0.5, fill: true, strokeColor: new PdfColor(0.11, 0.42, 1.0), fillColor: new PdfColor(0.11, 0.42, 1.0)); // Mar: 14
// page.DrawRectangle(x: 339.10, y: 383.00, width: 49.80, height: 230.00, lineWidth: 0.5, fill: true, strokeColor: new PdfColor(0.11, 0.42, 1.0), fillColor: new PdfColor(0.11, 0.42, 1.0)); // Apr: 22
// page.DrawText("Jan", x: 135.50, y: 371.00, new PdfDrawTextOptions { FontSize = 8, FillColor = new PdfGrayColor(0.3) });
// page.DrawText("Feb", x: 209.50, y: 371.00, new PdfDrawTextOptions { FontSize = 8, FillColor = new PdfGrayColor(0.3) });
// page.DrawText("Mar", x: 283.50, y: 371.00, new PdfDrawTextOptions { FontSize = 8, FillColor = new PdfGrayColor(0.3) });
// page.DrawText("Apr", x: 357.50, y: 371.00, new PdfDrawTextOptions { FontSize = 8, FillColor = new PdfGrayColor(0.3) });
// page.DrawText("22", x: 73.00, y: 616.00, new PdfDrawTextOptions { FontSize = 8, FillColor = new PdfGrayColor(0.3) });
// page.DrawText("0",  x: 73.00, y: 386.00, new PdfDrawTextOptions { FontSize = 8, FillColor = new PdfGrayColor(0.3) });

// var sampleOutputPath = Path.Combine(AppContext.BaseDirectory, "sample-output.pdf");
// File.WriteAllBytes(sampleOutputPath, sampleDoc.ToBytes());
// Console.WriteLine($"[Sample] PDF written to: {sampleOutputPath}");
