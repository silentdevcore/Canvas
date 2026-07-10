namespace PXA.FileImporter.ImageOcr;

// Shared internal layout models used across the image-OCR pipeline stages
// (VisualElementDetector, OcrVisualFusionEngine, CanvasElementBuilder).
// These were previously private nested types on ImageToPdfConverter; they were
// promoted to top-level internal types so the split pipeline stages can share them.

internal enum RuleOrientation
{
    Horizontal,
    Vertical,
}

internal readonly record struct RulePixelScore(double Contrast);

internal sealed record RuleSegment(RuleOrientation Orientation, int X, int Y, int Length, double Contrast);

internal enum OcrShapeKind
{
    HorizontalLine,
    VerticalLine,
    Rectangle,
    FilledRectangle,
    Circle,
    Ellipse,
}

internal sealed record OcrShapeCandidate(OcrShapeKind Kind, OcrBoundingBox Bounds, string? FillColor = null);

internal sealed record FillComponent(OcrBoundingBox Bounds, int PixelCount);

internal sealed record OcrCheckboxCandidate(OcrBoundingBox Bounds, string State, double Confidence);

internal sealed record OcrFieldCandidate(OcrBoundingBox Bounds, OcrLine LabelLine, double Confidence);

internal sealed record OcrSignatureCandidate(OcrBoundingBox Bounds, OcrLine LabelLine, double Confidence);

internal sealed record OcrImageRegionCandidate(OcrBoundingBox Bounds, double Confidence);

internal enum OcrTextRole
{
    Body,
    Heading,
    Caption,
}

internal sealed record OcrTextGroup(
    IReadOnlyList<OcrLine> Lines,
    OcrTextRole Role,
    int ColumnIndex,
    int ColumnCount);

internal sealed record OcrTextColumn(IReadOnlyList<OcrLine> Lines, int Index, int Count);

internal sealed record OcrTextRun(
    string Text,
    double X,
    double Y,
    double Width,
    double Height,
    double FontSize,
    string Color,
    double Confidence,
    OcrBoundingBox SourceBounds);

internal sealed record OcrTableCandidate(
    IReadOnlyList<OcrLine> Lines,
    IReadOnlyList<double> ColumnAnchors,
    OcrBoundingBox? RuleBounds,
    OcrBoundingBox? BackgroundBounds,
    IReadOnlyList<IReadOnlyList<OcrLine>> RowGroups,
    string Detector,
    string? RuleRejectionReason);

internal sealed record OcrTableRowCandidate(
    IReadOnlyList<OcrLine> Lines,
    double[] Anchors);

internal sealed record RuleBoundsMatch(OcrBoundingBox? Bounds, string? RejectionReason);

internal sealed record ImagePlacement(double X, double Y, double Width, double Height, double Scale);
