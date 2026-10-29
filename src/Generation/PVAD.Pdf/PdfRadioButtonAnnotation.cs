namespace PVAD.Pdf;

internal sealed class PdfRadioButtonAnnotation : IPdfAccessibleAnnotation
{
    public required string FieldName { get; init; }
    public required string ExportValue { get; init; }
    public required int OptionIndex { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }
    public bool IsSelected { get; init; }
    public PdfAccessibilityProperties? Accessibility { get; init; }
}
