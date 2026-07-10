using SkiaSharp;

namespace PXA.FileImporter.ImageAnalysis.Analysis;

/// <summary>
/// Base class for all elements extracted from a raster image during analysis.
/// Mirrors the PrimitiveObject hierarchy in Canvas.Importer (the PDF engine).
/// </summary>
public abstract class ImagePrimitive
{
    /// <summary>Bounding box in original image pixels (before scale normalisation).</summary>
    public required SKRectI Bounds { get; init; }

    /// <summary>Rendering Z-order; lower = further back.</summary>
    public int ZOrder { get; init; }
}

/// <summary>
/// A region of uniform or near-uniform colour — backgrounds, filled panels, dividers.
/// Produced by Phase 2 (colour/region analysis).
/// </summary>
public sealed class ImageRegionPrimitive : ImagePrimitive
{
    public required SKColor FillColor { get; init; }

    /// <summary>Fraction of total image area this region covers (0–1).</summary>
    public double Coverage { get; init; }

    /// <summary>Optional semantic classification such as image-region.</summary>
    public string? AnalysisType { get; init; }

    /// <summary>Detection confidence 0–1.</summary>
    public double Confidence { get; init; } = 0.90;

    /// <summary>Source classifier that explains how the region was detected.</summary>
    public string? SourceKind { get; init; }
}

/// <summary>
/// A geometric shape: axis-aligned rectangle, thin line, or ellipse.
/// Produced by Phase 3 (shape detection).
/// </summary>
public sealed class ImageShapePrimitive : ImagePrimitive
{
    public required ShapeKind Kind { get; init; }

    /// <summary>Fill colour; <c>SKColors.Transparent</c> if the shape is unfilled.</summary>
    public SKColor FillColor { get; init; } = SKColors.Transparent;

    /// <summary>Stroke colour; <c>SKColors.Transparent</c> if no visible border.</summary>
    public SKColor StrokeColor { get; init; } = SKColors.Transparent;

    /// <summary>Estimated stroke width in pixels.</summary>
    public int StrokeWidth { get; init; }

    /// <summary>Detection confidence 0–1.</summary>
    public double Confidence { get; init; }

    /// <summary>Optional semantic classification such as grid-line.</summary>
    public string? AnalysisType { get; init; }

    /// <summary>Optional grouped grid/table id for related line primitives.</summary>
    public int? GridId { get; init; }

    /// <summary>Optional orientation for grouped grid/table line primitives.</summary>
    public string? GridOrientation { get; init; }

    /// <summary>Optional bounds of the grouped grid/table in source pixels.</summary>
    public SKRectI? GridBounds { get; init; }

    /// <summary>Optional estimated corner radius in source pixels for rounded rectangles.</summary>
    public double? CornerRadiusPx { get; init; }
}

public enum ShapeKind
{
    Rect,
    Line,
    Ellipse,
    Icon,
}

/// <summary>
/// A single recognised character with its position and confidence.
/// Produced by Phase 4 (text engine).
/// </summary>
public sealed class RecognizedChar
{
    public required char Value { get; init; }
    public required SKRectI Bounds { get; init; }

    /// <summary>NCC match score 0–1; 0 for placeholder '?' characters.</summary>
    public double Confidence { get; init; }

    public GlyphRecognitionDiagnostics? Diagnostics { get; init; }
}

public sealed class GlyphRecognitionDiagnostics
{
    public required char InitialCandidate { get; init; }
    public required char SelectedCandidate { get; init; }
    public required string Method { get; init; }
    public double Score { get; init; }
    public int EnclosedWhiteRegions { get; init; }
    public bool ProjectionReranked { get; init; }
    public bool ZoningReranked { get; init; }
    public IReadOnlyDictionary<string, double> Signals { get; init; } = new Dictionary<string, double>();
    public IReadOnlyDictionary<string, double> DecisionWeights { get; init; } = new Dictionary<string, double>();
}

/// <summary>
/// A word (horizontal run of characters) within a text line.
/// </summary>
public sealed class RecognizedWord
{
    public required IReadOnlyList<RecognizedChar> Chars { get; init; }
    public required SKRectI Bounds { get; init; }
    public string Text => string.Concat(Chars.Select(c => c.Value));
}

/// <summary>
/// A text line: an ordered sequence of words on a single baseline.
/// Produced by Phase 4 (text engine).
/// </summary>
public sealed class ImageTextPrimitive : ImagePrimitive
{
    public required IReadOnlyList<RecognizedWord> Words { get; init; }
    public string Text => string.Join(" ", Words.Select(w => w.Text));

    /// <summary>Estimated font size in pixels (median character blob height).</summary>
    public double FontSizePx { get; init; }

    /// <summary>Estimated text baseline in source pixels.</summary>
    public double BaselineY { get; init; }

    /// <summary>Reading-order block id assigned during scene assembly.</summary>
    public int? TextBlockId { get; init; }

    /// <summary>Zero-based line index within the assigned text block.</summary>
    public int TextBlockLineIndex { get; init; }

    /// <summary>Dominant text colour sampled from the original image.</summary>
    public SKColor TextColor { get; init; } = SKColors.Black;
}

/// <summary>
/// Fully preprocessed image ready for analysis phases 2–4.
/// </summary>
public sealed class PreparedImage : IDisposable
{
    public required SKBitmap Original  { get; init; }
    public required SKBitmap Grayscale { get; init; }
    public required SKBitmap Binary    { get; init; }

    /// <summary>Width/height of the working bitmaps (after normalisation scaling).</summary>
    public int Width  => Original.Width;
    public int Height => Original.Height;

    /// <summary>
    /// Factor applied when scaling down the source image.
    /// Use this to convert working-resolution pixel coords back to original pixel coords
    /// when you need to sample <see cref="Original"/> at a position computed in working space.
    /// </summary>
    public double ScaleFactor { get; init; } = 1.0;

    public void Dispose()
    {
        Original.Dispose();
        Grayscale.Dispose();
        Binary.Dispose();
    }
}
