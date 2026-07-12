using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace PXA.WebApi.Services.Converters;

/// <summary>
/// Base for spreadsheet-library code converters (→ PXA spreadsheet API). Marks <see cref="Kind"/> as
/// "spreadsheet" and renders the preview as an <b>HTML grid</b> (UTF-8 bytes): it replays the converted
/// <c>PxaWorkbook</c> calls into a sparse cell model and emits a styled <c>&lt;table&gt;</c>. (The migrated
/// user code can't be executed here; this mirrors how the PDF converters replay recognizable draw calls.)
/// </summary>
public abstract class BaseSpreadsheetConverter : BasePdfConverter
{
    public override string Kind => "spreadsheet";

    public override byte[] GeneratePreview(string sourceCode)
    {
        var converted = ConvertCode(sourceCode);
        return Encoding.UTF8.GetBytes(RenderHtml(converted, FrameworkName));
    }

    // ── replay the converted PxaWorkbook calls into a grid ─────────────────────────────────────────
    private static string RenderHtml(string code, string frameworkName)
    {
        var sheets = ReplaySpreadsheetCalls(code);
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html><head><meta charset=\"utf-8\"><style>")
          .Append("body{font:13px/1.4 -apple-system,Segoe UI,Roboto,sans-serif;margin:0;padding:16px;color:#0f172a;background:#fff}")
          .Append("h2{font-size:14px;margin:0 0 4px}.sub{color:#64748b;font-size:12px;margin:0 0 12px}")
          .Append(".sheet{margin-bottom:20px}.sname{font-weight:600;font-size:12px;color:#334155;margin:0 0 4px}")
          .Append("table{border-collapse:collapse}td,th{border:1px solid #e2e8f0;padding:4px 8px;min-width:48px;text-align:left}")
          .Append("th{background:#f8fafc;color:#64748b;font-weight:600;text-align:center}")
          .Append(".f{color:#2563eb}.empty{color:#94a3b8}</style></head><body>")
          .Append($"<h2>{WebUtility.HtmlEncode(frameworkName)} → PXA spreadsheet</h2>")
          .Append("<p class=\"sub\">Preview of the converted workbook (recognized cells replayed from the code panel).</p>");

        if (sheets.Count == 0 || sheets.All(s => s.Cells.Count == 0))
        {
            sb.Append("<p class=\"empty\">No cell writes were recognized to preview — see the converted code in the code panel.</p>");
            return sb.Append("</body></html>").ToString();
        }

        foreach (var sheet in sheets)
        {
            if (sheet.Cells.Count == 0) continue;
            var maxRow = sheet.Cells.Keys.Max(k => k.row);
            var maxCol = sheet.Cells.Keys.Max(k => k.col);
            sb.Append("<div class=\"sheet\"><div class=\"sname\">")
              .Append(WebUtility.HtmlEncode(sheet.Name)).Append("</div><table><tr><th></th>");
            for (var c = 0; c <= maxCol; c++) sb.Append("<th>").Append(ColName(c)).Append("</th>");
            sb.Append("</tr>");
            for (var r = 0; r <= maxRow; r++)
            {
                sb.Append("<tr><th>").Append(r + 1).Append("</th>");
                for (var c = 0; c <= maxCol; c++)
                {
                    sheet.Cells.TryGetValue((r, c), out var cell);
                    var cls = cell?.IsFormula == true ? " class=\"f\"" : "";
                    sb.Append("<td").Append(cls).Append('>')
                      .Append(WebUtility.HtmlEncode(cell?.Text ?? "")).Append("</td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</table></div>");
        }
        return sb.Append("</body></html>").ToString();
    }

    private sealed record CellVal(string Text, bool IsFormula);
    private sealed class Sheet
    {
        public string Name = "Sheet1";
        public Dictionary<(int row, int col), CellVal> Cells = new();
    }

    private static List<Sheet> ReplaySpreadsheetCalls(string code)
    {
        var sheets = new List<Sheet>();
        Sheet current() { if (sheets.Count == 0) sheets.Add(new Sheet()); return sheets[^1]; }

        // wb.AddSheet("Name")  → start a new sheet
        foreach (Match m in Regex.Matches(code, @"\.AddSheet\(\s*""([^""]*)""\s*\)"))
            sheets.Add(new Sheet { Name = m.Groups[1].Value.Length > 0 ? m.Groups[1].Value : $"Sheet{sheets.Count + 1}" });

        // .Cell("A1").Value(x) / .Formula("=..")  and  .Cell(r, c).Value(x)
        foreach (Match m in Regex.Matches(code,
            @"\.Cell\(\s*(?:""([A-Za-z]+\d+)""|(\d+)\s*,\s*(\d+))\s*\)\s*\.(Value|Formula)\(\s*(""(?:[^""\\]|\\.)*""|[^)]*?)\s*\)"))
        {
            int row, col;
            if (m.Groups[1].Success) (row, col) = ParseA1(m.Groups[1].Value);
            else { row = int.Parse(m.Groups[2].Value); col = int.Parse(m.Groups[3].Value); }
            var isFormula = m.Groups[4].Value == "Formula";
            var raw = m.Groups[5].Value.Trim();
            var text = raw.StartsWith('"') && raw.EndsWith('"') && raw.Length >= 2
                ? raw[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\")
                : raw;
            current().Cells[(row, col)] = new CellVal(text, isFormula);
        }
        return sheets;
    }

    private static (int row, int col) ParseA1(string a1)
    {
        var i = 0;
        var col = 0;
        while (i < a1.Length && char.IsLetter(a1[i])) { col = col * 26 + (char.ToUpperInvariant(a1[i]) - 'A' + 1); i++; }
        var row = int.TryParse(a1[i..], out var r) ? r - 1 : 0;
        return (row, Math.Max(0, col - 1));
    }

    private static string ColName(int index)
    {
        var s = "";
        var n = index + 1;
        while (n > 0) { var rem = (n - 1) % 26; s = (char)('A' + rem) + s; n = (n - 1) / 26; }
        return s;
    }
}
