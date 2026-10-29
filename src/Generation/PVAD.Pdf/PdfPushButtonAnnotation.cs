namespace PVAD.Pdf;

internal enum PdfPushButtonAction
{
    None,
    Submit,
    Reset
}

internal sealed class PdfPushButtonAnnotation : IPdfAccessibleAnnotation
{
    public required string FieldName { get; init; }
    public required string Caption { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }
    public PdfPushButtonAction Action { get; init; }
    public PdfAccessibilityProperties? Accessibility { get; init; }
}
