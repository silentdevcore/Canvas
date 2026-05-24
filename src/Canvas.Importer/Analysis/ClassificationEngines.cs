using Canvas.Importer.Graphics;

namespace Canvas.Importer.Analysis;

public sealed record BarcodeDetection(PrimitiveClassification Kind, double Confidence, PdfRectangle Bounds);

public sealed class BarcodeDetector
{
    public BarcodeDetection? DetectLinearBarcode(IEnumerable<PrimitiveObject> primitives)
    {
        var bars = ReadingOrderEngine.Flatten(primitives)
            .OfType<PrimitiveShape>()
            .Where(static primitive => primitive.Bounds.Height > primitive.Bounds.Width * 4d && primitive.Bounds.Width <= 6d)
            .OrderBy(static primitive => primitive.Bounds.Left)
            .ToArray();

        if (bars.Length < 8)
        {
            return null;
        }

        var bounds = ReadingOrderEngine.Union(bars.Select(static bar => bar.Bounds));
        var density = bars.Sum(static bar => bar.Bounds.Width) / Math.Max(bounds.Width, 1d);
        return density is > 0.25d and < 0.85d
            ? new BarcodeDetection(PrimitiveClassification.LinearBarcode, Math.Min(0.95d, 0.45d + bars.Length / 50d), bounds)
            : null;
    }

    public BarcodeDetection? DetectMatrixBarcode(IEnumerable<PrimitiveObject> primitives)
    {
        var cells = ReadingOrderEngine.Flatten(primitives)
            .OfType<PrimitiveShape>()
            .Where(static primitive => Math.Abs(primitive.Bounds.Width - primitive.Bounds.Height) <= Math.Max(primitive.Bounds.Width, primitive.Bounds.Height) * 0.25d)
            .ToArray();

        if (cells.Length < 16)
        {
            return null;
        }

        var bounds = ReadingOrderEngine.Union(cells.Select(static cell => cell.Bounds));
        var area = Math.Max(bounds.Width * bounds.Height, 1d);
        var fillRatio = cells.Sum(static cell => cell.Bounds.Width * cell.Bounds.Height) / area;
        return fillRatio is > 0.15d and < 0.75d
            ? new BarcodeDetection(PrimitiveClassification.MatrixBarcode, Math.Min(0.95d, 0.35d + cells.Length / 100d), bounds)
            : null;
    }
}

public sealed class ObjectClassifier
{
    private readonly BarcodeDetector _barcodeDetector;

    public ObjectClassifier(BarcodeDetector? barcodeDetector = null)
    {
        _barcodeDetector = barcodeDetector ?? new BarcodeDetector();
    }

    public void Classify(IReadOnlyList<PrimitiveObject> primitives)
    {
        foreach (var primitive in ReadingOrderEngine.Flatten(primitives))
        {
            primitive.Classification = ClassifyOne(primitive);
        }

        var linearBarcode = _barcodeDetector.DetectLinearBarcode(primitives);
        MarkBarcode(primitives, linearBarcode);

        var matrixBarcode = _barcodeDetector.DetectMatrixBarcode(primitives);
        MarkBarcode(primitives, matrixBarcode);
    }

    private static PrimitiveClassification ClassifyOne(PrimitiveObject primitive)
    {
        return primitive switch
        {
            PrimitiveText text when IsSymbolFontIcon(text) => PrimitiveClassification.SymbolFontIcon,
            PrimitiveText => PrimitiveClassification.Text,
            PrimitiveImage => PrimitiveClassification.Image,
            PrimitiveShape shape when IsSeparator(shape) => PrimitiveClassification.Separator,
            PrimitiveShape shape when IsTableLine(shape) => PrimitiveClassification.TableLine,
            PrimitivePath path when IsVectorIcon(path) => PrimitiveClassification.VectorIcon,
            PrimitivePath path when IsDecoration(path) => PrimitiveClassification.Decoration,
            _ => primitive.Classification
        };
    }

    private static bool IsVectorIcon(PrimitivePath path)
    {
        var area = Math.Max(path.Bounds.Width * path.Bounds.Height, 1d);
        var density = path.Segments.Count / area;
        return path.Bounds.Width <= 64d && path.Bounds.Height <= 64d && path.Segments.Count >= 4 && density > 0.001d;
    }

    private static bool IsSymbolFontIcon(PrimitiveText text)
    {
        var font = text.FontName ?? string.Empty;
        return text.Text.Length <= 3 &&
            (font.Contains("FontAwesome", StringComparison.OrdinalIgnoreCase) ||
             font.Contains("Wingdings", StringComparison.OrdinalIgnoreCase) ||
             font.Contains("Symbol", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSeparator(PrimitiveShape shape)
    {
        return shape.Bounds.Width > shape.Bounds.Height * 20d || shape.Bounds.Height > shape.Bounds.Width * 20d;
    }

    private static bool IsTableLine(PrimitiveShape shape)
    {
        return IsSeparator(shape) && shape.GraphicsState.LineWidth <= 2d;
    }

    private static bool IsDecoration(PrimitivePath path)
    {
        return path.Bounds.Width <= 200d && path.Bounds.Height <= 40d && path.GraphicsState.LineWidth <= 2d;
    }

    private static void MarkBarcode(IReadOnlyList<PrimitiveObject> primitives, BarcodeDetection? detection)
    {
        if (detection is null)
        {
            return;
        }

        foreach (var primitive in ReadingOrderEngine.Flatten(primitives).Where(primitive => primitive.Bounds.Intersects(detection.Bounds)))
        {
            primitive.Classification = detection.Kind;
        }
    }
}
