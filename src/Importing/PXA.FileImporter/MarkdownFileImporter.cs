using System.Net;
using System.Text;
using Markdig;
using Markdig.Extensions.DefinitionLists;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Yaml;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using PXA.Core.Contracts;
using SkiaSharp;

namespace PXA.FileImporter;

/// <summary>
/// Converts a CommonMark/GFM Markdown (.md/.markdown) file into a <see cref="DesignExportDto"/>.
/// Inverse of <c>PXA.Infrastructure.Converters.MarkdownDocumentExporter</c>.
/// </summary>
public sealed class MarkdownFileImporter : IFileImporter
{
    public const int MaxInputCharacters = 2_000_000;
    public const int MaxGeneratedElements = 10_000;
    public const int MaxGeneratedPages = 500;
    public const int MaxEmbeddedImageBytes = 8 * 1024 * 1024;
    public const long MaxEmbeddedImagePixels = 40_000_000;

    private readonly IRemoteImageResolver? _remoteImageResolver;

    public MarkdownFileImporter(IRemoteImageResolver? remoteImageResolver = null)
    {
        _remoteImageResolver = remoteImageResolver;
    }

    public IReadOnlyList<string> SupportedExtensions { get; } = ["md", "markdown"];

    public Task<DesignExportDto> ImportAsync(Stream stream, string? name = null) =>
        ImportAsync(stream, name, CancellationToken.None);

    public async Task<DesignExportDto> ImportAsync(
        Stream stream,
        string? name,
        CancellationToken cancellationToken)
        => await ImportAsync(stream, name, assetBaseUri: null, cancellationToken);

    public async Task<DesignExportDto> ImportAsync(
        Stream stream,
        string? name,
        Uri? assetBaseUri,
        CancellationToken cancellationToken)
    {
        var text = await ReadBoundedTextAsync(stream, cancellationToken);
        var design = ImportText(text, name, cancellationToken);
        await ResolveRemoteImagesAsync(design, assetBaseUri, cancellationToken);
        return design;
    }

    private const double DefaultPageWidth = 595;
    private const double DefaultPageHeight = 842;
    private const double DefaultMargin = 48;

    public static DesignExportDto Import(Stream stream, string? name = null)
    {
        var text = ReadBoundedText(stream);
        return ImportText(text, name, CancellationToken.None);
    }

    private static DesignExportDto ImportText(
        string text,
        string? name,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(text)) return Empty(name);

        // .DisableHtml() makes Markdig HTML-encode raw HTML in the source instead of passing it through
        // unescaped. richtext elements render via dangerouslySetInnerHTML on the frontend with no further
        // sanitization, so this is the only thing standing between an uploaded .md file and stored XSS.
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseDefinitionLists()
            .UseFootnotes()
            .UseYamlFrontMatter()
            .DisableHtml()
            .Build();

        var document = Markdown.Parse(text, pipeline);
        var settings = ParseFrontMatter(document);

        var ctx = new RenderContext(settings);
        foreach (var block in document)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RenderBlock(block, pipeline, ctx);
        }

        var pages = ctx.FinalizePages();
        if (pages.Count > 1)
        {
            ctx.AddDiagnostic(
                "PXA-MD-100",
                "info",
                $"The Markdown document was split across {pages.Count} pages.");
        }

        return new DesignExportDto
        {
            Id    = Guid.NewGuid().ToString("N")[..12],
            Name  = settings.Title ?? name ?? ExtractDocumentTitle(document) ?? "Imported Markdown",
            Pages = pages,
            SharedElements = [],
            ImportDiagnostics = ctx.Diagnostics.Count > 0 ? ctx.Diagnostics : null,
            PageSettings  = new PageSettingsDto
            {
                Width       = settings.PageWidth,
                Height      = settings.PageHeight,
                Orientation = settings.Orientation,
                Margins     = new MarginsDto
                {
                    Top = settings.MarginTop,
                    Right = settings.MarginRight,
                    Bottom = settings.MarginBottom,
                    Left = settings.MarginLeft,
                },
                Metadata = new PdfMetadataDto
                {
                    Title = settings.Title,
                    Author = settings.Author,
                },
                SystemLanguage = settings.Language,
                ActiveLanguages = settings.Language is null ? null : [settings.Language],
                TargetLanguage = settings.Language,
            },
        };
    }

    private sealed class MarkdownImportSettings
    {
        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? Language { get; set; }
        public double PageWidth { get; set; } = DefaultPageWidth;
        public double PageHeight { get; set; } = DefaultPageHeight;
        public string Orientation { get; set; } = "portrait";
        public double MarginTop { get; set; } = DefaultMargin;
        public double MarginRight { get; set; } = DefaultMargin;
        public double MarginBottom { get; set; } = DefaultMargin;
        public double MarginLeft { get; set; } = DefaultMargin;
        public List<ImportDiagnosticDto> Diagnostics { get; } = [];
    }

    private static MarkdownImportSettings ParseFrontMatter(MarkdownDocument document)
    {
        var settings = new MarkdownImportSettings();
        var frontMatter = document.OfType<YamlFrontMatterBlock>().FirstOrDefault();
        if (frontMatter is null) return settings;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var margins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var section = "";

        foreach (var rawLine in frontMatter.Lines.ToString()
                     .Replace("\r\n", "\n", StringComparison.Ordinal)
                     .Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(rawLine) || rawLine.TrimStart().StartsWith('#'))
                continue;

            var colon = rawLine.IndexOf(':');
            if (colon <= 0) continue;

            var isIndented = char.IsWhiteSpace(rawLine[0]);
            var key = NormalizeFrontMatterKey(rawLine[..colon]);
            var value = UnquoteFrontMatterValue(rawLine[(colon + 1)..]);
            if (!isIndented)
            {
                section = value.Length == 0 ? key : "";
                values[key] = value;
            }
            else if (section == "margins")
            {
                margins[key] = value;
            }
        }

        settings.Title = GetBoundedFrontMatterValue(settings, values, "title", 512);
        settings.Author = GetBoundedFrontMatterValue(settings, values, "author", 256);

        if (GetNonEmpty(values, "language") is { } language)
        {
            if (IsSupportedLanguageTag(language))
                settings.Language = language;
            else
                AddFrontMatterDiagnostic(settings, "language", language);
        }

        if (GetNonEmpty(values, "pagesize") is { } pageSize)
        {
            if (TryGetPageSize(pageSize, out var width, out var height))
            {
                settings.PageWidth = width;
                settings.PageHeight = height;
            }
            else
            {
                AddFrontMatterDiagnostic(settings, "pageSize", pageSize);
            }
        }

        if (GetNonEmpty(values, "orientation") is { } orientation)
        {
            orientation = orientation.ToLowerInvariant();
            if (orientation is "portrait" or "landscape")
                settings.Orientation = orientation;
            else
                AddFrontMatterDiagnostic(settings, "orientation", orientation);
        }

        if (settings.Orientation == "landscape" && settings.PageHeight > settings.PageWidth)
            (settings.PageWidth, settings.PageHeight) = (settings.PageHeight, settings.PageWidth);
        else if (settings.Orientation == "portrait" && settings.PageWidth > settings.PageHeight)
            (settings.PageWidth, settings.PageHeight) = (settings.PageHeight, settings.PageWidth);

        if (GetNonEmpty(values, "margins") is { } uniformMargin)
        {
            if (TryParseDimension(uniformMargin, out var margin))
            {
                settings.MarginTop = margin;
                settings.MarginRight = margin;
                settings.MarginBottom = margin;
                settings.MarginLeft = margin;
            }
            else
            {
                AddFrontMatterDiagnostic(settings, "margins", uniformMargin);
            }
        }

        ApplyMargin(settings, margins, values, "top", value => settings.MarginTop = value);
        ApplyMargin(settings, margins, values, "right", value => settings.MarginRight = value);
        ApplyMargin(settings, margins, values, "bottom", value => settings.MarginBottom = value);
        ApplyMargin(settings, margins, values, "left", value => settings.MarginLeft = value);

        if (settings.PageWidth - settings.MarginLeft - settings.MarginRight < 120 ||
            settings.PageHeight - settings.MarginTop - settings.MarginBottom < 120)
        {
            settings.MarginTop = DefaultMargin;
            settings.MarginRight = DefaultMargin;
            settings.MarginBottom = DefaultMargin;
            settings.MarginLeft = DefaultMargin;
            AddFrontMatterDiagnostic(settings, "margins", "Margins leave insufficient page content space.");
        }

        return settings;
    }

    private static void ApplyMargin(
        MarkdownImportSettings settings,
        IReadOnlyDictionary<string, string> nestedMargins,
        IReadOnlyDictionary<string, string> rootValues,
        string side,
        Action<double> apply)
    {
        var rootKey = $"margin{side}";
        var value = nestedMargins.TryGetValue(side, out var nested)
            ? nested
            : rootValues.TryGetValue(rootKey, out var root) ? root : null;
        if (value is null) return;

        if (TryParseDimension(value, out var dimension))
            apply(dimension);
        else
            AddFrontMatterDiagnostic(settings, rootKey, value);
    }

    private static bool TryParseDimension(string value, out double points)
    {
        points = 0;
        var normalized = value.Trim().ToLowerInvariant();
        var multiplier = 1d;
        foreach (var (suffix, factor) in new[]
                 {
                     ("mm", 72d / 25.4),
                     ("cm", 72d / 2.54),
                     ("in", 72d),
                     ("pt", 1d),
                 })
        {
            if (!normalized.EndsWith(suffix, StringComparison.Ordinal)) continue;
            normalized = normalized[..^suffix.Length].Trim();
            multiplier = factor;
            break;
        }

        if (!double.TryParse(
                normalized,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var number))
            return false;

        points = number * multiplier;
        return double.IsFinite(points) && points is >= 0 and <= 288;
    }

    private static bool TryGetPageSize(string value, out double width, out double height)
    {
        (width, height) = value.Trim().ToLowerInvariant() switch
        {
            "a3" => (842, 1191),
            "a4" => (595, 842),
            "letter" => (612, 792),
            "legal" => (612, 1008),
            _ => (0, 0),
        };
        return width > 0;
    }

    private static string NormalizeFrontMatterKey(string value) =>
        new(value.Trim()
            .Where(character => character is not ('-' or '_') && !char.IsWhiteSpace(character))
            .Select(char.ToLowerInvariant)
            .ToArray());

    private static string UnquoteFrontMatterValue(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 &&
            ((trimmed[0] == '"' && trimmed[^1] == '"') ||
             (trimmed[0] == '\'' && trimmed[^1] == '\'')))
            return trimmed[1..^1];
        return trimmed;
    }

    private static string? GetNonEmpty(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static string? GetBoundedFrontMatterValue(
        MarkdownImportSettings settings,
        IReadOnlyDictionary<string, string> values,
        string key,
        int maxLength)
    {
        var value = GetNonEmpty(values, key);
        if (value is null || value.Length <= maxLength) return value;

        AddFrontMatterDiagnostic(settings, key, $"Value exceeds {maxLength} characters.");
        return null;
    }

    private static bool IsSupportedLanguageTag(string value) =>
        value.Length is >= 2 and <= 35 &&
        value.Split('-').All(part =>
            part.Length is >= 1 and <= 8 &&
            part.All(char.IsLetterOrDigit));

    private static void AddFrontMatterDiagnostic(
        MarkdownImportSettings settings,
        string key,
        string value)
    {
        settings.Diagnostics.Add(new ImportDiagnosticDto
        {
            Code = "PXA-MD-006",
            Severity = "warning",
            Message = $"The YAML front matter value for '{key}' was invalid and was ignored.",
            Source = value,
        });
    }

    // Owns pagination: places each element at the current running Y, starting a new PageDto whenever an
    // element wouldn't fit within the remaining space on a non-empty page. Without this, any document
    // longer than one page's 746pt of usable height (842 page height minus 48pt top/bottom margins)
    // would have most of its content positioned past the visible page and effectively missing from the
    // rendered PDF — the PDF engine (PdfPage.DrawParagraph) draws wherever it's told with no clipping.
    private sealed class RenderContext
    {
        private readonly MarkdownImportSettings _settings;

        public List<PageDto> Pages { get; } = [];
        public List<ElementDto> Elements { get; private set; } = [];
        public List<ImportDiagnosticDto> Diagnostics { get; } = [];
        public double Y;
        public bool HasElements => Elements.Count > 0;
        public double RemainingHeight => _settings.PageHeight - _settings.MarginBottom - Y;
        public double MarginLeft => _settings.MarginLeft;
        public double ContentWidth => _settings.PageWidth - _settings.MarginLeft - _settings.MarginRight;
        public double MaxContentHeight => _settings.PageHeight - _settings.MarginTop - _settings.MarginBottom;

        private int _seq;
        private int _pageNum = 1;

        public RenderContext(MarkdownImportSettings settings)
        {
            _settings = settings;
            Y = settings.MarginTop;
            Diagnostics.AddRange(settings.Diagnostics);
        }

        public string NextId(string prefix)
        {
            if (_seq >= MaxGeneratedElements)
                throw new InvalidDataException("Markdown generated too many design elements.");

            return $"{prefix}-{_seq++}";
        }

        public void AddDiagnostic(
            string code,
            string severity,
            string message,
            string? source = null)
        {
            Diagnostics.Add(new ImportDiagnosticDto
            {
                Code = code,
                Severity = severity,
                Message = message,
                Source = source,
            });
        }

        // Renderers split potentially large blocks before calling Place. This final guard also moves a
        // regular element to the next page when it does not fit in the current page's remaining space.
        public void Place(ElementDto element, double height, double gapAfter)
        {
            double maxY = _settings.PageHeight - _settings.MarginBottom;
            if (Elements.Count > 0 && Y + height > maxY) NewPage();

            element.Y = Math.Round(Y, 1);
            Elements.Add(element);
            Y += height + gapAfter;
        }

        private void NewPage()
        {
            if (Pages.Count >= MaxGeneratedPages - 1)
                throw new InvalidDataException("Markdown generated too many pages.");

            Pages.Add(new PageDto { Id = $"page-{_pageNum++}", Elements = Elements });
            Elements = [];
            Y = _settings.MarginTop;
        }

        public void StartNewPage()
        {
            if (Elements.Count > 0) NewPage();
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

            case DefinitionList definitions:
                RenderDefinitionList(definitions, ctx);
                break;

            case FootnoteGroup footnotes:
                RenderFootnotes(footnotes, ctx);
                break;

            case YamlFrontMatterBlock:
            case LinkReferenceDefinitionGroup:
                break;

            case QuoteBlock quote:
                RenderQuote(quote, ctx);
                break;

            case ThematicBreakBlock:
                ctx.Place(new ElementDto
                {
                    Id = ctx.NextId("hr"), Type = "line",
                    X = ctx.MarginLeft, Width = ctx.ContentWidth, Height = 1,
                    Style = new Dictionary<string, object> { ["borderColor"] = "#cbd5e1" },
                }, height: 1, gapAfter: 15);
                break;

            case CodeBlock code:
                RenderCode(code, ctx);
                break;

            case ParagraphBlock para:
                RenderParagraph(para, pipeline, ctx);
                break;

            default:
                ctx.AddDiagnostic(
                    "PXA-MD-004",
                    "warning",
                    $"Unsupported Markdown block '{block.GetType().Name}' was skipped.");
                break;
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
        double lineH = EstimateTextHeight(text, fontSize, ctx.ContentWidth);

        ctx.Place(new ElementDto
        {
            Id = ctx.NextId("h"), Type = "text",
            X = ctx.MarginLeft, Width = ctx.ContentWidth, Height = Math.Round(lineH, 1),
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
            var originalSource = image.Url ?? "";
            var imageSource = NormalizeImageSource(originalSource);
            if (originalSource.StartsWith("data:", StringComparison.OrdinalIgnoreCase) &&
                imageSource.Length == 0)
            {
                ctx.AddDiagnostic(
                    "PXA-MD-002",
                    "warning",
                    "An embedded image was rejected because it was invalid or unsupported.",
                    originalSource[..Math.Min(originalSource.Length, 80)]);
            }
            ctx.Place(new ElementDto
            {
                Id = ctx.NextId("img"), Type = "image",
                X = ctx.MarginLeft, Width = ctx.ContentWidth, Height = h,
                Content = imageSource,
                Name = string.IsNullOrWhiteSpace(alt) ? "image" : alt,
                Style = new Dictionary<string, object> { ["fitMode"] = "contain" },
            }, h, gapAfter: 8);
            return;
        }

        var removedLinks = SanitizeLinkTargets(inline);
        if (removedLinks > 0)
        {
            ctx.AddDiagnostic(
                "PXA-MD-001",
                "warning",
                $"{removedLinks} unsafe Markdown link target(s) were removed.");
        }
        if (inline.Descendants<LinkInline>().Any(link => link.IsImage))
        {
            ctx.AddDiagnostic(
                "PXA-MD-003",
                "warning",
                "Inline images inside text are not supported and were omitted.");
        }

        const double fontSize = 11;

        if (HasInlineFormatting(inline))
        {
            var html = RenderInlineHtml(inline, pipeline);
            if (string.IsNullOrWhiteSpace(html)) { ctx.Y += 6; return; }

            // Height must be estimated from the plain-text length, not the HTML markup length — the
            // markup itself isn't what wraps, the rendered text is.
            var plainForHeight = ExtractPlainText(inline);
            double lineH = EstimateTextHeight(plainForHeight, fontSize, ctx.ContentWidth);

            if (lineH > ctx.MaxContentHeight)
            {
                ctx.AddDiagnostic(
                    "PXA-MD-005",
                    "warning",
                    "Inline formatting was simplified while splitting an oversized paragraph across pages.");
                RenderPaginatedParagraphText(plainForHeight, ctx);
                return;
            }

            ctx.Place(new ElementDto
            {
                Id = ctx.NextId("rt"), Type = "richtext",
                X = ctx.MarginLeft, Width = ctx.ContentWidth, Height = Math.Round(lineH, 1),
                HtmlContent = html,
            }, lineH, gapAfter: 0);
        }
        else
        {
            var text = ExtractPlainText(inline);
            if (string.IsNullOrWhiteSpace(text)) { ctx.Y += 6; return; }

            double lineH = EstimateTextHeight(text, fontSize, ctx.ContentWidth);

            if (lineH > ctx.MaxContentHeight)
            {
                RenderPaginatedParagraphText(text, ctx);
                return;
            }

            ctx.Place(new ElementDto
            {
                Id = ctx.NextId("p"), Type = "text",
                X = ctx.MarginLeft, Width = ctx.ContentWidth, Height = Math.Round(lineH, 1),
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

    private static void RenderPaginatedParagraphText(string text, RenderContext ctx)
    {
        const double fontSize = 11;
        var remaining = text;

        while (!string.IsNullOrWhiteSpace(remaining))
        {
            var minimumHeight = fontSize * 1.4 + 6;
            if (ctx.HasElements && ctx.RemainingHeight < minimumHeight)
            {
                ctx.StartNewPage();
                continue;
            }

            var chunk = TakeTextChunk(
                remaining,
                fontSize,
                ctx.ContentWidth,
                ctx.RemainingHeight);
            if (string.IsNullOrEmpty(chunk.Text))
                throw new InvalidDataException("Markdown paragraph could not be paginated.");

            var height = EstimateTextHeight(chunk.Text, fontSize, ctx.ContentWidth);
            ctx.Place(new ElementDto
            {
                Id = ctx.NextId("p"), Type = "text",
                X = ctx.MarginLeft, Width = ctx.ContentWidth, Height = Math.Round(height, 1),
                Content = chunk.Text,
                Style = new Dictionary<string, object>
                {
                    ["fontSize"] = fontSize,
                    ["fontFamily"] = "Arial",
                    ["color"] = "#1e293b",
                    ["fontWeight"] = "normal",
                    ["textAlign"] = "left",
                },
            }, height, gapAfter: chunk.Remainder.Length == 0 ? 0 : 2);

            remaining = chunk.Remainder;
        }
    }

    private static void RenderCode(CodeBlock code, RenderContext ctx)
    {
        var raw = code.Lines.ToString();
        if (string.IsNullOrWhiteSpace(raw)) return;
        var language = code is FencedCodeBlock fenced
            ? (fenced.Info?.ToString() ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            : null;

        const double codeFontSize = 9;
        const double renderedLineHeight = 15;
        const double verticalPadding = 12;
        var charsPerLine = Math.Max(1, (int)Math.Floor(ctx.ContentWidth / (codeFontSize * 0.62)));
        var visualLines = raw
            .Replace("\r\n", "\n")
            .Split('\n')
            .SelectMany(line => SplitFixedWidth(line, charsPerLine))
            .ToList();
        var lineIndex = 0;

        while (lineIndex < visualLines.Count)
        {
            var availableLines = (int)Math.Floor(
                (ctx.RemainingHeight - verticalPadding) / renderedLineHeight);
            if (availableLines < 1 && ctx.HasElements)
            {
                ctx.StartNewPage();
                continue;
            }

            availableLines = Math.Max(1, availableLines);
            var take = Math.Min(availableLines, visualLines.Count - lineIndex);
            var chunk = visualLines.GetRange(lineIndex, take);
            var height = Math.Max(24, take * renderedLineHeight + verticalPadding);
            var encoded = string.Join("<br/>", chunk.Select(WebUtility.HtmlEncode));

            ctx.Place(new ElementDto
            {
                Id = ctx.NextId("code"), Type = "richtext",
                X = ctx.MarginLeft, Width = ctx.ContentWidth, Height = Math.Round(height, 1),
                HtmlContent = $"<pre style=\"font-family:monospace;background-color:#f1f5f9;\"><code>{encoded}</code></pre>",
                Style = string.IsNullOrWhiteSpace(language)
                    ? null
                    : new Dictionary<string, object> { ["codeLanguage"] = language },
            }, height, gapAfter: lineIndex + take == visualLines.Count ? 8 : 0);
            lineIndex += take;
        }
    }

    private static void RenderTable(Table table, RenderContext ctx)
    {
        var rows = table.Cast<TableRow>().ToList();
        if (rows.Count == 0) return;

        int cols = rows.Max(r => r.Count);
        var cellData = rows
            .Select(r => Enumerable.Range(0, cols)
                .Select(c => c < r.Count && r[c] is TableCell cell ? ExtractInlineCodeAwareText(cell) : "")
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
        // Tables are split into page-sized row chunks below; continuation chunks repeat the header.
        const double tableFontSize = 10;
        double colWidth = ctx.ContentWidth / Math.Max(1, cols);
        var rowHeights = cellData
            .Select(row => Math.Max(20, row.Max(cellText => EstimateTextHeight(cellText, tableFontSize, colWidth))))
            .ToArray();
        var headerRows = hasHeader ? 1 : 0;
        var expandedRows = ExpandOversizedTableRows(
            cellData,
            rowHeights,
            headerRows,
            tableFontSize,
            colWidth,
            ctx.MaxContentHeight);
        cellData = expandedRows.Rows;
        rowHeights = expandedRows.Heights;
        var nextBodyRow = headerRows;

        if (nextBodyRow == cellData.Length)
        {
            PlaceTableChunk(ctx, cellData, rowHeights.Sum(), cols, alignments, hasHeader, gapAfter: 8);
            return;
        }

        while (nextBodyRow < cellData.Length)
        {
            var headerHeight = hasHeader ? rowHeights[0] : 0;
            if (ctx.HasElements && headerHeight + rowHeights[nextBodyRow] > ctx.RemainingHeight)
                ctx.StartNewPage();

            var chunkRows = new List<string[]>();
            var chunkHeight = 0d;
            if (hasHeader)
            {
                chunkRows.Add(cellData[0]);
                chunkHeight += rowHeights[0];
            }

            var firstBodyRow = nextBodyRow;
            while (nextBodyRow < cellData.Length)
            {
                var nextHeight = rowHeights[nextBodyRow];
                if (nextBodyRow > firstBodyRow && chunkHeight + nextHeight > ctx.RemainingHeight)
                    break;

                chunkRows.Add(cellData[nextBodyRow]);
                chunkHeight += nextHeight;
                nextBodyRow++;
            }

            PlaceTableChunk(
                ctx,
                chunkRows.ToArray(),
                chunkHeight,
                cols,
                alignments,
                hasHeader,
                gapAfter: nextBodyRow == cellData.Length ? 8 : 0);
        }
    }

    private static (string[][] Rows, double[] Heights) ExpandOversizedTableRows(
        string[][] rows,
        double[] rowHeights,
        int headerRows,
        double fontSize,
        double columnWidth,
        double maxContentHeight)
    {
        var expandedRows = new List<string[]>();
        var expandedHeights = new List<double>();
        var headerHeight = headerRows == 1 ? Math.Min(rowHeights[0], maxContentHeight) : 0;
        var maxBodyHeight = Math.Max(20, maxContentHeight - headerHeight);

        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            if (rowIndex < headerRows || rowHeights[rowIndex] <= maxBodyHeight)
            {
                expandedRows.Add(rows[rowIndex]);
                expandedHeights.Add(Math.Min(rowHeights[rowIndex], maxContentHeight));
                continue;
            }

            var cellChunks = rows[rowIndex]
                .Select(cell => SplitTextForFixedHeight(cell, fontSize, columnWidth, maxBodyHeight))
                .ToArray();
            var fragmentCount = cellChunks.Max(chunks => chunks.Count);

            for (var fragmentIndex = 0; fragmentIndex < fragmentCount; fragmentIndex++)
            {
                var fragment = cellChunks
                    .Select(chunks => fragmentIndex < chunks.Count ? chunks[fragmentIndex] : "")
                    .ToArray();
                expandedRows.Add(fragment);
                expandedHeights.Add(Math.Max(
                    20,
                    fragment.Max(cell => EstimateTextHeight(cell, fontSize, columnWidth))));
            }
        }

        return (expandedRows.ToArray(), expandedHeights.ToArray());
    }

    private static void PlaceTableChunk(
        RenderContext ctx,
        string[][] rows,
        double height,
        int columns,
        string[] alignments,
        bool hasHeader,
        double gapAfter)
    {
        ctx.Place(new ElementDto
        {
            Id = ctx.NextId("tbl"), Type = "table",
            X = ctx.MarginLeft, Width = ctx.ContentWidth, Height = Math.Round(height, 1),
            CellData = rows,
            ColumnAlignments = alignments.Length > 0 ? alignments : null,
            HeaderRow = hasHeader,
            Style = new Dictionary<string, object>
            {
                ["rows"] = rows.Length,
                ["columns"] = columns,
                ["borderWidth"] = 1,
                ["borderColor"] = "#cbd5e1",
                ["cellPadding"] = 6,
            },
        }, height, gapAfter);
    }

    private static void RenderList(ListBlock list, RenderContext ctx)
    {
        var items = list.Cast<ListItemBlock>().ToList();
        if (items.Count == 0) return;

        if (items.Any(item => item.OfType<ListBlock>().Any()))
        {
            RenderNestedList(list, ctx, depth: 0);
            return;
        }

        bool isTaskList = items.Any(item =>
            item.OfType<ParagraphBlock>().Any(p => p.Inline?.FirstChild is TaskList));

        if (isTaskList)
        {
            // Placed per-item (not as one block-height element) so a long task list can correctly
            // split across a page boundary mid-list instead of needing to fit as a whole.
            foreach (var item in items)
            {
                var para = item.OfType<ParagraphBlock>().FirstOrDefault();
                var isChecked = para?.Inline?.FirstChild is TaskList { Checked: true };
                var label = para?.Inline is null ? "" : ExtractInlineCodeAwareText(para.Inline);

                double itemH = EstimateTextHeight(label, 11, ctx.ContentWidth - 24);
                ctx.Place(new ElementDto
                {
                    Id = ctx.NextId("chk"), Type = "checkbox",
                    X = ctx.MarginLeft, Width = ctx.ContentWidth, Height = Math.Round(itemH, 1),
                    FieldLabel = label,
                    CheckState = isChecked ? "checked" : "empty",
                }, itemH, gapAfter: 2);
            }
            return;
        }

        var options = items
            .Select(item =>
            {
                var para = item.OfType<ParagraphBlock>().FirstOrDefault();
                return para?.Inline is null ? "" : ExtractInlineCodeAwareText(para.Inline);
            })
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .ToArray();

        if (options.Length == 0) return;

        var itemHeights = options
            .Select(option => EstimateTextHeight(option, 11, ctx.ContentWidth - 20))
            .ToArray();
        var orderedStart = list.IsOrdered &&
            int.TryParse(list.OrderedStart?.ToString(), out var parsedStart)
                ? parsedStart
                : 1;
        var itemIndex = 0;

        while (itemIndex < options.Length)
        {
            if (ctx.HasElements && itemHeights[itemIndex] > ctx.RemainingHeight)
                ctx.StartNewPage();

            var chunkStart = itemIndex;
            var chunkHeight = 0d;
            while (itemIndex < options.Length)
            {
                var nextHeight = itemHeights[itemIndex];
                if (itemIndex > chunkStart && chunkHeight + nextHeight > ctx.RemainingHeight)
                    break;

                chunkHeight += nextHeight;
                itemIndex++;
            }

            var chunkOptions = options[chunkStart..itemIndex];
            ctx.Place(new ElementDto
            {
                Id = ctx.NextId("list"), Type = "optionlist",
                X = ctx.MarginLeft, Width = ctx.ContentWidth, Height = Math.Round(chunkHeight, 1),
                Options = chunkOptions,
                Ordered = list.IsOrdered,
                ListStyle = list.IsOrdered ? "decimal" : "disc",
                StartNumber = list.IsOrdered ? orderedStart + chunkStart : null,
            }, chunkHeight, gapAfter: itemIndex == options.Length ? 8 : 0);
        }
    }

    private static void RenderNestedList(ListBlock list, RenderContext ctx, int depth)
    {
        const double indent = 20;
        var x = ctx.MarginLeft + Math.Min(depth, 12) * indent;
        var width = Math.Max(120, ctx.ContentWidth - (x - ctx.MarginLeft));
        var orderedStart = list.IsOrdered &&
            int.TryParse(list.OrderedStart?.ToString(), out var parsedStart)
                ? parsedStart
                : 1;
        var itemNumber = 0;

        foreach (var item in list.Cast<ListItemBlock>())
        {
            var para = item.OfType<ParagraphBlock>().FirstOrDefault();
            if (para?.Inline is not null)
            {
                var option = ExtractInlineCodeAwareText(para.Inline);
                if (!string.IsNullOrWhiteSpace(option))
                {
                    var height = EstimateTextHeight(option, 11, width - 20);
                    ctx.Place(new ElementDto
                    {
                        Id = ctx.NextId("list"), Type = "optionlist",
                        X = x, Width = width, Height = Math.Round(height, 1),
                        Options = [option],
                        Ordered = list.IsOrdered,
                        ListStyle = list.IsOrdered ? "decimal" : "disc",
                        StartNumber = list.IsOrdered ? orderedStart + itemNumber : null,
                        Style = new Dictionary<string, object>
                        {
                            ["markdownListDepth"] = depth,
                        },
                    }, height, gapAfter: 2);
                }
            }

            itemNumber++;
            foreach (var childList in item.OfType<ListBlock>())
                RenderNestedList(childList, ctx, depth + 1);
        }
    }

    private static void RenderDefinitionList(DefinitionList list, RenderContext ctx)
    {
        foreach (var item in list.OfType<DefinitionItem>())
        {
            var terms = item.OfType<DefinitionTerm>()
                .Select(term => term.Inline is null ? "" : ExtractInlineCodeAwareText(term.Inline))
                .Where(term => !string.IsNullOrWhiteSpace(term))
                .ToArray();
            var definitions = item.OfType<ParagraphBlock>()
                .Select(definition => definition.Inline is null ? "" : ExtractInlineCodeAwareText(definition.Inline))
                .Where(definition => !string.IsNullOrWhiteSpace(definition))
                .ToArray();
            if (terms.Length == 0 && definitions.Length == 0) continue;

            var title = terms.Length == 0 ? "Definition" : string.Join(", ", terms);
            var body = string.Join("\n", definitions);
            var height = Math.Max(
                40,
                EstimateTextHeight(title, 12, ctx.ContentWidth - 16) +
                EstimateTextHeight(body, 10, ctx.ContentWidth - 16) + 12);
            ctx.Place(new ElementDto
            {
                Id = ctx.NextId("definition"), Type = "note",
                X = ctx.MarginLeft, Width = ctx.ContentWidth, Height = Math.Round(height, 1),
                NoteTitle = title,
                NoteBody = body,
                Style = new Dictionary<string, object> { ["markdownDefinition"] = true },
            }, height, gapAfter: 8);
        }
    }

    private static void RenderFootnotes(FootnoteGroup group, RenderContext ctx)
    {
        var bottomOffset = 0d;
        foreach (var footnote in group.OfType<Footnote>())
        {
            var text = string.Join(
                "\n",
                footnote.Descendants<ParagraphBlock>()
                    .Select(paragraph => paragraph.Inline is null
                        ? ""
                        : ExtractInlineCodeAwareText(paragraph.Inline))
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            if (string.IsNullOrWhiteSpace(text)) continue;

            var reference = string.IsNullOrWhiteSpace(footnote.Label)
                ? (footnote.Order + 1).ToString()
                : footnote.Label.TrimStart('^');
            var height = Math.Max(24, EstimateTextHeight($"{reference} {text}", 9, ctx.ContentWidth));
            ctx.Place(new ElementDto
            {
                Id = ctx.NextId("footnote"), Type = "footnote",
                X = ctx.MarginLeft, Width = ctx.ContentWidth, Height = Math.Round(height, 1),
                FootnoteRef = reference,
                FootnoteText = text,
                Style = new Dictionary<string, object>
                {
                    ["fontSize"] = 9,
                    ["markdownFootnote"] = true,
                    ["footnoteBottomOffset"] = bottomOffset,
                },
            }, height, gapAfter: 2);
            bottomOffset += height;
        }
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
        double titleH = EstimateTextHeight(title, 12, ctx.ContentWidth - 16);
        double bodyH = bodyParts.Sum(p => EstimateTextHeight(p, 10, ctx.ContentWidth - 16));
        double h = Math.Max(40, titleH + bodyH + 12);

        if (h > ctx.MaxContentHeight)
        {
            RenderPaginatedQuote(title, body, ctx);
            return;
        }

        ctx.Place(new ElementDto
        {
            Id = ctx.NextId("note"), Type = "note",
            X = ctx.MarginLeft, Width = ctx.ContentWidth, Height = Math.Round(h, 1),
            NoteTitle = title,
            NoteBody = body,
        }, h, gapAfter: 8);
    }

    private static void RenderPaginatedQuote(string title, string body, RenderContext ctx)
    {
        const double titleFontSize = 12;
        const double bodyFontSize = 10;
        var innerWidth = ctx.ContentWidth - 16;
        var titleHeight = EstimateTextHeight(title, titleFontSize, innerWidth);
        var remaining = body;

        while (!string.IsNullOrWhiteSpace(remaining))
        {
            var fixedHeight = titleHeight + 12;
            var minimumBodyHeight = bodyFontSize * 1.4 + 6;
            if (ctx.HasElements && ctx.RemainingHeight < fixedHeight + minimumBodyHeight)
            {
                ctx.StartNewPage();
                continue;
            }

            var chunk = TakeTextChunk(
                remaining,
                bodyFontSize,
                innerWidth,
                ctx.RemainingHeight - fixedHeight);
            if (string.IsNullOrEmpty(chunk.Text))
                throw new InvalidDataException("Markdown blockquote could not be paginated.");

            var height = Math.Max(
                40,
                fixedHeight + EstimateTextHeight(chunk.Text, bodyFontSize, innerWidth));
            ctx.Place(new ElementDto
            {
                Id = ctx.NextId("note"), Type = "note",
                X = ctx.MarginLeft, Width = ctx.ContentWidth, Height = Math.Round(height, 1),
                NoteTitle = title,
                NoteBody = chunk.Text,
            }, height, gapAfter: chunk.Remainder.Length == 0 ? 8 : 2);

            remaining = chunk.Remainder;
        }
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

    private static string NormalizeImageSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return "";
        if (!source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return source;

        var commaIndex = source.IndexOf(',');
        if (commaIndex <= 5) return "";

        var metadata = source[5..commaIndex];
        var parts = metadata.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !parts[1].Equals("base64", StringComparison.OrdinalIgnoreCase))
            return "";

        var mimeType = parts[0].ToLowerInvariant();
        if (mimeType is not ("image/png" or "image/jpeg"))
            return "";

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(source[(commaIndex + 1)..]);
        }
        catch (FormatException)
        {
            return "";
        }

        if (bytes.Length == 0 || bytes.Length > MaxEmbeddedImageBytes)
            return "";

        using var bitmap = SKBitmap.Decode(bytes);
        if (bitmap is null ||
            bitmap.Width <= 0 ||
            bitmap.Height <= 0 ||
            (long)bitmap.Width * bitmap.Height > MaxEmbeddedImagePixels)
            return "";

        return $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
    }

    private static bool HasInlineFormatting(ContainerInline? container)
    {
        for (var inline = container?.FirstChild; inline != null; inline = inline.NextSibling)
        {
            if (inline is EmphasisInline or LinkInline or CodeInline or FootnoteLink) return true;
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
            case FootnoteLink footnote when !footnote.IsBackLink:
                sb.Append((footnote.Footnote?.Order ?? 0) + 1);
                break;
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
                case FootnoteLink footnote when !footnote.IsBackLink:
                    sb.Append((footnote.Footnote?.Order ?? 0) + 1);
                    break;
                case LineBreakInline: sb.Append(' '); break;
                case ContainerInline nested: AppendPlainText(nested.FirstChild, sb); break;
            }
        }
    }

    private async Task ResolveRemoteImagesAsync(
        DesignExportDto design,
        Uri? assetBaseUri,
        CancellationToken cancellationToken)
    {
        if (_remoteImageResolver is null)
            return;

        foreach (var image in design.Pages
                     .SelectMany(page => page.Elements)
                     .Where(element => element.Type == "image"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = image.Content;
            if (string.IsNullOrWhiteSpace(source) ||
                source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                continue;

            Uri? uri;
            if (!Uri.TryCreate(source, UriKind.Absolute, out uri))
            {
                if (assetBaseUri is null || !Uri.TryCreate(assetBaseUri, source, out uri))
                {
                    image.Content = "";
                    AddImportDiagnostic(
                        design,
                        "PXA-MD-201",
                        "warning",
                        "A relative image could not be resolved because no asset base URI was provided.",
                        source);
                    continue;
                }
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                image.Content = "";
                AddImportDiagnostic(
                    design,
                    "PXA-MD-202",
                    "warning",
                    "An image source was rejected because only HTTP(S) assets are supported.",
                    source);
                continue;
            }

            image.Content = await _remoteImageResolver.ResolveAsDataUrlAsync(
                uri.AbsoluteUri,
                cancellationToken) ?? "";
            if (image.Content.Length == 0)
            {
                AddImportDiagnostic(
                    design,
                    "PXA-MD-203",
                    "warning",
                    "A remote image could not be downloaded or failed image validation.",
                    uri.AbsoluteUri);
            }
        }
    }

    private static void AddImportDiagnostic(
        DesignExportDto design,
        string code,
        string severity,
        string message,
        string? source = null)
    {
        design.ImportDiagnostics ??= [];
        design.ImportDiagnostics.Add(new ImportDiagnosticDto
        {
            Code = code,
            Severity = severity,
            Message = message,
            Source = source,
        });
    }

    private static string ExtractInlineCodeAwareText(TableCell cell)
    {
        var para = cell.Descendants<ParagraphBlock>().FirstOrDefault();
        return para?.Inline is null ? "" : ExtractInlineCodeAwareText(para.Inline);
    }

    private static string ExtractInlineCodeAwareText(Inline inline)
    {
        var sb = new StringBuilder();
        AppendInlineCodeAwareText(inline, sb);
        return sb.ToString().Trim();
    }

    private static void AppendInlineCodeAwareText(Inline? inline, StringBuilder sb)
    {
        for (var node = inline; node != null; node = node.NextSibling)
        {
            switch (node)
            {
                case LiteralInline literal:
                    sb.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    sb.Append('`').Append(code.Content).Append('`');
                    break;
                case LineBreakInline:
                    sb.Append(' ');
                    break;
                case ContainerInline nested:
                    AppendInlineCodeAwareText(nested.FirstChild, sb);
                    break;
            }
        }
    }

    private static string RenderInlineHtml(ContainerInline inline, MarkdownPipeline pipeline)
    {
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        pipeline.Setup(renderer);
        renderer.WriteChildren(inline);
        return writer.ToString().Trim();
    }

    private static IEnumerable<string> SplitFixedWidth(string line, int maxCharacters)
    {
        if (line.Length == 0)
        {
            yield return "";
            yield break;
        }

        for (var offset = 0; offset < line.Length; offset += maxCharacters)
            yield return line.Substring(offset, Math.Min(maxCharacters, line.Length - offset));
    }

    private static List<string> SplitTextForFixedHeight(
        string text,
        double fontSize,
        double width,
        double maxHeight)
    {
        if (string.IsNullOrEmpty(text)) return [""];

        var chunks = new List<string>();
        var remaining = text;
        while (remaining.Length > 0)
        {
            var chunk = TakeTextChunk(remaining, fontSize, width, maxHeight);
            if (chunk.Text.Length == 0)
                throw new InvalidDataException("Markdown text could not be split to fit a page.");

            chunks.Add(chunk.Text);
            remaining = chunk.Remainder;
        }

        return chunks;
    }

    private static (string Text, string Remainder) TakeTextChunk(
        string text,
        double fontSize,
        double width,
        double maxHeight)
    {
        if (string.IsNullOrEmpty(text) || maxHeight <= 0) return ("", text);

        var lineHeight = fontSize * 1.4;
        var maxLines = Math.Max(1, (int)Math.Floor((maxHeight - 6) / lineHeight));
        var charsPerLine = Math.Max(1, (int)Math.Floor(width / Math.Max(1, fontSize * 0.52)));
        var maxCharacters = Math.Max(1, maxLines * charsPerLine);
        if (text.Length <= maxCharacters) return (text, "");

        var splitAt = maxCharacters;
        while (splitAt > 0 && !char.IsWhiteSpace(text[splitAt - 1]))
            splitAt--;
        if (splitAt == 0) splitAt = maxCharacters;

        var chunk = text[..splitAt].TrimEnd();
        var remainderStart = splitAt;
        while (remainderStart < text.Length && char.IsWhiteSpace(text[remainderStart]))
            remainderStart++;

        return (chunk, text[remainderStart..]);
    }

    private static string ReadBoundedText(Stream stream)
    {
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 8192,
            leaveOpen: false);
        var buffer = new char[8192];
        var text = new StringBuilder();

        while (true)
        {
            var read = reader.ReadBlock(buffer, 0, buffer.Length);
            if (read == 0) break;
            if (text.Length + read > MaxInputCharacters)
                throw new InvalidDataException("Markdown exceeds the supported text-size limit.");

            text.Append(buffer, 0, read);
        }

        return text.ToString();
    }

    private static async Task<string> ReadBoundedTextAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 8192,
            leaveOpen: false);
        var buffer = new char[8192];
        var text = new StringBuilder();

        while (true)
        {
            var read = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0) break;
            if (text.Length + read > MaxInputCharacters)
                throw new InvalidDataException("Markdown exceeds the supported text-size limit.");

            text.Append(buffer, 0, read);
        }

        return text.ToString();
    }

    private static int SanitizeLinkTargets(ContainerInline container)
    {
        var removed = 0;
        for (var node = container.FirstChild; node is not null; node = node.NextSibling)
        {
            if (node is LinkInline link && !link.IsImage && !IsSafeLinkTarget(link.Url))
            {
                link.Url = "";
                removed++;
            }

            if (node is ContainerInline nested)
                removed += SanitizeLinkTargets(nested);
        }

        return removed;
    }

    private static bool IsSafeLinkTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return true;

        var normalized = WebUtility.HtmlDecode(target).Trim();
        for (var i = 0; i < 3; i++)
        {
            try
            {
                var decoded = Uri.UnescapeDataString(normalized);
                if (decoded == normalized) break;
                normalized = decoded;
            }
            catch (UriFormatException)
            {
                return false;
            }
        }

        if (normalized.StartsWith('#') ||
            normalized.StartsWith('/') ||
            normalized.StartsWith("./", StringComparison.Ordinal) ||
            normalized.StartsWith("../", StringComparison.Ordinal) ||
            normalized.StartsWith("?", StringComparison.Ordinal))
            return !normalized.StartsWith("//", StringComparison.Ordinal);

        if (!Uri.TryCreate(normalized, UriKind.RelativeOrAbsolute, out var uri))
            return false;

        if (!uri.IsAbsoluteUri)
            return !normalized.Contains(':', StringComparison.Ordinal);

        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
               uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
               uri.Scheme.Equals(Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase);
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
