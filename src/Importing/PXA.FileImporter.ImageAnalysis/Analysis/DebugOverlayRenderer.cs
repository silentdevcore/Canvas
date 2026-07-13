using SkiaSharp;

namespace PXA.FileImporter.ImageAnalysis.Analysis;

public static class DebugOverlayRenderer
{
    public static byte[] RenderPng(
        PreparedImage img,
        ColorAnalysisResult colors,
        ShapeDetectionResult shapes,
        TextAnalysisResult texts)
    {
        using var overlay = img.Original.Copy();
        using var canvas = new SKCanvas(overlay);

        DrawRegions(canvas, colors.Regions);
        DrawShapes(canvas, shapes.Shapes);
        DrawText(canvas, texts.Lines);

        using var image = SKImage.FromBitmap(overlay);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void DrawRegions(SKCanvas canvas, IReadOnlyList<ColorRegion> regions)
    {
        using var stroke = new SKPaint
        {
            Color = new SKColor(0, 180, 255, 210),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = false,
        };

        using var fill = new SKPaint
        {
            Color = new SKColor(0, 180, 255, 32),
            Style = SKPaintStyle.Fill,
            IsAntialias = false,
        };

        foreach (var region in regions)
        {
            var rect = ToRect(region.Bounds);
            canvas.DrawRect(rect, fill);
            canvas.DrawRect(rect, stroke);
        }
    }

    private static void DrawShapes(SKCanvas canvas, IReadOnlyList<ImageShapePrimitive> shapes)
    {
        using var stroke = new SKPaint
        {
            Color = new SKColor(255, 130, 0, 230),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = false,
        };

        foreach (var shape in shapes)
        {
            var rect = ToRect(shape.Bounds);
            if (shape.Kind == ShapeKind.Ellipse)
                canvas.DrawOval(rect, stroke);
            else
                canvas.DrawRect(rect, stroke);
        }
    }

    private static void DrawText(SKCanvas canvas, IReadOnlyList<ImageTextPrimitive> lines)
    {
        using var lineStroke = new SKPaint
        {
            Color = new SKColor(0, 210, 90, 230),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = false,
        };
        using var wordStroke = new SKPaint
        {
            Color = new SKColor(170, 80, 255, 230),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = false,
        };
        using var glyphStroke = new SKPaint
        {
            Color = new SKColor(255, 40, 90, 230),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = false,
        };

        foreach (var line in lines)
        {
            canvas.DrawRect(ToRect(line.Bounds), lineStroke);
            foreach (var word in line.Words)
            {
                canvas.DrawRect(ToRect(word.Bounds), wordStroke);
                foreach (var glyph in word.Chars)
                    canvas.DrawRect(ToRect(glyph.Bounds), glyphStroke);
            }
        }
    }

    private static SKRect ToRect(SKRectI r) =>
        new(r.Left, r.Top, r.Right, r.Bottom);
}
