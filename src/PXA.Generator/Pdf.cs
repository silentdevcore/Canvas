using Canvas.Pdf;

namespace PXA.Generator;

/// <summary>
/// Additive Power Dox Automation facade for the current Canvas PDF generator.
/// </summary>
public static class Pdf
{
    /// <summary>
    /// Creates a PDF document using the current Canvas.Pdf implementation.
    /// </summary>
#pragma warning disable PXA0001 // PXA facade intentionally delegates to the legacy implementation during compatibility window.
    public static PdfDocument CreateDocument(PdfStandardFont defaultFont = PdfStandardFont.Helvetica) =>
        new(defaultFont);
#pragma warning restore PXA0001
}
