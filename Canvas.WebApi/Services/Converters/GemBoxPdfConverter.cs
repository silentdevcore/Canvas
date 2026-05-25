namespace Canvas.WebApi.Services.Converters;

public sealed class GemBoxPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "GemBox";
    public override string FrameworkName => "GemBox.Pdf";
    public override string Description => "document.Pages.Add() → document.AddPage(); text content → page.DrawTextFromTop(...)";

    public override string ConvertCode(string sourceCode) =>
        SkeletonCanvasCode(FrameworkName) + $"""

        // --- GemBox.Pdf key mappings ---
        // using GemBox.Pdf → using Canvas.Pdf (add: using GemBoxPdf = GemBox.Pdf; to avoid name collision)
        // new PdfDocument()    → new Canvas.Pdf.PdfDocument()
        // document.Pages.Add() → document.AddPage()
        // text content         → page.DrawTextFromTop(text, x, y)
        // document.Save(path)  → document.Save(path)
        """;
}
