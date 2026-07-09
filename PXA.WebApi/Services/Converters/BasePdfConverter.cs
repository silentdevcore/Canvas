using System.Text.RegularExpressions;
using Canvas.Pdf;

namespace PXA.WebApi.Services.Converters;

public abstract class BasePdfConverter : ICodeConverter
{
    public abstract string FrameworkId { get; }
    public abstract string FrameworkName { get; }
    public virtual string Status => "skeleton";
    public abstract string Description { get; }
    public virtual string Kind => "pdf";

    public abstract string ConvertCode(string sourceCode);

    public virtual IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
        => Array.Empty<MigrationDiagnostic>();

    public virtual byte[] GeneratePreview(string sourceCode)
    {
        var converted = ConvertCode(sourceCode);
#pragma warning disable PXA0001 // Converter preview replays legacy Canvas.Pdf migration output during compatibility window.
        var document = new PdfDocument();
#pragma warning restore PXA0001

        // Count AddPage() calls so the preview has the right number of pages
        var pageCount = Math.Max(1, Regex.Matches(converted, @"document\.AddPage\(\)").Count);

        for (var i = 0; i < pageCount; i++)
        {
            var page = document.AddPage();
            DrawPreviewChrome(page, FrameworkName);

            var rendered = ReplayCanvasCalls(page, converted);

            if (!rendered)
            {
                var pageLabel = pageCount > 1 ? $"Page {i + 1} of {pageCount}" : "";
                if (!string.IsNullOrEmpty(pageLabel))
                    page.DrawTextFromTop(pageLabel, x: 60, topY: 50,
                        new PdfDrawTextOptions { FontSize = 10, FillColor = PdfColor.FromRgb(107, 114, 128) });

                page.DrawTextFromTop("Content requires manual migration.",
                    x: 60, topY: string.IsNullOrEmpty(pageLabel) ? 70 : 70,
                    new PdfDrawTextOptions { FontSize = 13, Bold = true });
                page.DrawTextFromTop(
                    "The converted code structure is correct — see the code panel for the draw calls to add.",
                    x: 60, topY: 92,
                    new PdfDrawTextOptions { FontSize = 10, FillColor = PdfColor.FromRgb(107, 114, 128) });
            }
        }

        return document.ToBytes();
    }

    // Parses and replays recognisable Canvas.Pdf draw calls from converted source.
    // Returns true if at least one call was rendered.
    protected static bool ReplayCanvasCalls(PdfPage page, string code)
    {
        var rendered = false;

        // DrawTextFromTop("text", x: X, topY: Y, fontSize: F)
        foreach (Match m in Regex.Matches(code,
            @"page\.DrawTextFromTop\(\s*(""[^""]*"")\s*,\s*x:\s*([\d.]+)\s*,\s*topY:\s*([\d.]+)\s*,\s*(?:fontSize:\s*)?([\d.]+)"))
        {
            if (TryText(m, out var t)) { page.DrawTextFromTop(t.text, x: t.x + 20, topY: t.y, new PdfDrawTextOptions { FontSize = Math.Clamp(t.size, 6, 36) }); rendered = true; }
        }

        // DrawTextFromTop("text", X, Y, F)  positional
        foreach (Match m in Regex.Matches(code,
            @"page\.DrawTextFromTop\(\s*(""[^""]*"")\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)"))
        {
            if (TryText(m, out var t)) { page.DrawTextFromTop(t.text, x: t.x + 20, topY: t.y, new PdfDrawTextOptions { FontSize = Math.Clamp(t.size, 6, 36) }); rendered = true; }
        }

        // DrawText("text", X, Y[, F])  — bottom-left coords (iText7 style)
        foreach (Match m in Regex.Matches(code,
            @"page\.DrawText\(\s*(""[^""]*"")\s*,\s*([\d.]+)\s*,\s*([\d.]+)(?:\s*,\s*([\d.]+))?"))
        {
            if (!double.TryParse(m.Groups[2].Value, out var x) ||
                !double.TryParse(m.Groups[3].Value, out var y)) continue;
            var fs = double.TryParse(m.Groups[4].Value, out var f) ? Math.Clamp(f, 6, 36) : 12.0;
            var text = m.Groups[1].Value.Trim('"');
            page.DrawText(text, x + 20, y, new PdfDrawTextOptions { FontSize = fs });
            rendered = true;
        }

        // DrawLineFromTop(x1, y1, x2, y2[, lineWidth: W])
        foreach (Match m in Regex.Matches(code,
            @"page\.DrawLineFromTop\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)"))
        {
            if (!double.TryParse(m.Groups[1].Value, out var x1) ||
                !double.TryParse(m.Groups[2].Value, out var y1) ||
                !double.TryParse(m.Groups[3].Value, out var x2) ||
                !double.TryParse(m.Groups[4].Value, out var y2)) continue;
            page.DrawLineFromTop(x1 + 20, y1, x2 + 20, y2, lineWidth: 1);
            rendered = true;
        }

        // DrawLine(x1, y1, x2, y2[, W])  — bottom-left coords
        foreach (Match m in Regex.Matches(code,
            @"page\.DrawLine\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)"))
        {
            if (!double.TryParse(m.Groups[1].Value, out var x1) ||
                !double.TryParse(m.Groups[2].Value, out var y1) ||
                !double.TryParse(m.Groups[3].Value, out var x2) ||
                !double.TryParse(m.Groups[4].Value, out var y2)) continue;
            page.DrawLine(x1 + 20, y1, x2 + 20, y2, lineWidth: 1);
            rendered = true;
        }

        // DrawRectangleFromTop(x, y, w, h, ...)
        foreach (Match m in Regex.Matches(code,
            @"page\.DrawRectangleFromTop\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)"))
        {
            if (!double.TryParse(m.Groups[1].Value, out var rx) ||
                !double.TryParse(m.Groups[2].Value, out var ry) ||
                !double.TryParse(m.Groups[3].Value, out var rw) ||
                !double.TryParse(m.Groups[4].Value, out var rh)) continue;
            page.DrawRectangleFromTop(rx + 20, ry, rw, rh, lineWidth: 1);
            rendered = true;
        }

        // DrawRectangle(x, y, w, h, lineWidth, fill)  — bottom-left coords
        foreach (Match m in Regex.Matches(code,
            @"page\.DrawRectangle\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*(true|false)"))
        {
            if (!double.TryParse(m.Groups[1].Value, out var rx) ||
                !double.TryParse(m.Groups[2].Value, out var ry) ||
                !double.TryParse(m.Groups[3].Value, out var rw) ||
                !double.TryParse(m.Groups[4].Value, out var rh)) continue;
            var fill = m.Groups[6].Value == "true";
            page.DrawRectangle(rx + 20, ry, rw, rh, lineWidth: 1, fill: fill);
            rendered = true;
        }

        return rendered;
    }

    private static bool TryText(Match m, out (string text, double x, double y, double size) result)
    {
        result = default;
        if (!double.TryParse(m.Groups[2].Value, out var x) ||
            !double.TryParse(m.Groups[3].Value, out var y) ||
            !double.TryParse(m.Groups[4].Value, out var size)) return false;
        result = (m.Groups[1].Value.Trim('"'), x, y, size);
        return true;
    }

    protected static void DrawPreviewChrome(PdfPage page, string frameworkName)
    {
        const double pageHeight = 841.89; // A4
        const double pageWidth  = 595.28;
        const double stripWidth = 22;
        const double footerH    = 28;

        // Left vertical strip
        page.DrawRectangle(0, footerH, stripWidth, pageHeight - footerH,
            lineWidth: 0.001, fill: true, fillColor: PdfColor.FromRgb(30, 64, 175));

        // Vertical label
        var label = $"Code migration for {frameworkName}";
        page.DrawText(label,
            x: stripWidth / 2 + 4,
            y: pageHeight / 2 - 40,
            new PdfDrawTextOptions
            {
                FontSize = 9,
                Bold = true,
                FillColor = PdfColor.White,
                RotationDegrees = 90,
            });

        // Footer bar
        page.DrawRectangle(0, 0, pageWidth, footerH,
            lineWidth: 0.001, fill: true, fillColor: PdfColor.FromRgb(30, 64, 175));

        // Footer text
        page.DrawText("Generated with Canvas.Pdf · The modern .NET PDF library · canvas-pdf.io",
            x: 30, y: 9,
            new PdfDrawTextOptions { FontSize = 8, FillColor = PdfColor.White });
    }

    protected static string SkeletonCanvasCode(string frameworkName) =>
        $"""
        // Converted from {frameworkName} — skeleton output
        // Full conversion not yet implemented. Use the pattern below as a starting point.
        using Canvas.Pdf;

        var document = new PdfDocument();
        var page = document.AddPage();

        page.DrawTextFromTop("Hello from Canvas.Pdf", x: 40, topY: 40, fontSize: 14);

        document.Save("output.pdf");
        """;
}
