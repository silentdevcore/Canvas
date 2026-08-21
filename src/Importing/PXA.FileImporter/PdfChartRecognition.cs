using System.Globalization;
using PXA.Core.Contracts;
using PXA.Importer.Analysis;
using PXA.Importer.Graphics;

namespace PXA.FileImporter;

public enum PdfChartRecognitionMode
{
    Off,
    Safe,
    Review
}

public sealed class PdfFileImportOptions
{
    public PdfChartRecognitionMode ChartRecognition { get; set; } = PdfChartRecognitionMode.Safe;
    public int MaximumChartCandidatesPerPage { get; set; } = 16;
    public int MaximumPrimitivesPerPage { get; set; } = 50_000;
}

internal sealed record PdfChartCandidate(
    PdfRectangle Bounds,
    ChartDefinitionDto Definition,
    IReadOnlySet<PrimitiveObject> Consumed,
    double Confidence,
    string DiagnosticCode);

internal static class PdfChartRecognitionEngine
{
    public static IReadOnlyList<PdfChartCandidate> Detect(
        PdfScenePage page,
        PdfFileImportOptions options,
        CancellationToken cancellationToken)
    {
        if (options.ChartRecognition == PdfChartRecognitionMode.Off)
            return [];

        var primitives = Flatten(page.Layers.SelectMany(static layer => layer.Objects))
            .Take(options.MaximumPrimitivesPerPage + 1)
            .ToArray();
        if (primitives.Length > options.MaximumPrimitivesPerPage)
            return [];

        cancellationToken.ThrowIfCancellationRequested();
        var candidates = new List<PdfChartCandidate>();
        var bar = DetectBarChart(primitives);
        if (bar is not null && ShouldEmit(bar.Confidence, options.ChartRecognition))
            candidates.Add(bar);

        foreach (var line in DetectLineCharts(primitives))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ShouldEmit(line.Confidence, options.ChartRecognition) &&
                !candidates.Any(existing => OverlapRatio(existing.Bounds, line.Bounds) > 0.5))
                candidates.Add(line);
            if (candidates.Count >= Math.Clamp(options.MaximumChartCandidatesPerPage, 1, 64))
                break;
        }

        return candidates;
    }

    private static PdfChartCandidate? DetectBarChart(IReadOnlyList<PrimitiveObject> primitives)
    {
        var bars = primitives.OfType<PrimitiveShape>()
            .Where(IsFilledRectangle)
            .Where(static shape => shape.Bounds.Width is >= 2 and <= 100 && shape.Bounds.Height >= 6)
            .OrderBy(static shape => shape.Bounds.Left)
            .ToArray();
        if (bars.Length < 3)
            return null;

        var groups = bars.GroupBy(shape => Math.Round(shape.Bounds.Bottom / 3d) * 3d)
            .Select(static group => group.OrderBy(shape => shape.Bounds.Left).ToArray())
            .Where(static group => group.Length >= 3)
            .OrderByDescending(static group => group.Length)
            .ToArray();
        if (groups.Length == 0)
            return null;

        var selected = groups[0];
        var bounds = Union(selected.Select(static bar => bar.Bounds));
        if (bounds.Width < 30 || bounds.Height < 12 ||
            selected.Max(static bar => bar.Bounds.Height) - selected.Min(static bar => bar.Bounds.Height) < 2)
            return null;

        var medianWidth = selected.Select(static bar => bar.Bounds.Width).Order().ElementAt(selected.Length / 2);
        if (selected.Any(bar => Math.Abs(bar.Bounds.Width - medianWidth) > Math.Max(2, medianWidth * 0.35)))
            return null;

        var texts = primitives.OfType<PrimitiveText>().ToArray();
        var categories = new List<string>(selected.Length);
        var values = new List<double?>(selected.Length);
        var exactValues = 0;
        foreach (var bar in selected)
        {
            var category = texts
                .Where(text => Math.Abs(text.Bounds.CenterX - bar.Bounds.CenterX) <= Math.Max(bar.Bounds.Width, text.Bounds.Width) &&
                               text.Bounds.Top <= bar.Bounds.Bottom + 4 && text.Bounds.Bottom >= bar.Bounds.Bottom - 28)
                .OrderBy(text => Math.Abs(text.Bounds.CenterX - bar.Bounds.CenterX))
                .FirstOrDefault();
            categories.Add(category?.Text.Trim() is { Length: > 0 } label ? label : $"Category {categories.Count + 1}");

            var dataLabel = texts
                .Select(text => (text, value: ParseNumber(text.Text)))
                .Where(item => item.value.HasValue &&
                               Math.Abs(item.text.Bounds.CenterX - bar.Bounds.CenterX) <= Math.Max(bar.Bounds.Width, 10) &&
                               item.text.Bounds.Bottom >= bar.Bounds.Top - 12 && item.text.Bounds.Top <= bar.Bounds.Top + 22)
                .OrderBy(item => Math.Abs(item.text.Bounds.CenterX - bar.Bounds.CenterX))
                .FirstOrDefault();
            if (dataLabel.value.HasValue)
            {
                values.Add(dataLabel.value);
                exactValues++;
            }
            else
            {
                values.Add(Math.Round(bar.Bounds.Height, 2));
            }
        }

        var horizontalAxis = primitives.OfType<PrimitivePath>().Any(path =>
            path.Bounds.Width >= bounds.Width * 0.7 && path.Bounds.Height <= 3 &&
            Math.Abs(path.Bounds.CenterY - bounds.Bottom) <= 8);
        var confidence = exactValues == selected.Length ? 0.88 : 0.72;
        if (horizontalAxis) confidence += 0.03;
        if (categories.All(static category => !category.StartsWith("Category ", StringComparison.Ordinal))) confidence += 0.02;
        confidence = Math.Min(confidence, 0.95);

        var consumed = new HashSet<PrimitiveObject>(selected);
        foreach (var text in texts.Where(text => text.Bounds.Intersects(bounds.Inflate(30, 30))))
            consumed.Add(text);
        foreach (var axis in primitives.OfType<PrimitivePath>().Where(path => path.Bounds.Intersects(bounds.Inflate(8, 8))))
            consumed.Add(axis);

        var status = confidence >= 0.85 ? "automatic" : "reviewRequired";
        return new PdfChartCandidate(bounds.Inflate(8, 26), new ChartDefinitionDto
        {
            Type = PxaChartTypes.Bar,
            Categories = categories,
            Series = [new ChartSeriesDto { Id = "series-1", Name = "Series 1", Values = values,
                Color = ToHex(selected[0].GraphicsState.FillColor) }],
            Recognition = new ChartRecognitionDto
            {
                Status = status,
                Confidence = confidence,
                SourceKind = "pdfVector",
                DiagnosticCode = exactValues == selected.Length ? "PXA-PDF-CHART-101" : "PXA-PDF-CHART-102"
            }
        }, consumed, confidence, exactValues == selected.Length ? "PXA-PDF-CHART-101" : "PXA-PDF-CHART-102");
    }

    private static IEnumerable<PdfChartCandidate> DetectLineCharts(IReadOnlyList<PrimitiveObject> primitives)
    {
        foreach (var path in primitives.OfType<PrimitivePath>())
        {
            var points = PathPoints(path).ToArray();
            if (points.Length < 3 || path.Bounds.Width < 50 || path.Bounds.Height < 15 ||
                path.Bounds.Width / Math.Max(path.Bounds.Height, 1) > 12)
                continue;

            var monotonicX = points.Zip(points.Skip(1)).All(pair => pair.Second.X > pair.First.X);
            if (!monotonicX)
                continue;

            var categories = Enumerable.Range(1, points.Length).Select(index => $"Point {index}").ToList();
            var minimum = points.Min(static point => point.Y);
            var values = points.Select(point => (double?)Math.Round(point.Y - minimum, 2)).ToList();
            yield return new PdfChartCandidate(path.Bounds.Inflate(8, 18), new ChartDefinitionDto
            {
                Type = PxaChartTypes.Line,
                Categories = categories,
                Series = [new ChartSeriesDto { Id = "series-1", Name = "Series 1", Values = values,
                    Color = ToHex(path.GraphicsState.StrokeColor), ShowMarkers = true }],
                Recognition = new ChartRecognitionDto
                {
                    Status = "reviewRequired",
                    Confidence = 0.66,
                    SourceKind = "pdfVector",
                    DiagnosticCode = "PXA-PDF-CHART-103"
                }
            }, new HashSet<PrimitiveObject> { path }, 0.66, "PXA-PDF-CHART-103");
        }
    }

    private static bool ShouldEmit(double confidence, PdfChartRecognitionMode mode) =>
        confidence >= 0.85 || mode == PdfChartRecognitionMode.Review && confidence >= 0.60;

    private static bool IsFilledRectangle(PrimitiveShape shape) =>
        shape.Segments.Count == 1 && shape.Segments[0] is RectangleSegment &&
        shape.SourceOperator.Operator.Name is "f" or "F" or "f*" or "B" or "B*" or "b" or "b*";

    private static IEnumerable<PdfPoint> PathPoints(PrimitivePath path)
    {
        foreach (var segment in path.Segments)
        {
            if (segment is MoveToSegment move) yield return move.Point;
            else if (segment is LineToSegment line) yield return line.Point;
            else if (segment is CurveToSegment curve) yield return curve.End;
        }
    }

    private static IEnumerable<PrimitiveObject> Flatten(IEnumerable<PrimitiveObject> primitives)
    {
        foreach (var primitive in primitives)
        {
            yield return primitive;
            foreach (var child in Flatten(primitive.Children))
                yield return child;
        }
    }

    private static double? ParseNumber(string text)
    {
        var normalized = text.Trim().Replace(" ", "", StringComparison.Ordinal);
        if (double.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out var invariant)) return invariant;
        if (double.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.GetCultureInfo("de-DE"), out var german)) return german;
        return null;
    }

    private static PdfRectangle Union(IEnumerable<PdfRectangle> rectangles) =>
        rectangles.Aggregate(static (left, right) => left.Union(right));

    private static double OverlapRatio(PdfRectangle left, PdfRectangle right)
    {
        var intersection = left.Intersect(right);
        if (intersection is null) return 0;
        return intersection.Value.Width * intersection.Value.Height /
               Math.Max(1, Math.Min(left.Width * left.Height, right.Width * right.Height));
    }

    private static string ToHex(PdfColor color)
    {
        static int Byte(double value) => Math.Clamp((int)Math.Round(value * 255), 0, 255);
        return color.ColorSpace switch
        {
            PdfColorSpace.DeviceRgb => $"#{Byte(color.C1):X2}{Byte(color.C2):X2}{Byte(color.C3):X2}",
            PdfColorSpace.DeviceGray => $"#{Byte(color.C1):X2}{Byte(color.C1):X2}{Byte(color.C1):X2}",
            _ => "#2563EB"
        };
    }
}
