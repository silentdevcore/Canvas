namespace Canvas.WebApi.Services.Converters;

public sealed class ActivePdfConverter : BasePdfConverter
{
    public override string FrameworkId => "ActivePdf";
    public override string FrameworkName => "ActivePDF";
    public override string Description => "ActivePDF API identification not yet confirmed. Placeholder converter.";

    public override string ConvertCode(string sourceCode) =>
        SkeletonCanvasCode(FrameworkName) + $"""

        // --- ActivePDF key mappings (to be confirmed) ---
        // new DocConverter() / new Toolkit()  → new Canvas.Pdf.PdfDocument()
        // AddPage / BeginPage                 → document.AddPage()
        // PrintText(text, x, y)               → page.DrawTextFromTop(text, x, y)
        // Save / CloseDocument                → document.Save(path)
        //
        // NOTE: ActivePDF has multiple products (Toolkit, DocConverter, WebGrabber).
        // Identify which product you are migrating from and map accordingly.
        """;
}
