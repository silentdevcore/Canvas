using PXA.Core.Contracts;
using ClosedXML.Excel;

namespace PXA.Infrastructure.Spreadsheet;

/// <summary>
/// Server-side formula calculation. Builds the workbook in ClosedXML, recalculates all formulas, and writes
/// the computed values back into each formula cell's <see cref="CellDto.Value"/> — so headless/API callers
/// get authoritative results without the browser engine. Formula errors come back as <c>#CODE</c> strings;
/// functions ClosedXML doesn't support degrade per-cell to <c>#ERROR</c>.
/// </summary>
public sealed class SpreadsheetCalculator
{
    public SpreadsheetDto Calculate(SpreadsheetDto workbook)
    {
        using var wb = ExcelWorkbookExporter.Build(workbook);
        ExcelWorkbookExporter.TryRecalculate(wb);

        var worksheets = wb.Worksheets.ToList();
        for (var i = 0; i < workbook.Sheets.Count && i < worksheets.Count; i++)
        {
            var ws = worksheets[i];
            foreach (var cell in workbook.Sheets[i].Cells)
            {
                if (cell.Type != "formula") continue;
                cell.Value = ReadComputed(ws.Cell(cell.Row + 1, cell.Col + 1));
            }
        }
        return workbook;
    }

    private static object? ReadComputed(IXLCell cell)
    {
        try
        {
            var v = cell.Value;
            if (v.IsError) return "#" + v.GetError().ToString().ToUpperInvariant();
            if (v.IsNumber) return v.GetNumber();
            if (v.IsBoolean) return v.GetBoolean();
            if (v.IsDateTime) return v.GetDateTime().ToString("o");
            if (v.IsText) return v.GetText();
            return v.ToString();
        }
        catch
        {
            return "#ERROR";
        }
    }
}
