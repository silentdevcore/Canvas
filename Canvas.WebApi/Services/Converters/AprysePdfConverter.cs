using System.Text.RegularExpressions;

namespace Canvas.WebApi.Services.Converters;

public sealed class AprysePdfConverter : BasePdfConverter
{
    public override string FrameworkId => "Apryse";
    public override string FrameworkName => "Apryse (PDFTron)";
    public override string Description => "Basic document/page/save mapping. new PDFDoc() → new PdfDocument(); PageCreate+PagePushBack → AddPage()";

    public override string ConvertCode(string sourceCode) =>
        SkeletonCanvasCode(FrameworkName) + $"""

        // --- Apryse key mappings ---
        // new PDFDoc()                          → new Canvas.Pdf.PdfDocument()
        // doc.PageCreate() + doc.PagePushBack() → document.AddPage()
        // ElementBuilder.CreateText(...)        → page.DrawTextFromTop(...)
        // doc.Save(path, ...)                   → document.Save(path)
        """;
}
