using System.Text;
using System.Net;
using System.Net.Http.Headers;
using PXA.Core.Contracts;
using PXA.FileImporter;
using SkiaSharp;

namespace PXA.Importer.Tests;

public sealed class MarkdownFileImporterTests
{
    private static DesignExportDto ImportText(string markdown)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
        return MarkdownFileImporter.Import(stream, "Test Doc");
    }

    [Fact]
    public async Task ImportAsync_CancelledRequest_StopsBeforeReading()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("# Cancelled"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new MarkdownFileImporter().ImportAsync(stream, "Cancelled", cancellation.Token));
    }

    [Fact]
    public async Task ImportAsync_RemoteImage_EmbedsResolvedDataUrl()
    {
        var expected = $"data:image/png;base64,{Convert.ToBase64String(CreatePng())}";
        var resolver = new StubRemoteImageResolver(expected);
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes("![remote](https://images.example/photo.png)"));

        var design = await new MarkdownFileImporter(resolver)
            .ImportAsync(stream, "Remote", CancellationToken.None);

        var image = Assert.Single(design.Pages[0].Elements);
        Assert.Equal(expected, image.Content);
        Assert.Equal(["https://images.example/photo.png"], resolver.Sources);
    }

    [Fact]
    public async Task ImportAsync_RelativeImageWithoutAssetBase_RemovesUnresolvedSource()
    {
        var resolver = new StubRemoteImageResolver("unused");
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes("![relative](assets/photo.png)"));

        var design = await new MarkdownFileImporter(resolver)
            .ImportAsync(stream, "Relative", CancellationToken.None);

        Assert.Equal("", Assert.Single(design.Pages[0].Elements).Content);
        Assert.Empty(resolver.Sources);
        var diagnostic = Assert.Single(design.ImportDiagnostics!);
        Assert.Equal("PXA-MD-201", diagnostic.Code);
        Assert.Equal("assets/photo.png", diagnostic.Source);
    }

    [Fact]
    public async Task ImportAsync_RelativeImageWithAssetBase_ResolvesAbsoluteUrl()
    {
        var expected = $"data:image/png;base64,{Convert.ToBase64String(CreatePng())}";
        var resolver = new StubRemoteImageResolver(expected);
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes("![relative](assets/photo.png)"));

        var design = await new MarkdownFileImporter(resolver).ImportAsync(
            stream,
            "Relative",
            new Uri("https://cdn.example/docs/"),
            CancellationToken.None);

        Assert.Equal(expected, Assert.Single(design.Pages[0].Elements).Content);
        Assert.Equal(["https://cdn.example/docs/assets/photo.png"], resolver.Sources);
        Assert.Null(design.ImportDiagnostics);
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
    public void Import_NestedLists_PreserveHierarchyAndIndependentNumbering()
    {
        var design = ImportText(
            "3. Parent\n" +
            "   - Child one\n" +
            "   - Child two\n" +
            "4. Next parent\n");

        var lists = design.Pages[0].Elements
            .Where(element => element.Type == "optionlist")
            .ToList();

        Assert.Equal(4, lists.Count);
        Assert.Equal(["Parent"], lists[0].Options!);
        Assert.Equal(3, lists[0].StartNumber);
        Assert.Equal(0, lists[0].Style!["markdownListDepth"]);
        Assert.Equal(["Child one"], lists[1].Options!);
        Assert.True(lists[1].X > lists[0].X);
        Assert.Equal(1, lists[1].Style!["markdownListDepth"]);
        Assert.Equal(["Child two"], lists[2].Options!);
        Assert.Equal(["Next parent"], lists[3].Options!);
        Assert.Equal(4, lists[3].StartNumber);
    }

    [Fact]
    public void Import_InlineCodeInListAndTable_PreservesCodeDelimiters()
    {
        var design = ImportText(
            "- Call `Render()` now\n\n" +
            "| API | Meaning |\n" +
            "|---|---|\n" +
            "| `Run()` | Executes `code` |\n");
        var elements = design.Pages[0].Elements;

        var list = Assert.Single(elements, element => element.Type == "optionlist");
        Assert.Equal(["Call `Render()` now"], list.Options!);

        var table = Assert.Single(elements, element => element.Type == "table");
        Assert.Equal("`Run()`", table.CellData![1][0]);
        Assert.Equal("Executes `code`", table.CellData[1][1]);
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
    public void Import_FencedCodeBlock_PreservesLanguageMetadata()
    {
        var design = ImportText("```csharp title=Example\nvar x = 1;\n```\n");

        var el = Assert.Single(design.Pages[0].Elements);
        Assert.Equal("csharp", el.Style!["codeLanguage"]);
    }

    [Fact]
    public void Import_Strikethrough_PreservesDelMarkup()
    {
        var design = ImportText("Keep ~~obsolete~~ current.");

        var el = Assert.Single(design.Pages[0].Elements);
        Assert.Equal("richtext", el.Type);
        Assert.Contains("<del>obsolete</del>", el.HtmlContent);
    }

    [Fact]
    public void Import_DefinitionList_ProducesEditableNote()
    {
        var design = ImportText("Renderer\n:   Converts a design into output.\n");

        var el = Assert.Single(design.Pages[0].Elements);
        Assert.Equal("note", el.Type);
        Assert.Equal("Renderer", el.NoteTitle);
        Assert.Equal("Converts a design into output.", el.NoteBody);
        Assert.Equal(true, el.Style!["markdownDefinition"]);
    }

    [Fact]
    public void Import_Footnote_ProducesFootnoteElementAndReferenceText()
    {
        var design = ImportText("A statement with a note.[^source]\n\n[^source]: Verified source.\n");
        var elements = design.Pages.SelectMany(page => page.Elements).ToList();

        var referenceText = Assert.Single(elements, element => element.Type == "richtext");
        Assert.Contains("id=\"fnref:1\"", referenceText.HtmlContent);
        Assert.Contains("href=\"#fn:1\"", referenceText.HtmlContent);
        var footnote = Assert.Single(elements, element => element.Type == "footnote");
        Assert.Equal("source", footnote.FootnoteRef);
        Assert.Contains("Verified source.", footnote.FootnoteText);
        Assert.Equal(true, footnote.Style!["markdownFootnote"]);
    }

    [Fact]
    public void Import_YamlFrontMatter_AppliesMetadataLanguageAndPageLayout()
    {
        var design = ImportText(
            "---\n" +
            "title: Quarterly Report\n" +
            "author: Jane Doe\n" +
            "language: de-DE\n" +
            "pageSize: Letter\n" +
            "orientation: landscape\n" +
            "margins:\n" +
            "  top: 18pt\n" +
            "  right: 10mm\n" +
            "  bottom: 0.5in\n" +
            "  left: 2cm\n" +
            "---\n" +
            "# Content\n");

        Assert.Equal("Quarterly Report", design.Name);
        var settings = Assert.IsType<PageSettingsDto>(design.PageSettings);
        Assert.Equal(792, settings.Width);
        Assert.Equal(612, settings.Height);
        Assert.Equal("landscape", settings.Orientation);
        Assert.Equal("Quarterly Report", settings.Metadata!.Title);
        Assert.Equal("Jane Doe", settings.Metadata.Author);
        Assert.Equal("de-DE", settings.SystemLanguage);
        Assert.Equal(["de-DE"], settings.ActiveLanguages);
        Assert.Equal("de-DE", settings.TargetLanguage);
        Assert.Equal(18, settings.Margins!.Top);
        Assert.Equal(72d / 25.4 * 10, settings.Margins.Right, precision: 5);
        Assert.Equal(36, settings.Margins.Bottom);
        Assert.Equal(72d / 2.54 * 2, settings.Margins.Left, precision: 5);

        var heading = Assert.Single(design.Pages[0].Elements);
        Assert.Equal(settings.Margins.Left, heading.X, precision: 5);
        Assert.Equal(settings.Margins.Top, heading.Y, precision: 5);
        Assert.Equal(
            settings.Width - settings.Margins.Left - settings.Margins.Right,
            heading.Width,
            precision: 5);
        Assert.Null(design.ImportDiagnostics);
    }

    [Fact]
    public void Import_InvalidYamlFrontMatter_UsesSafeDefaultsAndDiagnostics()
    {
        var design = ImportText(
            "---\n" +
            "language: not_valid!\n" +
            "pageSize: billboard\n" +
            "orientation: diagonal\n" +
            "margins: 500pt\n" +
            "---\n" +
            "Body\n");

        var settings = Assert.IsType<PageSettingsDto>(design.PageSettings);
        Assert.Equal(595, settings.Width);
        Assert.Equal(842, settings.Height);
        Assert.Equal("portrait", settings.Orientation);
        Assert.Equal(48, settings.Margins!.Top);
        Assert.Null(settings.SystemLanguage);
        Assert.Equal(4, design.ImportDiagnostics!.Count(
            diagnostic => diagnostic.Code == "PXA-MD-006"));
        Assert.Single(design.Pages[0].Elements);
    }

    [Fact]
    public void Import_OversizedYamlMetadata_IsIgnored()
    {
        var title = new string('x', 513);
        var design = ImportText($"---\ntitle: {title}\n---\nBody\n");

        Assert.Equal("Test Doc", design.Name);
        var diagnostic = Assert.Single(design.ImportDiagnostics!);
        Assert.Equal("PXA-MD-006", diagnostic.Code);
        Assert.Contains("title", diagnostic.Message);
    }

    [Theory]
    [InlineData("A3", 842, 1191)]
    [InlineData("A4", 595, 842)]
    [InlineData("Letter", 612, 792)]
    [InlineData("Legal", 612, 1008)]
    public void Import_YamlFrontMatter_SupportsNamedPageSizes(
        string pageSize,
        double expectedWidth,
        double expectedHeight)
    {
        var design = ImportText($"---\npage_size: {pageSize}\n---\nBody\n");

        Assert.Equal(expectedWidth, design.PageSettings!.Width);
        Assert.Equal(expectedHeight, design.PageSettings.Height);
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
    public void Import_EmbeddedPng_PreservesValidatedDataUrl()
    {
        using var bitmap = new SKBitmap(1, 1);
        bitmap.SetPixel(0, 0, SKColors.Red);
        using var imageData = SKImage.FromBitmap(bitmap);
        using var encoded = imageData.Encode(SKEncodedImageFormat.Png, 100);
        var png = Convert.ToBase64String(encoded.ToArray());
        var design = ImportText($"![pixel](data:image/png;base64,{png})");

        var image = Assert.Single(design.Pages[0].Elements);
        Assert.Equal("image", image.Type);
        Assert.StartsWith("data:image/png;base64,", image.Content);
    }

    [Theory]
    [InlineData("data:image/svg+xml;base64,PHN2Zz48L3N2Zz4=")]
    [InlineData("data:text/html;base64,PGh0bWw+PC9odG1sPg==")]
    [InlineData("data:image/png;base64,not-valid-base64")]
    public void Import_UnsafeOrInvalidEmbeddedImage_RemovesSource(string source)
    {
        var design = ImportText($"![unsafe]({source})");

        var image = Assert.Single(design.Pages[0].Elements);
        Assert.Equal("image", image.Type);
        Assert.Equal("", image.Content);
        Assert.Contains(design.ImportDiagnostics!, diagnostic => diagnostic.Code == "PXA-MD-002");
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

    [Theory]
    [InlineData("javascript:alert%281%29")]
    [InlineData("jAvAsCrIpT:alert%281%29")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("file:///etc/passwd")]
    [InlineData("vbscript:msgbox%281%29")]
    public void Import_UnsafeLinkScheme_RemovesTarget(string target)
    {
        var design = ImportText($"Open [this link]({target}).");

        var el = Assert.Single(design.Pages[0].Elements);
        Assert.Equal("richtext", el.Type);
        Assert.DoesNotContain(target.Split(':')[0], el.HtmlContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", el.HtmlContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("this link", el.HtmlContent);
    }

    [Theory]
    [InlineData("https://example.com/docs")]
    [InlineData("http://example.com/docs")]
    [InlineData("mailto:support@example.com")]
    [InlineData("../guide/getting-started.md")]
    [InlineData("#installation")]
    public void Import_SafeLinkTarget_IsPreserved(string target)
    {
        var design = ImportText($"Open [the documentation]({target}).");

        var el = Assert.Single(design.Pages[0].Elements);
        Assert.Contains($"href=\"{target}\"", el.HtmlContent);
    }

    [Fact]
    public void Import_TextBeyondConfiguredLimit_IsRejected()
    {
        var markdown = new string('a', MarkdownFileImporter.MaxInputCharacters + 1);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(markdown));

        var error = Assert.Throws<InvalidDataException>(() => MarkdownFileImporter.Import(stream));

        Assert.Contains("text-size limit", error.Message);
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

    // Pagination: a page has 746pt of usable height (842 page height - 48pt top/bottom margins). A
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
                Assert.True(
                    el.Y + el.Height <= maxY,
                    $"element '{el.Id}' ends at y={el.Y + el.Height} on page '{page.Id}' and exceeds the usable page height ({maxY})");
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

    [Fact]
    public void Import_LongOrdinaryList_SplitsAcrossPagesAndKeepsNumbering()
    {
        var markdown = string.Join(
            "\n",
            Enumerable.Range(7, 100)
                .Select((number, index) => $"{(index == 0 ? number : 1)}. List item {number} with enough text to occupy one rendered line"));

        var design = ImportText(markdown);
        var listElements = design.Pages
            .SelectMany(page => page.Elements)
            .Where(element => element.Type == "optionlist")
            .ToList();

        Assert.True(design.Pages.Count > 1);
        Assert.Equal(100, listElements.Sum(element => element.Options!.Length));
        Assert.Equal(7, listElements[0].StartNumber);
        for (var index = 1; index < listElements.Count; index++)
        {
            var previous = listElements[index - 1];
            Assert.Equal(previous.StartNumber + previous.Options!.Length, listElements[index].StartNumber);
        }

        const double maxY = 842 - 48;
        Assert.All(
            design.Pages.SelectMany(page => page.Elements),
            element => Assert.True(element.Y + element.Height <= maxY));
    }

    [Fact]
    public void Import_LongTable_SplitsByRowAndRepeatsHeader()
    {
        var markdown = new StringBuilder()
            .AppendLine("| Name | Value |")
            .AppendLine("|---|---:|");
        for (var index = 0; index < 100; index++)
            markdown.AppendLine($"| Row {index} | {index} |");

        var design = ImportText(markdown.ToString());
        var tables = design.Pages
            .SelectMany(page => page.Elements)
            .Where(element => element.Type == "table")
            .ToList();

        Assert.True(tables.Count > 1);
        Assert.All(tables, table => Assert.Equal(["Name", "Value"], table.CellData![0]));
        Assert.Equal(101, tables.Sum(table => table.CellData!.Length - 1) + 1);
        Assert.All(
            tables,
            table => Assert.True(table.Y + table.Height <= 842 - 48));
    }

    [Fact]
    public void Import_LongCodeBlock_SplitsAcrossPagesWithoutDroppingText()
    {
        var sourceLines = Enumerable.Range(0, 150)
            .Select(index => $"Console.WriteLine(\"line-{index:D3}\");")
            .ToArray();
        var design = ImportText($"```\n{string.Join("\n", sourceLines)}\n```\n");
        var codeElements = design.Pages
            .SelectMany(page => page.Elements)
            .Where(element => element.Type == "richtext")
            .ToList();

        Assert.True(design.Pages.Count > 1);
        Assert.True(codeElements.Count > 1);
        Assert.Contains("line-000", codeElements[0].HtmlContent);
        Assert.Contains("line-149", codeElements[^1].HtmlContent);
        Assert.All(
            codeElements,
            element => Assert.True(element.Y + element.Height <= 842 - 48));
    }

    [Fact]
    public void Import_SingleLongParagraph_SplitsAcrossPagesWithoutOverflow()
    {
        var words = Enumerable.Range(0, 8_000).Select(index => $"word{index}");
        var design = ImportText(string.Join(" ", words));
        var paragraphs = design.Pages
            .SelectMany(page => page.Elements)
            .Where(element => element.Type == "text")
            .ToList();

        Assert.True(design.Pages.Count > 1);
        Assert.Contains("word0", paragraphs[0].Content);
        Assert.Contains("word7999", paragraphs[^1].Content);
        Assert.All(paragraphs, element => Assert.True(element.Y + element.Height <= 842 - 48));
    }

    [Fact]
    public void Import_SingleLongBlockquote_SplitsAcrossPagesWithoutOverflow()
    {
        var body = string.Join(" ", Enumerable.Range(0, 7_000).Select(index => $"quote{index}"));
        var design = ImportText($"> **Important**\n> {body}");
        var notes = design.Pages
            .SelectMany(page => page.Elements)
            .Where(element => element.Type == "note")
            .ToList();

        Assert.True(notes.Count > 1);
        Assert.All(notes, note => Assert.Equal("Important", note.NoteTitle));
        Assert.Contains("quote0", notes[0].NoteBody);
        Assert.Contains("quote6999", notes[^1].NoteBody);
        Assert.All(notes, element => Assert.True(element.Y + element.Height <= 842 - 48));
    }

    [Fact]
    public void Import_TableWithSingleOversizedRow_SplitsRowWithoutOverflow()
    {
        var longCell = string.Join(" ", Enumerable.Range(0, 5_000).Select(index => $"cell{index}"));
        var design = ImportText($"| Name | Value |\n|---|---|\n| Row | {longCell} |\n");
        var tables = design.Pages
            .SelectMany(page => page.Elements)
            .Where(element => element.Type == "table")
            .ToList();

        Assert.True(tables.Count > 1);
        Assert.All(tables, table => Assert.Equal(["Name", "Value"], table.CellData![0]));
        Assert.Contains("cell0", tables[0].CellData![1][1]);
        Assert.Contains("cell4999", tables[^1].CellData![1][1]);
        Assert.All(tables, element => Assert.True(element.Y + element.Height <= 842 - 48));
    }

    [Fact]
    public async Task SafeRemoteImageResolver_PublicPng_ReturnsValidatedDataUrl()
    {
        var png = CreatePng();
        using var client = new HttpClient(new StubHttpHandler(_ =>
        {
            var content = new ByteArrayContent(png);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }));
        using var resolver = new SafeRemoteImageResolver(
            client,
            (_, _) => Task.FromResult(true));

        var result = await resolver.ResolveAsDataUrlAsync(
            "https://images.example/photo.png",
            CancellationToken.None);

        Assert.Equal($"data:image/png;base64,{Convert.ToBase64String(png)}", result);
    }

    [Fact]
    public async Task SafeRemoteImageResolver_BlockedPrivateTarget_DoesNotSendRequest()
    {
        var handler = new StubHttpHandler(_ =>
            throw new InvalidOperationException("Blocked requests must not reach the transport."));
        using var client = new HttpClient(handler);
        using var resolver = new SafeRemoteImageResolver(
            client,
            (_, _) => Task.FromResult(false));

        var result = await resolver.ResolveAsDataUrlAsync(
            "http://127.0.0.1/private.png",
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task SafeRemoteImageResolver_OversizedDeclaredResponse_IsRejected()
    {
        using var client = new HttpClient(new StubHttpHandler(_ =>
        {
            var content = new ByteArrayContent([]);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Headers.ContentLength = MarkdownFileImporter.MaxEmbeddedImageBytes + 1;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }));
        using var resolver = new SafeRemoteImageResolver(
            client,
            (_, _) => Task.FromResult(true));

        var result = await resolver.ResolveAsDataUrlAsync(
            "https://images.example/oversized.png",
            CancellationToken.None);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("http://127.0.0.1/image.png")]
    [InlineData("http://10.0.0.1/image.png")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://[::1]/image.png")]
    public async Task SafeRemoteImageResolver_PrivateLiteral_IsBlocked(string source)
    {
        var uri = new Uri(source);

        Assert.False(await SafeRemoteImageResolver.IsAllowedRemoteUriAsync(
            uri,
            CancellationToken.None));
    }

    private static byte[] CreatePng()
    {
        using var bitmap = new SKBitmap(1, 1);
        bitmap.SetPixel(0, 0, SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private sealed class StubRemoteImageResolver(string result) : IRemoteImageResolver
    {
        public List<string> Sources { get; } = [];

        public Task<string?> ResolveAsDataUrlAsync(
            string source,
            CancellationToken cancellationToken)
        {
            Sources.Add(source);
            return Task.FromResult<string?>(result);
        }
    }

    private sealed class StubHttpHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(responseFactory(request));
        }
    }
}
