using System.Text.Json;
using Canvas.Core.Contracts;

namespace Canvas.Infrastructure.Spreadsheet;

/// <summary>
/// Data-driven sheet building: turn JSON rows into a sheet (header + typed cells — the DataTable
/// equivalent), and fill a template sheet's <c>{{token}}</c> placeholders from a data object
/// (smart-marker-style templating).
/// </summary>
public sealed class SpreadsheetData
{
    /// <summary>Builds a sheet from JSON row objects: a bold header row (union of keys, first-seen order)
    /// plus one typed row per object.</summary>
    public SheetDto FromRows(IReadOnlyList<Dictionary<string, JsonElement>> rows, string name = "Sheet1")
    {
        var sheet = new SheetDto { Id = Guid.NewGuid().ToString("n"), Name = name };
        if (rows.Count == 0) return sheet;

        var keys = new List<string>();
        foreach (var r in rows)
            foreach (var k in r.Keys)
                if (!keys.Contains(k)) keys.Add(k);

        for (var c = 0; c < keys.Count; c++)
            sheet.Cells.Add(new CellDto { Row = 0, Col = c, Type = "text", Value = keys[c], Style = new CellStyleDto { Bold = true } });

        for (var r = 0; r < rows.Count; r++)
            for (var c = 0; c < keys.Count; c++)
                if (rows[r].TryGetValue(keys[c], out var je) && FromJson(je) is { Type: not "empty" } cell)
                {
                    cell.Row = r + 1;
                    cell.Col = c;
                    sheet.Cells.Add(cell);
                }

        sheet.RowCount = Math.Max(100, rows.Count + 1);
        sheet.ColCount = Math.Max(26, keys.Count);
        return sheet;
    }

    /// <summary>Replaces <c>{{key}}</c> placeholders in text cells across the workbook with values from
    /// <paramref name="data"/> (dotted keys resolve nested objects). Returns the number of cells changed.</summary>
    public int Fill(SpreadsheetDto workbook, Dictionary<string, JsonElement> data)
    {
        var count = 0;
        foreach (var sheet in workbook.Sheets)
            foreach (var cell in sheet.Cells)
            {
                if (cell.Type != "text") continue;
                var text = AsString(cell.Value);
                if (text is null || !text.Contains("{{")) continue;
                var filled = System.Text.RegularExpressions.Regex.Replace(text, @"\{\{\s*([A-Za-z_][\w.]*)\s*\}\}",
                    m => Resolve(data, m.Groups[1].Value) ?? m.Value);
                if (filled != text) { cell.Value = filled; count++; }
            }
        return count;
    }

    private static string? Resolve(Dictionary<string, JsonElement> data, string path)
    {
        var parts = path.Split('.');
        if (!data.TryGetValue(parts[0], out var cur)) return null;
        for (var i = 1; i < parts.Length; i++)
        {
            if (cur.ValueKind != JsonValueKind.Object || !cur.TryGetProperty(parts[i], out var next)) return null;
            cur = next;
        }
        return cur.ValueKind == JsonValueKind.String ? cur.GetString() : cur.ToString();
    }

    private static CellDto FromJson(JsonElement je) => je.ValueKind switch
    {
        JsonValueKind.Number => new CellDto { Type = "number", Value = je.GetDouble() },
        JsonValueKind.True => new CellDto { Type = "boolean", Value = true },
        JsonValueKind.False => new CellDto { Type = "boolean", Value = false },
        JsonValueKind.String => je.GetString() is { Length: > 0 } s ? new CellDto { Type = "text", Value = s } : new CellDto { Type = "empty" },
        _ => new CellDto { Type = "empty" },
    };

    private static string? AsString(object? v) => v switch
    {
        string s => s,
        JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString(),
        _ => null,
    };
}
