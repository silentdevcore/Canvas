using Canvas.Importer.Graphics;

namespace Canvas.Importer.Analysis;

public sealed record ReadingLine(int Order, IReadOnlyList<PrimitiveText> Texts, PdfRectangle Bounds)
{
    public string Text => string.Join(" ", Texts.Select(text => text.Text).Where(static text => text.Length > 0));
}

public sealed record ReadingParagraph(int Order, IReadOnlyList<ReadingLine> Lines, PdfRectangle Bounds)
{
    public string Text => string.Join(Environment.NewLine, Lines.Select(static line => line.Text));
}

public sealed record ReadingColumn(int Order, IReadOnlyList<ReadingParagraph> Paragraphs, PdfRectangle Bounds);

public sealed class ReadingOrderResult
{
    public IReadOnlyList<ReadingColumn> Columns { get; init; } = [];
    public IReadOnlyList<ReadingParagraph> Paragraphs { get; init; } = [];
    public IReadOnlyList<ReadingLine> Lines { get; init; } = [];
}

public sealed class ReadingOrderEngine
{
    public IReadOnlyList<ReadingLine> BuildLines(IEnumerable<PrimitiveObject> primitives)
    {
        var texts = Flatten(primitives).OfType<PrimitiveText>().Where(static text => !string.IsNullOrWhiteSpace(text.Text)).ToList();
        var groups = new List<List<PrimitiveText>>();

        foreach (var text in texts.OrderByDescending(static text => text.Bounds.CenterY).ThenBy(static text => text.Bounds.Left))
        {
            var line = groups.FirstOrDefault(candidate => IsSameBaseline(candidate[0], text));
            if (line is null)
            {
                groups.Add([text]);
            }
            else
            {
                line.Add(text);
            }
        }

        return groups
            .Select((line, index) =>
            {
                var ordered = line.OrderBy(static text => text.Bounds.Left).ToArray();
                return new ReadingLine(index, ordered, Union(ordered.Select(static text => text.Bounds)));
            })
            .OrderByDescending(static line => line.Bounds.CenterY)
            .ThenBy(static line => line.Bounds.Left)
            .Select((line, index) => line with { Order = index })
            .ToArray();
    }

    public IReadOnlyList<ReadingParagraph> BuildParagraphs(IEnumerable<ReadingLine> lines)
    {
        var paragraphs = new List<List<ReadingLine>>();
        foreach (var line in lines.OrderBy(static line => line.Order))
        {
            var previous = paragraphs.LastOrDefault()?.LastOrDefault();
            if (previous is null || StartsNewParagraph(previous, line))
            {
                paragraphs.Add([line]);
            }
            else
            {
                paragraphs[^1].Add(line);
            }
        }

        return paragraphs
            .Select((paragraph, index) => new ReadingParagraph(index, paragraph, Union(paragraph.Select(static line => line.Bounds))))
            .ToArray();
    }

    public IReadOnlyList<ReadingColumn> DetectColumns(IEnumerable<ReadingParagraph> paragraphs)
    {
        var ordered = paragraphs.OrderBy(static paragraph => paragraph.Bounds.Left).ToList();
        var columns = new List<List<ReadingParagraph>>();

        foreach (var paragraph in ordered)
        {
            var column = columns.FirstOrDefault(candidate => HorizontallyOverlaps(candidate[0].Bounds, paragraph.Bounds));
            if (column is null)
            {
                columns.Add([paragraph]);
            }
            else
            {
                column.Add(paragraph);
            }
        }

        return columns
            .Select((column, index) => new ReadingColumn(
                index,
                column.OrderByDescending(static paragraph => paragraph.Bounds.Top).ToArray(),
                Union(column.Select(static paragraph => paragraph.Bounds))))
            .OrderBy(static column => column.Bounds.Left)
            .Select((column, index) => column with { Order = index })
            .ToArray();
    }

    public ReadingOrderResult Analyze(IEnumerable<PrimitiveObject> primitives)
    {
        var lines = BuildLines(primitives);
        var paragraphs = BuildParagraphs(lines);
        var columns = DetectColumns(paragraphs);
        return new ReadingOrderResult
        {
            Lines = lines,
            Paragraphs = paragraphs,
            Columns = columns
        };
    }

    private static bool IsSameBaseline(PrimitiveText left, PrimitiveText right)
    {
        var tolerance = Math.Max(left.FontSize, right.FontSize) * 0.6d + 1d;
        var rotationDelta = Math.Abs(left.Geometry.RotationDegrees - right.Geometry.RotationDegrees);
        return Math.Abs(left.Bounds.CenterY - right.Bounds.CenterY) <= tolerance && rotationDelta <= 8d;
    }

    private static bool StartsNewParagraph(ReadingLine previous, ReadingLine current)
    {
        var lineHeight = Math.Max(previous.Bounds.Top - previous.Bounds.Bottom, 1d);
        var verticalGap = previous.Bounds.Bottom - current.Bounds.Top;
        var indentDelta = Math.Abs(previous.Bounds.Left - current.Bounds.Left);
        return verticalGap > lineHeight * 1.4d || indentDelta > lineHeight * 3d;
    }

    private static bool HorizontallyOverlaps(PdfRectangle left, PdfRectangle right)
    {
        var overlap = Math.Min(left.Right, right.Right) - Math.Max(left.Left, right.Left);
        return overlap > Math.Min(left.Width, right.Width) * 0.25d;
    }

    internal static IEnumerable<PrimitiveObject> Flatten(IEnumerable<PrimitiveObject> primitives)
    {
        foreach (var primitive in primitives)
        {
            yield return primitive;
            foreach (var child in Flatten(primitive.Children))
            {
                yield return child;
            }
        }
    }

    internal static PdfRectangle Union(IEnumerable<PdfRectangle> bounds)
    {
        using var enumerator = bounds.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return new PdfRectangle(0, 0, 0, 0);
        }

        var union = enumerator.Current;
        while (enumerator.MoveNext())
        {
            union = union.Union(enumerator.Current);
        }

        return union;
    }
}
