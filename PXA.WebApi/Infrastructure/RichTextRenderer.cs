using System.Globalization;
using System.Text.RegularExpressions;
using Canvas.Pdf;

namespace PXA.WebApi.Infrastructure;

/// <summary>
/// Parses TipTap / ProseMirror HTML into styled span runs and renders them
/// with mixed bold/italic/color/size on the same line via sequential DrawText calls.
/// </summary>
internal static partial class RichTextRenderer
{
    private record SpanRun(
        string Text,
        double FontSize,
        bool Bold,
        bool Italic,
        bool Underline,
        bool Strikethrough,
        PdfColor Color
    );

    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Renders rich-text HTML at the given PDF coordinates.
    /// Returns the Y position after the last rendered line.
    /// </summary>
    public static double Render(
        PdfPage page,
        string html,
        double x,
        double firstBaselineY,
        double maxWidth,
        double baseFontSize,
        PdfColor baseColor,
        double? baseLineHeight = null)
    {
        var paragraphs = ParseHtml(html, baseFontSize, baseColor);
        var currentY = firstBaselineY;

        foreach (var para in paragraphs)
        {
            if (para.Count == 0) continue;

            // Line height uses the tallest span in the paragraph, or the base size
            var maxFontSize = para.Max(s => s.FontSize);
            var lineH = baseLineHeight ?? maxFontSize * 1.35;

            var lines = WrapParagraph(para, maxWidth);

            foreach (var line in lines)
            {
                if (line.Count == 0) continue;
                var cx = x;
                foreach (var (span, word) in line)
                {
                    if (string.IsNullOrEmpty(word))
                        continue;

                    // PdfPage.DrawText rejects whitespace-only text; advance cursor instead.
                    if (string.IsNullOrWhiteSpace(word))
                    {
                        cx += SpanWidth(word, span.FontSize, span.Bold);
                        continue;
                    }

                    page.DrawText(word, cx, currentY, new PdfDrawTextOptions
                    {
                        FontSize = span.FontSize,
                        Bold = span.Bold,
                        Italic = span.Italic,
                        Underline = span.Underline,
                        Strikethrough = span.Strikethrough,
                        FillColor = span.Color
                    });
                    cx += SpanWidth(word, span.FontSize, span.Bold);
                }
                currentY -= lineH;
            }

            // Extra gap after each paragraph block (≈ 30 % of font size)
            currentY -= maxFontSize * 0.3;
        }

        return currentY;
    }

    // ── HTML parser ───────────────────────────────────────────────────────────

    private static List<List<SpanRun>> ParseHtml(string html, double baseFontSize, PdfColor baseColor)
    {
        var paragraphs = new List<List<SpanRun>>();
        var current = new List<SpanRun>();

        // Style stacks — one entry per open tag
        var boldStack = new Stack<bool>();        boldStack.Push(false);
        var italicStack = new Stack<bool>();      italicStack.Push(false);
        var underlineStack = new Stack<bool>();   underlineStack.Push(false);
        var strikeStack = new Stack<bool>();      strikeStack.Push(false);
        var colorStack = new Stack<PdfColor>();   colorStack.Push(baseColor);
        var sizeStack = new Stack<double>();      sizeStack.Push(baseFontSize);

        // Tokenise: either a tag or a text node
        foreach (Match m in TokenRegex().Matches(html))
        {
            if (m.Groups[1].Success)
            {
                ProcessTag(m.Groups[1].Value,
                    boldStack, italicStack, underlineStack, strikeStack,
                    colorStack, sizeStack, baseFontSize,
                    ref current, paragraphs);
            }
            else if (m.Groups[2].Success)
            {
                var text = DecodeEntities(m.Groups[2].Value);
                if (!string.IsNullOrEmpty(text))
                {
                    current.Add(new SpanRun(
                        text,
                        sizeStack.Peek(),
                        boldStack.Peek(),
                        italicStack.Peek(),
                        underlineStack.Peek(),
                        strikeStack.Peek(),
                        colorStack.Peek()
                    ));
                }
            }
        }

        if (current.Count > 0)
            paragraphs.Add(current);

        return paragraphs;
    }

    private static void ProcessTag(
        string tag,
        Stack<bool> boldStack,
        Stack<bool> italicStack,
        Stack<bool> underlineStack,
        Stack<bool> strikeStack,
        Stack<PdfColor> colorStack,
        Stack<double> sizeStack,
        double baseFontSize,
        ref List<SpanRun> current,
        List<List<SpanRun>> paragraphs)
    {
        var isClose = tag.StartsWith("</", StringComparison.Ordinal);
        var tagName = TagNameRegex().Match(tag).Groups[1].Value.ToLowerInvariant();

        if (isClose)
        {
            switch (tagName)
            {
                case "strong" or "b":
                    if (boldStack.Count > 1) boldStack.Pop();
                    break;
                case "em" or "i":
                    if (italicStack.Count > 1) italicStack.Pop();
                    break;
                case "u":
                    if (underlineStack.Count > 1) underlineStack.Pop();
                    break;
                case "s" or "strike" or "del":
                    if (strikeStack.Count > 1) strikeStack.Pop();
                    break;
                case "span":
                    if (colorStack.Count > 1) colorStack.Pop();
                    if (sizeStack.Count > 1) sizeStack.Pop();
                    break;
                case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                    if (sizeStack.Count > 1) sizeStack.Pop();
                    if (boldStack.Count > 1) boldStack.Pop();
                    FlushParagraph(ref current, paragraphs);
                    break;
                case "p" or "div" or "li" or "blockquote":
                    FlushParagraph(ref current, paragraphs);
                    break;
            }
        }
        else
        {
            switch (tagName)
            {
                case "strong" or "b":
                    boldStack.Push(true);
                    break;
                case "em" or "i":
                    italicStack.Push(true);
                    break;
                case "u":
                    underlineStack.Push(true);
                    break;
                case "s" or "strike" or "del":
                    strikeStack.Push(true);
                    break;
                case "br":
                    FlushParagraph(ref current, paragraphs);
                    break;
                case "li":
                    FlushParagraph(ref current, paragraphs);
                    // Prepend bullet using base style
                    current.Add(new SpanRun("• ", sizeStack.Peek(), boldStack.Peek(), false, false, false, colorStack.Peek()));
                    break;
                case "span":
                    var inlineStyle = InlineStyleRegex().Match(tag).Groups[1].Value;
                    var newColor = ParseInlineColor(inlineStyle, colorStack.Peek());
                    var newSize  = ParseInlineFontSize(inlineStyle, sizeStack.Peek());
                    colorStack.Push(newColor);
                    sizeStack.Push(newSize);
                    break;
                case "h1": sizeStack.Push(baseFontSize * 2.0); boldStack.Push(true); break;
                case "h2": sizeStack.Push(baseFontSize * 1.6); boldStack.Push(true); break;
                case "h3": sizeStack.Push(baseFontSize * 1.3); boldStack.Push(true); break;
                case "h4": sizeStack.Push(baseFontSize * 1.15); boldStack.Push(true); break;
                case "h5" or "h6": sizeStack.Push(baseFontSize); boldStack.Push(true); break;
                // p, div, blockquote, ul, ol — just structural; don't push style
            }
        }
    }

    private static void FlushParagraph(ref List<SpanRun> current, List<List<SpanRun>> paragraphs)
    {
        if (current.Count > 0)
        {
            paragraphs.Add(current);
            current = [];
        }
    }

    // ── Word-wrap ─────────────────────────────────────────────────────────────

    private static List<List<(SpanRun Span, string Word)>> WrapParagraph(
        List<SpanRun> spans, double maxWidth)
    {
        var lines = new List<List<(SpanRun, string)>>();
        var currentLine = new List<(SpanRun, string)>();
        var lineWidth = 0.0;

        foreach (var span in spans)
        {
            // Split into word+space parts; trailing space stays with the word
            var parts = SplitWords(span.Text);

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                var partW = SpanWidth(part, span.FontSize, span.Bold);

                // Line break when word exceeds limit — but never break on a leading space
                if (lineWidth + partW > maxWidth && currentLine.Count > 0 && part.Trim().Length > 0)
                {
                    lines.Add(currentLine);
                    currentLine = [];
                    lineWidth = 0;

                    var trimmed = part.TrimStart();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        currentLine.Add((span, trimmed));
                        lineWidth = SpanWidth(trimmed, span.FontSize, span.Bold);
                    }
                }
                else
                {
                    currentLine.Add((span, part));
                    lineWidth += partW;
                }
            }
        }

        if (currentLine.Count > 0)
            lines.Add(currentLine);

        return lines;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Approximate width of text — mirrors the PDF engine's internal estimator.</summary>
    private static double SpanWidth(string text, double fontSize, bool bold)
    {
        // Helvetica: 0.52 avg char width factor; bold slightly wider
        var factor = bold ? 0.57 : 0.52;
        return text.Length * fontSize * factor;
    }

    /// <summary>Splits text into parts, keeping trailing space attached to each word.</summary>
    private static List<string> SplitWords(string text)
    {
        var parts = new List<string>();
        var words = text.Split(' ');
        for (var i = 0; i < words.Length; i++)
        {
            var w = i < words.Length - 1 ? words[i] + " " : words[i];
            if (w.Length > 0)
                parts.Add(w);
        }
        return parts;
    }

    private static PdfColor ParseInlineColor(string style, PdfColor fallback)
    {
        var m = ColorInStyleRegex().Match(style);
        if (!m.Success) return fallback;
        var raw = m.Groups[1].Value.Trim();
        // Handle hex colors
        if (raw.StartsWith('#'))
        {
            var hex = raw.TrimStart('#');
            if (hex.Length == 3) hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);
            if (hex.Length == 6 &&
                int.TryParse(hex[..2], NumberStyles.HexNumber, null, out var r) &&
                int.TryParse(hex[2..4], NumberStyles.HexNumber, null, out var g) &&
                int.TryParse(hex[4..6], NumberStyles.HexNumber, null, out var b))
                return new PdfColor(r / 255.0, g / 255.0, b / 255.0);
        }
        // Handle rgb(r, g, b)
        var rgb = RgbRegex().Match(raw);
        if (rgb.Success &&
            double.TryParse(rgb.Groups[1].Value, out var rr) &&
            double.TryParse(rgb.Groups[2].Value, out var gg) &&
            double.TryParse(rgb.Groups[3].Value, out var bb))
            return new PdfColor(rr / 255.0, gg / 255.0, bb / 255.0);

        return fallback;
    }

    private static double ParseInlineFontSize(string style, double fallback)
    {
        var m = FontSizeInStyleRegex().Match(style);
        if (!m.Success) return fallback;
        return double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var fs)
            ? fs : fallback;
    }

    private static string DecodeEntities(string text) =>
        text.Replace("&nbsp;",  " ")
            .Replace("&amp;",   "&")
            .Replace("&lt;",    "<")
            .Replace("&gt;",    ">")
            .Replace("&quot;",  "\"")
            .Replace("&#39;",   "'");

    // ── Compiled regexes ──────────────────────────────────────────────────────

    [System.Text.RegularExpressions.GeneratedRegex(@"(<[^>]+>)|([^<]+)", RegexOptions.None)]
    private static partial Regex TokenRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"</?(\w+)", RegexOptions.None)]
    private static partial Regex TagNameRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"style=""([^""]*?)""", RegexOptions.None)]
    private static partial Regex InlineStyleRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"color:\s*([^;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ColorInStyleRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"font-size:\s*(\d+(?:\.\d+)?)(?:px|pt)?", RegexOptions.IgnoreCase)]
    private static partial Regex FontSizeInStyleRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"rgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex RgbRegex();
}
