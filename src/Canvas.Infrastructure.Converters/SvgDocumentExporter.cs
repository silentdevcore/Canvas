using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Canvas.Core.Abstractions;
using Canvas.Core.Contracts;
using Canvas.Core.Primitives;
using SkiaSharp;

namespace Canvas.Infrastructure.Converters;

public sealed class SvgDocumentExporter : IDocumentExporter
{
    public string FormatKey     => "svg";
    public string MimeType      => "image/svg+xml";
    public string FileExtension => ".svg";
    public IExporterCapabilities Capabilities => new ExporterCapabilities(SupportsFormFields: false);

    public byte[] Export(DesignExportDto design)
    {
        var ps = design.PageSettings ?? new PageSettingsDto();

        var plannedPages = DesignLayoutPlanner.BuildPages(design);

        // Web fonts used by the editor (Google Fonts) must be linked so the standalone
        // SVG renders the same typefaces instead of falling back to a default font.
        var fontImport = BuildFontImport(plannedPages.SelectMany(p => p.Elements));

        if (plannedPages.Count == 1)
            return RenderPageSvg(plannedPages[0].Elements, ps, fontImport);

        // Multi-page → zip
        using var zipStream = new MemoryStream();
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (int i = 0; i < plannedPages.Count; i++)
            {
                var svgBytes = RenderPageSvg(plannedPages[i].Elements, ps, fontImport);
                var entry    = zip.CreateEntry($"page-{i + 1}.svg");
                using var es = entry.Open();
                es.Write(svgBytes, 0, svgBytes.Length);
            }
        }
        return zipStream.ToArray();
    }

    private static byte[] RenderPageSvg(IReadOnlyList<ElementDto> elements, PageSettingsDto ps, string? fontImport)
    {
        var bgColor = ps.BackgroundColor ?? "#ffffff";

        var svg = new XElement("svg",
            new XAttribute("xmlns", "http://www.w3.org/2000/svg"),
            new XAttribute(XNamespace.Xmlns + "xlink", "http://www.w3.org/1999/xlink"),
            new XAttribute("viewBox", $"0 0 {Num(ps.Width)} {Num(ps.Height)}"),
            new XAttribute("width", ps.Width),
            new XAttribute("height", ps.Height),
            fontImport is not null
                ? new XElement("defs", new XElement("style", new XAttribute("type", "text/css"), new XCData(fontImport)))
                : null,
            // Background
            new XElement("rect",
                new XAttribute("width", ps.Width),
                new XAttribute("height", ps.Height),
                new XAttribute("fill", bgColor)),
            elements.Select(MapElement).Where(e => e is not null));

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), svg);
        using var ms = new MemoryStream();
        using var writer = new System.Xml.XmlTextWriter(ms, Encoding.UTF8);
        doc.WriteTo(writer);
        writer.Flush();
        return ms.ToArray();
    }

    private static XElement? MapElement(ElementDto el)
    {
        var s   = el.Style ?? [];
        var rot = s.TryGetValue("rotation", out var rv) && rv is not null
            ? $"rotate({Num(s.GetNum("rotation", 0))} {Num(el.X + el.Width / 2)} {Num(el.Y + el.Height / 2)})"
            : null;
        string? transform = rot is not null ? rot : null;

        XElement? node = el.Type switch
        {
            "rect" or "shape" => new XElement("rect",
                new XAttribute("x", el.X), new XAttribute("y", el.Y),
                new XAttribute("width", el.Width), new XAttribute("height", el.Height),
                new XAttribute("fill", s.GetStr("backgroundColor", s.GetStr("fill", "none"))),
                SvgStroke(s),
                s.GetNum("borderRadius", 0) > 0 ? new XAttribute("rx", s.GetNum("borderRadius", 0)) : null),

            "circle" => new XElement("ellipse",
                new XAttribute("cx", el.X + el.Width / 2), new XAttribute("cy", el.Y + el.Height / 2),
                new XAttribute("rx", el.Width / 2), new XAttribute("ry", el.Height / 2),
                new XAttribute("fill", s.GetStr("backgroundColor", s.GetStr("fill", "none"))),
                SvgStroke(s)),

            "line" => new XElement("line",
                new XAttribute("x1", el.X), new XAttribute("y1", el.Y + el.Height / 2),
                new XAttribute("x2", el.X + el.Width), new XAttribute("y2", el.Y + el.Height / 2),
                new XAttribute("stroke", s.GetStr("backgroundColor", "#9ca3af")),
                new XAttribute("stroke-width", el.Height)),

            "text" => SvgText(el, s),

            "link" => new XElement("a",
                new XAttribute(XNamespace.Xml + "href", el.Href ?? "#"),
                new XAttribute("target", "_blank"),
                SvgText(el, s, el.Content ?? el.Href ?? "")),

            "image" => new XElement("image",
                new XAttribute("x", el.X), new XAttribute("y", el.Y),
                new XAttribute("width", el.Width), new XAttribute("height", el.Height),
                new XAttribute("href", el.Content ?? ""),
                new XAttribute("preserveAspectRatio", FitToPreserveAspectRatio(el.FitMode))),

            "richtext" => SvgRichText(el, s),

            "table" => SvgTable(el, s),

            _ => null
        };

        if (node is not null && transform is not null)
            node.Add(new XAttribute("transform", transform));

        return node;
    }

    private static XElement SvgText(ElementDto el, Dictionary<string, object> s, string? overrideText = null)
    {
        var text       = overrideText ?? el.Content ?? "";
        var fs         = s.GetNum("fontSize", 14);
        var ff         = s.GetStr("fontFamily", "Arial");
        var fill       = s.GetStr("color", "#111827");
        var fw         = s.GetStr("fontWeight", "normal");
        var lineHeight = s.GetNum("lineHeight", 1.4);
        var align      = s.GetStr("textAlign", "left");
        var padL       = s.GetNum("paddingLeft", 0);
        var padR       = s.GetNum("paddingRight", 0);
        var padT       = s.GetNum("paddingTop", 0);

        var maxWidth = Math.Max(1, el.Width - padL - padR);
        var lines    = WrapText(text, maxWidth, ff, fs, fw);

        // Horizontal anchor mirrors the editor's text-align.
        var (anchorX, anchor) = align switch
        {
            "center"           => (el.X + padL + maxWidth / 2, "middle"),
            "right" or "end"   => (el.X + el.Width - padR,     "end"),
            _                  => (el.X + padL,                "start"),
        };

        // Top-aligned first baseline, matching the editor's box-flow layout.
        var firstBaseline = el.Y + padT + fs;

        var node = new XElement("text",
            new XAttribute("font-size", Num(fs)),
            new XAttribute("font-family", ff),
            new XAttribute("fill", fill),
            new XAttribute("font-weight", fw),
            new XAttribute("text-anchor", anchor));

        for (int i = 0; i < lines.Count; i++)
            node.Add(new XElement("tspan",
                new XAttribute("x", Num(anchorX)),
                new XAttribute("y", Num(firstBaseline + i * fs * lineHeight)),
                lines[i]));

        return node;
    }

    /// <summary>
    /// Word-wraps <paramref name="text"/> to <paramref name="maxWidth"/> using SkiaSharp
    /// font metrics, honouring explicit newlines and breaking over-long words by character.
    /// </summary>
    private static List<string> WrapText(string text, double maxWidth, string fontFamily, double fontSize, string fontWeight)
    {
        var bold = fontWeight is "bold" or "600" or "700" or "800" or "900";
        using var tf = SKTypeface.FromFamilyName(
            fontFamily,
            bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        using var font = new SKFont(tf, (float)fontSize);

        var lines = new List<string>();
        foreach (var rawLine in text.Replace("\r", "").Split('\n'))
        {
            if (rawLine.Length == 0) { lines.Add(""); continue; }

            var cur = new StringBuilder();
            foreach (var word in rawLine.Split(' '))
            {
                var candidate = cur.Length == 0 ? word : $"{cur} {word}";
                if (cur.Length == 0 || font.MeasureText(candidate) <= maxWidth)
                {
                    cur.Clear().Append(candidate);
                    continue;
                }

                lines.Add(cur.ToString());
                cur.Clear();

                // A single word wider than the box is broken across characters.
                if (font.MeasureText(word) > maxWidth)
                {
                    foreach (var ch in word)
                    {
                        if (cur.Length > 0 && font.MeasureText($"{cur}{ch}") > maxWidth)
                        {
                            lines.Add(cur.ToString());
                            cur.Clear();
                        }
                        cur.Append(ch);
                    }
                }
                else
                {
                    cur.Append(word);
                }
            }
            if (cur.Length > 0) lines.Add(cur.ToString());
        }

        if (lines.Count == 0) lines.Add("");
        return lines;
    }

    // Void HTML elements that must be self-closed to be well-formed XHTML.
    private static readonly Regex VoidTagRegex = new(
        @"<(area|base|br|col|embed|hr|img|input|link|meta|param|source|track|wbr)\b([^>/]*)>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Named HTML entities (e.g. &nbsp;) that are not the five XML-predefined ones.
    private static readonly Regex NamedEntityRegex = new(
        @"&(?!amp;|lt;|gt;|quot;|apos;|#)([a-zA-Z][a-zA-Z0-9]+);",
        RegexOptions.Compiled);

    /// <summary>
    /// Renders a rich-text element. The HTML is embedded as an XHTML
    /// <c>&lt;foreignObject&gt;</c> when it can be made well-formed; otherwise it
    /// gracefully degrades to a plain SVG text element so export never fails.
    /// </summary>
    private static XElement SvgRichText(ElementDto el, Dictionary<string, object> s)
    {
        var html = el.HtmlContent ?? "";

        try
        {
            var xhtml = SanitizeToXhtml(html);
            var div = XElement.Parse(
                $"<div xmlns=\"http://www.w3.org/1999/xhtml\" style=\"overflow:hidden;width:100%;height:100%;\">{xhtml}</div>");

            return new XElement("foreignObject",
                new XAttribute("x", el.X), new XAttribute("y", el.Y),
                new XAttribute("width", el.Width), new XAttribute("height", el.Height),
                div);
        }
        catch (System.Xml.XmlException)
        {
            // Malformed HTML (e.g. mismatched tags) — fall back to readable plain text.
            var text = WebUtility.HtmlDecode(StripTags(html));
            return SvgText(el, s, text);
        }
    }

    /// <summary>
    /// Best-effort conversion of HTML to well-formed XHTML: self-closes void tags
    /// (<c>&lt;br&gt;</c>, <c>&lt;hr&gt;</c>, …) and replaces named entities such as
    /// <c>&amp;nbsp;</c> with their Unicode equivalents.
    /// </summary>
    private static string SanitizeToXhtml(string html)
    {
        html = VoidTagRegex.Replace(html, m => $"<{m.Groups[1].Value}{m.Groups[2].Value}/>");
        html = NamedEntityRegex.Replace(html, m =>
        {
            var decoded = WebUtility.HtmlDecode(m.Value);
            // If it decoded to something other than the original, inline the char(s).
            return decoded == m.Value ? m.Value : System.Security.SecurityElement.Escape(decoded) ?? decoded;
        });
        return html;
    }

    private static string StripTags(string html) =>
        Regex.Replace(Regex.Replace(html, "<[^>]+>", " "), @"\s+", " ").Trim();

    private static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    /// Builds a CSS <c>@import</c> for the Google Font families used by the document so the
    /// standalone SVG renders the editor's typefaces. Returns null for system-only fonts.
    /// </summary>
    private static string? BuildFontImport(IEnumerable<ElementDto> elements)
    {
        var url = GoogleFontCss.BuildUrl(elements);
        return url is null ? null : $"@import url('{url}');";
    }

    private static XElement? SvgTable(ElementDto el, Dictionary<string, object> s)
    {
        var cellData = el.CellData;
        if (cellData is null || cellData.Length == 0) return null;

        var cols     = cellData[0]?.Length ?? 0;
        var rows     = cellData.Length;
        var bw       = s.GetNum("borderWidth", 1);
        var bc       = s.GetStr("borderColor", "#000000");
        var hasHdr   = el.HeaderRow == true;
        var hdrBg    = el.HeaderBgColor ?? "#f1f5f9";
        var cellW    = cols > 0 ? el.Width / cols : el.Width;
        var cellH    = rows > 0 ? el.Height / rows : el.Height;

        var g = new XElement("g");
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var cx   = el.X + c * cellW;
                var cy   = el.Y + r * cellH;
                var fill = hasHdr && r == 0 ? hdrBg : "none";
                var cell = cellData[r]?.Length > c ? cellData[r][c] : "";

                g.Add(new XElement("rect",
                    new XAttribute("x", cx), new XAttribute("y", cy),
                    new XAttribute("width", cellW), new XAttribute("height", cellH),
                    new XAttribute("fill", fill),
                    new XAttribute("stroke", bc), new XAttribute("stroke-width", bw)));

                const double cellFs = 10;
                var cellFw  = hasHdr && r == 0 ? "bold" : "normal";
                var cellLns = WrapText(cell ?? "", Math.Max(1, cellW - 8), "Arial", cellFs, cellFw);

                var cellText = new XElement("text",
                    new XAttribute("font-size", cellFs),
                    new XAttribute("font-family", "Arial"),
                    new XAttribute("fill", "#111827"),
                    new XAttribute("font-weight", cellFw));

                // Vertically centre the wrapped block within the cell.
                var blockH    = cellLns.Count * cellFs * 1.3;
                var firstBase = cy + Math.Max(cellFs + 2, (cellH - blockH) / 2 + cellFs);
                for (int li = 0; li < cellLns.Count; li++)
                    cellText.Add(new XElement("tspan",
                        new XAttribute("x", Num(cx + 4)),
                        new XAttribute("y", Num(firstBase + li * cellFs * 1.3)),
                        cellLns[li]));

                g.Add(cellText);
            }
        }
        return g;
    }

    private static XAttribute? SvgStroke(Dictionary<string, object> s)
    {
        var bw = s.GetNum("borderWidth", 0);
        if (bw <= 0) return null;
        return new XAttribute("stroke", s.GetStr("borderColor", "#000000"));
    }

    private static string FitToPreserveAspectRatio(string? fitMode) => fitMode switch
    {
        "cover"   => "xMidYMid slice",
        "fill"    => "none",
        "none"    => "xMidYMid meet",
        _         => "xMidYMid meet",
    };
}
