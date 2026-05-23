using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Canvas.Core.Abstractions;
using Canvas.Core.Contracts;
using Canvas.Core.Primitives;

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

        if (plannedPages.Count == 1)
            return RenderPageSvg(plannedPages[0].Elements, ps);

        // Multi-page → zip
        using var zipStream = new MemoryStream();
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (int i = 0; i < plannedPages.Count; i++)
            {
                var svgBytes = RenderPageSvg(plannedPages[i].Elements, ps);
                var entry    = zip.CreateEntry($"page-{i + 1}.svg");
                using var es = entry.Open();
                es.Write(svgBytes, 0, svgBytes.Length);
            }
        }
        return zipStream.ToArray();
    }

    private static byte[] RenderPageSvg(IReadOnlyList<ElementDto> elements, PageSettingsDto ps)
    {
        var bgColor = ps.BackgroundColor ?? "#ffffff";

        var svg = new XElement("svg",
            new XAttribute("xmlns", "http://www.w3.org/2000/svg"),
            new XAttribute(XNamespace.Xmlns + "xlink", "http://www.w3.org/1999/xlink"),
            new XAttribute("viewBox", $"0 0 {ps.Width} {ps.Height}"),
            new XAttribute("width", ps.Width),
            new XAttribute("height", ps.Height),
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
            ? $"rotate({rv} {el.X + el.Width / 2} {el.Y + el.Height / 2})"
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

            "richtext" => new XElement("foreignObject",
                new XAttribute("x", el.X), new XAttribute("y", el.Y),
                new XAttribute("width", el.Width), new XAttribute("height", el.Height),
                XElement.Parse($"<div xmlns=\"http://www.w3.org/1999/xhtml\" style=\"overflow:hidden;width:100%;height:100%;\">{el.HtmlContent ?? ""}</div>")),

            "table" => SvgTable(el, s),

            _ => null
        };

        if (node is not null && transform is not null)
            node.Add(new XAttribute("transform", transform));

        return node;
    }

    private static XElement SvgText(ElementDto el, Dictionary<string, object> s, string? overrideText = null)
    {
        var text = overrideText ?? el.Content ?? "";
        var fs   = s.GetNum("fontSize", 14);
        var ff   = s.GetStr("fontFamily", "Arial");
        var fill = s.GetStr("color", "#111827");
        var fw   = s.GetStr("fontWeight", "normal");

        // SVG text is baseline-aligned; nudge y by font size
        return new XElement("text",
            new XAttribute("x", el.X),
            new XAttribute("y", el.Y + fs),
            new XAttribute("width", el.Width),
            new XAttribute("font-size", fs),
            new XAttribute("font-family", ff),
            new XAttribute("fill", fill),
            new XAttribute("font-weight", fw),
            text);
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

                g.Add(new XElement("text",
                    new XAttribute("x", cx + 4), new XAttribute("y", cy + cellH / 2 + 4),
                    new XAttribute("font-size", 10),
                    new XAttribute("font-family", "Arial"),
                    new XAttribute("fill", "#111827"),
                    new XAttribute("font-weight", hasHdr && r == 0 ? "bold" : "normal"),
                    cell ?? ""));
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
