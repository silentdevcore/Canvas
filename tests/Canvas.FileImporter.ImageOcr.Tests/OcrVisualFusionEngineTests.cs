using SkiaSharp;

namespace Canvas.FileImporter.ImageOcr.Tests;

public sealed class OcrVisualFusionEngineTests
{
    [Fact]
    public void DetectTables_GroupsAlignedRowsAndLeavesOtherTextOut()
    {
        var row1 = MakeLine(
            "Qty Price",
            28,
            28,
            116,
            12,
            [
                MakeWord("Qty", 28, 28, 26, 12),
                MakeWord("Price", 104, 28, 40, 12),
            ]);
        var row2 = MakeLine(
            "2 19",
            30,
            68,
            92,
            12,
            [
                MakeWord("2", 30, 68, 10, 12),
                MakeWord("19", 104, 68, 18, 12),
            ]);
        var outside = MakeLine("Thank you", 24, 134, 70, 12);

        var tables = OcrVisualFusionEngine.DetectTables([row1, row2, outside], new ImageToPdfConversionOptions());

        var table = Assert.Single(tables);
        Assert.Equal([row1, row2], table.Lines);
        Assert.Equal(2, table.ColumnAnchors.Count);
        Assert.Equal(2, table.RowGroups.Count);
        Assert.DoesNotContain(outside, table.Lines);
    }

    [Fact]
    public void DetectFields_MapsLeftLabelToRectangleField()
    {
        using var bitmap = NewWhiteBitmap(220, 80);
        var label = MakeLine("Name", 20, 34, 42, 12);
        var rectangle = new OcrShapeCandidate(OcrShapeKind.Rectangle, new OcrBoundingBox(78, 28, 120, 24));

        var fields = OcrVisualFusionEngine.DetectFields([rectangle], bitmap, [label]);

        var field = Assert.Single(fields);
        Assert.Same(label, field.LabelLine);
        Assert.Equal(new OcrBoundingBox(78, 28, 120, 24), field.Bounds);
    }

    [Fact]
    public void DetectFields_DoesNotConsumeFarawayParagraphText()
    {
        using var bitmap = NewWhiteBitmap(540, 260);
        var paragraph = MakeLine("This paragraph explains the form section.", 20, 24, 220, 16);
        // Rectangle is far to the right and far below the paragraph: no left or above label match.
        var rectangle = new OcrShapeCandidate(OcrShapeKind.Rectangle, new OcrBoundingBox(400, 200, 120, 24));

        var fields = OcrVisualFusionEngine.DetectFields([rectangle], bitmap, [paragraph]);

        Assert.Empty(fields);
    }

    [Fact]
    public void DetectSignatures_MapsLabelToHorizontalLine()
    {
        var label = MakeLine("Signature", 20, 30, 70, 12);
        var line = new OcrShapeCandidate(OcrShapeKind.HorizontalLine, new OcrBoundingBox(100, 36, 160, 1));

        var signatures = OcrVisualFusionEngine.DetectSignatures([line], [label]);

        var signature = Assert.Single(signatures);
        Assert.Same(label, signature.LabelLine);
        Assert.Equal(new OcrBoundingBox(100, 36, 160, 1), signature.Bounds);
    }

    [Fact]
    public void BuildTextGroups_KeepsParagraphTextStandalone()
    {
        var paragraph = MakeLine("Terms and conditions apply.", 20, 70, 160, 14);

        var groups = OcrVisualFusionEngine.BuildTextGroups([paragraph], new ImageToPdfConversionOptions());

        var group = Assert.Single(groups);
        Assert.Equal([paragraph], group.Lines);
    }

    private static SKBitmap NewWhiteBitmap(int width, int height)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        return bitmap;
    }

    private static OcrLine MakeLine(string text, int x, int y, int width, int height) =>
        MakeLine(text, x, y, width, height, [MakeWord(text, x, y, width, height)]);

    private static OcrLine MakeLine(
        string text,
        int x,
        int y,
        int width,
        int height,
        IReadOnlyList<OcrWord> words) =>
        new()
        {
            Text = text,
            Bounds = new OcrBoundingBox(x, y, width, height),
            Confidence = 0.91,
            Words = words,
        };

    private static OcrWord MakeWord(string text, int x, int y, int width, int height) =>
        new()
        {
            Text = text,
            Bounds = new OcrBoundingBox(x, y, width, height),
            Confidence = 0.91,
        };
}
