namespace Canvas.WebApi.Services.Converters;

public sealed class SpirePdfConverter : BasePdfConverter
{
    public override string FrameworkId => "Spire";
    public override string FrameworkName => "Spire.PDF";
    public override string Description => "new PdfDocument() → alias to Canvas.Pdf.PdfDocument; page.Canvas.DrawString(...) → page.DrawTextFromTop(...)";

    public override string ConvertCode(string sourceCode) =>
        SkeletonCanvasCode(FrameworkName) + $"""

        // --- Spire.PDF key mappings ---
        // using Spire.Pdf → using Canvas.Pdf (add: using SpirePdf = Spire.Pdf; to avoid name collision)
        // new PdfDocument()                          → new Canvas.Pdf.PdfDocument()
        // document.Pages.Add(...)                    → document.AddPage()
        // page.Canvas.DrawString(text, font, brush, x, y) → page.DrawTextFromTop(text, x, y, fontSize)
        // document.SaveToFile(path)                  → document.Save(path)
        """;
}
