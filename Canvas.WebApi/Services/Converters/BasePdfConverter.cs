using Canvas.Pdf;

namespace Canvas.WebApi.Services.Converters;

public abstract class BasePdfConverter : ICodeConverter
{
    public abstract string FrameworkId { get; }
    public abstract string FrameworkName { get; }
    public virtual string Status => "skeleton";
    public abstract string Description { get; }

    public abstract string ConvertCode(string sourceCode);

    public virtual IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
        => Array.Empty<MigrationDiagnostic>();

    public virtual byte[] GeneratePreview(string sourceCode)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.DrawTextFromTop($"Migration preview: {FrameworkName}", x: 40, topY: 60,
            new PdfDrawTextOptions { FontSize = 18, Bold = true });
        page.DrawTextFromTop("Skeleton converter — full conversion not yet implemented.", x: 40, topY: 100,
            new PdfDrawTextOptions { FontSize = 12 });
        page.DrawTextFromTop("Convert your code using the Canvas.Pdf API:", x: 40, topY: 130,
            new PdfDrawTextOptions { FontSize = 12 });
        page.DrawTextFromTop("  var document = new PdfDocument();", x: 40, topY: 155,
            new PdfDrawTextOptions { FontSize = 11 });
        page.DrawTextFromTop("  var page = document.AddPage();", x: 40, topY: 175,
            new PdfDrawTextOptions { FontSize = 11 });
        page.DrawTextFromTop("  page.DrawTextFromTop(\"Hello\", 40, 40);", x: 40, topY: 195,
            new PdfDrawTextOptions { FontSize = 11 });
        page.DrawTextFromTop("  document.Save(path);", x: 40, topY: 215,
            new PdfDrawTextOptions { FontSize = 11 });
        return document.ToBytes();
    }

    protected static string SkeletonCanvasCode(string frameworkName) =>
        $"""
        // Converted from {frameworkName} — skeleton output
        // Full conversion not yet implemented. Use the pattern below as a starting point.
        using Canvas.Pdf;

        var document = new PdfDocument();
        var page = document.AddPage();

        page.DrawTextFromTop("Hello from Canvas.Pdf", x: 40, y: 40, fontSize: 14);

        document.Save("output.pdf");
        """;
}
