using System.Text;
using Canvas.Core.Abstractions;
using Canvas.Core.Contracts;
using Canvas.Core.Primitives;

namespace Canvas.Infrastructure.Converters;

public sealed class HtmlDocumentExporter : IDocumentExporter
{
    public string FormatKey    => "html";
    public string MimeType     => "text/html; charset=utf-8";
    public string FileExtension => ".html";
    public IExporterCapabilities Capabilities => new ExporterCapabilities();

    public byte[] Export(DesignExportDto design)
    {
        var sb = new StringBuilder();
        var ps = design.PageSettings ?? new PageSettingsDto();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine($"  <meta charset=\"utf-8\">");
        sb.AppendLine($"  <title>{Esc(design.PageSettings?.Metadata?.Title ?? design.Name)}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }");
        sb.AppendLine("    body { font-family: Arial, Helvetica, sans-serif; background: #e5e7eb; }");
        sb.AppendLine("    .canvas-page { position: relative; background: #fff; margin: 20px auto; box-shadow: 0 4px 24px rgba(0,0,0,.15); page-break-after: always; }");
        sb.AppendLine("    .canvas-page:last-child { page-break-after: avoid; }");
        sb.AppendLine("    @media print { body { background: #fff; } .canvas-page { margin: 0; box-shadow: none; } }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        var plannedPages = DesignLayoutPlanner.BuildPages(design);

        foreach (var page in plannedPages)
        {
            var bgColor = ps.BackgroundColor ?? "#ffffff";
            sb.AppendLine($"  <div class=\"canvas-page\" style=\"width:{ps.Width}px; height:{ps.Height}px; background:{bgColor}\">");

            foreach (var el in page.Elements)
                RenderElement(sb, el);

            sb.AppendLine("  </div>");
        }

        // Render any QR code placeholders using qrcode.js (loaded from CDN)
        var hasQr = plannedPages.SelectMany(p => p.Elements)
            .Any(e => e.Type == "qrcode" && e.Hidden != true);
        if (hasQr)
        {
            sb.AppendLine("  <script src=\"https://cdn.jsdelivr.net/npm/qrcode/build/qrcode.min.js\"></script>");
            sb.AppendLine("  <script>");
            sb.AppendLine("    document.querySelectorAll('[data-qr-value]').forEach(function(el) {");
            sb.AppendLine("      var val = el.getAttribute('data-qr-value');");
            sb.AppendLine("      if (!val) return;");
            sb.AppendLine("      el.innerHTML = '';");
            sb.AppendLine("      var canvas = document.createElement('canvas');");
            sb.AppendLine("      el.appendChild(canvas);");
            sb.AppendLine("      QRCode.toCanvas(canvas, val, { width: Math.min(el.offsetWidth, el.offsetHeight) });");
            sb.AppendLine("    });");
            sb.AppendLine("  </script>");
        }
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static void RenderElement(StringBuilder sb, ElementDto el)
    {
        var s = el.Style ?? [];
        var posStyle = $"position:absolute; left:{el.X}px; top:{el.Y}px; width:{el.Width}px; height:{el.Height}px; overflow:hidden;";
        var rotation = s.TryGetValue("rotation", out var rot) && rot is not null ? $" transform:rotate({rot}deg); transform-origin:center;" : "";

        switch (el.Type)
        {
            case "text":
            {
                var textStyle = BuildTextStyle(s);
                sb.AppendLine($"    <div style=\"{posStyle}{rotation}{textStyle}\">{Esc(el.Content ?? "")}</div>");
                break;
            }

            case "richtext":
                sb.AppendLine($"    <div style=\"{posStyle}{rotation} overflow:hidden;\">{el.HtmlContent ?? ""}</div>");
                break;

            case "link":
            {
                var textStyle = BuildTextStyle(s);
                var href = Esc(el.Href ?? "#");
                var target = el.LinkTarget ?? "_blank";
                sb.AppendLine($"    <a href=\"{href}\" target=\"{target}\" rel=\"noopener noreferrer\" style=\"{posStyle}{rotation}{textStyle} text-decoration:underline; color:{s.GetStr("color", "#2563eb")};\">{Esc(el.Content ?? href)}</a>");
                break;
            }

            case "button":
            {
                var href = el.ButtonAction ?? "#";
                var bg   = s.GetStr("backgroundColor", "#3b82f6");
                var color = s.GetStr("color", "#ffffff");
                var fs   = s.GetNum("fontSize", 14);
                var br   = s.GetNum("borderRadius", 4);
                sb.AppendLine($"    <a href=\"{Esc(href)}\" target=\"_blank\" rel=\"noopener noreferrer\" class=\"btn\" style=\"{posStyle}{rotation} display:flex; align-items:center; justify-content:center; background:{bg}; color:{color}; font-size:{fs}px; border-radius:{br}px; text-decoration:none;\">{Esc(el.Content ?? "Button")}</a>");
                break;
            }

            case "rect":
            case "shape":
            {
                var bg = s.GetStr("backgroundColor", s.GetStr("fill", "transparent"));
                var br = s.GetNum("borderRadius", 0);
                var border = BuildBorderStyle(s);
                sb.AppendLine($"    <div style=\"{posStyle}{rotation} background:{bg}; border-radius:{br}px;{border}\"></div>");
                break;
            }

            case "circle":
            {
                var bg = s.GetStr("backgroundColor", s.GetStr("fill", "transparent"));
                var border = BuildBorderStyle(s);
                sb.AppendLine($"    <div style=\"{posStyle}{rotation} background:{bg}; border-radius:50%;{border}\"></div>");
                break;
            }

            case "line":
            {
                var color = s.GetStr("backgroundColor", "#9ca3af");
                sb.AppendLine($"    <div style=\"{posStyle}{rotation} background:{color};\"></div>");
                break;
            }

            case "image":
            {
                var src    = Esc(el.Content ?? "");
                var fit    = el.FitMode ?? "contain";
                sb.AppendLine($"    <img src=\"{src}\" alt=\"\" style=\"{posStyle}{rotation} object-fit:{fit};\">");
                break;
            }

            case "table":
                RenderTable(sb, el, posStyle, rotation);
                break;

            case "qrcode":
                sb.AppendLine($"    <div data-qr-value=\"{Esc(el.QrValue ?? "")}\" style=\"{posStyle}{rotation} display:flex; align-items:center; justify-content:center; background:#f8fafc; border:1px solid #e2e8f0;\"><span style=\"font-size:10px; color:#6b7280;\">QR: {Esc(el.QrValue ?? "")}</span></div>");
                break;

            case "field":
            {
                var label = Esc(el.FieldLabel ?? "");
                var req   = el.Required == true ? " *" : "";
                sb.AppendLine($"    <div style=\"{posStyle}{rotation} padding:4px; border:1px solid #93c5fd; background:#eff6ff;\"><label style=\"font-size:11px; font-weight:600; color:#1d4ed8; display:block; margin-bottom:2px;\">{label}{req}</label><input type=\"text\" name=\"{Esc(el.FieldName ?? el.Id)}\" style=\"width:100%; border:1px solid #bfdbfe; border-radius:2px; padding:2px 4px;\"></div>");
                break;
            }

            case "checkbox":
            {
                var label = Esc(el.FieldLabel ?? "");
                sb.AppendLine($"    <div style=\"{posStyle}{rotation} display:flex; align-items:center; gap:6px;\"><input type=\"checkbox\" name=\"{Esc(el.FieldName ?? el.Id)}\"><label>{label}</label></div>");
                break;
            }

            case "signature":
            {
                var label = Esc(el.SignatureLabel ?? "Signature");
                sb.AppendLine($"    <div style=\"{posStyle}{rotation} display:flex; flex-direction:column; justify-content:flex-end; padding:4px;\"><div style=\"border-bottom:1px solid #111827; width:100%; margin-bottom:2px;\"></div><span style=\"font-size:10px; color:#6b7280;\">{label}</span></div>");
                break;
            }

            case "note":
            {
                var bg    = s.GetStr("backgroundColor", "#fef3c7");
                var color = s.GetStr("color", "#78350f");
                var title = Esc(el.NoteTitle ?? "Note");
                var body  = Esc(el.NoteBody ?? "");
                sb.AppendLine($"    <div style=\"{posStyle}{rotation} background:{bg}; color:{color}; padding:8px; overflow:auto;\"><strong style=\"display:block; margin-bottom:4px;\">{title}</strong><span>{body}</span></div>");
                break;
            }

            default:
                sb.AppendLine($"    <!-- {el.Type} not yet supported in HTML export -->");
                break;
        }
    }

    private static void RenderTable(StringBuilder sb, ElementDto el, string posStyle, string rotation)
    {
        var s        = el.Style ?? [];
        var bw       = (int)s.GetNum("borderWidth", 1);
        var bc       = s.GetStr("borderColor", "#000000");
        var hasHeader = el.HeaderRow == true;
        var headerBg = el.HeaderBgColor ?? "#f1f5f9";
        var cellData = el.CellData ?? [];
        var rows     = cellData.Length > 0 ? cellData.Length : (int)s.GetNum("rows", 3);
        var cols     = cellData.Length > 0 ? (cellData[0]?.Length ?? 0) : (int)s.GetNum("columns", 3);

        var zebraStyle = el.ZebraEnabled == true ? el.ZebraColor ?? "#f9fafb" : null;

        sb.AppendLine($"    <table style=\"{posStyle}{rotation} border-collapse:collapse; table-layout:fixed;\">");

        for (int r = 0; r < rows; r++)
        {
            var isHeader = hasHeader && r == 0;
            var rowBg    = !isHeader && zebraStyle != null && r % 2 == 1 ? $" background:{zebraStyle};" : "";
            var tag      = isHeader ? "th" : "td";
            var hdrStyle = isHeader ? $" background:{headerBg}; font-weight:bold;" : "";
            sb.Append($"      <tr style=\"{rowBg}\">");
            for (int c = 0; c < cols; c++)
            {
                var cell = cellData.Length > r ? (cellData[r]?.Length > c ? cellData[r][c] : "") : "";
                var align = el.ColumnAlignments?.Length > c ? el.ColumnAlignments[c] : "left";
                sb.Append($"<{tag} style=\"border:{bw}px solid {bc}; padding:4px; text-align:{align};{hdrStyle}\">{Esc(cell ?? "")}</{tag}>");
            }
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("    </table>");
    }

    private static string BuildTextStyle(Dictionary<string, object> s)
    {
        var fs    = s.GetNum("fontSize", 14);
        var ff    = s.GetStr("fontFamily", "Arial, sans-serif");
        var color = s.GetStr("color", "#111827");
        var fw    = s.GetStr("fontWeight", "normal");
        var fi    = s.GetStr("fontStyle", "normal");
        var td    = s.GetStr("textDecoration", "none");
        var ta    = s.GetStr("textAlign", "left");
        var lh    = s.GetNum("lineHeight", 1.4);
        return $" font-size:{fs}px; font-family:{ff}; color:{color}; font-weight:{fw}; font-style:{fi}; text-decoration:{td}; text-align:{ta}; line-height:{lh}; white-space:pre-wrap; word-break:break-word;";
    }

    private static string BuildBorderStyle(Dictionary<string, object> s)
    {
        var bw = s.GetNum("borderWidth", 0);
        if (bw <= 0) return "";
        var bc = s.GetStr("borderColor", "#000000");
        var bs = s.GetStr("borderStyle", "solid");
        return $" border:{bw}px {bs} {bc};";
    }

    private static string Esc(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}

