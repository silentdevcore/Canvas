namespace Canvas.WebApi.Services.Converters;

public sealed class PdfKitNetConverter : BasePdfConverter
{
    public override string FrameworkId => "PdfKitNet";
    public override string FrameworkName => "PDFKit.NET";
    public override string Description => "PDFKit.NET package identity unconfirmed. Placeholder converter.";

    public override string ConvertCode(string sourceCode) =>
        SkeletonCanvasCode(FrameworkName) + $"""

        // --- PDFKit.NET key mappings (package identity to be confirmed) ---
        // new Document()          → new Canvas.Pdf.PdfDocument()
        // document.NewPage()      → document.AddPage()
        // page.DrawText(text)     → page.DrawTextFromTop(text, x, y)
        // document.Render(path)   → document.Save(path)
        //
        // NOTE: PDFKit.NET namespace and package name must be confirmed before
        // implementing full pattern matching.
        """;
}
