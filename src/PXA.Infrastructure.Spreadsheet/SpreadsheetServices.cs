using PXA.Core.Contracts;
using CanvasSpreadsheet = Canvas.Infrastructure.Spreadsheet;

namespace PXA.Infrastructure.Spreadsheet;

public sealed class SpreadsheetCalculator
{
    private readonly CanvasSpreadsheet.SpreadsheetCalculator inner = new();

    public SpreadsheetDto Calculate(SpreadsheetDto workbook) =>
        inner.Calculate(workbook.ToCanvas()).ToPxa();
}

public sealed class SpreadsheetValidator
{
    private readonly CanvasSpreadsheet.SpreadsheetValidator inner = new();

    public SpreadsheetValidationResult Validate(SpreadsheetDto workbook)
    {
        var result = inner.Validate(workbook.ToCanvas());
        return new SpreadsheetValidationResult(
            result.Valid,
            result.Version,
            result.SupportedVersion,
            result.Issues.Select(i => new SpreadsheetValidationIssue(i.Severity, i.Path, i.Message)).ToArray());
    }
}

public sealed record SpreadsheetValidationIssue(string Severity, string Path, string Message);

public sealed record SpreadsheetValidationResult(
    bool Valid,
    string Version,
    string SupportedVersion,
    IReadOnlyList<SpreadsheetValidationIssue> Issues);

public sealed class SpreadsheetToDesignConverter
{
    private readonly CanvasSpreadsheet.SpreadsheetToDesignConverter inner = new();

    public DesignExportDto Convert(SpreadsheetDto workbook, int sheetIndex = 0, bool gridlines = false) =>
        inner.Convert(workbook.ToCanvas(), sheetIndex, gridlines).ToPxa();
}

public sealed class SpreadsheetOperations
{
    private readonly CanvasSpreadsheet.SpreadsheetOperations inner = new();

    public SpreadsheetDto SortRange(SpreadsheetDto workbook, int sheetIndex, string a1Range, int keyColumnOffset, bool ascending = true)
    {
        var canvasWorkbook = workbook.ToCanvas();
        if (sheetIndex < 0 || sheetIndex >= canvasWorkbook.Sheets.Count)
            throw new ArgumentOutOfRangeException(nameof(sheetIndex));

        inner.SortRange(canvasWorkbook.Sheets[sheetIndex], a1Range, keyColumnOffset, ascending);
        return canvasWorkbook.ToPxa();
    }

    public int FindReplace(SpreadsheetDto workbook, string find, string replace, bool matchCase = false)
    {
        var canvasWorkbook = workbook.ToCanvas();
        var replacements = inner.FindReplace(canvasWorkbook, find, replace, matchCase);
        CopyWorkbook(canvasWorkbook.ToPxa(), workbook);
        return replacements;
    }

    private static void CopyWorkbook(SpreadsheetDto source, SpreadsheetDto target)
    {
        target.Schema = source.Schema;
        target.SchemaVersion = source.SchemaVersion;
        target.Id = source.Id;
        target.Name = source.Name;
        target.Sheets = source.Sheets;
        target.DefinedNames = source.DefinedNames;
    }
}
