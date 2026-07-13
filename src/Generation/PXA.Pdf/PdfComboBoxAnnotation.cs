namespace PXA.Pdf;

internal sealed class PdfComboBoxAnnotation
{
    public required string FieldName { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }
    public required IReadOnlyList<string> Options { get; init; }
    public string? SelectedValue { get; init; }
    public double FontSize { get; init; } = 10;
}
