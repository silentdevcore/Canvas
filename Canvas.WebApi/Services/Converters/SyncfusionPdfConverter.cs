using System.Text.RegularExpressions;
using Canvas.Migration.SyncfusionPdf;
using Canvas.Pdf;

namespace Canvas.WebApi.Services.Converters;

public sealed class SyncfusionPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "Syncfusion";

    public override string FrameworkName => "Syncfusion PDF";

    public override string Status => "full";

    public override string Description =>
        "Roslyn-based conversion with top-left coordinate adapter. Covers document/page/text/line/rectangle/image/save and reports manual follow-up items.";

    public override string ConvertCode(string sourceCode)
    {
        return new SyncfusionPdfMigration().Migrate(sourceCode).MigratedCode;
    }

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
    {
        return new SyncfusionPdfMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static diagnostic => new MigrationDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.Message))
            .ToArray();
    }

    public override byte[] GeneratePreview(string sourceCode)
    {
        var converted = ConvertCode(sourceCode);
        var document = new PdfDocument();
        var page = document.AddPage();

        page.DrawTextFromTop("Migration Preview - Syncfusion -> Canvas.Pdf", x: 40, topY: 30,
            new PdfDrawTextOptions { FontSize = 14, Bold = true });
        page.DrawLineFromTop(40, 52, 555, 52, lineWidth: 0.5);

        var renderedAny = false;
        foreach (var (text, x, topY, fontSize) in FindPreviewTextCalls(converted))
        {
            page.DrawTextFromTop(text, x: x, topY: topY + 80,
                new PdfDrawTextOptions { FontSize = Math.Clamp(fontSize, 8, 36) });
            renderedAny = true;
        }

        if (!renderedAny)
        {
            page.DrawTextFromTop("(No simple text calls detected - see Canvas code panel for the full output)",
                x: 40, topY: 80, new PdfDrawTextOptions { FontSize = 11, Italic = true });
        }

        page.DrawTextFromTop("Generated Canvas.Pdf code:", x: 40, topY: 420,
            new PdfDrawTextOptions { FontSize = 10, Bold = true });

        var lineY = 438.0;
        foreach (var line in converted.Split('\n').Take(20))
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.Length > 90)
            {
                trimmed = trimmed[..87] + "...";
            }

            page.DrawTextFromTop(trimmed, x: 40, topY: lineY,
                new PdfDrawTextOptions { FontSize = 8 });
            lineY += 12;
        }

        return document.ToBytes();
    }

    private static IEnumerable<(string Text, double X, double TopY, double FontSize)> FindPreviewTextCalls(string converted)
    {
        foreach (Match match in Regex.Matches(converted,
            @"page\.DrawTextFromTop\(\s*(""[^""]*"")\s*,\s*x:\s*([\d.]+)\s*,\s*topY:\s*([\d.]+)\s*,\s*fontSize:\s*([\d.]+)"))
        {
            if (TryParseTextCall(match, out var call))
            {
                yield return call;
            }
        }

        foreach (Match match in Regex.Matches(converted,
            @"page\.DrawTextFromTop\(\s*(""[^""]*"")\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)"))
        {
            if (TryParseTextCall(match, out var call))
            {
                yield return call;
            }
        }
    }

    private static bool TryParseTextCall(
        Match match,
        out (string Text, double X, double TopY, double FontSize) call)
    {
        call = default;

        if (!double.TryParse(match.Groups[2].Value, out var x)
            || !double.TryParse(match.Groups[3].Value, out var topY)
            || !double.TryParse(match.Groups[4].Value, out var fontSize))
        {
            return false;
        }

        call = (match.Groups[1].Value.Trim('"'), x, topY, fontSize);
        return true;
    }
}
