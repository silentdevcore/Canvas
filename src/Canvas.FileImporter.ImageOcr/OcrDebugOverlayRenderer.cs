using SkiaSharp;

namespace Canvas.FileImporter.ImageOcr;

internal static class OcrDebugOverlayRenderer
{
    public static byte[] Render(SKBitmap source, IReadOnlyList<OcrPage> pages)
    {
        using var overlay = new SKBitmap(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(overlay);
        canvas.DrawBitmap(source, 0, 0);

        using var linePaint = new SKPaint
        {
            Color = new SKColor(37, 99, 235, 210),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(2, source.Width / 600f),
            IsAntialias = false,
        };

        using var wordPaint = new SKPaint
        {
            Color = new SKColor(22, 163, 74, 210),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(1, source.Width / 900f),
            IsAntialias = false,
        };

        foreach (var page in pages)
        {
            foreach (var line in page.Blocks.SelectMany(b => b.Lines))
            {
                DrawRect(canvas, line.Bounds, linePaint);
                foreach (var word in line.Words)
                    DrawRect(canvas, word.Bounds, wordPaint);
            }
        }

        using var image = SKImage.FromBitmap(overlay);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void DrawRect(SKCanvas canvas, OcrBoundingBox bounds, SKPaint paint)
    {
        canvas.DrawRect(bounds.X, bounds.Y, bounds.Width, bounds.Height, paint);
    }
}
