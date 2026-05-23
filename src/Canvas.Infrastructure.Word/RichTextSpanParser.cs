using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Canvas.Infrastructure.Word;

internal static partial class RichTextSpanParser
{
    internal sealed record RichRun(
        string Text,
        bool Bold,
        bool Italic,
        bool Underline,
        bool Strike,
        string? ColorHex,
        double? FontSizePt);

    internal sealed record RichParagraph(IReadOnlyList<RichRun> Runs);

    internal static IReadOnlyList<RichParagraph> Parse(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return [];

        var paragraphs = new List<RichParagraph>();
        var currentRuns = new List<RichRun>();

        var boldStack = new Stack<bool>();
        var italicStack = new Stack<bool>();
        var underlineStack = new Stack<bool>();
        var strikeStack = new Stack<bool>();
        var colorStack = new Stack<string?>();
        var fontSizeStack = new Stack<double?>();

        boldStack.Push(false);
        italicStack.Push(false);
        underlineStack.Push(false);
        strikeStack.Push(false);
        colorStack.Push(null);
        fontSizeStack.Push(null);

        foreach (Match m in TokenRegex().Matches(html))
        {
            if (m.Groups[1].Success)
            {
                ProcessTag(
                    m.Groups[1].Value,
                    boldStack,
                    italicStack,
                    underlineStack,
                    strikeStack,
                    colorStack,
                    fontSizeStack,
                    ref currentRuns,
                    paragraphs);
                continue;
            }

            if (!m.Groups[2].Success)
                continue;

            var text = WebUtility.HtmlDecode(m.Groups[2].Value);
            if (text.Length == 0)
                continue;

            currentRuns.Add(new RichRun(
                text,
                boldStack.Peek(),
                italicStack.Peek(),
                underlineStack.Peek(),
                strikeStack.Peek(),
                colorStack.Peek(),
                fontSizeStack.Peek()));
        }

        FlushParagraph(ref currentRuns, paragraphs);
        return paragraphs;
    }

    private static void ProcessTag(
        string tag,
        Stack<bool> bold,
        Stack<bool> italic,
        Stack<bool> underline,
        Stack<bool> strike,
        Stack<string?> color,
        Stack<double?> size,
        ref List<RichRun> currentRuns,
        List<RichParagraph> paragraphs)
    {
        var closing = tag.StartsWith("</", StringComparison.Ordinal);
        var tagName = TagNameRegex().Match(tag).Groups[1].Value.ToLowerInvariant();

        if (closing)
        {
            switch (tagName)
            {
                case "strong" or "b":
                    if (bold.Count > 1) bold.Pop();
                    break;
                case "em" or "i":
                    if (italic.Count > 1) italic.Pop();
                    break;
                case "u":
                    if (underline.Count > 1) underline.Pop();
                    break;
                case "s" or "strike" or "del":
                    if (strike.Count > 1) strike.Pop();
                    break;
                case "span":
                    if (color.Count > 1) color.Pop();
                    if (size.Count > 1) size.Pop();
                    if (bold.Count > 1) bold.Pop();
                    if (italic.Count > 1) italic.Pop();
                    if (underline.Count > 1) underline.Pop();
                    if (strike.Count > 1) strike.Pop();
                    break;
                case "p" or "div" or "li" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                    FlushParagraph(ref currentRuns, paragraphs);
                    if (tagName is "h1" or "h2" or "h3" or "h4" or "h5" or "h6")
                    {
                        if (bold.Count > 1) bold.Pop();
                        if (size.Count > 1) size.Pop();
                    }
                    break;
            }

            return;
        }

        switch (tagName)
        {
            case "strong" or "b":
                bold.Push(true);
                break;
            case "em" or "i":
                italic.Push(true);
                break;
            case "u":
                underline.Push(true);
                break;
            case "s" or "strike" or "del":
                strike.Push(true);
                break;
            case "br":
                FlushParagraph(ref currentRuns, paragraphs);
                break;
            case "li":
                FlushParagraph(ref currentRuns, paragraphs);
                currentRuns.Add(new RichRun("• ", bold.Peek(), italic.Peek(), underline.Peek(), strike.Peek(), color.Peek(), size.Peek()));
                break;
            case "span":
            {
                var inlineStyle = InlineStyleRegex().Match(tag).Groups[1].Value;
                color.Push(ParseInlineColor(inlineStyle, color.Peek()));
                size.Push(ParseInlineFontSize(inlineStyle, size.Peek()));
                bold.Push(ParseInlineBold(inlineStyle, bold.Peek()));
                italic.Push(ParseInlineItalic(inlineStyle, italic.Peek()));
                var deco = ParseInlineDecoration(inlineStyle);
                underline.Push(deco.HasFlag(TextDeco.Underline) || underline.Peek());
                strike.Push(deco.HasFlag(TextDeco.LineThrough) || strike.Peek());
                break;
            }
            case "h1":
                bold.Push(true);
                size.Push(28);
                break;
            case "h2":
                bold.Push(true);
                size.Push(24);
                break;
            case "h3":
                bold.Push(true);
                size.Push(20);
                break;
            case "h4":
                bold.Push(true);
                size.Push(18);
                break;
            case "h5":
                bold.Push(true);
                size.Push(16);
                break;
            case "h6":
                bold.Push(true);
                size.Push(14);
                break;
        }
    }

    private static void FlushParagraph(ref List<RichRun> currentRuns, List<RichParagraph> paragraphs)
    {
        if (currentRuns.Count == 0)
            return;

        paragraphs.Add(new RichParagraph(currentRuns));
        currentRuns = [];
    }

    private static string? ParseInlineColor(string style, string? fallback)
    {
        var match = ColorInStyleRegex().Match(style);
        if (!match.Success)
            return fallback;

        var raw = match.Groups[1].Value.Trim();
        if (!raw.StartsWith("#", StringComparison.Ordinal))
            return fallback;

        var hex = raw.TrimStart('#');
        if (hex.Length == 3 && hex.All(Uri.IsHexDigit))
            return string.Concat(hex.Select(c => $"{c}{c}"));

        return hex.Length == 6 && hex.All(Uri.IsHexDigit) ? hex : fallback;
    }

    private static double? ParseInlineFontSize(string style, double? fallback)
    {
        var match = FontSizeInStyleRegex().Match(style);
        if (!match.Success)
            return fallback;

        return double.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    [GeneratedRegex(@"(<[^>]+>)|([^<]+)")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"</?([a-zA-Z0-9]+)")]
    private static partial Regex TagNameRegex();

    [GeneratedRegex(@"style=""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex InlineStyleRegex();

    [GeneratedRegex(@"color\s*:\s*([^;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ColorInStyleRegex();

    [GeneratedRegex(@"font-size\s*:\s*([0-9]+(?:\.[0-9]+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex FontSizeInStyleRegex();

    [GeneratedRegex(@"font-weight\s*:\s*([^;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex FontWeightRegex();

    [GeneratedRegex(@"font-style\s*:\s*([^;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex FontStyleRegex();

    [GeneratedRegex(@"text-decoration\s*:\s*([^;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex TextDecorationRegex();

    [Flags]
    private enum TextDeco { None = 0, Underline = 1, LineThrough = 2 }

    private static bool ParseInlineBold(string style, bool fallback)
    {
        var m = FontWeightRegex().Match(style);
        if (!m.Success) return fallback;
        return m.Groups[1].Value.Trim() is "bold" or "700" or "800" or "900";
    }

    private static bool ParseInlineItalic(string style, bool fallback)
    {
        var m = FontStyleRegex().Match(style);
        if (!m.Success) return fallback;
        return m.Groups[1].Value.Trim() == "italic";
    }

    private static TextDeco ParseInlineDecoration(string style)
    {
        var m = TextDecorationRegex().Match(style);
        if (!m.Success) return TextDeco.None;
        var val = m.Groups[1].Value;
        var result = TextDeco.None;
        if (val.Contains("underline", StringComparison.OrdinalIgnoreCase)) result |= TextDeco.Underline;
        if (val.Contains("line-through", StringComparison.OrdinalIgnoreCase)) result |= TextDeco.LineThrough;
        return result;
    }
}
