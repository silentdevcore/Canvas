using PXA.Infrastructure.Word;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace PXA.Export.Tests;

/// <summary>
/// Smoke + behaviour coverage for element types that previously had no Word-export tests.
/// </summary>
public sealed class WordElementCoverageTests
{
    private static WordprocessingDocument ExportSingle(ElementDto el, MemoryStream ms, bool fidelityV2 = true)
    {
        var design = new DesignExportDto
        {
            Id = "cov",
            Name = "Coverage",
            Pages = [new PageDto { Id = "p1", Elements = [el] }],
        };
        var bytes = new WordDocumentExporter().Export(design, new ExportOptions(WordFidelityV2: fidelityV2));
        ms.Write(bytes);
        ms.Position = 0;
        return WordprocessingDocument.Open(ms, false);
    }

    [Fact]
    public void Footnote_AddsFootnotesPartWithText()
    {
        using var ms = new MemoryStream();
        using var doc = ExportSingle(new ElementDto
        { Id = "fn", Type = "footnote", X = 0, Y = 0, Width = 100, Height = 20, FootnoteText = "A footnote" }, ms);

        var fnPart = doc.MainDocumentPart!.FootnotesPart;
        Assert.NotNull(fnPart);
        Assert.Contains("A footnote", fnPart!.Footnotes!.InnerText);
    }

    [Fact]
    public void Endnote_AddsEndnotesPartWithText()
    {
        using var ms = new MemoryStream();
        using var doc = ExportSingle(new ElementDto
        { Id = "en", Type = "endnote", X = 0, Y = 0, Width = 100, Height = 20, FootnoteText = "An endnote" }, ms);

        var enPart = doc.MainDocumentPart!.EndnotesPart;
        Assert.NotNull(enPart);
        Assert.Contains("An endnote", enPart!.Endnotes!.InnerText);
    }

    [Fact]
    public void Comment_AddsCommentsPartWithAuthorAndText()
    {
        using var ms = new MemoryStream();
        using var doc = ExportSingle(new ElementDto
        {
            Id = "cm", Type = "comment", X = 0, Y = 0, Width = 100, Height = 20,
            CommentText = "Please review", CommentAuthor = "Alice",
        }, ms);

        var part = doc.MainDocumentPart!.WordprocessingCommentsPart;
        Assert.NotNull(part);
        var comment = part!.Comments!.Elements<Comment>().First();
        Assert.Equal("Alice", comment.Author!.Value);
        Assert.Contains("Please review", comment.InnerText);
    }

    [Fact]
    public void Bookmark_EmitsBookmarkStartWithName()
    {
        using var ms = new MemoryStream();
        using var doc = ExportSingle(new ElementDto
        { Id = "bm", Type = "bookmark", X = 0, Y = 0, Width = 100, Height = 20, BookmarkName = "chapter1", Content = "Ch. 1" }, ms);

        var start = doc.MainDocumentPart!.Document!.Body!.Descendants<BookmarkStart>().FirstOrDefault();
        Assert.NotNull(start);
        Assert.Equal("chapter1", start!.Name!.Value);
    }

    [Fact]
    public void Toc_EmitsTocFieldAndTitle()
    {
        using var ms = new MemoryStream();
        using var doc = ExportSingle(new ElementDto
        { Id = "toc", Type = "toc", X = 0, Y = 0, Width = 400, Height = 100, TocTitle = "Contents" }, ms);

        var body = doc.MainDocumentPart!.Document!.Body!;
        Assert.Contains("Contents", body.InnerText);
        var fieldCode = body.Descendants<FieldCode>().FirstOrDefault();
        Assert.NotNull(fieldCode);
        Assert.Contains("TOC", fieldCode!.Text);
    }

    [Fact]
    public void Checkbox_RendersBoxGlyphAndLabel()
    {
        using var ms = new MemoryStream();
        using var doc = ExportSingle(new ElementDto
        { Id = "cb", Type = "checkbox", X = 0, Y = 0, Width = 100, Height = 20, FieldLabel = "Agree" }, ms);

        var text = doc.MainDocumentPart!.Document!.Body!.InnerText;
        Assert.Contains("☐", text);
        Assert.Contains("Agree", text);
    }

    [Fact]
    public void Field_RendersLabelAndUnderscoreLine()
    {
        using var ms = new MemoryStream();
        using var doc = ExportSingle(new ElementDto
        { Id = "f", Type = "field", X = 0, Y = 0, Width = 200, Height = 20, FieldLabel = "Name", Required = true }, ms);

        var text = doc.MainDocumentPart!.Document!.Body!.InnerText;
        Assert.Contains("Name", text);
        Assert.Contains("_", text);
        Assert.Contains("*", text); // required marker
    }

    [Fact]
    public void Textarea_RendersMultipleUnderscoreLines()
    {
        using var ms = new MemoryStream();
        using var doc = ExportSingle(new ElementDto
        { Id = "ta", Type = "textarea", X = 0, Y = 0, Width = 200, Height = 80, FieldLabel = "Notes" }, ms);

        var text = doc.MainDocumentPart!.Document!.Body!.InnerText;
        Assert.Contains("Notes", text);
        Assert.Contains("_______________", text);
    }

    [Fact]
    public void OptionList_RendersOrderedItems()
    {
        using var ms = new MemoryStream();
        using var doc = ExportSingle(new ElementDto
        {
            Id = "ol", Type = "optionlist", X = 0, Y = 0, Width = 200, Height = 60,
            Options = ["Alpha", "Beta"], Ordered = true,
        }, ms);

        var text = doc.MainDocumentPart!.Document!.Body!.InnerText;
        Assert.Contains("1. Alpha", text);
        Assert.Contains("2. Beta", text);
    }

    [Fact]
    public void Note_RendersTitleAndBody()
    {
        using var ms = new MemoryStream();
        using var doc = ExportSingle(new ElementDto
        { Id = "nt", Type = "note", X = 0, Y = 0, Width = 200, Height = 60, NoteTitle = "Heads up", NoteBody = "Body text" }, ms);

        var text = doc.MainDocumentPart!.Document!.Body!.InnerText;
        Assert.Contains("Heads up", text);
        Assert.Contains("Body text", text);
    }

    [Fact]
    public void Number_RendersValue()
    {
        using var ms = new MemoryStream();
        using var doc = ExportSingle(new ElementDto
        { Id = "num", Type = "number", X = 0, Y = 0, Width = 100, Height = 20, NumberValue = 42 }, ms);

        Assert.Contains("42", doc.MainDocumentPart!.Document!.Body!.InnerText);
    }

    [Fact]
    public void Date_RendersNonEmptyText()
    {
        using var ms = new MemoryStream();
        using var doc = ExportSingle(new ElementDto
        { Id = "dt", Type = "date", X = 0, Y = 0, Width = 100, Height = 20, DateFormat = "yyyy" }, ms);

        var text = doc.MainDocumentPart!.Document!.Body!.InnerText;
        Assert.Matches(@"\d{4}", text);
    }

    [Fact]
    public void QrCode_EmbedsImagePart()
    {
        using var ms = new MemoryStream();
        using var doc = ExportSingle(new ElementDto
        { Id = "qr", Type = "qrcode", X = 0, Y = 0, Width = 80, Height = 80, QrValue = "https://example.com" }, ms);

        Assert.NotEmpty(doc.MainDocumentPart!.ImageParts);
    }

    [Fact]
    public void Barcode_EmbedsImagePart()
    {
        using var ms = new MemoryStream();
        using var doc = ExportSingle(new ElementDto
        { Id = "bc", Type = "barcode", X = 0, Y = 0, Width = 200, Height = 60, BarcodeValue = "123456789012", BarcodeType = "code128" }, ms);

        Assert.NotEmpty(doc.MainDocumentPart!.ImageParts);
    }

    [Fact]
    public void ContentControl_EmitsSdtBlock()
    {
        using var ms = new MemoryStream();
        using var doc = ExportSingle(new ElementDto
        {
            Id = "cc", Type = "contentcontrol", X = 0, Y = 0, Width = 200, Height = 20,
            ContentControlTitle = "Pick", ContentControlTag = "pick", ContentControlPlaceholder = "choose…",
        }, ms);

        Assert.NotEmpty(doc.MainDocumentPart!.Document!.Body!.Descendants<SdtBlock>());
    }

    [Fact]
    public void Dropdown_EmitsSdtBlockWithListItems()
    {
        using var ms = new MemoryStream();
        using var doc = ExportSingle(new ElementDto
        {
            Id = "dd", Type = "dropdown", X = 0, Y = 0, Width = 200, Height = 20,
            FieldLabel = "Choose", Options = ["One", "Two"],
        }, ms);

        var sdt = doc.MainDocumentPart!.Document!.Body!.Descendants<SdtBlock>().FirstOrDefault();
        Assert.NotNull(sdt);
        Assert.NotEmpty(sdt!.Descendants<ListItem>());
    }

    [Fact]
    public void Circle_FidelityV2_EmitsAnchoredDrawing()
    {
        using var ms = new MemoryStream();
        using var doc = ExportSingle(new ElementDto
        {
            Id = "c", Type = "circle", X = 10, Y = 10, Width = 50, Height = 50,
            Style = new Dictionary<string, object> { ["backgroundColor"] = "#ff0000" },
        }, ms);

        Assert.NotEmpty(doc.MainDocumentPart!.Document!.Body!.Descendants<Drawing>());
    }

    [Theory]
    // NormalizeHexColor accepts CSS rgb()/rgba()/hsl()/named colors, not just hex — the paragraph
    // Shading Fill is the resolved 6-digit hex (alpha dropped; Word shading has no alpha).
    [InlineData("rgb(255, 0, 0)", "FF0000")]
    [InlineData("rgba(0, 128, 0, 0.5)", "008000")]
    [InlineData("rgb(100%, 0%, 0%)", "FF0000")]
    [InlineData("hsl(120, 100%, 50%)", "00FF00")]
    [InlineData("red", "FF0000")]
    [InlineData("navy", "000080")]
    [InlineData("#0a0", "00AA00")]
    public void BackgroundColor_NormalizesCssColors_ToShadingFill(string css, string expected)
    {
        using var ms = new MemoryStream();
        using var doc = ExportSingle(new ElementDto
        {
            Id = "t", Type = "text", X = 0, Y = 0, Width = 100, Height = 20, Content = "hi",
            Style = new Dictionary<string, object> { ["backgroundColor"] = css },
        }, ms, fidelityV2: false);

        var shading = doc.MainDocumentPart!.Document!.Body!.Descendants<Shading>().FirstOrDefault();
        Assert.NotNull(shading);
        Assert.Equal(expected, shading!.Fill!.Value);
    }

    [Fact]
    public void UnsupportedElement_RendersPlaceholderAndWarning()
    {
        using var ms = new MemoryStream();
        using var doc = ExportSingle(new ElementDto
        { Id = "wm", Type = "watermark", X = 0, Y = 0, Width = 100, Height = 20, Content = "DRAFT" }, ms);

        // Placeholder text is emitted and the warning is surfaced in package description.
        Assert.Contains("[watermark", doc.MainDocumentPart!.Document!.Body!.InnerText);
        Assert.Contains("ExportWarnings", doc.PackageProperties.Description ?? "");
    }
}
