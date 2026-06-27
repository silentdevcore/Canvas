using Canvas.Core.Contracts;
using ClosedXML.Excel;

namespace Canvas.Infrastructure.Sheet;

/// <summary>
/// Imports an <c>.xlsx</c> file into a <see cref="SpreadsheetDto"/> via ClosedXML, preserving A1 formulas,
/// typed values, number formats, styles, merges, column widths, frozen panes, and defined names.
/// The reverse of <see cref="ExcelWorkbookExporter"/>.
/// </summary>
public sealed class ExcelWorkbookImporter
{
    public SpreadsheetDto Import(Stream xlsx, string? fileName = null)
    {
        using var wb = new XLWorkbook(xlsx);
        var dto = new SpreadsheetDto
        {
            Id = Guid.NewGuid().ToString("n"),
            Name = string.IsNullOrWhiteSpace(fileName) ? "Workbook" : Path.GetFileNameWithoutExtension(fileName),
        };

        foreach (var ws in wb.Worksheets)
            dto.Sheets.Add(ReadSheet(ws));

        foreach (var nr in wb.NamedRanges)
            dto.DefinedNames.Add(new DefinedNameDto { Name = nr.Name, RefersTo = SafeRefersTo(nr) });

        return dto;
    }

    private static SheetDto ReadSheet(IXLWorksheet ws)
    {
        var sheet = new SheetDto { Id = Guid.NewGuid().ToString("n"), Name = ws.Name };

        var used = ws.RangeUsed();
        if (used is not null)
        {
            sheet.RowCount = Math.Max(sheet.RowCount, used.LastRow().RowNumber());
            sheet.ColCount = Math.Max(sheet.ColCount, used.LastColumn().ColumnNumber());

            foreach (var cell in used.CellsUsed())
            {
                var dc = new CellDto { Row = cell.Address.RowNumber - 1, Col = cell.Address.ColumnNumber - 1 };
                if (cell.HasFormula)
                {
                    dc.Type = "formula";
                    dc.Formula = "=" + cell.FormulaA1;
                    dc.Value = ReadValue(cell);
                }
                else
                {
                    (dc.Type, dc.Value) = ReadTypedValue(cell);
                    if (dc.Type == "empty") { if (cell.Style is null) continue; }
                }

                var nf = cell.Style.NumberFormat.Format;
                if (!string.IsNullOrEmpty(nf)) dc.NumberFormat = nf;
                dc.Style = ReadStyle(cell);

                if (dc.Type != "empty" || dc.Style is not null)
                    sheet.Cells.Add(dc);
            }
        }

        foreach (var mr in ws.MergedRanges)
            sheet.Merges.Add(mr.RangeAddress.ToStringRelative());

        var lastCol = used?.LastColumn().ColumnNumber() ?? 0;
        for (var c = 1; c <= lastCol; c++)
        {
            var col = ws.Column(c);
            var width = col.Width;
            var hidden = col.IsHidden;
            if (hidden || Math.Abs(width - ws.ColumnWidth) > 0.01)
                sheet.Columns.Add(new SheetColumnDto { Index = c - 1, Width = width, Hidden = hidden });
        }

        try
        {
            sheet.FrozenRows = ws.SheetView.SplitRow;
            sheet.FrozenCols = ws.SheetView.SplitColumn;
        }
        catch { /* frozen panes unavailable */ }

        return sheet;
    }

    private static (string Type, object? Value) ReadTypedValue(IXLCell cell) => cell.DataType switch
    {
        XLDataType.Number => ("number", cell.GetDouble()),
        XLDataType.Boolean => ("boolean", cell.GetBoolean()),
        XLDataType.DateTime => ("date", cell.GetDateTime().ToString("o")),
        XLDataType.Blank => ("empty", null),
        _ => cell.GetString() is { Length: > 0 } s ? ("text", s) : ("empty", null),
    };

    // The cached/computed value of a (formula) cell, as a JSON-friendly primitive.
    private static object? ReadValue(IXLCell cell)
    {
        try
        {
            return cell.DataType switch
            {
                XLDataType.Number => cell.GetDouble(),
                XLDataType.Boolean => cell.GetBoolean(),
                XLDataType.DateTime => cell.GetDateTime().ToString("o"),
                _ => cell.GetString(),
            };
        }
        catch { return cell.GetString(); }
    }

    private static CellStyleDto? ReadStyle(IXLCell cell)
    {
        var s = cell.Style;
        var dto = new CellStyleDto { Row = cell.Address.RowNumber - 1, Col = cell.Address.ColumnNumber - 1 };
        var any = false;

        if (s.Fill.BackgroundColor.ColorType == XLColorType.Color)
        {
            var c = s.Fill.BackgroundColor.Color;
            // Skip the default no-fill (white/transparent) so cells stay sparse.
            if (!(c.R == 255 && c.G == 255 && c.B == 255) && c.A != 0)
            { dto.BackgroundColor = ToHex(s.Fill.BackgroundColor); any = true; }
        }

        if (s.Font.Bold) { dto.Bold = true; any = true; }
        if (s.Font.Italic) { dto.Italic = true; any = true; }
        if (!string.IsNullOrEmpty(s.Font.FontName) && s.Font.FontName != "Calibri") { dto.FontFamily = s.Font.FontName; any = true; }
        if (s.Font.FontSize is > 0 and not 11) { dto.FontSize = s.Font.FontSize; any = true; }
        if (s.Font.FontColor.ColorType == XLColorType.Color)
        {
            var fc = s.Font.FontColor.Color;
            if (!(fc.R == 0 && fc.G == 0 && fc.B == 0)) { dto.Color = ToHex(s.Font.FontColor); any = true; }
        }

        dto.TextAlign = s.Alignment.Horizontal switch
        {
            XLAlignmentHorizontalValues.Center => "center",
            XLAlignmentHorizontalValues.Right => "right",
            _ => null,
        };
        if (dto.TextAlign is not null) any = true;

        if (s.Border.TopBorder != XLBorderStyleValues.None || s.Border.BottomBorder != XLBorderStyleValues.None
            || s.Border.LeftBorder != XLBorderStyleValues.None || s.Border.RightBorder != XLBorderStyleValues.None)
        {
            dto.BorderColor = ToHex(s.Border.TopBorderColor);
            dto.BorderWidth = 1;
            any = true;
        }

        return any ? dto : null;
    }

    private static string ToHex(XLColor color)
    {
        try { var c = color.Color; return $"#{c.R:X2}{c.G:X2}{c.B:X2}"; }
        catch { return "#000000"; }
    }

    private static string SafeRefersTo(IXLDefinedName nr)
    {
        try { return nr.RefersTo; } catch { return ""; }
    }
}
