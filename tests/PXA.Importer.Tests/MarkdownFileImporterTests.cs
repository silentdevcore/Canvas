using System.Text;
using PXA.Core.Contracts;
using PXA.FileImporter;

namespace PXA.Importer.Tests;

public sealed class MarkdownFileImporterTests
{
    private static DesignExportDto ImportText(string markdown)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
        return MarkdownFileImporter.Import(stream, "Test Doc");
    }

    [Fact]
    public void Import_HeadingLevels_ProduceTextElementsWithMappedFontSizesAndHeadingLevel()
    {
        var design = ImportText("# Title\n\n## Section\n\n### Subsection\n");

        var elements = design.Pages[0].Elements;
        Assert.Equal(3, elements.Count);

        Assert.Equal("text", elements[0].Type);
        Assert.Equal("Title", elements[0].Content);
        Assert.Equal(1, elements[0].HeadingLevel);
        Assert.Equal(28d, elements[0].Style!["fontSize"]);

        Assert.Equal(2, elements[1].HeadingLevel);
        Assert.Equal(24d, elements[1].Style!["fontSize"]);

        Assert.Equal(3, elements[2].HeadingLevel);
        Assert.Equal(18d, elements[2].Style!["fontSize"]);
    }

    [Fact]
    public void Import_PlainParagraph_ProducesTextElement()
    {
        var design = ImportText("Just a plain sentence with no formatting.");

        var el = Assert.Single(design.Pages[0].Elements);
        Assert.Equal("text", el.Type);
        Assert.Equal("Just a plain sentence with no formatting.", el.Content);
    }

    [Fact]
    public void Import_ParagraphWithBoldItalicAndLink_ProducesRichTextElement()
    {
        var design = ImportText("This has **bold**, *italic*, and a [link](https://example.com).");

        var el = Assert.Single(design.Pages[0].Elements);
        Assert.Equal("richtext", el.Type);
        Assert.Contains("<strong>bold</strong>", el.HtmlContent);
        Assert.Contains("<em>italic</em>", el.HtmlContent);
        Assert.Contains("<a href=\"https://example.com\">link</a>", el.HtmlContent);
    }

    [Fact]
    public void Import_PipeTable_ProducesTableElementWithHeaderAndAlignments()
    {
        var design = ImportText(
            "| Name | Qty | Price |\n" +
            "|:---|:---:|---:|\n" +
            "| Widget | 3 | 9.99 |\n" +
            "| Gadget | 1 | 19.99 |\n");

        var el = Assert.Single(design.Pages[0].Elements);
        Assert.Equal("table", el.Type);
        Assert.True(el.HeaderRow);
        Assert.Equal(["left", "center", "right"], el.ColumnAlignments!);
        Assert.Equal(3, el.CellData!.Length);
        Assert.Equal(["Name", "Qty", "Price"], el.CellData[0]);
        Assert.Equal(["Widget", "3", "9.99"], el.CellData[1]);
        Assert.Equal(["Gadget", "1", "19.99"], el.CellData[2]);
    }

    [Fact]
    public void Import_UnorderedList_ProducesOptionListElement()
    {
        var design = ImportText("- Apples\n- Bananas\n- Cherries\n");

        var el = Assert.Single(design.Pages[0].Elements);
        Assert.Equal("optionlist", el.Type);
        Assert.Equal(["Apples", "Bananas", "Cherries"], el.Options!);
        Assert.False(el.Ordered);
        Assert.Equal("disc", el.ListStyle);
    }

    [Fact]
    public void Import_OrderedList_ProducesOptionListElementMarkedOrdered()
    {
        var design = ImportText("1. First\n2. Second\n3. Third\n");

        var el = Assert.Single(design.Pages[0].Elements);
        Assert.Equal("optionlist", el.Type);
        Assert.True(el.Ordered);
        Assert.Equal("decimal", el.ListStyle);
    }

    [Fact]
    public void Import_GfmTaskList_ProducesOneCheckboxElementPerItem()
    {
        var design = ImportText("- [x] Done thing\n- [ ] Not done thing\n");

        var elements = design.Pages[0].Elements;
        Assert.Equal(2, elements.Count);

        Assert.Equal("checkbox", elements[0].Type);
        Assert.Equal("Done thing", elements[0].FieldLabel);
        Assert.Equal("checked", elements[0].CheckState);

        Assert.Equal("checkbox", elements[1].Type);
        Assert.Equal("Not done thing", elements[1].FieldLabel);
        Assert.Equal("empty", elements[1].CheckState);
    }

    [Fact]
    public void Import_Blockquote_ProducesNoteElement()
    {
        var design = ImportText("> **Heads up**\n> This is the body of the note.\n");

        var el = Assert.Single(design.Pages[0].Elements);
        Assert.Equal("note", el.Type);
        Assert.Equal("Heads up", el.NoteTitle);
        Assert.Equal("This is the body of the note.", el.NoteBody);
    }

    [Fact]
    public void Import_ThematicBreak_ProducesLineElement()
    {
        var design = ImportText("Above\n\n---\n\nBelow\n");

        var elements = design.Pages[0].Elements;
        Assert.Equal(3, elements.Count);
        Assert.Equal("line", elements[1].Type);
    }

    [Fact]
    public void Import_FencedCodeBlock_ProducesRichTextElementWithPreCode()
    {
        var design = ImportText("```\nvar x = 1;\n```\n");

        var el = Assert.Single(design.Pages[0].Elements);
        Assert.Equal("richtext", el.Type);
        Assert.Contains("<pre", el.HtmlContent);
        Assert.Contains("<code>", el.HtmlContent);
        Assert.Contains("var x = 1;", el.HtmlContent);
    }

    [Fact]
    public void Import_StandaloneImage_ProducesImageElement()
    {
        var design = ImportText("![A nice photo](https://example.com/photo.png)\n");

        var el = Assert.Single(design.Pages[0].Elements);
        Assert.Equal("image", el.Type);
        Assert.Equal("https://example.com/photo.png", el.Content);
        Assert.Equal("A nice photo", el.Name);
    }

    [Fact]
    public void Import_RawScriptTagInFormattedParagraph_IsEscapedInHtmlContent()
    {
        // richtext.HtmlContent renders via dangerouslySetInnerHTML on the frontend with no further
        // sanitization anywhere in the codebase — .DisableHtml() must keep raw HTML/script tags from a
        // malicious .md upload from becoming a stored-XSS vector once opened in the editor. Inline
        // formatting (bold) forces this paragraph through the richtext/HtmlContent path, the actual sink.
        var design = ImportText("This is **important**: <script>alert('xss')</script>");

        var el = Assert.Single(design.Pages[0].Elements);
        Assert.Equal("richtext", el.Type);
        Assert.DoesNotContain("<script>", el.HtmlContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", el.HtmlContent);
    }

    [Fact]
    public void Import_EmptyInput_ReturnsSinglePageWithNoElements()
    {
        var design = ImportText("");

        Assert.Single(design.Pages);
        Assert.Empty(design.Pages[0].Elements);
    }

    [Fact]
    public void Import_UsesDocumentH1AsName_WhenNoExplicitNameProvided()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("# My Document Title\n\nSome body text.\n"));
        var design = MarkdownFileImporter.Import(stream);

        Assert.Equal("My Document Title", design.Name);
    }

    [Fact]
    public void SupportedExtensions_IncludesMdAndMarkdown()
    {
        var importer = new MarkdownFileImporter();
        Assert.Contains("md", importer.SupportedExtensions);
        Assert.Contains("markdown", importer.SupportedExtensions);
    }

    // The PDF renderer has no clipping — an element's Height must scale with its actual content length,
    // or a long paragraph/cell draws straight through whatever comes after it in the exported PDF.

    [Fact]
    public void Import_LongParagraph_ProducesTallerElementThanShortParagraph()
    {
        var shortDesign = ImportText("Short line.");
        var longText = string.Concat(Enumerable.Repeat("This is a fairly long sentence that keeps going. ", 12));
        var longDesign = ImportText(longText);

        var shortHeight = Assert.Single(shortDesign.Pages[0].Elements).Height;
        var longHeight = Assert.Single(longDesign.Pages[0].Elements).Height;

        Assert.True(longHeight > shortHeight * 2, $"expected long paragraph height ({longHeight}) to be much taller than short paragraph height ({shortHeight})");
    }

    [Fact]
    public void Import_LongHeading_ProducesTallerElementThanShortHeading()
    {
        var shortDesign = ImportText("# Hi");
        var longDesign = ImportText("# " + string.Concat(Enumerable.Repeat("Very long heading text ", 10)));

        var shortHeight = Assert.Single(shortDesign.Pages[0].Elements).Height;
        var longHeight = Assert.Single(longDesign.Pages[0].Elements).Height;

        Assert.True(longHeight > shortHeight, $"expected long heading height ({longHeight}) to exceed short heading height ({shortHeight})");
    }

    [Fact]
    public void Import_TableWithLongCell_ProducesTallerElementThanTableWithShortCells()
    {
        var shortDesign = ImportText("| A | B |\n|---|---|\n| x | y |\n");
        var longCellText = string.Concat(Enumerable.Repeat("wordy ", 40)).Trim();
        var longDesign = ImportText($"| A | B |\n|---|---|\n| {longCellText} | y |\n");

        var shortHeight = Assert.Single(shortDesign.Pages[0].Elements).Height;
        var longHeight = Assert.Single(longDesign.Pages[0].Elements).Height;

        Assert.True(longHeight > shortHeight, $"expected table with a long cell ({longHeight}) to be taller than one with only short cells ({shortHeight})");
    }

    [Fact]
    public void Import_LongListItem_ProducesTallerElementThanShortItems()
    {
        var shortDesign = ImportText("- a\n- b\n");
        var longItemText = string.Concat(Enumerable.Repeat("long item text ", 20)).Trim();
        var longDesign = ImportText($"- {longItemText}\n- b\n");

        var shortHeight = Assert.Single(shortDesign.Pages[0].Elements).Height;
        var longHeight = Assert.Single(longDesign.Pages[0].Elements).Height;

        Assert.True(longHeight > shortHeight, $"expected list with a long item ({longHeight}) to be taller than one with only short items ({shortHeight})");
    }

    [Fact]
    public void Import_LongBlockquoteBody_ProducesTallerElementThanShortBody()
    {
        var shortDesign = ImportText("> **Title**\n> short body\n");
        var longBody = string.Concat(Enumerable.Repeat("long quoted body text ", 20)).Trim();
        var longDesign = ImportText($"> **Title**\n> {longBody}\n");

        var shortHeight = Assert.Single(shortDesign.Pages[0].Elements).Height;
        var longHeight = Assert.Single(longDesign.Pages[0].Elements).Height;

        Assert.True(longHeight > shortHeight, $"expected note with a long body ({longHeight}) to be taller than one with a short body ({shortHeight})");
    }

    // Pagination: a page has ~794pt of usable height (842 page height − 48 top/bottom margins). A
    // document longer than that must split into multiple PageDto entries instead of positioning content
    // past the visible page — this was the actual root cause behind "conversion doesn't work" for any
    // realistically long markdown file (e.g. a multi-slide presentation script).

    [Fact]
    public void Import_ShortDocument_ProducesExactlyOnePage()
    {
        var design = ImportText("# Title\n\nOne short paragraph.\n");

        Assert.Single(design.Pages);
    }

    [Fact]
    public void Import_LongDocument_SplitsAcrossMultiplePages()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 40; i++)
        {
            sb.AppendLine($"## Section {i}");
            sb.AppendLine();
            sb.AppendLine($"This is paragraph number {i} with some body text to occupy vertical space on the page.");
            sb.AppendLine();
        }

        var design = ImportText(sb.ToString());

        Assert.True(design.Pages.Count > 1, $"expected a long document to span multiple pages, got {design.Pages.Count}");
    }

    [Fact]
    public void Import_LongDocument_EveryPageStaysWithinUsableHeight()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 40; i++)
        {
            sb.AppendLine($"## Section {i}");
            sb.AppendLine();
            sb.AppendLine($"This is paragraph number {i} with some body text to occupy vertical space on the page.");
            sb.AppendLine();
        }

        var design = ImportText(sb.ToString());
        const double maxY = 842 - 48; // PageHeight - MarginY

        foreach (var page in design.Pages)
        {
            foreach (var el in page.Elements)
            {
                Assert.True(el.Y < maxY, $"element '{el.Id}' at y={el.Y} on page '{page.Id}' exceeds the usable page height ({maxY})");
            }
        }
    }

    [Fact]
    public void Import_LongDocument_DistributesElementsAcrossPages_NotAllOnFirstPage()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 40; i++)
        {
            sb.AppendLine($"## Section {i}");
            sb.AppendLine();
            sb.AppendLine($"This is paragraph number {i} with some body text to occupy vertical space on the page.");
            sb.AppendLine();
        }

        var design = ImportText(sb.ToString());

        Assert.True(design.Pages.Count > 1);
        foreach (var page in design.Pages)
        {
            Assert.NotEmpty(page.Elements);
        }
    }

    [Fact]
    public void Import_LongTaskList_SplitsAcrossPagesMidList()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 80; i++)
            sb.AppendLine($"- [{(i % 2 == 0 ? "x" : " ")}] Task item number {i}");

        var design = ImportText(sb.ToString());

        Assert.True(design.Pages.Count > 1, $"expected a long task list to split across pages, got {design.Pages.Count}");
        var totalCheckboxes = design.Pages.Sum(p => p.Elements.Count(e => e.Type == "checkbox"));
        Assert.Equal(80, totalCheckboxes);
    }
}
