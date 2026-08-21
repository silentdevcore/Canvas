using System.Globalization;
using PXA.Pdf.Layout;

namespace PXA.Pdf;

public sealed class PdfDesignSnapshot
{
    public List<PdfDesignPageSnapshot> Pages { get; } = [];
}

public sealed class PdfDesignPageSnapshot
{
    public required double Width { get; init; }
    public required double Height { get; init; }
    public List<PdfDesignElementSnapshot> Elements { get; } = [];
}

public sealed class PdfDesignElementSnapshot
{
    public required string Kind { get; init; }
    public required int OperationIndex { get; init; }
    public string? Text { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public double X2 { get; init; }
    public double Y2 { get; init; }
    public double FontSize { get; init; }
    public double StrokeWidth { get; init; }
    public double CornerRadius { get; init; }
    public string? FillColor { get; init; }
    public string? StrokeColor { get; init; }
    public string? Language { get; init; }
    public string? TextDirection { get; init; }
    public bool HasEmbeddedImage { get; init; }
}

internal static class PdfDesignSnapshotFactory
{
    public static PdfDesignSnapshot Create(PdfDocument document)
    {
        var snapshot = new PdfDesignSnapshot();
        foreach (var page in document.Pages)
        {
            var pageSnapshot = new PdfDesignPageSnapshot { Width = page.Width, Height = page.Height };
            for (var index = 0; index < page.Elements.Count; index++)
                pageSnapshot.Elements.Add(CreateElement(page.Elements[index], index));
            snapshot.Pages.Add(pageSnapshot);
        }
        return snapshot;
    }

    private static PdfDesignElementSnapshot CreateElement(PdfPageElement element, int operationIndex) => element switch
    {
        TextElement value => new()
        {
            Kind = "text", OperationIndex = operationIndex, Text = value.Text,
            X = value.X, Y = value.Y, FontSize = value.FontSize,
            Width = Math.Max(20, value.Text.Length * value.FontSize * 0.55), Height = value.FontSize * 1.4,
            FillColor = Color(value.FillColor), Language = value.Language, TextDirection = value.TextDirection,
        },
        RoundedRectangleElement value => new()
        {
            Kind = "rectangle", OperationIndex = operationIndex, X = value.X, Y = value.Y,
            Width = value.Width, Height = value.Height, CornerRadius = value.CornerRadius,
            StrokeWidth = value.StrokeStyle.LineWidth,
            FillColor = value.Fill ? Color(value.FillColor) : null,
            StrokeColor = value.Stroke ? Color(value.StrokeColor) : null,
        },
        RectangleElement value => new()
        {
            Kind = "rectangle", OperationIndex = operationIndex, X = value.X, Y = value.Y,
            Width = value.Width, Height = value.Height, StrokeWidth = value.StrokeStyle.LineWidth,
            FillColor = value.Fill ? Color(value.FillColor) : null,
            StrokeColor = value.Stroke ? Color(value.StrokeColor) : null,
        },
        LineElement value => new()
        {
            Kind = "line", OperationIndex = operationIndex, X = value.X1, Y = value.Y1,
            X2 = value.X2, Y2 = value.Y2, Width = Math.Abs(value.X2 - value.X1),
            Height = Math.Max(value.StrokeStyle.LineWidth, Math.Abs(value.Y2 - value.Y1)),
            StrokeWidth = value.StrokeStyle.LineWidth, StrokeColor = Color(value.StrokeColor),
        },
        CircleElement value => new()
        {
            Kind = "circle", OperationIndex = operationIndex,
            X = value.CenterX - value.Radius, Y = value.CenterY - value.Radius,
            Width = value.Radius * 2, Height = value.Radius * 2,
            StrokeWidth = value.StrokeStyle.LineWidth,
            FillColor = value.Fill ? Color(value.FillColor) : null,
            StrokeColor = value.Stroke ? Color(value.StrokeColor) : null,
        },
        ImageElement value => new()
        {
            Kind = "image", OperationIndex = operationIndex, X = value.X, Y = value.Y,
            Width = value.Width, Height = value.Height, HasEmbeddedImage = true,
        },
        PolygonElement value => Bounds("polygon", value.Points, value.StrokeStyle.LineWidth,
            Color(value.FillColor), Color(value.StrokeColor), operationIndex),
        BezierCurveElement value => Bounds("path", [value.Start, value.Control1, value.Control2, value.End],
            value.StrokeStyle.LineWidth, null, Color(value.StrokeColor), operationIndex),
        _ => new() { Kind = "unsupported", OperationIndex = operationIndex },
    };

    private static PdfDesignElementSnapshot Bounds(
        string kind, IReadOnlyList<PdfPoint> points, double strokeWidth,
        string? fillColor, string? strokeColor, int operationIndex)
    {
        var minX = points.Count == 0 ? 0 : points.Min(point => point.X);
        var minY = points.Count == 0 ? 0 : points.Min(point => point.Y);
        var maxX = points.Count == 0 ? 0 : points.Max(point => point.X);
        var maxY = points.Count == 0 ? 0 : points.Max(point => point.Y);
        return new PdfDesignElementSnapshot
        {
            Kind = kind, OperationIndex = operationIndex, X = minX, Y = minY,
            Width = maxX - minX, Height = maxY - minY, StrokeWidth = strokeWidth,
            FillColor = fillColor, StrokeColor = strokeColor,
        };
    }

    private static string? Color(IPdfColor? color) => color switch
    {
        null => null,
        PdfColor rgb => Hex(rgb.Red, rgb.Green, rgb.Blue),
        PdfGrayColor gray => Hex(gray.Gray, gray.Gray, gray.Gray),
        PdfCmykColor cmyk => Hex(
            (1 - cmyk.Cyan) * (1 - cmyk.Black),
            (1 - cmyk.Magenta) * (1 - cmyk.Black),
            (1 - cmyk.Yellow) * (1 - cmyk.Black)),
        _ => null,
    };

    private static string Hex(double red, double green, double blue) => string.Create(
        CultureInfo.InvariantCulture,
        $"#{Byte(red):X2}{Byte(green):X2}{Byte(blue):X2}");

    private static int Byte(double value) => Math.Clamp((int)Math.Round(value * 255), 0, 255);
}
