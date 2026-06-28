using System.Globalization;
using System.Text;
using System.Text.Json;
using Canvas.Core.Contracts;

namespace Canvas.Infrastructure.Spreadsheet;

/// <summary>Server-side delimited text (RFC 4180 CSV, or TSV with a tab delimiter) for the spreadsheet
/// model. Exports a sheet's values (formula cells use their cached value); imports into a single sheet with
/// number/text detection.</summary>
public static class CsvSheetIo
{
    public static string ToCsv(SheetDto sheet, char delimiter = ',')
    {
        int maxRow = -1, maxCol = -1;
        foreach (var c in sheet.Cells) { if (c.Row > maxRow) maxRow = c.Row; if (c.Col > maxCol) maxCol = c.Col; }
        if (maxRow < 0) return "";

        var byPos = sheet.Cells.ToDictionary(c => (c.Row, c.Col));
        var sb = new StringBuilder();
        for (var r = 0; r <= maxRow; r++)
        {
            for (var c = 0; c <= maxCol; c++)
            {
                if (c > 0) sb.Append(delimiter);
                if (byPos.TryGetValue((r, c), out var cell)) sb.Append(Field(ValueToString(cell.Value), delimiter));
            }
            if (r < maxRow) sb.Append("\r\n");
        }
        return sb.ToString();
    }

    public static SheetDto FromCsv(string text, string name = "Sheet1", char delimiter = ',')
    {
        var rows = Parse(text, delimiter);
        var sheet = new SheetDto { Id = Guid.NewGuid().ToString("n"), Name = name };
        for (var r = 0; r < rows.Count; r++)
            for (var c = 0; c < rows[r].Count; c++)
            {
                var v = rows[r][c];
                if (v.Length == 0) continue;
                var isNum = double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var n);
                sheet.Cells.Add(new CellDto { Row = r, Col = c, Type = isNum ? "number" : "text", Value = isNum ? n : v });
            }
        sheet.RowCount = Math.Max(100, rows.Count);
        sheet.ColCount = Math.Max(26, rows.Count > 0 ? rows.Max(r => r.Count) : 0);
        return sheet;
    }

    private static string Field(string v, char delimiter) =>
        v.IndexOfAny([delimiter, '"', '\n', '\r']) >= 0 ? $"\"{v.Replace("\"", "\"\"")}\"" : v;

    private static List<List<string>> Parse(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var any = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            any = true;
            if (inQuotes)
            {
                if (ch == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; } else inQuotes = false; }
                else field.Append(ch);
            }
            else if (ch == '"') inQuotes = true;
            else if (ch == delimiter) { row.Add(field.ToString()); field.Clear(); }
            else if (ch == '\r') { /* handled by \n */ }
            else if (ch == '\n') { row.Add(field.ToString()); field.Clear(); rows.Add(row); row = []; }
            else field.Append(ch);
        }
        if (any && (field.Length > 0 || row.Count > 0)) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }

    private static string ValueToString(object? value) => value switch
    {
        null => "",
        JsonElement je => je.ValueKind switch { JsonValueKind.Null or JsonValueKind.Undefined => "", JsonValueKind.String => je.GetString() ?? "", _ => je.ToString() },
        _ => value.ToString() ?? "",
    };
}
