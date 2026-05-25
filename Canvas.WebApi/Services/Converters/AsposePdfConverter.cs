namespace Canvas.WebApi.Services.Converters;

public sealed class AsposePdfConverter : BasePdfConverter
{
    public override string FrameworkId => "Aspose";
    public override string FrameworkName => "Aspose.PDF";
    public override string Description => "new Document() → new PdfDocument() (alias needed); page.Paragraphs.Add(TextFragment) → page.DrawTextFromTop(...)";

    public override string ConvertCode(string sourceCode) =>
        SkeletonCanvasCode(FrameworkName) + $"""

        // --- Aspose.PDF key mappings ---
        // using Aspose.Pdf → using Canvas.Pdf (add: using AsposePdf = Aspose.Pdf; if needed)
        // new Document()           → new Canvas.Pdf.PdfDocument()
        // document.Pages.Add()    → document.AddPage()
        // new TextFragment("...")  → page.DrawTextFromTop("...", x, y)
        // document.Save(path)     → document.Save(path)
        """;
}
