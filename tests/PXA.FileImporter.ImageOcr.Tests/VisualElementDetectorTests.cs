using SkiaSharp;

namespace PXA.FileImporter.ImageOcr.Tests;

public sealed class VisualElementDetectorTests
{
    [Fact]
    public void DetectRuleSegmentsAndShapes_FindsRulesAndRectanglesWithoutOcrText()
    {
        using var bitmap = new SKBitmap(180, 120, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = false,
            StrokeWidth = 1,
        })
        {
            canvas.Clear(SKColors.White);
            canvas.DrawLine(20, 20, 160, 20, paint);
            canvas.DrawLine(20, 60, 160, 60, paint);
            canvas.DrawLine(20, 100, 160, 100, paint);
            canvas.DrawLine(20, 20, 20, 100, paint);
            canvas.DrawLine(90, 20, 90, 100, paint);
            canvas.DrawLine(160, 20, 160, 100, paint);
        }

        var segments = VisualElementDetector.DetectRuleSegments(bitmap);
        Assert.True(segments.Count >= 6);
        Assert.Contains(segments, s => s.Orientation == RuleOrientation.Horizontal);
        Assert.Contains(segments, s => s.Orientation == RuleOrientation.Vertical);

        var shapes = VisualElementDetector.DetectShapes(segments);
        Assert.Contains(shapes, s =>
            s.Kind == OcrShapeKind.Rectangle &&
            s.Bounds.X is >= 18 and <= 22 &&
            s.Bounds.Y is >= 18 and <= 22 &&
            s.Bounds.Width is >= 60 and <= 145);
    }

    [Fact]
    public void DetectRuleSegments_FaintLine_DetectedOnlyBelowContrastThreshold()
    {
        using var bitmap = new SKBitmap(180, 60, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint
        {
            Color = new SKColor(240, 240, 240), // ~15 luma contrast against white
            IsAntialias = false,
            StrokeWidth = 1,
        })
        {
            canvas.Clear(SKColors.White);
            canvas.DrawLine(20, 30, 160, 30, paint);
        }

        // Faint line is found with the more-sensitive default threshold...
        var sensitive = VisualElementDetector.DetectRuleSegments(bitmap, minContrast: 12);
        Assert.Contains(sensitive, s => s.Orientation == RuleOrientation.Horizontal);

        // ...but skipped at the original, stricter threshold.
        var strict = VisualElementDetector.DetectRuleSegments(bitmap, minContrast: 18);
        Assert.DoesNotContain(strict, s => s.Orientation == RuleOrientation.Horizontal);
    }

    [Fact]
    public void DetectCheckboxes_FindsCheckboxWithoutOcrText()
    {
        using var bitmap = new SKBitmap(120, 80, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
        })
        {
            canvas.Clear(SKColors.White);
            canvas.DrawRect(24, 26, 18, 18, paint);
        }

        var checkboxes = VisualElementDetector.DetectCheckboxes(bitmap, []);

        var checkbox = Assert.Single(checkboxes);
        Assert.Equal("empty", checkbox.State);
        Assert.InRange(checkbox.Confidence, 0.8, 1.0);
        Assert.InRange(checkbox.Bounds.X, 23, 25);
        Assert.InRange(checkbox.Bounds.Y, 25, 27);
        Assert.InRange(checkbox.Bounds.Width, 18, 19);
        Assert.InRange(checkbox.Bounds.Height, 18, 19);
    }
}
