using System.IO.Compression;
using System.Xml.Linq;
using Canvas.Core.Contracts;

namespace Canvas.Infrastructure.Converters;

/// <summary>
/// Converts an ODF 1.x .odt file into a <see cref="DesignExportDto"/>.
/// Parses content.xml from the ODF ZIP, walks text:p / text:h elements,
/// and stacks them as Text elements on a single Canvas page.
/// </summary>
public static class OdtImporter
{
    private static readonly XNamespace Text   = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace Style  = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace FoNs   = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
    private static readonly XNamespace Draw   = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private static readonly XNamespace Svg    = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";

    private const double PageWidth  = 595;
    private const double PageHeight = 842;
    private const double MarginX    = 48;
    private const double MarginY    = 48;

    public static DesignExportDto Import(Stream stream, string? name = null)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        // ── Parse automatic/named styles from styles.xml + content.xml ────────
        var styleMap = new Dictionary<string, ParaStyle>();
        ParseStyles(zip, "styles.xml",  styleMap);
        ParseStyles(zip, "content.xml", styleMap);

        // ── Parse content ─────────────────────────────────────────────────────
        var contentEntry = zip.GetEntry("content.xml")
            ?? throw new InvalidDataException("Not a valid ODT file (missing content.xml).");

        XDocument contentDoc;
        using (var s = contentEntry.Open()) contentDoc = XDocument.Load(s);

        var body = contentDoc.Descendants(Text + "body").FirstOrDefault();
        if (body is null) return Empty(name);

        var elements = new List<ElementDto>();
        double y = MarginY;
        int seq = 0;

        // Walk direct paragraph / heading / draw:frame children of text:body > text:text
        var textSection = body.Element(Text + "text") ?? body;

        foreach (var node in textSection.Elements())
        {
            if (node.Name == Draw + "frame")
            {
                elements.AddRange(ParseFrame(node, ref seq));
                continue;
            }

            if (node.Name != Text + "p" && node.Name != Text + "h") continue;

            string content = ExtractText(node);
            if (string.IsNullOrWhiteSpace(content)) { y += 6; continue; }

            // Style resolution
            string? styleName = (string?)node.Attribute(Text + "style-name");
            var ps = styleName is not null && styleMap.TryGetValue(styleName, out var s) ? s : new ParaStyle();

            double fontSize = ps.FontSize > 0 ? ps.FontSize : 11;
            double lineH    = fontSize * 1.4 + 4;

            elements.Add(new ElementDto
            {
                Id      = $"p-{seq++}",
                Type    = "text",
                X       = MarginX,
                Y       = Math.Round(y, 1),
                Width   = PageWidth - MarginX * 2,
                Height  = Math.Round(lineH, 1),
                Content = content,
                Style   = new Dictionary<string, object>
                {
                    ["fontSize"]   = Math.Round(fontSize, 1),
                    ["fontFamily"] = ps.FontFamily ?? "Arial",
                    ["color"]      = ps.Color      ?? "#000000",
                    ["fontWeight"] = ps.Bold   ? (object)"bold"   : "normal",
                    ["fontStyle"]  = ps.Italic ? (object)"italic" : "normal",
                    ["textAlign"]  = ps.Align  ?? "left",
                },
            });

            y += lineH;
        }

        // ── Meta ──────────────────────────────────────────────────────────────
        string docName = name ?? ExtractMetaTitle(zip) ?? "Imported ODT";

        return new DesignExportDto
        {
            Id    = Guid.NewGuid().ToString("N")[..12],
            Name  = docName,
            Pages = [new PageDto { Id = "page-1", Elements = elements }],
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void ParseStyles(ZipArchive zip, string entryName, Dictionary<string, ParaStyle> map)
    {
        var entry = zip.GetEntry(entryName);
        if (entry is null) return;

        XDocument doc;
        using (var s = entry.Open()) doc = XDocument.Load(s);

        foreach (var styleEl in doc.Descendants(Style + "style"))
        {
            string? sn = (string?)styleEl.Attribute(Style + "name");
            if (sn is null) continue;

            var ps = new ParaStyle();

            // Paragraph properties
            var pPr = styleEl.Element(Style + "paragraph-properties");
            if (pPr is not null)
            {
                ps.Align = ((string?)pPr.Attribute(FoNs + "text-align")) switch
                {
                    "center"  => "center",
                    "end"     => "right",
                    "right"   => "right",
                    "justify" => "justify",
                    _         => "left",
                };
            }

            // Text/run properties
            var tPr = styleEl.Element(Style + "text-properties");
            if (tPr is not null)
            {
                var fsStr = (string?)tPr.Attribute(FoNs + "font-size");
                if (fsStr is not null) ps.FontSize = ParsePt(fsStr);
                ps.FontFamily = (string?)tPr.Attribute(FoNs + "font-family")
                             ?? (string?)tPr.Attribute("font-name");
                var fw = (string?)tPr.Attribute(FoNs + "font-weight");
                ps.Bold = fw is "bold" or "700" or "800" or "900";
                ps.Italic = (string?)tPr.Attribute(FoNs + "font-style") == "italic";
                var col = (string?)tPr.Attribute(FoNs + "color");
                if (col is not null) ps.Color = col;
            }

            map[sn] = ps;
        }
    }

    private static List<ElementDto> ParseFrame(XElement frame, ref int seq)
    {
        var result = new List<ElementDto>();
        var imgEl = frame.Element(Draw + "image");
        if (imgEl is null) return result;

        double x = ParseMm((string?)frame.Attribute(Svg + "x") ?? "0") * 96 / 25.4;
        double y = ParseMm((string?)frame.Attribute(Svg + "y") ?? "0") * 96 / 25.4;
        double w = ParseMm((string?)frame.Attribute(Svg + "width")  ?? "100") * 96 / 25.4;
        double h = ParseMm((string?)frame.Attribute(Svg + "height") ?? "50")  * 96 / 25.4;

        string href = (string?)imgEl.Attribute(XNamespace.Get("http://www.w3.org/1999/xlink") + "href") ?? "";

        result.Add(new ElementDto
        {
            Id      = $"img-{seq++}",
            Type    = "image",
            X       = Math.Max(0, x),
            Y       = Math.Max(0, y),
            Width   = Math.Max(10, w),
            Height  = Math.Max(10, h),
            Content = href,
            Style   = new Dictionary<string, object> { ["fitMode"] = "contain" },
        });
        return result;
    }

    private static string ExtractText(XElement el)
        => string.Concat(el.DescendantNodes()
            .OfType<XText>()
            .Select(t => t.Value));

    private static string? ExtractMetaTitle(ZipArchive zip)
    {
        var entry = zip.GetEntry("meta.xml");
        if (entry is null) return null;
        XDocument doc;
        using (var s = entry.Open()) doc = XDocument.Load(s);
        var dcNs = XNamespace.Get("http://purl.org/dc/elements/1.1/");
        return (string?)doc.Descendants(dcNs + "title").FirstOrDefault();
    }

    private static double ParsePt(string value)
    {
        value = value.Trim();
        if (value.EndsWith("pt",  StringComparison.OrdinalIgnoreCase))
            return double.TryParse(value[..^2], System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 11;
        if (value.EndsWith("px",  StringComparison.OrdinalIgnoreCase))
            return double.TryParse(value[..^2], System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v2) ? v2 * 0.75 : 11;
        return double.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v3) ? v3 : 11;
    }

    private static double ParseMm(string value)
    {
        value = value.Trim();
        if (value.EndsWith("mm", StringComparison.OrdinalIgnoreCase))
            return double.TryParse(value[..^2], System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
        if (value.EndsWith("cm", StringComparison.OrdinalIgnoreCase))
            return double.TryParse(value[..^2], System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v2) ? v2 * 10 : 0;
        return 0;
    }

    private static DesignExportDto Empty(string? name) => new()
    {
        Id    = Guid.NewGuid().ToString("N")[..12],
        Name  = name ?? "Imported ODT",
        Pages = [new PageDto { Id = "page-1", Elements = [] }],
        SharedElements = [],
    };

    private sealed class ParaStyle
    {
        public double FontSize  { get; set; }
        public string? FontFamily { get; set; }
        public string? Color    { get; set; }
        public bool Bold        { get; set; }
        public bool Italic      { get; set; }
        public string? Align    { get; set; }
    }
}
