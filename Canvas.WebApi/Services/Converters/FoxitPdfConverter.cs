namespace Canvas.WebApi.Services.Converters;

public sealed class FoxitPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "Foxit";
    public override string FrameworkName => "Foxit PDF SDK";
    public override string Description => "new PDFDoc() → new PdfDocument(); page content drawing via Graphics object.";

    public override string ConvertCode(string sourceCode) =>
        SkeletonCanvasCode(FrameworkName) + $"""

        // --- Foxit PDF SDK key mappings ---
        // using foxit.pdf → using Canvas.Pdf
        // new PDFDoc()             → new Canvas.Pdf.PdfDocument()
        // doc.InsertPage(...)      → document.AddPage()
        // Graphics text/line/rect  → page.DrawTextFromTop / DrawLineFromTop / DrawRectangleFromTop
        // doc.Save(path, ...)      → document.Save(path)
        """;
}
