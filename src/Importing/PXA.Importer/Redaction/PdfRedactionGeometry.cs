using PXA.Importer.Graphics;

namespace PXA.Importer.Redaction;

/// <summary>
/// Shared by preflight and apply so that what apply removes is exactly what preflight
/// promised. Divergence between the two would be a correctness defect, not a detail.
/// </summary>
internal static class PdfRedactionGeometry
{
    public static PdfRedactionCoverage Classify(PdfRectangle element, PdfRectangle region)
    {
        if (element.Width <= 0 || element.Height <= 0 || region.Width <= 0 || region.Height <= 0)
        {
            return PdfRedactionCoverage.None;
        }

        if (!element.Intersects(region))
        {
            return PdfRedactionCoverage.None;
        }

        return element.Left >= region.Left && element.Right <= region.Right &&
            element.Bottom >= region.Bottom && element.Top <= region.Top
            ? PdfRedactionCoverage.Contained
            : PdfRedactionCoverage.Partial;
    }
}
