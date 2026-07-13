using System.IO.Compression;
using System.Text;
using System.Xml;
using PXA.Core.Abstractions;
using PXA.Core.Contracts;

namespace PXA.Infrastructure.Converters;

/// <summary>
/// Exports a <see cref="DesignExportDto"/> to OpenDocument Text format (.odt).
/// Produces a spec-compliant ODF 1.3 ZIP package with absolute-positioned draw frames
/// so that the pixel-accurate layout from the PXA designer is preserved in
/// LibreOffice / Google Docs.
/// </summary>
public sealed class OdtDocumentExporter : DocumentExporter
{
    public string FormatKey     => "odt";
    public string MimeType      => "application/vnd.oasis.opendocument.text";
    public string FileExtension => ".odt";
    public IExporterCapabilities Capabilities => new ExporterCapabilities(
        SupportsMultiPage: true, SupportsImages: true, SupportsRichText: true);

    public byte[] Export(DesignExportDto design) => Export(design, null);

    public byte[] Export(DesignExportDto design, ExportOptions? options)
    {
        var ps = design.PageSettings ?? new PageSettingsDto();

        using var ms  = new MemoryStream();
        using var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true);

        // ODF spec §2.2.1: mimetype MUST be first entry, uncompressed.
        WriteUncompressed(zip, "mimetype", "application/vnd.oasis.opendocument.text");

        WriteXml(zip, "META-INF/manifest.xml",   BuildManifest());
        WriteXml(zip, "meta.xml",                BuildMeta(design));
        WriteXml(zip, "styles.xml",              BuildStyles(ps));
        WriteXml(zip, "content.xml",             BuildContent(design, ps));

        zip.Dispose();
        return ms.ToArray();
    }

    // ── Package parts ─────────────────────────────────────────────────────────

    private static string BuildManifest() => """
        <?xml version="1.0" encoding="UTF-8"?>
        <manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0"
                           manifest:version="1.3">
          <manifest:file-entry manifest:full-path="/"
                               manifest:version="1.3"
                               manifest:media-type="application/vnd.oasis.opendocument.text"/>
          <manifest:file-entry manifest:full-path="content.xml"  manifest:media-type="text/xml"/>
          <manifest:file-entry manifest:full-path="styles.xml"   manifest:media-type="text/xml"/>
          <manifest:file-entry manifest:full-path="meta.xml"     manifest:media-type="text/xml"/>
        </manifest:manifest>
        """;

    private static string BuildMeta(DesignExportDto design)
    {
        var title  = Esc(design.PageSettings?.Metadata?.Title  ?? design.Name);
        var author = Esc(design.PageSettings?.Metadata?.Author ?? "");
        var now    = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document-meta
                xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                xmlns:meta="urn:oasis:names:tc:opendocument:xmlns:meta:1.0"
                xmlns:dc="http://purl.org/dc/elements/1.1/"
                office:version="1.3">
              <office:meta>
                <dc:title>{title}</dc:title>
                <dc:creator>{author}</dc:creator>
                <meta:creation-date>{now}</meta:creation-date>
              </office:meta>
            </office:document-meta>
            """;
    }

    private static string BuildStyles(PageSettingsDto ps)
    {
        var pageW  = MmStr(ps.Width);
        var pageH  = MmStr(ps.Height);
        var mTop   = MmStr(ps.Margins?.Top    ?? 0);
        var mBot   = MmStr(ps.Margins?.Bottom ?? 0);
        var mLeft  = MmStr(ps.Margins?.Left   ?? 0);
        var mRight = MmStr(ps.Margins?.Right  ?? 0);
        var orient = (ps.Orientation?.ToLowerInvariant() == "landscape") ? "landscape" : "portrait";

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document-styles
                xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0"
                xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0"
                office:version="1.3">
              <office:automatic-styles>
                <style:page-layout style:name="PageLayout">
                  <style:page-layout-properties
                      fo:page-width="{pageW}"
                      fo:page-height="{pageH}"
                      style:print-orientation="{orient}"
                      fo:margin-top="{mTop}"
                      fo:margin-bottom="{mBot}"
                      fo:margin-left="{mLeft}"
                      fo:margin-right="{mRight}"/>
                </style:page-layout>
              </office:automatic-styles>
              <office:master-styles>
                <style:master-page style:name="Standard" style:page-layout-name="PageLayout"/>
              </office:master-styles>
            </office:document-styles>
            """;
    }

    private static string BuildContent(DesignExportDto design, PageSettingsDto ps)
    {
        var sb = new StringBuilder();
        var cellStyleDefs = BuildCellStyleDefs(design);
        sb.AppendLine($$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document-content
                xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"
                xmlns:draw="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0"
                xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0"
                xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0"
                xmlns:svg="urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0"
                xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0"
                xmlns:xlink="http://www.w3.org/1999/xlink"
                office:version="1.3">
              <office:automatic-styles>
                <style:style style:name="Standard" style:family="paragraph">
                  <style:paragraph-properties fo:margin="0cm" fo:padding="0cm"/>
                </style:style>
                <style:style style:name="pAlignLeft" style:family="paragraph"><style:paragraph-properties fo:text-align="start"/></style:style>
                <style:style style:name="pAlignCenter" style:family="paragraph"><style:paragraph-properties fo:text-align="center"/></style:style>
                <style:style style:name="pAlignRight" style:family="paragraph"><style:paragraph-properties fo:text-align="end"/></style:style>
            {{cellStyleDefs}}  </office:automatic-styles>
              <office:body>
                <office:text>
            """);

        var pages = design.Pages.Count > 0 ? design.Pages : [new PageDto { Elements = design.SharedElements }];

        for (int pi = 0; pi < pages.Count; pi++)
        {
            if (pi > 0)
                sb.AppendLine("""      <text:p><text:s/></text:p>"""); // page break paragraph

            var elements = pages[pi].Elements
                .Concat(design.SharedElements.Where(s => !pages[pi].Elements.Any(e => e.Id == s.Id)))
                .Where(e => e.Hidden != true)
                .OrderBy(e => e.Y).ThenBy(e => e.X);

            foreach (var el in elements)
                sb.Append(RenderElement(el));
        }

        sb.AppendLine("""
                </office:text>
              </office:body>
            </office:document-content>
            """);

        return sb.ToString();
    }

    // ── Element renderers ─────────────────────────────────────────────────────

    private static string RenderElement(ElementDto el)
    {
        var s = el.Style ?? [];
        return el.Type switch
        {
            "text" or "link" or "number" or "date" or "pagenumber"
                => TextFrame(el, s, el.Content ?? ""),

            "richtext"
                => TextFrame(el, s, StripHtml(el.HtmlContent ?? el.Content ?? "")),

            "rect" or "shape"
                => RectFrame(el, s),

            "circle"
                => EllipseFrame(el, s),

            "line"
                => LineShape(el, s),

            "image"
                => ImageFrame(el),

            "table"
                => TableFrame(el),

            "note"
                => TextFrame(el, s, $"[{el.NoteTitle ?? "Note"}: {el.NoteBody ?? ""}]"),

            "footnote"
                => FootnoteInline(el),

            "endnote"
                => EndnoteInline(el),

            "bookmark"
                => BookmarkTag(el),

            "contentcontrol"
                => TextFrame(el, s, el.ContentControlPlaceholder ?? el.Content ?? ""),

            _ => "" // unsupported element silently skipped
        };
    }

    private static string TextFrame(ElementDto el, Dictionary<string, object> s, string text)
    {
        var x      = MmStr(el.X);
        var y      = MmStr(el.Y);
        var w      = MmStr(el.Width);
        var h      = MmStr(el.Height);
        var bg     = s.GetStr("backgroundColor", "transparent");
        var color  = s.GetStr("color", "#111827");
        var fs     = s.GetNum("fontSize", 12);
        var bold   = IsBold(s) ? "bold" : "normal";
        var italic = s.GetStr("fontStyle", "normal");
        var align  = s.GetStr("textAlign", "left");

        // Named paragraph / character style reference
        var styleAttr = string.IsNullOrWhiteSpace(el.StyleName) ? "" : $""" text:style-name="{Esc(el.StyleName)}" """;
        var charStyle  = string.IsNullOrWhiteSpace(el.CharacterStyle) ? "" : $"""<text:span text:style-name="{Esc(el.CharacterStyle)}">{Esc(text)}</text:span>""";
        var textContent = string.IsNullOrWhiteSpace(el.CharacterStyle) ? Esc(text) : charStyle;

        var bgAttr = bg is "transparent" or "" ? "" : $"""fo:background-color="{bg}" """;

        return $"""
              <draw:frame draw:name="{Esc(el.Id)}"
                  svg:x="{x}" svg:y="{y}" svg:width="{w}" svg:height="{h}"
                  text:anchor-type="page">
                <draw:text-box>
                  <text:p{styleAttr}
                      fo:text-align="{align}"
                      fo:color="{color}"
                      fo:font-size="{fs}pt"
                      fo:font-weight="{bold}"
                      fo:font-style="{italic}"
                      {bgAttr}>{textContent}</text:p>
                </draw:text-box>
              </draw:frame>
            """;
    }

    private static string RectFrame(ElementDto el, Dictionary<string, object> s)
    {
        var fill   = s.GetStr("backgroundColor", s.GetStr("fill", "transparent"));
        var stroke = s.GetStr("borderColor", "none");
        var bw     = s.GetNum("borderWidth", 0);
        var radius = s.GetNum("borderRadius", 0);
        return $"""
              <draw:rect svg:x="{MmStr(el.X)}" svg:y="{MmStr(el.Y)}"
                  svg:width="{MmStr(el.Width)}" svg:height="{MmStr(el.Height)}"
                  draw:corner-radius="{MmStr(radius)}"
                  draw:fill="solid" draw:fill-color="{fill}"
                  draw:stroke="{(bw > 0 ? "solid" : "none")}"
                  draw:stroke-color="{stroke}"
                  svg:stroke-width="{MmStr(bw)}"/>
            """;
    }

    private static string EllipseFrame(ElementDto el, Dictionary<string, object> s)
    {
        var fill   = s.GetStr("backgroundColor", s.GetStr("fill", "transparent"));
        var stroke = s.GetStr("borderColor", "none");
        var bw     = s.GetNum("borderWidth", 0);
        return $"""
              <draw:ellipse svg:x="{MmStr(el.X)}" svg:y="{MmStr(el.Y)}"
                  svg:width="{MmStr(el.Width)}" svg:height="{MmStr(el.Height)}"
                  draw:fill="solid" draw:fill-color="{fill}"
                  draw:stroke="{(bw > 0 ? "solid" : "none")}"
                  draw:stroke-color="{stroke}"
                  svg:stroke-width="{MmStr(bw)}"/>
            """;
    }

    private static string LineShape(ElementDto el, Dictionary<string, object> s)
    {
        var color = s.GetStr("backgroundColor", "#9ca3af");
        var bw    = s.GetNum("borderWidth", el.Height > 0 ? el.Height : 1);
        return $"""
              <draw:line svg:x1="{MmStr(el.X)}" svg:y1="{MmStr(el.Y + el.Height / 2)}"
                  svg:x2="{MmStr(el.X + el.Width)}" svg:y2="{MmStr(el.Y + el.Height / 2)}"
                  draw:stroke="solid" draw:stroke-color="{color}"
                  svg:stroke-width="{MmStr(bw)}"/>
            """;
    }

    private static string ImageFrame(ElementDto el)
    {
        var src = el.Content ?? "";
        if (string.IsNullOrWhiteSpace(src)) return "";

        // Inline base64 images use xlink:href with data URI.
        var href = src.StartsWith("data:") ? src : Esc(src);
        return $"""
              <draw:frame draw:name="{Esc(el.Id)}"
                  svg:x="{MmStr(el.X)}" svg:y="{MmStr(el.Y)}"
                  svg:width="{MmStr(el.Width)}" svg:height="{MmStr(el.Height)}"
                  text:anchor-type="page">
                <draw:image xlink:href="{href}" xlink:type="simple" xlink:show="embed" xlink:actuate="onLoad"/>
              </draw:frame>
            """;
    }

    private static bool HasCellBorderOrFill(CellStyleDto cs) =>
        cs.BackgroundColor != null || cs.Padding != null || cs.BorderColor != null || cs.BorderWidth != null
        || cs.BorderTop != null || cs.BorderRight != null || cs.BorderBottom != null || cs.BorderLeft != null;

    private static string CellStyleName(string elementId, int r, int c)
    {
        var safe = new string((elementId ?? "t").Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
        return $"tc_{safe}_{r}_{c}";
    }

    // Emit one <style:style family="table-cell"> per styled cell into office:automatic-styles. ODF requires
    // cell background/borders to be referenced by name (not inlined), so they are collected up-front.
    private static string BuildCellStyleDefs(DesignExportDto design)
    {
        var sb = new StringBuilder();
        var elements = design.Pages.SelectMany(p => p.Elements).Concat(design.SharedElements);
        foreach (var el in elements.Where(e => e.Type == "table" && e.CellStyles is { Length: > 0 }))
            foreach (var cs in el.CellStyles!)
            {
                if (!HasCellBorderOrFill(cs)) continue;
                var props = new StringBuilder();
                if (cs.BackgroundColor is { } bg) props.Append($" fo:background-color=\"{bg}\"");
                if (cs.Padding is { } p) props.Append($" fo:padding=\"{p.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}pt\"");
                AppendOdtBorder(props, "fo:border-top", cs.BorderTop, cs);
                AppendOdtBorder(props, "fo:border-right", cs.BorderRight, cs);
                AppendOdtBorder(props, "fo:border-bottom", cs.BorderBottom, cs);
                AppendOdtBorder(props, "fo:border-left", cs.BorderLeft, cs);
                sb.AppendLine($"""    <style:style style:name="{CellStyleName(el.Id, cs.Row, cs.Col)}" style:family="table-cell"><style:table-cell-properties{props}/></style:style>""");
            }
        return sb.ToString();
    }

    private static void AppendOdtBorder(StringBuilder props, string attr, CellBorderSideDto? side, CellStyleDto cs)
    {
        var hasUniform = cs.BorderColor != null || cs.BorderWidth != null;
        if (side is null && !hasUniform) return;            // unset side stays at default (no explicit border)
        var width = side?.Width ?? cs.BorderWidth ?? 1;
        var color = side?.Color ?? cs.BorderColor ?? "#000000";
        var w = width.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        props.Append($" {attr}=\"{w}pt solid {color}\"");
    }

    private static string TableFrame(ElementDto el)
    {
        var cellData = el.CellData;
        if (cellData is null || cellData.Length == 0) return "";

        var sb   = new StringBuilder();
        var cols  = cellData[0]?.Length ?? 1;
        var colW  = el.Width / Math.Max(cols, 1);

        sb.AppendLine($"""      <draw:frame svg:x="{MmStr(el.X)}" svg:y="{MmStr(el.Y)}" svg:width="{MmStr(el.Width)}" svg:height="{MmStr(el.Height)}" text:anchor-type="page">""");
        sb.AppendLine("        <draw:text-box>");
        sb.AppendLine($"""          <table:table xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0">""");

        for (int c = 0; c < cols; c++)
            sb.AppendLine($"""            <table:table-column table:style-name="col{c}" style:column-width="{MmStr(colW)}"/>""");

        var cellStyleLookup = (el.CellStyles ?? []).GroupBy(x => (x.Row, x.Col)).ToDictionary(gp => gp.Key, gp => gp.First());
        for (int r = 0; r < cellData.Length; r++)
        {
            var row   = cellData[r] ?? [];
            var isHdr = el.HeaderRow == true && r == 0;
            sb.AppendLine("            <table:table-row>");
            for (int c = 0; c < cols; c++)
            {
                var cell = row.Length > c ? Esc(row[c] ?? "") : "";
                var cs   = cellStyleLookup.GetValueOrDefault((r, c));
                var font = new StringBuilder();
                if (isHdr || cs?.Bold == true) font.Append("fo:font-weight=\"bold\" ");
                if (cs?.Italic == true) font.Append("fo:font-style=\"italic\" ");
                if (cs?.Color is { } fc) font.Append($"fo:color=\"{fc}\" ");
                if (cs?.FontSize is { } fz) font.Append($"fo:font-size=\"{fz.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}pt\" ");
                var bold = font.ToString().TrimEnd();
                var cellAttr = cs is not null && HasCellBorderOrFill(cs)
                    ? $" table:style-name=\"{CellStyleName(el.Id, r, c)}\"" : "";
                var pAttr = cs?.TextAlign switch
                {
                    "center" => " text:style-name=\"pAlignCenter\"",
                    "right"  => " text:style-name=\"pAlignRight\"",
                    "left"   => " text:style-name=\"pAlignLeft\"",
                    _        => ""
                };
                sb.AppendLine($"""              <table:table-cell{cellAttr}><text:p{pAttr} {bold}>{cell}</text:p></table:table-cell>""");
            }
            sb.AppendLine("            </table:table-row>");
        }

        sb.AppendLine("          </table:table>");
        sb.AppendLine("        </draw:text-box>");
        sb.AppendLine("      </draw:frame>");
        return sb.ToString();
    }

    private static string FootnoteInline(ElementDto el)
    {
        var text = Esc(el.FootnoteText ?? el.Content ?? "");
        return $"""
              <text:p>
                <text:note text:note-class="footnote">
                  <text:note-citation/>
                  <text:note-body><text:p>{text}</text:p></text:note-body>
                </text:note>
              </text:p>
            """;
    }

    private static string EndnoteInline(ElementDto el)
    {
        var text = Esc(el.FootnoteText ?? el.Content ?? "");
        return $"""
              <text:p>
                <text:note text:note-class="endnote">
                  <text:note-citation/>
                  <text:note-body><text:p>{text}</text:p></text:note-body>
                </text:note>
              </text:p>
            """;
    }

    private static string BookmarkTag(ElementDto el)
    {
        var name = Esc(el.BookmarkName ?? el.Name ?? el.Id);
        var text = Esc(el.Content ?? "");
        return $"""
              <text:p><text:bookmark-start text:name="{name}"/>{text}<text:bookmark-end text:name="{name}"/></text:p>
            """;
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static string MmStr(double px)
    {
        // PXA uses 96 dpi pixels; 1 inch = 25.4 mm; 1 px ≈ 0.2646 mm
        var mm = px * 25.4 / 96.0;
        return $"{mm:F3}mm";
    }

    private static string Esc(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", "");
    }

    private static bool IsBold(Dictionary<string, object> s)
    {
        var fw = s.GetStr("fontWeight", "normal");
        return fw is "bold" or "700" or "800" or "900";
    }

    private static void WriteUncompressed(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteXml(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }
}
