namespace PVAD.Pdf;

internal sealed class PdfListBoxAnnotation : IPdfAccessibleAnnotation
{
    public required string FieldName { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }
    public required IReadOnlyList<string> Options { get; init; }
    public IReadOnlyList<string> SelectedValues { get; init; } = [];
    public bool MultiSelect { get; init; }
    public double FontSize { get; init; } = 10;
    public PdfAccessibilityProperties? Accessibility { get; init; }
}
