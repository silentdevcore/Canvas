namespace PVAD.Pdf;

internal sealed class PdfSignatureFieldAnnotation : IPdfAccessibleAnnotation
{
    public required string FieldName { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }
    public PdfAccessibilityProperties? Accessibility { get; init; }
}
