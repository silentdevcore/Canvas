namespace Canvas.WebApi.Services.Converters;

public sealed class IronPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "IronPdf";
    public override string FrameworkName => "IronPDF";
    public override string Description => "IronPDF renders HTML via Chrome. Canvas is a low-level drawing API. Migration requires manual HTML-to-drawing conversion — out of scope for v1.";

    public override string ConvertCode(string sourceCode) =>
        """
        // IronPDF uses HTML/CSS rendering via a headless Chrome browser.
        // Canvas.Pdf is a low-level vector drawing API — there is no automatic
        // HTML-to-Canvas conversion.
        //
        // Migration strategy:
        //   1. Identify what content your HTML template renders (text, images, tables).
        //   2. Re-express each element as explicit Canvas.Pdf draw calls.
        //   3. Use page.DrawTextFromTop(), page.DrawRectangleFromTop(), page.DrawImageFromTop(), etc.
        //
        // Minimal Canvas.Pdf equivalent:
        using Canvas.Pdf;

        var document = new PdfDocument();
        var page = document.AddPage();

        // Replace with explicit layout matching your HTML template:
        page.DrawTextFromTop("Hello from Canvas.Pdf", x: 40, y: 40, fontSize: 24);

        document.Save("output.pdf");
        """;

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode) =>
    [
        new MigrationDiagnostic("CANMIGIRONPDF001", "Warning",
            "IronPDF is HTML-to-PDF. Automatic conversion to Canvas draw calls is not supported. Manual rewrite required.")
    ];
}
