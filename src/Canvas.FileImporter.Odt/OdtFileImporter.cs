using System.IO.Compression;
using System.Xml.Linq;
using Canvas.Core.Contracts;
using Canvas.FileImporter.Abstractions;

namespace Canvas.FileImporter.Odt;

/// <summary>
/// Converts an ODF 1.x .odt file into a <see cref="DesignExportDto"/>.
/// </summary>
public sealed class OdtFileImporter : IFileImporter
{
    public IReadOnlyList<string> SupportedExtensions { get; } = ["odt"];

    public Task<DesignExportDto> ImportAsync(Stream stream, string? name = null) =>
        Task.FromResult(Import(stream, name));

    private static readonly XNamespace TextNs  = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace StyleNs = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace FoNs    = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
    private static readonly XNamespace DrawNs  = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private static readonly XNamespace SvgNs   = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";

    private const double PageWidth  = 595;
    private const double PageHeight = 842;
    private const double MarginX    = 48;
    private const double MarginY    = 48;

    public static DesignExportDto Import(Stream stream, string? name = null)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        var styleMap = new Dictionary<string, ParaStyle>();
        ParseStyles(zip, "styles.xml",  styleMap);
        ParseStyles(zip, "content.xml", styleMap);

        var contentEntry = zip.GetEntry("content.xml")
            ?? throw new InvalidDataException("Not a valid ODT file (missing content.xml).");

        XDocument contentDoc;
        using (var s = contentEntry.Open()) contentDoc = XDocument.Load(s);

        var body = contentDoc.Descendants(TextNs + "body").FirstOrDefault();
        if (body is null) return Empty(name);

        var elements = new List<ElementDto>();
        double y = MarginY;
        int seq = 0;

        var textSection = body.Element(TextNs + "text") ?? body;

        foreach (var node in textSection.Elements())
        {
            if (node.Name == DrawNs + "frame")
            {
                elements.AddRange(ParseFrame(node, ref seq));
                continue;
            }

            if (node.Name != TextNs + "p" && node.Name != TextNs + "h") continue;

            string content = ExtractText(node);
            if (string.IsNullOrWhiteSpace(content)) { y += 6; continue; }

            string? styleName = (string?)node.Attribute(TextNs + "style-name");
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

    private static void ParseStyles(ZipArchive zip, string entryName, Dictionary<string, ParaStyle> map)
    {
        var entry = zip.GetEntry(entryName);
        if (entry is null) return;

        XDocument doc;
        using (var s = entry.Open()) doc = XDocument.Load(s);

        foreach (var styleEl in doc.Descendants(StyleNs + "style"))
        {
            string? sn = (string?)styleEl.Attribute(StyleNs + "name");
            if (sn is null) continue;

            var ps = new ParaStyle();

            var pPr = styleEl.Element(StyleNs + "paragraph-properties");
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

            var tPr = styleEl.Element(StyleNs + "text-properties");
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
        var imgEl = frame.Element(DrawNs + "image");
        if (imgEl is null) return result;

        double x = ParseMm((string?)frame.Attribute(SvgNs + "x")      ?? "0")   * 96 / 25.4;
        double y = ParseMm((string?)frame.Attribute(SvgNs + "y")      ?? "0")   * 96 / 25.4;
        double w = ParseMm((string?)frame.Attribute(SvgNs + "width")  ?? "100") * 96 / 25.4;
        double h = ParseMm((string?)frame.Attribute(SvgNs + "height") ?? "50")  * 96 / 25.4;

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

    private static string ExtractText(XElement el) =>
        string.Concat(el.DescendantNodes().OfType<XText>().Select(t => t.Value));

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
        if (value.EndsWith("pt", StringComparison.OrdinalIgnoreCase))
            return double.TryParse(value[..^2], System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 11;
        if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
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
        public double FontSize   { get; set; }
        public string? FontFamily { get; set; }
        public string? Color     { get; set; }
        public bool Bold         { get; set; }
        public bool Italic       { get; set; }
        public string? Align     { get; set; }
    }
}
