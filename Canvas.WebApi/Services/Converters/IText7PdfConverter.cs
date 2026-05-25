namespace Canvas.WebApi.Services.Converters;

public sealed class IText7PdfConverter : BasePdfConverter
{
    public override string FrameworkId => "iText7";
    public override string FrameworkName => "iText7";
    public override string Description => "PdfWriter+PdfDocument+Document triple → new PdfDocument(); doc.Add(new Paragraph) → page.DrawTextFromTop(...)";

    public override string ConvertCode(string sourceCode) =>
        SkeletonCanvasCode(FrameworkName) + $"""

        // --- iText7 key mappings ---
        // using iText.Kernel.Pdf; using iText.Layout; → using Canvas.Pdf;
        // new PdfWriter(path) + new PdfDocument(writer) + new Document(pdf)
        //   → var document = new Canvas.Pdf.PdfDocument();
        //      var page = document.AddPage();
        // document.Add(new Paragraph("text")) → page.DrawTextFromTop("text", x, y)
        // document.Add(new Table(...))        → page.DrawTable(...) [review manually]
        // document.Close()                    → document.Save(path)
        """;
}
