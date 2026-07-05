using PXA.Core.Contracts;
using CanvasSpreadsheet = Canvas.Infrastructure.Spreadsheet;

namespace PXA.Infrastructure.Spreadsheet;

public sealed class ExcelWorkbookExporter
{
    private readonly CanvasSpreadsheet.ExcelWorkbookExporter inner = new();

    public byte[] Export(SpreadsheetDto workbook, bool recalculate = false) =>
        inner.Export(workbook.ToCanvas(), recalculate);
}

public sealed class ExcelWorkbookImporter
{
    private readonly CanvasSpreadsheet.ExcelWorkbookImporter inner = new();

    public SpreadsheetDto Import(Stream xlsx, string? fileName = null) =>
        inner.Import(xlsx, fileName).ToPxa();
}

public sealed class XlsWorkbookIo
{
    private readonly CanvasSpreadsheet.XlsWorkbookIo inner = new();

    public byte[] Export(SpreadsheetDto workbook) => inner.Export(workbook.ToCanvas());

    public SpreadsheetDto Import(Stream xls, string? fileName = null) =>
        inner.Import(xls, fileName).ToPxa();
}

public static class CsvSheetIo
{
    public static string ToCsv(SheetDto sheet, char delimiter = ',') =>
        CanvasSpreadsheet.CsvSheetIo.ToCsv(sheet.ToCanvasSheet(), delimiter);

    public static SheetDto FromCsv(string text, string name = "Sheet1", char delimiter = ',') =>
        CanvasSpreadsheet.CsvSheetIo.FromCsv(text, name, delimiter).ToPxaSheet();
}
