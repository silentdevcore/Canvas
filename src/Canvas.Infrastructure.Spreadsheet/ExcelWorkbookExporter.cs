using System.Text.Json;
using Canvas.Core.Contracts;
using ClosedXML.Excel;

namespace Canvas.Infrastructure.Spreadsheet;

/// <summary>
/// Exports a <see cref="SpreadsheetDto"/> workbook to <c>.xlsx</c> via ClosedXML — typed cell values,
/// real A1 formulas (<see cref="IXLCell.FormulaA1"/>), number formats, styles, merges, column/row sizing,
/// frozen panes, and defined names. The reverse of <see cref="ExcelWorkbookImporter"/>.
/// </summary>
public sealed class ExcelWorkbookExporter
{
    /// <summary>Exports the workbook to .xlsx bytes. When <paramref name="recalculate"/> is true, formulas
    /// are evaluated server-side (ClosedXML) so the file carries fresh cached values.</summary>
    public byte[] Export(SpreadsheetDto workbook, bool recalculate = false)
    {
        using var wb = Build(workbook);
        if (recalculate) TryRecalculate(wb);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>Builds the ClosedXML workbook from the model (without saving). Shared with the calculator.</summary>
    internal static XLWorkbook Build(SpreadsheetDto workbook)
    {
        var wb = new XLWorkbook();
        foreach (var sheet in workbook.Sheets)
            RenderSheet(wb, sheet);
        if (!wb.Worksheets.Any())
            wb.Worksheets.Add("Sheet1");

        foreach (var dn in workbook.DefinedNames)
            if (!string.IsNullOrWhiteSpace(dn.Name) && !string.IsNullOrWhiteSpace(dn.RefersTo))
                try { wb.NamedRanges.Add(dn.Name, dn.RefersTo); } catch { /* skip invalid name */ }

        return wb;
    }

    internal static void TryRecalculate(XLWorkbook wb)
    {
        try { wb.RecalculateAllFormulas(); } catch { /* some functions are unsupported — cells fall back per-cell */ }
    }

    private static void RenderSheet(XLWorkbook wb, SheetDto sheet)
    {
        var ws = wb.Worksheets.Add(SanitizeSheetName(sheet.Name));

        foreach (var c in sheet.Cells)
        {
            var cell = ws.Cell(c.Row + 1, c.Col + 1);
            ApplyValue(cell, c);
            if (c.NumberFormat is { Length: > 0 } nf) cell.Style.NumberFormat.Format = nf;
            if (c.Style is not null) ApplyCellStyle(cell, c.Style);
        }

        foreach (var m in sheet.Merges)
            try { ws.Range(m).Merge(); } catch { /* skip invalid range */ }

        foreach (var col in sheet.Columns)
        {
            if (col.Width is { } w and > 0) ws.Column(col.Index + 1).Width = w;
            if (col.Hidden) ws.Column(col.Index + 1).Hide();
        }
        foreach (var row in sheet.Rows)
        {
            if (row.Height is { } h and > 0) ws.Row(row.Index + 1).Height = h;
            if (row.Hidden) ws.Row(row.Index + 1).Hide();
        }

        if (sheet.FrozenRows > 0) ws.SheetView.FreezeRows(sheet.FrozenRows);
        if (sheet.FrozenCols > 0) ws.SheetView.FreezeColumns(sheet.FrozenCols);
    }

    // ── values ───────────────────────────────────────────────────────────────────────────────────────
    private static void ApplyValue(IXLCell cell, CellDto c)
    {
        if (c.Type == "formula" && c.Formula is { Length: > 0 } f)
        {
            cell.FormulaA1 = f.StartsWith('=') ? f[1..] : f;   // ClosedXML expects no leading '='
            return;
        }

        var v = Unwrap(c.Value);
        switch (c.Type)
        {
            case "number":
                if (TryDouble(v, out var d)) cell.Value = d;
                else if (v is not null) cell.Value = v.ToString();
                break;
            case "boolean":
                cell.Value = v is bool b ? b : string.Equals(v?.ToString(), "true", StringComparison.OrdinalIgnoreCase);
                break;
            case "date":
                if (v is DateTime dt) cell.Value = dt;
                else if (DateTime.TryParse(v?.ToString(), System.Globalization.CultureInfo.InvariantCulture,
                         System.Globalization.DateTimeStyles.RoundtripKind, out var pd)) cell.Value = pd;
                else if (v is not null) cell.Value = v.ToString();
                break;
            case "empty":
                break;
            default: // text
                if (v is not null) cell.Value = v.ToString();
                break;
        }
    }

    // JSON round-trips object? values as JsonElement; unwrap to a primitive for ClosedXML.
    private static object? Unwrap(object? value) => value switch
    {
        JsonElement je => je.ValueKind switch
        {
            JsonValueKind.Number => je.GetDouble(),
            JsonValueKind.String => je.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => je.ToString(),
        },
        _ => value,
    };

    private static bool TryDouble(object? v, out double d)
    {
        switch (v)
        {
            case double dd: d = dd; return true;
            case int i: d = i; return true;
            case long l: d = l; return true;
            case float f: d = f; return true;
            case decimal m: d = (double)m; return true;
        }
        return double.TryParse(v?.ToString(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out d);
    }

    // ── styling (mirrors ExcelDocumentExporter's cell-style mapping) ───────────────────────────────────
    private static void ApplyCellStyle(IXLCell cell, CellStyleDto cs)
    {
        if (cs.BackgroundColor is { Length: > 0 } bg) cell.Style.Fill.BackgroundColor = ParseColor(bg);
        if (cs.FontFamily is { Length: > 0 } ff) cell.Style.Font.FontName = ff;
        if (cs.FontSize is { } fsz and > 0) cell.Style.Font.FontSize = fsz;
        if (cs.Bold == true) cell.Style.Font.Bold = true;
        if (cs.Italic == true) cell.Style.Font.Italic = true;
        if (cs.Color is { Length: > 0 } clr) cell.Style.Font.FontColor = ParseColor(clr);
        if (cs.TextAlign is { Length: > 0 } al)
            cell.Style.Alignment.Horizontal = al switch
            {
                "center" => XLAlignmentHorizontalValues.Center,
                "right" => XLAlignmentHorizontalValues.Right,
                _ => XLAlignmentHorizontalValues.Left,
            };

        var hasUniform = cs.BorderColor != null || cs.BorderWidth != null;
        if (hasUniform || cs.BorderTop != null || cs.BorderRight != null || cs.BorderBottom != null || cs.BorderLeft != null)
        {
            var b = cell.Style.Border;
            XLColor UColor(CellBorderSideDto? side) => ParseColor(side?.Color ?? cs.BorderColor ?? "#000000");
            XLBorderStyleValues UStyle(CellBorderSideDto? side) => BorderStyleFor(side?.Width ?? cs.BorderWidth ?? 1);
            if (cs.BorderTop is not null || hasUniform) { b.TopBorder = UStyle(cs.BorderTop); b.TopBorderColor = UColor(cs.BorderTop); }
            if (cs.BorderRight is not null || hasUniform) { b.RightBorder = UStyle(cs.BorderRight); b.RightBorderColor = UColor(cs.BorderRight); }
            if (cs.BorderBottom is not null || hasUniform) { b.BottomBorder = UStyle(cs.BorderBottom); b.BottomBorderColor = UColor(cs.BorderBottom); }
            if (cs.BorderLeft is not null || hasUniform) { b.LeftBorder = UStyle(cs.BorderLeft); b.LeftBorderColor = UColor(cs.BorderLeft); }
        }
    }

    private static XLBorderStyleValues BorderStyleFor(double widthPt) =>
        widthPt >= 3 ? XLBorderStyleValues.Thick : widthPt >= 2 ? XLBorderStyleValues.Medium : XLBorderStyleValues.Thin;

    private static XLColor ParseColor(string hex)
    {
        try { return XLColor.FromHtml(hex); }
        catch { return XLColor.White; }
    }

    private static string SanitizeSheetName(string name)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var safe = string.Concat((name ?? "Sheet").Select(c => invalid.Contains(c) ? '_' : c));
        if (string.IsNullOrWhiteSpace(safe)) safe = "Sheet";
        return safe.Length > 31 ? safe[..31] : safe;
    }
}
