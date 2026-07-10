using System.Globalization;
using System.Text.Json;
using PXA.Core.Contracts;
using PXA.Core.Primitives;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;

namespace PXA.Infrastructure.Spreadsheet;

/// <summary>
/// Legacy Excel <c>.xls</c> (BIFF8) round-trip via NPOI — values, formulas, and merges. (Styles are not
/// carried for <c>.xls</c>; use <c>.xlsx</c> (<see cref="ExcelWorkbookExporter"/>) for full fidelity.)
/// </summary>
public sealed class XlsWorkbookIo
{
    public byte[] Export(SpreadsheetDto workbook)
    {
        using var wb = new HSSFWorkbook();
        foreach (var sheet in workbook.Sheets)
        {
            var ws = wb.CreateSheet(SafeName(sheet.Name));
            foreach (var c in sheet.Cells)
            {
                var row = ws.GetRow(c.Row) ?? ws.CreateRow(c.Row);
                ApplyValue(row.CreateCell(c.Col), c);
            }
            foreach (var m in sheet.Merges)
                try { ws.AddMergedRegion(CellRangeAddress.ValueOf(m)); } catch { }
            foreach (var col in sheet.Columns)
                if (col.Width is { } w and > 0) try { ws.SetColumnWidth(col.Index, (int)(w * 256)); } catch { }
        }
        if (wb.NumberOfSheets == 0) wb.CreateSheet("Sheet1");

        using var ms = new MemoryStream();
        wb.Write(ms, leaveOpen: true);
        return ms.ToArray();
    }

    public SpreadsheetDto Import(Stream xls, string? fileName = null)
    {
        using var wb = new HSSFWorkbook(xls);
        var dto = new SpreadsheetDto
        {
            Id = Guid.NewGuid().ToString("n"),
            Name = string.IsNullOrWhiteSpace(fileName) ? "Workbook" : Path.GetFileNameWithoutExtension(fileName),
        };

        for (var si = 0; si < wb.NumberOfSheets; si++)
        {
            var ws = wb.GetSheetAt(si);
            var sheet = new SheetDto { Id = Guid.NewGuid().ToString("n"), Name = ws.SheetName };
            sheet.RowCount = Math.Max(sheet.RowCount, ws.LastRowNum + 1);

            for (var r = ws.FirstRowNum; r <= ws.LastRowNum; r++)
            {
                var row = ws.GetRow(r);
                if (row is null) continue;
                for (var c = row.FirstCellNum; c < row.LastCellNum && c >= 0; c++)
                {
                    var cell = row.GetCell(c);
                    if (cell is null) continue;
                    if (ReadCell(cell, r, c) is { } dc) sheet.Cells.Add(dc);
                }
            }
            for (var i = 0; i < ws.NumMergedRegions; i++)
            {
                var rg = ws.GetMergedRegion(i);
                sheet.Merges.Add($"{A1Reference.ToA1(rg.FirstRow, rg.FirstColumn)}:{A1Reference.ToA1(rg.LastRow, rg.LastColumn)}");
            }
            dto.Sheets.Add(sheet);
        }
        return dto;
    }

    private static void ApplyValue(ICell cell, CellDto c)
    {
        if (c.Type == "formula" && c.Formula is { Length: > 0 } f) { cell.SetCellFormula(f.TrimStart('=')); return; }
        var v = Unwrap(c.Value);
        switch (c.Type)
        {
            case "number": if (TryDouble(v, out var d)) cell.SetCellValue(d); else if (v is not null) cell.SetCellValue(v.ToString()); break;
            case "boolean": cell.SetCellValue(v is bool b ? b : string.Equals(v?.ToString(), "true", StringComparison.OrdinalIgnoreCase)); break;
            case "empty": break;
            default: if (v is not null) cell.SetCellValue(v.ToString()); break; // text + date (as string)
        }
    }

    private static CellDto? ReadCell(ICell cell, int row, int col)
    {
        var dc = new CellDto { Row = row, Col = col };
        switch (cell.CellType)
        {
            case CellType.Formula:
                dc.Type = "formula";
                dc.Formula = "=" + cell.CellFormula;
                dc.Value = cell.CachedFormulaResultType == CellType.Numeric ? cell.NumericCellValue
                    : cell.CachedFormulaResultType == CellType.Boolean ? cell.BooleanCellValue
                    : cell.CachedFormulaResultType == CellType.String ? cell.StringCellValue : null;
                break;
            case CellType.Numeric:
                dc.Type = "number"; dc.Value = cell.NumericCellValue; break;
            case CellType.Boolean:
                dc.Type = "boolean"; dc.Value = cell.BooleanCellValue; break;
            case CellType.String:
                var s = cell.StringCellValue;
                if (string.IsNullOrEmpty(s)) return null;
                dc.Type = "text"; dc.Value = s; break;
            default:
                return null;
        }
        return dc;
    }

    private static object? Unwrap(object? value) => value switch
    {
        JsonElement je => je.ValueKind switch
        {
            JsonValueKind.Number => je.GetDouble(),
            JsonValueKind.String => je.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        },
        _ => value,
    };

    private static bool TryDouble(object? v, out double d)
    {
        if (v is double dd) { d = dd; return true; }
        return double.TryParse(v?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d);
    }

    private static string SafeName(string name)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var safe = string.Concat((name ?? "Sheet").Select(ch => invalid.Contains(ch) ? '_' : ch));
        if (string.IsNullOrWhiteSpace(safe)) safe = "Sheet";
        return safe.Length > 31 ? safe[..31] : safe;
    }
}
