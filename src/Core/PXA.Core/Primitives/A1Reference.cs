namespace PXA.Core.Primitives;

/// <summary>
/// Converts between 0-based row/column indices and spreadsheet A1 notation
/// ("A" = col 0, "AA" = col 26; "A1" = row 0, col 0). Shared by the xlsx importer/exporter and tests.
/// </summary>
public static class A1Reference
{
    /// <summary>0-based column index -> letters ("A", "Z", "AA", "AB", ...).</summary>
    public static string ColumnName(int colZeroBased)
    {
        if (colZeroBased < 0) throw new ArgumentOutOfRangeException(nameof(colZeroBased));
        var name = "";
        var n = colZeroBased + 1;
        while (n > 0)
        {
            var rem = (n - 1) % 26;
            name = (char)('A' + rem) + name;
            n = (n - 1) / 26;
        }
        return name;
    }

    /// <summary>Column letters ("A", "aa") -> 0-based column index.</summary>
    public static int ColumnIndex(string columnName)
    {
        if (string.IsNullOrEmpty(columnName)) throw new ArgumentException("Empty column name.", nameof(columnName));
        var n = 0;
        foreach (var ch in columnName)
        {
            var c = char.ToUpperInvariant(ch);
            if (c < 'A' || c > 'Z') throw new FormatException($"Invalid column name '{columnName}'.");
            n = n * 26 + (c - 'A' + 1);
        }
        return n - 1;
    }

    /// <summary>(row, col) 0-based -> "A1".</summary>
    public static string ToA1(int row, int col) => $"{ColumnName(col)}{row + 1}";

    /// <summary>"A1" -> (row, col) 0-based. Ignores '$' anchors.</summary>
    public static (int Row, int Col) Parse(string a1)
    {
        if (string.IsNullOrWhiteSpace(a1)) throw new ArgumentException("Empty reference.", nameof(a1));
        var s = a1.Replace("$", "").Trim();
        var i = 0;
        while (i < s.Length && char.IsLetter(s[i])) i++;
        if (i == 0 || i == s.Length) throw new FormatException($"Invalid A1 reference '{a1}'.");
        var col = ColumnIndex(s[..i]);
        if (!int.TryParse(s[i..], out var rowOneBased) || rowOneBased < 1)
            throw new FormatException($"Invalid A1 reference '{a1}'.");
        return (rowOneBased - 1, col);
    }
}
