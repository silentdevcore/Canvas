namespace PXA.Pdf.Layout;

internal abstract record PdfPageElement
{
    internal PdfAccessibilityProperties? Accessibility { get; set; }

    internal bool IsArtifact { get; set; }
}
