using System.Text.Json;
using PXA.Core.Contracts;
using ClosedXML.Excel;

namespace PXA.Infrastructure.Spreadsheet;

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
            if (c.Comment is { Length: > 0 } cm) try { cell.CreateComment().AddText(cm); } catch { }
            if (c.Hyperlink is { Length: > 0 } hl) try { cell.SetHyperlink(new XLHyperlink(hl)); } catch { }
        }

        foreach (var m in sheet.Merges)
            try { ws.Range(m).Merge(); } catch { /* skip invalid range */ }

        foreach (var col in sheet.Columns)
        {
            if (col.Width is { } w and > 0) ws.Column(col.Index + 1).Width = w;
            if (col.Hidden) ws.Column(col.Index + 1).Hide();
            if (col.OutlineLevel > 0) try { ws.Column(col.Index + 1).OutlineLevel = col.OutlineLevel; } catch { }
        }
        foreach (var row in sheet.Rows)
        {
            if (row.Height is { } h and > 0) ws.Row(row.Index + 1).Height = h;
            if (row.Hidden) ws.Row(row.Index + 1).Hide();
            if (row.OutlineLevel > 0) try { ws.Row(row.Index + 1).OutlineLevel = row.OutlineLevel; } catch { }
        }

        if (sheet.FrozenRows > 0) ws.SheetView.FreezeRows(sheet.FrozenRows);
        if (sheet.FrozenCols > 0) ws.SheetView.FreezeColumns(sheet.FrozenCols);

        if (sheet.AutoFilterRange is { Length: > 0 } af) try { ws.Range(af).SetAutoFilter(); } catch { }
        ApplyPageSetup(ws, sheet.PageSetup);
        foreach (var dv in sheet.DataValidations) ApplyDataValidation(ws, dv);
        foreach (var cf in sheet.ConditionalFormats) ApplyConditionalFormat(ws, cf);
        foreach (var image in sheet.Images) ApplyImage(ws, image);
        if (sheet.Protection is { Protected: true } p)
            try { if (p.Password is { Length: > 0 } pw) ws.Protect(pw); else ws.Protect(); } catch { }
    }

    private static void ApplyImage(IXLWorksheet ws, SpreadsheetImageDto image)
    {
        if (string.IsNullOrWhiteSpace(image.Data) || !image.Data.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            return;
        try
        {
            var separator = image.Data.IndexOf(',');
            if (separator < 0) return;
            using var content = new MemoryStream(Convert.FromBase64String(image.Data[(separator + 1)..]));
            var picture = ws.AddPicture(content, image.FileName ?? $"Image-{image.Id}")
                .MoveTo(ws.Cell(image.Row + 1, image.Col + 1));
            picture.WithSize(Math.Max(1, image.Width), Math.Max(1, image.Height));
        }
        catch
        {
            // Invalid transient image data is ignored; workbook data remains exportable.
        }
    }

    private static void ApplyPageSetup(IXLWorksheet ws, PageSetupDto? ps)
    {
        if (ps is null) return;
        var s = ws.PageSetup;
        try
        {
            if (ps.Orientation is { Length: > 0 } o)
                s.PageOrientation = o.Equals("landscape", StringComparison.OrdinalIgnoreCase) ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
            if (ps.PaperSize is { Length: > 0 } paper && Enum.TryParse<XLPaperSize>(paper + "Paper", true, out var size)) s.PaperSize = size;
            if (ps.PrintArea is { Length: > 0 } pa) s.PrintAreas.Add(pa);
            if (ps.Header is { Length: > 0 } h) s.Header.Center.AddText(h);
            if (ps.Footer is { Length: > 0 } f) s.Footer.Center.AddText(f);
            if (ps.FitToWidth is { } fw && ps.FitToHeight is { } fh) s.FitToPages(fw, fh);
            else if (ps.Scale is { } sc and > 0) s.Scale = (int)sc;
            if (ps.Margins is { } m)
            {
                if (m.Top is { } t) s.Margins.Top = t;
                if (m.Bottom is { } b) s.Margins.Bottom = b;
                if (m.Left is { } l) s.Margins.Left = l;
                if (m.Right is { } r) s.Margins.Right = r;
            }
            foreach (var rb in ps.RowPageBreaks) s.AddHorizontalPageBreak(rb + 1);
            foreach (var cb in ps.ColPageBreaks) s.AddVerticalPageBreak(cb + 1);
        }
        catch { /* best-effort page setup */ }
    }

    private static void ApplyDataValidation(IXLWorksheet ws, DataValidationDto dv)
    {
        if (string.IsNullOrWhiteSpace(dv.Range)) return;
        try
        {
            var v = ws.Range(dv.Range).CreateDataValidation();
            switch (dv.Type)
            {
                case "list":
                    var src = dv.ListSource ?? "";
                    v.List(src.Contains('!') || src.Contains(':') ? src : $"\"{src}\"");
                    break;
                case "wholeNumber": v.WholeNumber.Between(dv.Value1 ?? "0", dv.Value2 ?? "0"); break;
                case "decimal": v.Decimal.Between(dv.Value1 ?? "0", dv.Value2 ?? "0"); break;
                case "textLength": v.TextLength.Between(dv.Value1 ?? "0", dv.Value2 ?? "0"); break;
            }
        }
        catch { /* best-effort */ }
    }

    private static void ApplyConditionalFormat(IXLWorksheet ws, ConditionalFormatDto cf)
    {
        if (string.IsNullOrWhiteSpace(cf.Range)) return;
        try
        {
            var color = cf.Color is { Length: > 0 } c ? ParseColor(c) : XLColor.Yellow;
            if (cf.Type == "colorScale")
            {
                ws.Range(cf.Range).AddConditionalFormat().ColorScale()
                    .LowestValue(XLColor.White).HighestValue(color);
                return;
            }
            var rule = ws.Range(cf.Range).AddConditionalFormat();
            var styled = (cf.Operator ?? "greaterThan") switch
            {
                "lessThan" => rule.WhenLessThan(cf.Value ?? "0"),
                "equalTo" => rule.WhenEquals(cf.Value ?? "0"),
                "between" => rule.WhenBetween(cf.Value ?? "0", cf.Value2 ?? "0"),
                "contains" => rule.WhenContains(cf.Value ?? ""),
                _ => rule.WhenGreaterThan(cf.Value ?? "0"),
            };
            styled.Fill.SetBackgroundColor(color);
        }
        catch { /* best-effort */ }
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
