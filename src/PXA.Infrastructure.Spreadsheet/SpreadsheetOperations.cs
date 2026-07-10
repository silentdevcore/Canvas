using System.Globalization;
using PXA.Core.Contracts;
using PXA.Core.Primitives;

namespace PXA.Infrastructure.Spreadsheet;

/// <summary>Pure-model spreadsheet operations (no ClosedXML): sort a range, find/replace across cells.</summary>
public sealed class SpreadsheetOperations
{
    /// <summary>Sorts the rows of an A1 range by a key column (0-based offset within the range). Moves whole
    /// cells (value + style + format); formulas move as-is (their A1 references are not re-pointed).</summary>
    public void SortRange(SheetDto sheet, string a1Range, int keyColumnOffset, bool ascending = true)
    {
        var (r0, c0, r1, c1) = ParseRange(a1Range);
        var keyCol = c0 + keyColumnOffset;

        var byRow = sheet.Cells
            .Where(c => c.Row >= r0 && c.Row <= r1 && c.Col >= c0 && c.Col <= c1)
            .GroupBy(c => c.Row)
            .ToDictionary(g => g.Key, g => g.ToList());

        var orderedRows = Enumerable.Range(r0, r1 - r0 + 1)
            .Select(r => (row: r, key: byRow.GetValueOrDefault(r)?.FirstOrDefault(c => c.Col == keyCol)?.Value))
            .OrderBy(x => x.key, Comparer<object?>.Create(Compare))
            .ToList();
        if (!ascending) orderedRows.Reverse();

        sheet.Cells.RemoveAll(c => c.Row >= r0 && c.Row <= r1 && c.Col >= c0 && c.Col <= c1);
        for (var i = 0; i < orderedRows.Count; i++)
        {
            var targetRow = r0 + i;
            foreach (var cell in byRow.GetValueOrDefault(orderedRows[i].row) ?? [])
            {
                cell.Row = targetRow;
                sheet.Cells.Add(cell);
            }
        }
    }

    /// <summary>Replaces <paramref name="find"/> with <paramref name="replace"/> in text and formula cells
    /// across the workbook. Returns the number of cells changed.</summary>
    public int FindReplace(SpreadsheetDto workbook, string find, string replace, bool matchCase = false)
    {
        if (string.IsNullOrEmpty(find)) return 0;
        var cmp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var count = 0;
        foreach (var sheet in workbook.Sheets)
            foreach (var cell in sheet.Cells)
            {
                if (cell.Type == "text" && cell.Value is string s && s.Contains(find, cmp))
                { cell.Value = ReplaceAll(s, find, replace, cmp); count++; }
                else if (cell.Type == "formula" && cell.Formula is { } f && f.Contains(find, cmp))
                { cell.Formula = ReplaceAll(f, find, replace, cmp); count++; }
            }
        return count;
    }

    private static (int r0, int c0, int r1, int c1) ParseRange(string a1)
    {
        var parts = a1.Split(':');
        var a = A1Reference.Parse(parts[0]);
        var b = parts.Length > 1 ? A1Reference.Parse(parts[1]) : a;
        return (Math.Min(a.Row, b.Row), Math.Min(a.Col, b.Col), Math.Max(a.Row, b.Row), Math.Max(a.Col, b.Col));
    }

    private static int Compare(object? a, object? b)
    {
        if (a is null && b is null) return 0;
        if (a is null) return -1;
        if (b is null) return 1;
        if (TryNum(a, out var na) && TryNum(b, out var nb)) return na.CompareTo(nb);
        return string.Compare(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryNum(object v, out double n)
    {
        if (v is double d) { n = d; return true; }
        return double.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out n);
    }

    private static string ReplaceAll(string input, string find, string replace, StringComparison cmp)
    {
        var sb = new System.Text.StringBuilder();
        var i = 0;
        int idx;
        while ((idx = input.IndexOf(find, i, cmp)) >= 0)
        {
            sb.Append(input, i, idx - i).Append(replace);
            i = idx + find.Length;
        }
        sb.Append(input, i, input.Length - i);
        return sb.ToString();
    }
}
