namespace PXA.Pdf;

public sealed class PdfStrokeStyle
{
    public static PdfStrokeStyle Default { get; } = new();

    public double LineWidth { get; init; } = 1;

    public PdfLineCapStyle LineCap { get; init; } = PdfLineCapStyle.Butt;

    public PdfLineJoinStyle LineJoin { get; init; } = PdfLineJoinStyle.Miter;

    public IReadOnlyList<double>? DashArray { get; init; }

    public double DashPhase { get; init; }
}
