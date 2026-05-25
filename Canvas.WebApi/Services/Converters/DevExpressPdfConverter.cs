namespace Canvas.WebApi.Services.Converters;

public sealed class DevExpressPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "DevExpress";
    public override string FrameworkName => "DevExpress PDF";
    public override string Description => "DevExpress has both PDF processor (read/edit) and generation APIs. Mapping depends on which product is used.";

    public override string ConvertCode(string sourceCode) =>
        SkeletonCanvasCode(FrameworkName) + $"""

        // --- DevExpress PDF key mappings ---
        // DevExpress.Pdf.PdfDocumentProcessor → read-only operations, out of scope
        //
        // For DevExpress XtraReports / PDF export:
        // PdfExportOptions / XtraReport.ExportToPdf → manual redraw with Canvas.Pdf
        //
        // Direct drawing (DevExpress.Drawing):
        // new PdfDocument()               → new Canvas.Pdf.PdfDocument()
        // document.Pages.Add()            → document.AddPage()
        // page.Canvas.DrawString(text, …) → page.DrawTextFromTop(text, x, y)
        // document.Save(path)             → document.Save(path)
        //
        // NOTE: Confirm which DevExpress PDF product you are migrating from
        // before applying the mappings above.
        """;
}
