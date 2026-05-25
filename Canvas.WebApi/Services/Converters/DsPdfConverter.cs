namespace Canvas.WebApi.Services.Converters;

public sealed class DsPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "DsPdf";
    public override string FrameworkName => "DsPdf (GrapeCity)";
    public override string Description => "new GcPdfDocument() → new PdfDocument(); page.Graphics.DrawString(...) → page.DrawTextFromTop(...)";

    public override string ConvertCode(string sourceCode) =>
        SkeletonCanvasCode(FrameworkName) + $"""

        // --- DsPdf (GrapeCity) key mappings ---
        // using GrapeCity.Documents.Pdf → using Canvas.Pdf
        // new GcPdfDocument()                      → new Canvas.Pdf.PdfDocument()
        // doc.NewPage()                            → document.AddPage()
        // page.Graphics.DrawString(text, fmt, pt)  → page.DrawTextFromTop(text, pt.X, pt.Y)
        // doc.Save(path)                           → document.Save(path)
        """;
}
