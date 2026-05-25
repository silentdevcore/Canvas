namespace Canvas.WebApi.Services.Converters;

public sealed class LeadtoolsPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "Leadtools";
    public override string FrameworkName => "LEADTOOLS";
    public override string Description => "LEADTOOLS is primarily raster/OCR/document conversion. Direct PDF generation mapping is partial.";

    public override string ConvertCode(string sourceCode) =>
        SkeletonCanvasCode(FrameworkName) + $"""

        // --- LEADTOOLS key mappings ---
        // Leadtools.Pdf.PdfDocument            → new Canvas.Pdf.PdfDocument()
        // document.Pages.Add(new PdfPage(...)) → document.AddPage()
        // PdfDocumentWriter text drawing        → page.DrawTextFromTop(text, x, y)
        // document.Save(path)                  → document.Save(path)
        //
        // NOTE: LEADTOOLS raster/OCR/document conversion pipelines are out of scope.
        // Only vector PDF generation operations are mapped above.
        """;
}
