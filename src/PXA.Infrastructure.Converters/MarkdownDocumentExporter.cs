using System.Text;
using System.Text.RegularExpressions;
using PXA.Core.Abstractions;
using PXA.Core.Contracts;
using PXA.Core.Primitives;

namespace PXA.Infrastructure.Converters;

public sealed class MarkdownDocumentExporter : DocumentExporter
{
    public string FormatKey     => "md";
    public string MimeType      => "text/markdown; charset=utf-8";
    public string FileExtension => ".md";
    public IExporterCapabilities Capabilities => new ExporterCapabilities(SupportsImages: false, SupportsFormFields: false);

    public byte[] Export(DesignExportDto design)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {design.Name}");
        sb.AppendLine();

        var plannedPages = DesignLayoutPlanner.BuildPages(design);

        for (int i = 0; i < plannedPages.Count; i++)
        {
            if (i > 0)
            {
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine($"<!-- Page {i + 1} -->");
                sb.AppendLine("---");
                sb.AppendLine();
            }

            var allElements = plannedPages[i].Elements;

            foreach (var el in allElements)
                RenderElement(sb, el);
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static void RenderElement(StringBuilder sb, ElementDto el)
    {
        var s = el.Style ?? [];

        switch (el.Type)
        {
            case "text":
            {
                var fs = s.GetNum("fontSize", 14);
                var fw = s.GetStr("fontWeight", "normal");
                var text = el.Content ?? "";
                var bold = fw is "bold" or "700" or "800" or "900";

                if (fs >= 24)
                    sb.AppendLine($"## {text}");
                else if (fs >= 18)
                    sb.AppendLine($"### {text}");
                else if (bold)
                    sb.AppendLine($"**{text}**");
                else
                    sb.AppendLine(text);
                break;
            }

            case "richtext":
            {
                var md = HtmlToMarkdown(el.HtmlContent ?? "");
                sb.AppendLine(md);
                break;
            }

            case "link":
            {
                var text = el.Content ?? el.Href ?? "";
                var href = el.Href ?? "#";
                sb.AppendLine($"[{text}]({href})");
                break;
            }

            case "button":
            {
                if (!string.IsNullOrWhiteSpace(el.ButtonAction))
                    sb.AppendLine($"[{el.Content ?? "Button"}]({el.ButtonAction})");
                else
                    sb.AppendLine($"**{el.Content ?? "Button"}**");
                break;
            }

            case "image":
            {
                var alt = el.Name ?? "image";
                var src = el.Content ?? "";
                sb.AppendLine($"![{alt}]({src})");
                break;
            }

            case "table":
                RenderTable(sb, el);
                break;

            case "optionlist":
            {
                var opts   = el.Options ?? [];
                var style  = el.ListStyle ?? (el.Ordered == true ? "decimal" : "disc");
                var isOrdered = style is "decimal" or "lower-alpha" or "upper-alpha" or "lower-roman" or "upper-roman";
                for (int i = 0; i < opts.Length; i++)
                    sb.AppendLine(isOrdered ? $"{i + 1}. {opts[i]}" : $"- {opts[i]}");
                break;
            }

            case "checkbox":
            {
                var label   = el.FieldLabel ?? "";
                var checked_ = el.CheckState is "checked" or "dot";
                sb.AppendLine($"- [{(checked_ ? "x" : " ")}] {label}");
                break;
            }

            case "line":
            case "pageboundary":
                sb.AppendLine("---");
                break;

            case "note":
            {
                var title = el.NoteTitle ?? "Note";
                var body  = el.NoteBody ?? "";
                sb.AppendLine($"> **{title}**");
                if (!string.IsNullOrWhiteSpace(body))
                    sb.AppendLine($"> {body}");
                break;
            }

            case "number":
                sb.AppendLine(el.NumberValue?.ToString() ?? "");
                break;

            case "signature":
            {
                var label = el.SignatureLabel ?? "Signature";
                sb.AppendLine($"*{label}:* _______________");
                break;
            }

            case "field":
            {
                var label = el.FieldLabel ?? "";
                var req   = el.Required == true ? " *(required)*" : "";
                sb.AppendLine($"**{label}**{req}: _______________");
                break;
            }

            default:
                if (!string.IsNullOrWhiteSpace(el.Content))
                    sb.AppendLine($"<!-- {el.Type}: {el.Content} -->");
                break;
        }

        sb.AppendLine();
    }

    private static void RenderTable(StringBuilder sb, ElementDto el)
    {
        var cellData = el.CellData;
        if (cellData is null || cellData.Length == 0) return;

        var cols     = cellData[0]?.Length ?? 0;
        var hasHdr   = el.HeaderRow == true;
        var aligns   = el.ColumnAlignments ?? [];

        for (int r = 0; r < cellData.Length; r++)
        {
            var row = cellData[r] ?? [];
            sb.Append("| ");
            sb.Append(string.Join(" | ", Enumerable.Range(0, cols).Select(c => row.Length > c ? row[c] ?? "" : "")));
            sb.AppendLine(" |");

            if (hasHdr && r == 0)
            {
                sb.Append("| ");
                sb.Append(string.Join(" | ", Enumerable.Range(0, cols).Select(c =>
                {
                    var al = aligns.Length > c ? aligns[c] : "left";
                    return al switch { "center" => ":---:", "right" => "---:", _ => "---" };
                })));
                sb.AppendLine(" |");
            }
        }
    }

    private static string HtmlToMarkdown(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var md = html;
        md = Regex.Replace(md, @"<strong[^>]*>(.*?)</strong>", "**$1**", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        md = Regex.Replace(md, @"<b[^>]*>(.*?)</b>",           "**$1**", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        md = Regex.Replace(md, @"<em[^>]*>(.*?)</em>",         "*$1*",   RegexOptions.IgnoreCase | RegexOptions.Singleline);
        md = Regex.Replace(md, @"<i[^>]*>(.*?)</i>",           "*$1*",   RegexOptions.IgnoreCase | RegexOptions.Singleline);
        md = Regex.Replace(md, @"<br\s*/?>",                   "\n",     RegexOptions.IgnoreCase);
        md = Regex.Replace(md, @"<p[^>]*>(.*?)</p>",           "$1\n",   RegexOptions.IgnoreCase | RegexOptions.Singleline);
        md = Regex.Replace(md, @"<[^>]+>", "");
        return md.Trim();
    }
}
