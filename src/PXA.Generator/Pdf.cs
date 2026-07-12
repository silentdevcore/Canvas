using PXA.Pdf;

namespace PXA.Generator;

/// <summary>
/// Power Dox Automation facade for the PXA PDF generator.
/// </summary>
public static class Pdf
{
    /// <summary>
    /// Creates a PDF document using the current PXA PDF implementation.
    /// </summary>
#pragma warning disable PXA0001 // The facade is the preferred public entry point for constructing PdfDocument.
    public static PdfDocument CreateDocument(PdfStandardFont defaultFont = PdfStandardFont.Helvetica) =>
        new(defaultFont);
#pragma warning restore PXA0001
}
