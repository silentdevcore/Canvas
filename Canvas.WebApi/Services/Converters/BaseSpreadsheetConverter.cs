using System.Text.RegularExpressions;
using Canvas.Pdf;

namespace Canvas.WebApi.Services.Converters;

/// <summary>
/// Base for spreadsheet-library code converters (→ Canvas spreadsheet API). Marks <see cref="Kind"/> as
/// "spreadsheet" and renders a spreadsheet-appropriate informational preview (the converted CanvasWorkbook
/// code can't be executed here; the code panel carries the result).
/// </summary>
public abstract class BaseSpreadsheetConverter : BasePdfConverter
{
    public override string Kind => "spreadsheet";

    public override byte[] GeneratePreview(string sourceCode)
    {
        var converted = ConvertCode(sourceCode);
        var document = new PdfDocument();
        var page = document.AddPage();
        DrawPreviewChrome(page, FrameworkName);

        page.DrawTextFromTop("Spreadsheet code migration",
            x: 60, topY: 60, new PdfDrawTextOptions { FontSize = 15, Bold = true });
        page.DrawTextFromTop("The converted Canvas spreadsheet (CanvasWorkbook) code is in the code panel — copy it into your project.",
            x: 60, topY: 86, new PdfDrawTextOptions { FontSize = 10, FillColor = PdfColor.FromRgb(107, 114, 128) });

        var sheets = Regex.Matches(converted, @"\.AddSheet\(").Count;
        var cells = Regex.Matches(converted, @"\.Cell\(").Count;
        page.DrawTextFromTop($"Sheets created: {sheets}    Cells written: {cells}",
            x: 60, topY: 116, new PdfDrawTextOptions { FontSize = 11 });

        return document.ToBytes();
    }
}
