using System.Net;
using System.Text;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using PXA.Core.Contracts;

namespace PXA.FileImporter;

/// <summary>
/// Converts a CommonMark/GFM Markdown (.md/.markdown) file into a <see cref="DesignExportDto"/>.
/// Inverse of <c>PXA.Infrastructure.Converters.MarkdownDocumentExporter</c>.
/// </summary>
public sealed class MarkdownFileImporter : IFileImporter
{
    public IReadOnlyList<string> SupportedExtensions { get; } = ["md", "markdown"];

    public Task<DesignExportDto> ImportAsync(Stream stream, string? name = null) =>
        Task.FromResult(Import(stream, name));

    private const double PageWidth    = 595;
    private const double PageHeight   = 842;
    private const double MarginX      = 48;
    private const double MarginY      = 48;
    private const double ContentWidth = PageWidth - MarginX * 2;

    public static DesignExportDto Import(Stream stream, string? name = null)
    {
        string text;
        using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            text = reader.ReadToEnd();

        if (string.IsNullOrWhiteSpace(text)) return Empty(name);

        // .DisableHtml() makes Markdig HTML-encode raw HTML in the source instead of passing it through
        // unescaped. richtext elements render via dangerouslySetInnerHTML on the frontend with no further
        // sanitization, so this is the only thing standing between an uploaded .md file and stored XSS.
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()
            .Build();

        var document = Markdown.Parse(text, pipeline);

        var ctx = new RenderContext();
        foreach (var block in document)
            RenderBlock(block, pipeline, ctx);

        return new DesignExportDto
        {
            Id    = Guid.NewGuid().ToString("N")[..12],
            Name  = name ?? ExtractDocumentTitle(document) ?? "Imported Markdown",
            Pages = ctx.FinalizePages(),
            SharedElements = [],
            PageSettings  = new PageSettingsDto
            {
                Width       = PageWidth,
                Height      = PageHeight,
                Orientation = "portrait",
                Margins     = new MarginsDto { Top = MarginY, Right = MarginX, Bottom = MarginY, Left = MarginX },
            },
        };
    }

    // Owns pagination: places each element at the current running Y, starting a new PageDto whenever an
    // element wouldn't fit within the remaining space on a non-empty page. Without this, any document
    // longer than one page's ~794pt of usable height (842 page height minus 48pt top/bottom margins)
    // would have most of its content positioned past the visible page and effectively missing from the
    // rendered PDF — the PDF engine (PdfPage.DrawParagraph) draws wherever it's told with no clipping.
    private sealed class RenderContext
    {
        public List<PageDto> Pages { get; } = [];
        public List<ElementDto> Elements { get; private set; } = [];
        public double Y = MarginY;

        private int _seq;
        private int _pageNum = 1;

        public string NextId(string prefix) => $"{prefix}-{_seq++}";

        // A single element taller than one full page's usable height still won't split itself across
        // pages — it lands whole on a fresh page and may still overflow that one page. Rare in practice
        // (a huge table or code block); deferred as a separate follow-up rather than solved here.
        public void Place(ElementDto element, double height, double gapAfter)
        {
            double maxY = PageHeight - MarginY;
            if (Elements.Count > 0 && Y + height > maxY) NewPage();

            element.Y = Math.Round(Y, 1);
            Elements.Add(element);
            Y += height + gapAfter;
        }

        private void NewPage()
        {
            Pages.Add(new PageDto { Id = $"page-{_pageNum++}", Elements = Elements });
            Elements = [];
            Y = MarginY;
        }

        public List<PageDto> FinalizePages()
        {
            Pages.Add(new PageDto { Id = $"page-{_pageNum}", Elements = Elements });
            return Pages;
        }
    }

    // Rough proportional-font average character width as a fraction of font size — enough to estimate
    // how many lines a run of text will wrap to at a given box width, so the next element doesn't get
    // placed on top of this one's overflow. The canvas masks under-estimates with CSS overflow:hidden,
    // but the real PDF renderer (PdfPage.DrawParagraph) has no clipping — it draws every wrapped line
    // starting at the given y, however many there are, so an under-sized box collides with whatever
    // comes next.
    private static int EstimateLineCount(string text, double fontSize, double width, double avgCharWidthFactor = 0.52)
    {
        if (string.IsNullOrEmpty(text)) return 1;
        double charsPerLine = Math.Max(1, width / Math.Max(1, fontSize * avgCharWidthFactor));
        return Math.Max(1, (int)Math.Ceiling(text.Length / charsPerLine));
    }

    private static double EstimateTextHeight(string text, double fontSize, double width, double avgCharWidthFactor = 0.52) =>
        EstimateLineCount(text, fontSize, width, avgCharWidthFactor) * (fontSize * 1.4) + 6;

    private static void RenderBlock(Block block, MarkdownPipeline pipeline, RenderContext ctx)
    {
        switch (block)
        {
            case HeadingBlock heading:
                RenderHeading(heading, ctx);
                break;

            case Table table:
                RenderTable(table, ctx);
                break;

            case ListBlock list:
                RenderList(list, ctx);
                break;

            case QuoteBlock quote:
                RenderQuote(quote, ctx);
                break;

            case ThematicBreakBlock:
                ctx.Place(new ElementDto
                {
                    Id = ctx.NextId("hr"), Type = "line",
                    X = MarginX, Width = ContentWidth, Height = 1,
                    Style = new Dictionary<string, object> { ["borderColor"] = "#cbd5e1" },
                }, height: 1, gapAfter: 15);
                break;

            case CodeBlock code:
                RenderCode(code, ctx);
                break;

            case ParagraphBlock para:
                RenderParagraph(para, pipeline, ctx);
                break;

            // Unhandled block types (raw HTML blocks, footnotes, etc.) are skipped.
        }
    }

    private static void RenderHeading(HeadingBlock heading, RenderContext ctx)
    {
        var text = heading.Inline is null ? "" : ExtractPlainText(heading.Inline);
        if (string.IsNullOrWhiteSpace(text)) return;

        // The exporter maps fontSize>=24 -> "##" and >=18 -> "###" (H1 is reserved for the document
        // title line); levels 2/3 round-trip exactly, level 1 gets the largest size, 4-6 degrade to bold.
        double fontSize = heading.Level switch
        {
            1 => 28d,
            2 => 24d,
            3 => 18d,
            _ => 14d,
        };
        double lineH = EstimateTextHeight(text, fontSize, ContentWidth);

        ctx.Place(new ElementDto
        {
            Id = ctx.NextId("h"), Type = "text",
            X = MarginX, Width = ContentWidth, Height = Math.Round(lineH, 1),
            Content = text,
            HeadingLevel = heading.Level,
            Style = new Dictionary<string, object>
            {
                ["fontSize"]   = fontSize,
                ["fontWeight"] = "bold",
                ["color"]      = "#0f172a",
            },
        }, lineH, gapAfter: 0);
    }

    private static void RenderParagraph(ParagraphBlock para, MarkdownPipeline pipeline, RenderContext ctx)
    {
        var inline = para.Inline;
        if (inline is null) { ctx.Y += 10; return; }

        if (TryGetStandaloneImage(inline, out var image) && image is not null)
        {
            const double h = 200;
            var alt = ExtractPlainText(image);
            ctx.Place(new ElementDto
            {
                Id = ctx.NextId("img"), Type = "image",
                X = MarginX, Width = ContentWidth, Height = h,
                Content = image.Url ?? "",
                Name = string.IsNullOrWhiteSpace(alt) ? "image" : alt,
                Style = new Dictionary<string, object> { ["fitMode"] = "contain" },
            }, h, gapAfter: 8);
            return;
        }

        const double fontSize = 11;

        if (HasInlineFormatting(inline))
        {
            var html = RenderInlineHtml(inline, pipeline);
            if (string.IsNullOrWhiteSpace(html)) { ctx.Y += 6; return; }

            // Height must be estimated from the plain-text length, not the HTML markup length — the
            // markup itself isn't what wraps, the rendered text is.
            var plainForHeight = ExtractPlainText(inline);
            double lineH = EstimateTextHeight(plainForHeight, fontSize, ContentWidth);

            ctx.Place(new ElementDto
            {
                Id = ctx.NextId("rt"), Type = "richtext",
                X = MarginX, Width = ContentWidth, Height = Math.Round(lineH, 1),
                HtmlContent = html,
            }, lineH, gapAfter: 0);
        }
        else
        {
            var text = ExtractPlainText(inline);
            if (string.IsNullOrWhiteSpace(text)) { ctx.Y += 6; return; }

            double lineH = EstimateTextHeight(text, fontSize, ContentWidth);

            ctx.Place(new ElementDto
            {
                Id = ctx.NextId("p"), Type = "text",
                X = MarginX, Width = ContentWidth, Height = Math.Round(lineH, 1),
                Content = text,
                Style = new Dictionary<string, object>
                {
                    ["fontSize"]   = fontSize,
                    ["fontFamily"] = "Arial",
                    ["color"]      = "#1e293b",
                    ["fontWeight"] = "normal",
                    ["textAlign"]  = "left",
                },
            }, lineH, gapAfter: 0);
        }
    }

    private static void RenderCode(CodeBlock code, RenderContext ctx)
    {
        var raw = code.Lines.ToString();
        if (string.IsNullOrWhiteSpace(raw)) return;

        // Monospace characters run wider per point than the proportional-font default, so estimate each
        // source line's own wrap count separately (a long unbroken line can still wrap within the box)
        // rather than assuming one rendered line per source line.
        const double codeFontSize = 9;
        var sourceLines = raw.Replace("\r\n", "\n").Split('\n');
        int wrappedLineCount = sourceLines.Sum(l => EstimateLineCount(l, codeFontSize, ContentWidth, avgCharWidthFactor: 0.62));
        double h = Math.Max(24, wrappedLineCount * 15 + 12);

        var encoded = WebUtility.HtmlEncode(raw).Replace("\n", "<br/>");
        ctx.Place(new ElementDto
        {
            Id = ctx.NextId("code"), Type = "richtext",
            X = MarginX, Width = ContentWidth, Height = Math.Round(h, 1),
            HtmlContent = $"<pre style=\"font-family:monospace;background:#f1f5f9;padding:8px;border-radius:4px;\"><code>{encoded}</code></pre>",
        }, h, gapAfter: 8);
    }

    private static void RenderTable(Table table, RenderContext ctx)
    {
        var rows = table.Cast<TableRow>().ToList();
        if (rows.Count == 0) return;

        int cols = rows.Max(r => r.Count);
        var cellData = rows
            .Select(r => Enumerable.Range(0, cols)
                .Select(c => c < r.Count && r[c] is TableCell cell ? ExtractCellText(cell) : "")
                .ToArray())
            .ToArray();

        // Markdig can report one extra trailing ColumnDefinition beyond the actual cell count for
        // certain pipe-table separator rows — clip to the real column count derived from row data.
        var alignments = table.ColumnDefinitions
            .Take(cols)
            .Select(cd => cd.Alignment switch
            {
                TableColumnAlign.Center => "center",
                TableColumnAlign.Right  => "right",
                _                       => "left",
            })
            .ToArray();

        bool hasHeader = rows[0].IsHeader;

        // Estimate each row's height from its longest-wrapping cell at that column's share of the
        // table width (assumes a conservative table-body font size, since the table style dict doesn't
        // carry an explicit fontSize) — a flat rowHeight would overflow for any cell with real content.
        // NOTE: the whole table is still placed as one atomic element — a table taller than one full
        // page won't split itself across pages (see RenderContext.Place's doc comment).
        const double tableFontSize = 10;
        double colWidth = ContentWidth / Math.Max(1, cols);
        double tableH = cellData.Sum(row => Math.Max(20, row.Max(cellText => EstimateTextHeight(cellText, tableFontSize, colWidth))));

        ctx.Place(new ElementDto
        {
            Id = ctx.NextId("tbl"), Type = "table",
            X = MarginX, Width = ContentWidth, Height = Math.Round(tableH, 1),
            CellData = cellData,
            ColumnAlignments = alignments.Length > 0 ? alignments : null,
            HeaderRow = hasHeader,
            Style = new Dictionary<string, object>
            {
                ["rows"]        = rows.Count,
                ["columns"]     = cols,
                ["borderWidth"] = 1,
                ["borderColor"] = "#cbd5e1",
                ["cellPadding"] = 6,
            },
        }, tableH, gapAfter: 8);
    }

    private static void RenderList(ListBlock list, RenderContext ctx)
    {
        var items = list.Cast<ListItemBlock>().ToList();
        if (items.Count == 0) return;

        bool isTaskList = items.Any(item =>
            item.Descendants<ParagraphBlock>().Any(p => p.Inline?.FirstChild is TaskList));

        if (isTaskList)
        {
            // Placed per-item (not as one block-height element) so a long task list can correctly
            // split across a page boundary mid-list instead of needing to fit as a whole.
            foreach (var item in items)
            {
                var para = item.Descendants<ParagraphBlock>().FirstOrDefault();
                var isChecked = para?.Inline?.FirstChild is TaskList { Checked: true };
                var label = para?.Inline is null ? "" : ExtractPlainText(para.Inline);

                double itemH = EstimateTextHeight(label, 11, ContentWidth - 24);
                ctx.Place(new ElementDto
                {
                    Id = ctx.NextId("chk"), Type = "checkbox",
                    X = MarginX, Width = ContentWidth, Height = Math.Round(itemH, 1),
                    FieldLabel = label,
                    CheckState = isChecked ? "checked" : "empty",
                }, itemH, gapAfter: 2);
            }
            return;
        }

        var options = items
            .Select(item =>
            {
                var para = item.Descendants<ParagraphBlock>().FirstOrDefault();
                return para?.Inline is null ? "" : ExtractPlainText(para.Inline);
            })
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .ToArray();

        if (options.Length == 0) return;

        double h = options.Sum(o => EstimateTextHeight(o, 11, ContentWidth - 20));

        ctx.Place(new ElementDto
        {
            Id = ctx.NextId("list"), Type = "optionlist",
            X = MarginX, Width = ContentWidth, Height = Math.Round(h, 1),
            Options = options,
            Ordered = list.IsOrdered,
            ListStyle = list.IsOrdered ? "decimal" : "disc",
        }, h, gapAfter: 8);
    }

    private static void RenderQuote(QuoteBlock quote, RenderContext ctx)
    {
        var paragraphs = quote.Descendants<ParagraphBlock>().ToList();
        if (paragraphs.Count == 0) return;

        string title = "Note";
        var bodyParts = new List<string>();
        var otherParagraphs = paragraphs;

        // A bold-only title line (e.g. `> **Title**`) followed by more quoted lines without a blank
        // separator parses as ONE paragraph — the title emphasis plus a line break plus the rest of the
        // text — rather than as separate ParagraphBlocks. This is also exactly how the exporter's own
        // `> **Title**` / `> body` output round-trips, so it must be handled here, not just the
        // blank-line-separated case.
        if (paragraphs[0].Inline?.FirstChild is EmphasisInline { DelimiterCount: 2 } strong)
        {
            title = ExtractPlainTextOfSingleNode(strong);

            var remainder = strong.NextSibling;
            if (remainder is LineBreakInline) remainder = remainder.NextSibling;
            if (remainder is not null)
            {
                var remainderText = ExtractPlainText(remainder);
                if (!string.IsNullOrWhiteSpace(remainderText)) bodyParts.Add(remainderText);
            }
            otherParagraphs = paragraphs.Skip(1).ToList();
        }

        bodyParts.AddRange(otherParagraphs
            .Select(p => p.Inline is null ? "" : ExtractPlainText(p.Inline))
            .Where(t => !string.IsNullOrWhiteSpace(t)));

        var body = string.Join("\n", bodyParts);
        double titleH = EstimateTextHeight(title, 12, ContentWidth - 16);
        double bodyH = bodyParts.Sum(p => EstimateTextHeight(p, 10, ContentWidth - 16));
        double h = Math.Max(40, titleH + bodyH + 12);

        ctx.Place(new ElementDto
        {
            Id = ctx.NextId("note"), Type = "note",
            X = MarginX, Width = ContentWidth, Height = Math.Round(h, 1),
            NoteTitle = title,
            NoteBody = body,
        }, h, gapAfter: 8);
    }

    // ── Inline helpers ───────────────────────────────────────────────────────

    private static bool TryGetStandaloneImage(ContainerInline container, out LinkInline? image)
    {
        if (container.FirstChild is LinkInline { IsImage: true } link && link.NextSibling is null)
        {
            image = link;
            return true;
        }
        image = null;
        return false;
    }

    private static bool HasInlineFormatting(ContainerInline? container)
    {
        for (var inline = container?.FirstChild; inline != null; inline = inline.NextSibling)
        {
            if (inline is EmphasisInline or LinkInline or CodeInline) return true;
            if (inline is ContainerInline nested && HasInlineFormatting(nested)) return true;
        }
        return false;
    }

    // Extracts text for `inline` AND every later sibling in its chain — use for "the rest of this
    // paragraph/list-item/cell starting here".
    private static string ExtractPlainText(Inline inline)
    {
        var sb = new StringBuilder();
        AppendPlainText(inline, sb);
        return sb.ToString().Trim();
    }

    // Extracts text for `inline`'s own subtree only, ignoring its NextSibling chain — use when a single
    // inline node (e.g. one EmphasisInline) has already been located and its siblings mean something else.
    private static string ExtractPlainTextOfSingleNode(Inline inline)
    {
        var sb = new StringBuilder();
        switch (inline)
        {
            case LiteralInline lit: sb.Append(lit.Content.ToString()); break;
            case CodeInline code: sb.Append(code.Content); break;
            case LineBreakInline: sb.Append(' '); break;
            case ContainerInline nested: AppendPlainText(nested.FirstChild, sb); break;
        }
        return sb.ToString().Trim();
    }

    private static void AppendPlainText(Inline? inline, StringBuilder sb)
    {
        for (var node = inline; node != null; node = node.NextSibling)
        {
            switch (node)
            {
                case LiteralInline lit: sb.Append(lit.Content.ToString()); break;
                case CodeInline code: sb.Append(code.Content); break;
                case LineBreakInline: sb.Append(' '); break;
                case ContainerInline nested: AppendPlainText(nested.FirstChild, sb); break;
            }
        }
    }

    private static string ExtractCellText(TableCell cell)
    {
        var para = cell.Descendants<ParagraphBlock>().FirstOrDefault();
        return para?.Inline is null ? "" : ExtractPlainText(para.Inline);
    }

    private static string RenderInlineHtml(ContainerInline inline, MarkdownPipeline pipeline)
    {
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        pipeline.Setup(renderer);
        renderer.WriteChildren(inline);
        return writer.ToString().Trim();
    }

    private static string? ExtractDocumentTitle(MarkdownDocument document)
    {
        var firstHeading = document.OfType<HeadingBlock>().FirstOrDefault(h => h.Level == 1);
        return firstHeading?.Inline is null ? null : ExtractPlainText(firstHeading.Inline);
    }

    private static DesignExportDto Empty(string? name) => new()
    {
        Id    = Guid.NewGuid().ToString("N")[..12],
        Name  = name ?? "Imported Markdown",
        Pages = [new PageDto { Id = "page-1", Elements = [] }],
        SharedElements = [],
    };
}
