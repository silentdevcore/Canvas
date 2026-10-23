using PVAD.Pdf;

namespace PVAD.Generator;

/// <summary>
/// PVADE facade for the PVAD PDF generator.
/// </summary>
public static class Pdf
{
    /// <summary>
    /// Creates a PDF document using the current PVAD PDF implementation.
    /// </summary>
#pragma warning disable PXA0001 // The facade is the preferred public entry point for constructing PdfDocument.
    public static PdfDocument CreateDocument(PdfStandardFont defaultFont = PdfStandardFont.Helvetica) =>
        new(defaultFont);
#pragma warning restore PXA0001
}
