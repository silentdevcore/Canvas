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
    public static PdfDocument CreateDocument(PdfStandardFont defaultFont = PdfStandardFont.Helvetica) =>
        new(defaultFont);
}
