using System.Text;
using System.Text.RegularExpressions;
using Canvas.Pdf;

namespace Canvas.WebApi.Services.Converters;

public sealed class SyncfusionPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "Syncfusion";
    public override string FrameworkName => "Syncfusion PDF";
    public override string Status => "full";
    public override string Description => "Full pattern-based conversion with top-left coordinate adapter. Covers document/page/text/line/rectangle/image/save.";

    public override string ConvertCode(string sourceCode)
    {
        var result = sourceCode;

        // 1. Namespaces
        result = RemoveSyncfusionUsings(result);
        result = EnsureCanvasUsing(result);

        // 2. Document creation — using var → var (Canvas.PdfDocument not IDisposable)
        result = Regex.Replace(result,
            @"using\s+var\s+(\w+)\s*=\s*new\s+PdfDocument\s*\(\s*\)",
            "var $1 = new Canvas.Pdf.PdfDocument()");

        result = Regex.Replace(result,
            @"\bnew\s+PdfDocument\s*\(\s*\)",
            "new Canvas.Pdf.PdfDocument()");

        // 3. Pages.Add() → AddPage()
        result = Regex.Replace(result,
            @"(\w+)\.Pages\.Add\s*\(\s*\)",
            "$1.AddPage()");

        // 4. Remove graphics variable assignments: var graphics = page.Graphics;
        result = Regex.Replace(result,
            @"[ \t]*var\s+(\w+)\s*=\s*\w+\.Graphics\s*;\s*\r?\n",
            "");

        // 5. DrawString with 5 args (text, font, brush, x, y)
        //    page.Graphics.DrawString("text", font, brush, x, y)
        //    or graphics.DrawString("text", font, brush, x, y)
        //    Font arg may contain a comma inside parens: new PdfStandardFont(PdfFontFamily.Helvetica, 14)
        result = Regex.Replace(result,
            @"(?:\w+\.Graphics|graphics|gfx)\s*\.\s*DrawString\s*\(\s*" +
            @"(""[^""]*""|\w+)\s*,\s*" +              // text
            @"(new\s+PdfStandardFont\s*\([^)]+\)|[^,]+)\s*,\s*" + // font (handles inner comma)
            @"[^,]+\s*,\s*" +                           // brush (discard)
            @"([\d.]+)\s*,\s*" +                        // x
            @"([\d.]+)\s*\)",                            // y
            m =>
            {
                var text = m.Groups[1].Value;
                var font = m.Groups[2].Value.Trim();
                var x = m.Groups[3].Value;
                var y = m.Groups[4].Value;
                var fontSize = ExtractFontSize(font);
                return $"page.DrawTextFromTop({text}, x: {x}, topY: {y}, {fontSize})";
            });

        // 6. DrawString with PointF (text, font, brush, new PointF(x, y))
        result = Regex.Replace(result,
            @"(?:\w+\.Graphics|graphics|gfx)\s*\.\s*DrawString\s*\(\s*" +
            @"(""[^""]*""|\w+)\s*,\s*" +
            @"(new\s+PdfStandardFont\s*\([^)]+\)|[^,]+)\s*,\s*" +
            @"[^,]+\s*,\s*" +
            @"new\s+(?:\w+\.)?PointF\s*\(\s*([\d.]+)\s*,\s*([\d.]+)\s*\)\s*\)",
            m =>
            {
                var text = m.Groups[1].Value;
                var font = m.Groups[2].Value.Trim();
                var x = m.Groups[3].Value;
                var y = m.Groups[4].Value;
                var fontSize = ExtractFontSize(font);
                return $"page.DrawTextFromTop({text}, x: {x}, topY: {y}, {fontSize})";
            });

        // 7. DrawLine(pen, x1, y1, x2, y2)
        result = Regex.Replace(result,
            @"(?:\w+\.Graphics|graphics|gfx)\s*\.\s*DrawLine\s*\(\s*" +
            @"([^,]+)\s*,\s*" +  // pen
            @"([\d.]+)\s*,\s*" + // x1
            @"([\d.]+)\s*,\s*" + // y1
            @"([\d.]+)\s*,\s*" + // x2
            @"([\d.]+)\s*\)",    // y2
            m =>
            {
                var pen = m.Groups[1].Value.Trim();
                var lineWidth = ExtractPenWidth(pen);
                var color = ExtractPenColor(pen);
                return $"page.DrawLineFromTop({m.Groups[2].Value}, {m.Groups[3].Value}, {m.Groups[4].Value}, {m.Groups[5].Value}, lineWidth: {lineWidth}{color})";
            });

        // 8. DrawRectangle(pen/brush, x, y, w, h)
        result = Regex.Replace(result,
            @"(?:\w+\.Graphics|graphics|gfx)\s*\.\s*DrawRectangle\s*\(\s*" +
            @"([^,]+)\s*,\s*" +  // pen or brush
            @"([\d.]+)\s*,\s*" + // x
            @"([\d.]+)\s*,\s*" + // y
            @"([\d.]+)\s*,\s*" + // w
            @"([\d.]+)\s*\)",    // h
            m =>
            {
                var penOrBrush = m.Groups[1].Value.Trim();
                var isBrush = penOrBrush.Contains("Brush") || penOrBrush.Contains("brush");
                if (isBrush)
                {
                    var fillColor = MapBrushToColor(penOrBrush);
                    return $"page.DrawRectangleFromTop({m.Groups[2].Value}, {m.Groups[3].Value}, {m.Groups[4].Value}, {m.Groups[5].Value}, fill: true, fillColor: {fillColor})";
                }
                var strokeColor = ExtractPenColor(penOrBrush);
                var lineW = ExtractPenWidth(penOrBrush);
                return $"page.DrawRectangleFromTop({m.Groups[2].Value}, {m.Groups[3].Value}, {m.Groups[4].Value}, {m.Groups[5].Value}, lineWidth: {lineW}{strokeColor})";
            });

        // 9. DrawImage(image, x, y, w, h)
        result = Regex.Replace(result,
            @"(?:\w+\.Graphics|graphics|gfx)\s*\.\s*DrawImage\s*\(\s*" +
            @"PdfImage\.FromFile\s*\(([^)]+)\)\s*,\s*" + // path
            @"([\d.]+)\s*,\s*" + // x
            @"([\d.]+)\s*,\s*" + // y
            @"([\d.]+)\s*,\s*" + // w
            @"([\d.]+)\s*\)",    // h
            "page.DrawImageFromTop($1, $2, $3, $4, $5)");

        result = Regex.Replace(result,
            @"(?:\w+\.Graphics|graphics|gfx)\s*\.\s*DrawImage\s*\(\s*" +
            @"([^,]+)\s*,\s*" +  // image
            @"([\d.]+)\s*,\s*" + // x
            @"([\d.]+)\s*,\s*" + // y
            @"([\d.]+)\s*,\s*" + // w
            @"([\d.]+)\s*\)",    // h
            "page.DrawImageFromTop($2, $3, $4, $5) /* TODO: map image source from $1 */");

        // 10. document.Close(...)
        result = Regex.Replace(result,
            @"[ \t]*\w+\.Close\s*\([^)]*\)\s*;\s*\r?\n?",
            "");

        // 11. PdfBrushes color → PdfColor
        result = result
            .Replace("PdfBrushes.Black", "PdfColor.Black")
            .Replace("PdfBrushes.White", "PdfColor.White")
            .Replace("PdfBrushes.Red", "PdfColor.RedColor")
            .Replace("PdfBrushes.Green", "PdfColor.GreenColor")
            .Replace("PdfBrushes.Blue", "PdfColor.BlueColor")
            .Replace("PdfBrushes.Gray", "PdfColor.Gray");

        // 12. new PdfSolidBrush(Color.FromArgb(r, g, b)) → PdfColor.FromRgb(r, g, b)
        result = Regex.Replace(result,
            @"new\s+PdfSolidBrush\s*\(\s*Color\.FromArgb\s*\(([^)]+)\)\s*\)",
            "PdfColor.FromRgb($1)");

        return result;
    }

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
    {
        var diagnostics = new List<MigrationDiagnostic>();

        if (sourceCode.Contains("new PdfDocument()"))
            diagnostics.Add(new("CANMIGSYNC001", "Info", "Document creation migrated to Canvas.Pdf.PdfDocument."));
        if (sourceCode.Contains(".Pages.Add()"))
            diagnostics.Add(new("CANMIGSYNC002", "Info", "Page creation migrated to document.AddPage()."));
        if (Regex.IsMatch(sourceCode, @"DrawString"))
            diagnostics.Add(new("CANMIGSYNC003", "Info", "DrawString migrated to page.DrawTextFromTop() using top-left coordinate adapter."));
        if (Regex.IsMatch(sourceCode, @"DrawLine"))
            diagnostics.Add(new("CANMIGSYNC010", "Info", "DrawLine migrated to page.DrawLineFromTop()."));
        if (Regex.IsMatch(sourceCode, @"DrawRectangle"))
            diagnostics.Add(new("CANMIGSYNC011", "Info", "DrawRectangle migrated to page.DrawRectangleFromTop()."));
        if (Regex.IsMatch(sourceCode, @"DrawImage"))
            diagnostics.Add(new("CANMIGSYNC014", "Info", "DrawImage migrated to page.DrawImageFromTop() — verify image source mapping."));
        if (Regex.IsMatch(sourceCode, @"PdfGrid"))
            diagnostics.Add(new("CANMIGSYNC005", "Warning", "PdfGrid (table) has no direct Canvas.Pdf equivalent in v1 — manual rewrite required."));
        if (Regex.IsMatch(sourceCode, @"Security|Encrypt|PdfPermissions"))
            diagnostics.Add(new("CANMIGSYNC006", "Warning", "PDF security/encryption is not supported by Canvas.Pdf."));
        if (Regex.IsMatch(sourceCode, @"PdfForm|AcroForm|FormField"))
            diagnostics.Add(new("CANMIGSYNC006", "Warning", "PDF forms (AcroForms) are not supported by Canvas.Pdf."));
        if (Regex.IsMatch(sourceCode, @"new RectangleF\s*\("))
            diagnostics.Add(new("CANMIGSYNC004", "Warning", "DrawString with RectangleF layout uses DrawTextBoxFromTop — review text wrapping and alignment."));
        if (Regex.IsMatch(sourceCode, @"var\s+\w+\s*=\s*\w+\.Graphics\s*;"))
            diagnostics.Add(new("CANMIGSYNC009", "Info", "PdfGraphics variable removed — all usages inlined onto the page variable."));

        return diagnostics;
    }

    public override byte[] GeneratePreview(string sourceCode)
    {
        var converted = ConvertCode(sourceCode);
        var document = new PdfDocument();
        var page = document.AddPage();

        // Draw a header
        page.DrawTextFromTop("Migration Preview - Syncfusion -> Canvas.Pdf", x: 40, topY: 30,
            new PdfDrawTextOptions { FontSize = 14, Bold = true });
        page.DrawLineFromTop(40, 52, 555, 52, lineWidth: 0.5);

        // Extract and replay simple DrawTextFromTop calls from the converted code
        var textCalls = Regex.Matches(converted,
            @"page\.DrawTextFromTop\(\s*(""[^""]*"")\s*,\s*x:\s*([\d.]+)\s*,\s*topY:\s*([\d.]+)\s*,\s*fontSize:\s*([\d.]+)");

        var renderedAny = false;
        foreach (Match m in textCalls)
        {
            var text = m.Groups[1].Value.Trim('"');
            if (double.TryParse(m.Groups[2].Value, out var x) &&
                double.TryParse(m.Groups[3].Value, out var topY) &&
                double.TryParse(m.Groups[4].Value, out var fontSize))
            {
                // Offset below the header
                page.DrawTextFromTop(text, x: x, topY: topY + 80,
                    new PdfDrawTextOptions { FontSize = Math.Clamp(fontSize, 8, 36) });
                renderedAny = true;
            }
        }

        if (!renderedAny)
        {
            page.DrawTextFromTop("(No simple text calls detected — see Canvas code panel for the full output)",
                x: 40, topY: 80, new PdfDrawTextOptions { FontSize = 11, Italic = true });
        }

        // Render converted code snippet at the bottom
        page.DrawTextFromTop("Generated Canvas.Pdf code:", x: 40, topY: 420,
            new PdfDrawTextOptions { FontSize = 10, Bold = true });
        var lines = converted.Split('\n').Take(20);
        var lineY = 438.0;
        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.Length > 90) trimmed = trimmed[..87] + "...";
            page.DrawTextFromTop(trimmed, x: 40, topY: lineY,
                new PdfDrawTextOptions { FontSize = 8 });
            lineY += 12;
        }

        return document.ToBytes();
    }

    // --- Helpers ---

    private static string RemoveSyncfusionUsings(string code)
    {
        var usingsToRemove = new[]
        {
            @"using\s+Syncfusion\.Pdf\s*;",
            @"using\s+Syncfusion\.Pdf\.Graphics\s*;",
            @"using\s+Syncfusion\.Pdf\.Grid\s*;",
            @"using\s+Syncfusion\.Drawing\s*;",
            @"using\s+Syncfusion\.\w+(?:\.\w+)*\s*;"
        };
        foreach (var pattern in usingsToRemove)
            code = Regex.Replace(code, pattern + @"\s*\r?\n?", "");
        return code;
    }

    private static string EnsureCanvasUsing(string code)
    {
        if (!code.Contains("using Canvas.Pdf;"))
            code = "using Canvas.Pdf;\n" + code.TrimStart('\n');
        return code;
    }

    private static string ExtractFontSize(string fontExpr)
    {
        // new PdfStandardFont(PdfFontFamily.Helvetica, 12) → fontSize: 12
        var m = Regex.Match(fontExpr, @"new\s+PdfStandardFont\s*\([^,]+,\s*([\d.]+)\s*\)");
        if (m.Success)
            return $"fontSize: {m.Groups[1].Value}";
        return "fontSize: 12";
    }

    private static string ExtractPenWidth(string penExpr)
    {
        // new PdfPen(color, width) or PdfPens.Black (default 1)
        var m = Regex.Match(penExpr, @"new\s+PdfPen\s*\([^,]+,\s*([\d.]+)\s*\)");
        return m.Success ? m.Groups[1].Value : "1";
    }

    private static string ExtractPenColor(string penExpr)
    {
        if (penExpr.Contains("Black")) return ", strokeColor: PdfColor.Black";
        if (penExpr.Contains("Red")) return ", strokeColor: PdfColor.RedColor";
        if (penExpr.Contains("Blue")) return ", strokeColor: PdfColor.BlueColor";
        if (penExpr.Contains("Green")) return ", strokeColor: PdfColor.GreenColor";
        if (penExpr.Contains("Gray")) return ", strokeColor: PdfColor.Gray";
        var m = Regex.Match(penExpr, @"Color\.FromArgb\s*\(([^)]+)\)");
        if (m.Success) return $", strokeColor: PdfColor.FromRgb({m.Groups[1].Value})";
        return "";
    }

    private static string MapBrushToColor(string brushExpr)
    {
        if (brushExpr.Contains("Black")) return "PdfColor.Black";
        if (brushExpr.Contains("Red")) return "PdfColor.RedColor";
        if (brushExpr.Contains("Blue")) return "PdfColor.BlueColor";
        if (brushExpr.Contains("Green")) return "PdfColor.GreenColor";
        if (brushExpr.Contains("Gray")) return "PdfColor.Gray";
        var m = Regex.Match(brushExpr, @"Color\.FromArgb\s*\(([^)]+)\)");
        if (m.Success) return $"PdfColor.FromRgb({m.Groups[1].Value})";
        return "PdfColor.Black";
    }
}
