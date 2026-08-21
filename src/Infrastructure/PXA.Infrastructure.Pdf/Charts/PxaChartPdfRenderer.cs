using System.Globalization;
using PXA.Core.Contracts;
using PXA.Core.Primitives;
using PXA.Pdf;
using SkiaSharp;
using PdfPoint = PXA.Pdf.PdfPoint;

namespace PXA.Infrastructure.Pdf.Charts;

public enum ChartRenderMode
{
    Vector,
    RasterFallback,
    Empty
}

public sealed record ChartRenderResult(ChartRenderMode Mode, string? Diagnostic = null);

public interface IChartRenderer
{
    ChartRenderResult Render(PdfPage page, ElementDto element, double x, double y, double width, double height);
}

public sealed class PxaChartPdfRenderer : IChartRenderer
{
    private static readonly PdfColor GridColor = PdfColor.FromRgb(226, 232, 240);
    private static readonly PdfColor AxisColor = PdfColor.FromRgb(100, 116, 139);
    private static readonly PdfColor TextColor = PdfColor.FromRgb(51, 65, 85);
    private static readonly string[] Palette =
    [
        "#2563eb", "#16a34a", "#f59e0b", "#dc2626", "#7c3aed", "#0891b2"
    ];

    public ChartRenderResult Render(PdfPage page, ElementDto element, double x, double y, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(element);
        var chart = ChartDefinitionNormalizer.Normalize(element);
        if (!HasData(chart))
        {
            DrawEmpty(page, x, y, width, height);
            return new ChartRenderResult(ChartRenderMode.Empty, "PXACHART001: Chart contains no renderable data.");
        }

        try
        {
            DrawVector(page, chart, x, y, width, height);
            return new ChartRenderResult(ChartRenderMode.Vector);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var png = RenderRasterFallback(chart, Math.Max(240, (int)Math.Ceiling(width * 3)),
                Math.Max(180, (int)Math.Ceiling(height * 3)));
            page.DrawImage(png, x, y, width, height);
            return new ChartRenderResult(ChartRenderMode.RasterFallback,
                $"PXACHART002: Vector rendering fell back to raster ({exception.GetType().Name}).");
        }
    }

    private static bool HasData(ChartDefinitionDto chart) =>
        chart.Categories.Count > 0 && chart.Series.Any(series => series.Values.Any(value => value.HasValue));

    private static void DrawVector(PdfPage page, ChartDefinitionDto chart, double x, double y, double width, double height)
    {
        if (width < 40 || height < 30)
            throw new ArgumentOutOfRangeException(nameof(width), "Chart bounds are too small.");

        var titleHeight = string.IsNullOrWhiteSpace(chart.Title) ? 0 : 16;
        if (titleHeight > 0)
            page.DrawText(chart.Title!, x + 4, y + height - 12, 10, PdfFontFamily.Helvetica, bold: true);

        var legendHeight = chart.Legend?.Visible == false ? 0 : 16;
        var plotX = x + 36;
        var plotY = y + 22 + legendHeight;
        var plotWidth = Math.Max(10, width - 48);
        var plotHeight = Math.Max(10, height - 32 - legendHeight - titleHeight);

        if (chart.Type is PxaChartTypes.Pie or PxaChartTypes.Doughnut)
            DrawCircular(page, chart, plotX, plotY, plotWidth, plotHeight);
        else
            DrawCartesian(page, chart, plotX, plotY, plotWidth, plotHeight);

        if (legendHeight > 0)
            DrawLegend(page, chart, x + 4, y + 5, width - 8);
    }

    private static void DrawCartesian(PdfPage page, ChartDefinitionDto chart,
        double x, double y, double width, double height)
    {
        var domain = CalculateDomain(chart);
        var ticks = NiceTicks(domain.minimum, domain.maximum, 5);
        var minimum = ticks[0];
        var maximum = ticks[^1];
        var range = Math.Max(maximum - minimum, 1e-9);
        double MapY(double value) => y + ((value - minimum) / range * height);

        foreach (var tick in ticks)
        {
            var tickY = MapY(tick);
            if (chart.ValueAxes.FirstOrDefault()?.GridLines != false)
                page.DrawLine(x, tickY, x + width, tickY, 0.5, GridColor);
            page.DrawText(FormatValue(tick, chart.ValueAxes.FirstOrDefault()?.NumberFormat, chart.Locale),
                Math.Max(0, x - 33), tickY - 3, 7, PdfFontFamily.Helvetica);
        }
        page.DrawLine(x, y, x, y + height, 0.8, AxisColor);
        var zeroY = minimum <= 0 && maximum >= 0 ? MapY(0) : y;
        page.DrawLine(x, zeroY, x + width, zeroY, 0.8, AxisColor);

        var count = chart.Categories.Count;
        var slot = width / Math.Max(count, 1);
        for (var index = 0; index < count; index++)
        {
            var label = Truncate(chart.Categories[index], 14);
            page.DrawText(label, x + index * slot + 2, y - 11, 7, PdfFontFamily.Helvetica);
        }

        if (chart.Type == PxaChartTypes.StackedBar)
            DrawStackedBars(page, chart, x, slot, zeroY, MapY);
        else
        {
            var barSeries = chart.Series.Select((series, index) => (series, index))
                .Where(item => EffectiveType(chart, item.series, item.index) == PxaChartTypes.Bar).ToList();
            DrawGroupedBars(page, chart, barSeries, x, slot, zeroY, MapY);
            foreach (var (series, seriesIndex) in chart.Series.Select((series, index) => (series, index)))
            {
                var type = EffectiveType(chart, series, seriesIndex);
                if (type == PxaChartTypes.Line)
                    DrawLineSeries(page, series, seriesIndex, x, slot, MapY);
                else if (type == PxaChartTypes.Area)
                    DrawAreaSeries(page, series, seriesIndex, x, slot, zeroY, MapY);
            }
        }
    }

    private static void DrawGroupedBars(PdfPage page, ChartDefinitionDto chart,
        List<(ChartSeriesDto series, int index)> barSeries, double x, double slot,
        double zeroY, Func<double, double> mapY)
    {
        if (barSeries.Count == 0) return;
        var groupWidth = slot * 0.76;
        var barWidth = Math.Max(1, groupWidth / barSeries.Count);
        for (var categoryIndex = 0; categoryIndex < chart.Categories.Count; categoryIndex++)
        {
            for (var barIndex = 0; barIndex < barSeries.Count; barIndex++)
            {
                var (series, seriesIndex) = barSeries[barIndex];
                if (categoryIndex >= series.Values.Count || series.Values[categoryIndex] is not { } value) continue;
                var valueY = mapY(value);
                var bottom = Math.Min(zeroY, valueY);
                var barHeight = Math.Max(0.5, Math.Abs(valueY - zeroY));
                var barX = x + categoryIndex * slot + slot * 0.12 + barIndex * barWidth;
                page.DrawRectangle(barX, bottom, Math.Max(0.5, barWidth - 1), barHeight,
                    0.2, fill: true, strokeColor: Color(series, seriesIndex), fillColor: Color(series, seriesIndex));
                DrawDataLabel(page, chart, value, barX, valueY, barWidth);
            }
        }
    }

    private static void DrawStackedBars(PdfPage page, ChartDefinitionDto chart,
        double x, double slot, double zeroY, Func<double, double> mapY)
    {
        for (var categoryIndex = 0; categoryIndex < chart.Categories.Count; categoryIndex++)
        {
            double positive = 0;
            double negative = 0;
            for (var seriesIndex = 0; seriesIndex < chart.Series.Count; seriesIndex++)
            {
                var series = chart.Series[seriesIndex];
                if (categoryIndex >= series.Values.Count || series.Values[categoryIndex] is not { } value) continue;
                var start = value >= 0 ? positive : negative;
                var end = start + value;
                if (value >= 0) positive = end; else negative = end;
                var startY = value == 0 ? zeroY : mapY(start);
                var endY = mapY(end);
                page.DrawRectangle(x + categoryIndex * slot + slot * 0.18, Math.Min(startY, endY), slot * 0.64,
                    Math.Max(0.5, Math.Abs(endY - startY)), 0.2, fill: true,
                    strokeColor: Color(series, seriesIndex), fillColor: Color(series, seriesIndex));
            }
        }
    }

    private static void DrawLineSeries(PdfPage page, ChartSeriesDto series, int seriesIndex,
        double x, double slot, Func<double, double> mapY)
    {
        PdfPoint? previous = null;
        for (var index = 0; index < series.Values.Count; index++)
        {
            if (series.Values[index] is not { } value)
            {
                previous = null;
                continue;
            }
            var point = new PdfPoint(x + index * slot + slot / 2, mapY(value));
            if (previous is { } from)
                page.DrawLine(from.X, from.Y, point.X, point.Y, 1.6, Color(series, seriesIndex));
            if (series.ShowMarkers)
                page.DrawCircle(point.X, point.Y, 2.2, 0.5, fill: true,
                    strokeColor: Color(series, seriesIndex), fillColor: Color(series, seriesIndex));
            previous = point;
        }
    }

    private static void DrawAreaSeries(PdfPage page, ChartSeriesDto series, int seriesIndex,
        double x, double slot, double zeroY, Func<double, double> mapY)
    {
        var points = series.Values.Select((value, index) => value.HasValue
            ? new PdfPoint?(new PdfPoint(x + index * slot + slot / 2, mapY(value.Value)))
            : null).Where(point => point.HasValue).Select(point => point!.Value).ToList();
        if (points.Count < 2) return;
        var polygon = new List<PdfPoint> { new(points[0].X, zeroY) };
        polygon.AddRange(points);
        polygon.Add(new PdfPoint(points[^1].X, zeroY));
        page.DrawPolygon(polygon, 0.5, fill: true, strokeColor: Color(series, seriesIndex),
            fillColor: Lighten(Color(series, seriesIndex), 0.72));
        DrawLineSeries(page, series, seriesIndex, x, slot, mapY);
    }

    private static void DrawCircular(PdfPage page, ChartDefinitionDto chart,
        double x, double y, double width, double height)
    {
        var values = chart.Series[0].Values.Select(value => Math.Max(value ?? 0, 0)).ToArray();
        var total = values.Sum();
        if (total <= 0) throw new InvalidOperationException("Circular charts require positive values.");
        var centerX = x + width / 2;
        var centerY = y + height / 2;
        var radius = Math.Max(4, Math.Min(width, height) * 0.42);
        var start = -Math.PI / 2;
        for (var index = 0; index < values.Length; index++)
        {
            if (values[index] <= 0) continue;
            var end = start + values[index] / total * Math.PI * 2;
            var segments = Math.Max(4, (int)Math.Ceiling((end - start) / (Math.PI / 18)));
            var points = new List<PdfPoint> { new(centerX, centerY) };
            for (var segment = 0; segment <= segments; segment++)
            {
                var angle = start + (end - start) * segment / segments;
                points.Add(new PdfPoint(centerX + Math.Cos(angle) * radius, centerY + Math.Sin(angle) * radius));
            }
            page.DrawPolygon(points, 0.4, fill: true, strokeColor: PdfColor.White,
                fillColor: ParseColor(chart.Palette.ElementAtOrDefault(index) ?? Palette[index % Palette.Length]));
            start = end;
        }
        if (chart.Type == PxaChartTypes.Doughnut)
            page.DrawCircle(centerX, centerY, radius * 0.46, 0.2, fill: true,
                strokeColor: PdfColor.White, fillColor: PdfColor.White);
    }

    private static void DrawLegend(PdfPage page, ChartDefinitionDto chart, double x, double y, double width)
    {
        var cursor = x;
        for (var index = 0; index < chart.Series.Count && cursor < x + width - 25; index++)
        {
            var series = chart.Series[index];
            page.DrawRectangle(cursor, y, 7, 7, 0.2, fill: true,
                strokeColor: Color(series, index), fillColor: Color(series, index));
            page.DrawText(Truncate(series.Name, 18), cursor + 10, y, 7, PdfFontFamily.Helvetica);
            cursor += Math.Min(100, 18 + series.Name.Length * 4.2);
        }
    }

    private static (double minimum, double maximum) CalculateDomain(ChartDefinitionDto chart)
    {
        var values = new List<double>();
        if (chart.Type == PxaChartTypes.StackedBar)
        {
            for (var category = 0; category < chart.Categories.Count; category++)
            {
                values.Add(chart.Series.Sum(series => category < series.Values.Count ? Math.Max(series.Values[category] ?? 0, 0) : 0));
                values.Add(chart.Series.Sum(series => category < series.Values.Count ? Math.Min(series.Values[category] ?? 0, 0) : 0));
            }
        }
        else
            values.AddRange(chart.Series.SelectMany(series => series.Values).Where(value => value.HasValue).Select(value => value!.Value));

        var axis = chart.ValueAxes.FirstOrDefault();
        var minimum = axis?.Minimum ?? Math.Min(0, values.DefaultIfEmpty(0).Min());
        var maximum = axis?.Maximum ?? Math.Max(0, values.DefaultIfEmpty(1).Max());
        if (minimum >= maximum)
        {
            minimum -= 1;
            maximum += 1;
        }
        return (minimum, maximum);
    }

    internal static double[] NiceTicks(double minimum, double maximum, int targetCount)
    {
        var rawStep = Math.Max((maximum - minimum) / Math.Max(targetCount - 1, 1), double.Epsilon);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        var residual = rawStep / magnitude;
        var niceResidual = residual <= 1 ? 1 : residual <= 2 ? 2 : residual <= 5 ? 5 : 10;
        var step = niceResidual * magnitude;
        var start = Math.Floor(minimum / step) * step;
        var end = Math.Ceiling(maximum / step) * step;
        return Enumerable.Range(0, Math.Min(20, (int)Math.Round((end - start) / step) + 1))
            .Select(index => start + index * step).ToArray();
    }

    private static string EffectiveType(ChartDefinitionDto chart, ChartSeriesDto series, int index)
    {
        if (chart.Type == PxaChartTypes.Combo)
            return PxaChartTypes.Supported.Contains(series.Type ?? "") ? series.Type! : index == 0 ? PxaChartTypes.Bar : PxaChartTypes.Line;
        return chart.Type;
    }

    private static void DrawDataLabel(PdfPage page, ChartDefinitionDto chart, double value,
        double x, double y, double width)
    {
        if (chart.DataLabels?.Visible != true) return;
        page.DrawText(FormatValue(value, chart.DataLabels.NumberFormat, chart.Locale), x, y + 3,
            Math.Min(7, Math.Max(5, width / 4)), PdfFontFamily.Helvetica);
    }

    private static string FormatValue(double value, string? format, string? locale)
    {
        CultureInfo culture;
        try { culture = string.IsNullOrWhiteSpace(locale) ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(locale); }
        catch (CultureNotFoundException) { culture = CultureInfo.InvariantCulture; }
        if (string.IsNullOrWhiteSpace(format)) return value.ToString("0.##", culture);
        try { return value.ToString(format, culture); }
        catch (FormatException) { return value.ToString("0.##", culture); }
    }

    private static string Truncate(string value, int maximum) =>
        value.Length <= maximum ? value : $"{value[..Math.Max(1, maximum - 1)]}…";

    private static PdfColor Color(ChartSeriesDto series, int index) =>
        ParseColor(series.Color ?? Palette[index % Palette.Length]);

    private static PdfColor ParseColor(string value)
    {
        var hex = value.Trim().TrimStart('#');
        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            return PdfColor.FromRgb(37, 99, 235);
        return PdfColor.FromRgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
    }

    private static PdfColor Lighten(PdfColor color, double amount) => new(
        color.Red + (1 - color.Red) * amount,
        color.Green + (1 - color.Green) * amount,
        color.Blue + (1 - color.Blue) * amount);

    private static void DrawEmpty(PdfPage page, double x, double y, double width, double height)
    {
        page.DrawRectangle(x, y, width, height, 0.7, fill: true,
            strokeColor: GridColor, fillColor: PdfColor.FromRgb(248, 250, 252));
        page.DrawText("No chart data", x + 8, y + Math.Max(10, height / 2), 9, PdfFontFamily.Helvetica);
    }

    private static byte[] RenderRasterFallback(ChartDefinitionDto chart, int width, int height)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var textPaint = new SKPaint { Color = SKColors.DarkSlateGray, IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, Math.Max(18, height / 28f));
        canvas.DrawText(chart.Title ?? "Chart", 24, 36, SKTextAlign.Left, font, textPaint);
        var values = chart.Series.SelectMany(series => series.Values).Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        var min = Math.Min(0, values.DefaultIfEmpty(0).Min());
        var max = Math.Max(1, values.DefaultIfEmpty(1).Max());
        var range = Math.Max(max - min, 1);
        var plot = new SKRect(42, 54, width - 20, height - 32);
        using var axis = new SKPaint { Color = SKColors.SlateGray, StrokeWidth = 2, IsAntialias = true };
        canvas.DrawLine(plot.Left, plot.Top, plot.Left, plot.Bottom, axis);
        canvas.DrawLine(plot.Left, plot.Bottom, plot.Right, plot.Bottom, axis);
        var count = Math.Max(chart.Categories.Count, 1);
        var slot = plot.Width / count;
        for (var seriesIndex = 0; seriesIndex < chart.Series.Count; seriesIndex++)
        {
            var series = chart.Series[seriesIndex];
            using var paint = new SKPaint { Color = SKColor.Parse(series.Color ?? Palette[seriesIndex % Palette.Length]),
                StrokeWidth = 4, IsAntialias = true, Style = SKPaintStyle.Fill };
            for (var index = 0; index < series.Values.Count; index++)
            {
                if (series.Values[index] is not { } value) continue;
                var barHeight = (float)(Math.Abs(value) / range * plot.Height);
                var barWidth = Math.Max(2, slot / Math.Max(chart.Series.Count + 1, 2));
                canvas.DrawRect(plot.Left + index * slot + seriesIndex * barWidth,
                    plot.Bottom - barHeight, barWidth - 1, barHeight, paint);
            }
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
}
