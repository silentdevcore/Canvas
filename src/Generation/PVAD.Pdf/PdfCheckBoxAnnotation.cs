namespace PVAD.Pdf;

public sealed record PdfCheckBoxAnnotation : IPdfAccessibleAnnotation
{
    public required string FieldName { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }
    public bool IsChecked { get; init; } = false;
    PdfAccessibilityProperties? IPdfAccessibleAnnotation.Accessibility => Accessibility;
    internal PdfAccessibilityProperties? Accessibility { get; init; }
}
