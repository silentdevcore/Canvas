using System.Globalization;

namespace PVAD.Importer.Editing;

/// <summary>Parses Nutrient/GdPicture one-based page-range expressions.</summary>
public static class PdfPageRangeParser
{
    public static IReadOnlyList<int> Parse(string expression, int pageCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        if (pageCount < 1)
            throw new ArgumentOutOfRangeException(nameof(pageCount), pageCount, "A PDF must contain at least one page.");

        var pages = new List<int>();
        foreach (var token in expression.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token == "*")
            {
                pages.AddRange(Enumerable.Range(1, pageCount));
                continue;
            }

            var parts = token.Split('-', StringSplitOptions.TrimEntries);
            if (parts.Length is < 1 or > 2 ||
                !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var first) ||
                first < 1 || first > pageCount)
            {
                throw new FormatException($"Invalid one-based PDF page range token '{token}'.");
            }

            var last = first;
            if (parts.Length == 2 &&
                (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out last) ||
                 last < first || last > pageCount))
            {
                throw new FormatException($"Invalid one-based PDF page range token '{token}'.");
            }

            pages.AddRange(Enumerable.Range(first, last - first + 1));
        }

        if (pages.Count == 0)
            throw new FormatException("The PDF page range does not select any pages.");
        if (pages.Distinct().Count() != pages.Count)
            throw new FormatException("A PDF page range cannot select the same page more than once.");
        return pages;
    }
}
