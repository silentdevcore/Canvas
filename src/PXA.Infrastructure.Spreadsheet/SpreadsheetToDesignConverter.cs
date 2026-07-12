using System.Text.Json;
using PXA.Core.Contracts;

namespace PXA.Infrastructure.Spreadsheet;

/// <summary>
/// Bridges the Spreadsheet SDK into the document model: maps a worksheet to a PXA <c>table</c>
/// <see cref="ElementDto"/> inside a <see cref="DesignExportDto"/>, so a sheet can be embedded in a
/// PDF/Word/HTML document via the existing exporters. Uses each cell's (computed) value as the cell text.
/// </summary>
public sealed class SpreadsheetToDesignConverter
{
    public DesignExportDto Convert(SpreadsheetDto workbook, int sheetIndex = 0, bool gridlines = false)
    {
        var sheet = workbook.Sheets.Count > sheetIndex && sheetIndex >= 0
            ? workbook.Sheets[sheetIndex]
            : workbook.Sheets.FirstOrDefault() ?? new SheetDto();

        int maxRow = -1, maxCol = -1;
        foreach (var c in sheet.Cells)
        {
            if (c.Row > maxRow) maxRow = c.Row;
            if (c.Col > maxCol) maxCol = c.Col;
        }
        var rows = maxRow + 1;
        var cols = maxCol + 1;

        var cellData = new string[Math.Max(rows, 0)][];
        for (var r = 0; r < rows; r++)
        {
            cellData[r] = new string[cols];
            Array.Fill(cellData[r], "");
        }

        var cellStyles = new List<CellStyleDto>();
        foreach (var c in sheet.Cells)
        {
            if (c.Row < rows && c.Col < cols) cellData[c.Row][c.Col] = ValueToString(c.Value);
            if (c.Style is not null)
            {
                c.Style.Row = c.Row;
                c.Style.Col = c.Col;
                cellStyles.Add(c.Style);
            }
        }

        double[]? columnWidths = null;
        if (cols > 0)
        {
            columnWidths = new double[cols];
            Array.Fill(columnWidths, 64d);
            foreach (var col in sheet.Columns)
                if (col.Index >= 0 && col.Index < cols && col.Width is { } w)
                    columnWidths[col.Index] = w * 7d; // Excel char-units → points (approx)
        }

        var element = new ElementDto
        {
            Id = "sheet-table",
            Type = "table",
            Name = sheet.Name,
            X = 24,
            Y = 24,
            Width = columnWidths?.Sum() ?? Math.Max(cols * 80, 200),
            Height = Math.Max(rows * 22, 60),
            CellData = rows > 0 ? cellData : null,
            ColumnWidths = columnWidths,
            CellStyles = cellStyles.Count > 0 ? cellStyles.ToArray() : null,
        };

        if (gridlines) // render like a worksheet: a light grid + a styled header row
        {
            element.HeaderRow = rows > 0;
            element.Style = new Dictionary<string, object> { ["borderColor"] = "#cccccc", ["borderWidth"] = 0.5 };
        }

        return new DesignExportDto
        {
            Id = string.IsNullOrEmpty(workbook.Id) ? "workbook" : workbook.Id,
            Name = string.IsNullOrWhiteSpace(sheet.Name) ? workbook.Name : sheet.Name,
            Pages = [new PageDto { Id = "p1", Elements = [element] }],
            PageSettings = new PageSettingsDto { Width = 595, Height = 842, Orientation = "portrait" },
        };
    }

    private static string ValueToString(object? value) => value switch
    {
        null => "",
        JsonElement je => je.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => "",
            JsonValueKind.String => je.GetString() ?? "",
            _ => je.ToString(),
        },
        _ => value.ToString() ?? "",
    };
}
