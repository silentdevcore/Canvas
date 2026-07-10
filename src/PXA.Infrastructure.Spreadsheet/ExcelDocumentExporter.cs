using PXA.Core.Abstractions;
using PXA.Core.Contracts;
using ClosedXML.Excel;
using System.Text.Json;

namespace PXA.Infrastructure.Spreadsheet;

public sealed class ExcelDocumentExporter : IDocumentExporter
{
    public string FormatKey     => "excel";
    public string MimeType      => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public string FileExtension => ".xlsx";
    public IExporterCapabilities Capabilities => new ExporterCapabilities(SupportsRichText: false, SupportsFormFields: false);

    public byte[] Export(DesignExportDto design)
    {
        using var wb = new XLWorkbook();

        var allElements = design.Pages
            .SelectMany(p => p.Elements)
            .Concat(design.SharedElements)
            .Where(e => e.Hidden != true)
            .ToList();

        var tables = allElements.Where(e => e.Type == "table").ToList();
        int tableIdx = 1;

        // One sheet per table
        foreach (var table in tables)
        {
            var sheetName = SanitizeSheetName(table.Name ?? $"Table {tableIdx++}");
            var ws        = wb.Worksheets.Add(sheetName);
            RenderTable(ws, table);
        }

        // Summary sheet for non-table elements
        var nonTable = allElements.Where(e => e.Type != "table").ToList();
        if (nonTable.Count > 0)
        {
            var summary = wb.Worksheets.Add("Summary");
            summary.Cell(1, 1).Value = "Type";
            summary.Cell(1, 2).Value = "Name";
            summary.Cell(1, 3).Value = "Content";
            var hdrRow = summary.Row(1);
            hdrRow.Style.Font.Bold = true;
            hdrRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#f1f5f9");

            int r = 2;
            foreach (var el in nonTable)
            {
                var content = el.Type switch
                {
                    "text"       => el.Content ?? "",
                    "richtext"   => StripTags(el.HtmlContent ?? ""),
                    "link"       => el.Href ?? el.Content ?? "",
                    "number"     => el.NumberValue?.ToString() ?? "",
                    "field"      => el.FieldLabel ?? "",
                    "checkbox"   => el.FieldLabel ?? "",
                    "signature"  => el.SignatureLabel ?? "",
                    "note"       => el.NoteTitle ?? "",
                    "optionlist" => string.Join("; ", el.Options ?? []),
                    "dropdown"   => string.Join("; ", el.Options ?? []),
                    "radio"      => string.Join("; ", el.Options ?? []),
                    _            => el.Content ?? "",
                };
                summary.Cell(r, 1).Value = el.Type;
                summary.Cell(r, 2).Value = el.Name ?? el.Id;
                summary.Cell(r, 3).Value = content;
                r++;
            }

            summary.Columns().AdjustToContents();
        }

        if (!wb.Worksheets.Any())
            wb.Worksheets.Add("Sheet1");

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void RenderTable(IXLWorksheet ws, ElementDto el)
    {
        var s        = el.Style ?? [];
        var cellData = el.CellData ?? [];
        if (cellData.Length == 0) return;

        var hasHdr   = el.HeaderRow == true;
        var hdrBg    = ParseColor(el.HeaderBgColor ?? "#f1f5f9");
        var zebraClr = el.ZebraEnabled == true ? ParseColor(el.ZebraColor ?? "#f9fafb") : null;
        var border   = (int)s.GetNum("borderWidth", 1);
        var matrixHeaders = RdlMatrixHeaders(s);
        var cellStyleLookup = (el.CellStyles ?? []).GroupBy(x => (x.Row, x.Col)).ToDictionary(gp => gp.Key, gp => gp.First());
        var rowOffset = 0;

        foreach (var header in matrixHeaders)
        {
            var rowNumber = ++rowOffset;
            var range = ws.Range(rowNumber, 1, rowNumber, Math.Max(cellData[0]?.Length ?? 1, 1));
            range.Merge();
            range.Value = header;
            range.Style.Font.Bold = true;
            range.Style.Font.FontColor = XLColor.FromHtml("#075985");
            range.Style.Fill.BackgroundColor = XLColor.FromHtml("#e0f2fe");
        }

        for (int r = 0; r < cellData.Length; r++)
        {
            var row = cellData[r] ?? [];
            for (int c = 0; c < row.Length; c++)
            {
                var cell = ws.Cell(r + 1 + rowOffset, c + 1);
                cell.Value = row[c] ?? "";
                var cs = cellStyleLookup.GetValueOrDefault((r, c));

                var isHdr = hasHdr && r == 0;
                if (isHdr) cell.Style.Font.Bold = true;
                if (cs?.BackgroundColor is { } cbg)
                    cell.Style.Fill.BackgroundColor = ParseColor(cbg);
                else if (isHdr)
                    cell.Style.Fill.BackgroundColor = hdrBg;
                else if (zebraClr is not null && r % 2 == 1)
                    cell.Style.Fill.BackgroundColor = zebraClr;

                if (cs is not null && HasCellBorder(cs))
                {
                    ApplyExcelCellBorders(cell, cs);
                }
                else if (border > 0)
                {
                    var bc = ParseColor(s.GetStr("borderColor", "#000000"));
                    cell.Style.Border.OutsideBorder      = XLBorderStyleValues.Thin;
                    cell.Style.Border.OutsideBorderColor = bc;
                }

                var align = cs?.TextAlign ?? (el.ColumnAlignments is { } a && a.Length > c ? a[c] : null);
                if (align is not null)
                {
                    cell.Style.Alignment.Horizontal = align switch
                    {
                        "center" => XLAlignmentHorizontalValues.Center,
                        "right"  => XLAlignmentHorizontalValues.Right,
                        _        => XLAlignmentHorizontalValues.Left,
                    };
                }

                if (cs is not null)
                {
                    if (cs.FontFamily is { Length: > 0 } ff) cell.Style.Font.FontName = ff;
                    if (cs.FontSize is { } fsz and > 0) cell.Style.Font.FontSize = fsz;
                    if (cs.Bold == true) cell.Style.Font.Bold = true;
                    if (cs.Italic == true) cell.Style.Font.Italic = true;
                    if (cs.Color is { Length: > 0 } clr) cell.Style.Font.FontColor = ParseColor(clr);
                }
            }

            // Column widths
            if (el.ColumnWidths is { Length: > 0 })
            {
                for (int c = 0; c < el.ColumnWidths.Length; c++)
                    ws.Column(c + 1).Width = el.ColumnWidths[c] / 7.5;
            }
        }

        ws.Columns().AdjustToContents();
    }

    private static bool HasCellBorder(CellStyleDto cs) =>
        cs.BorderColor != null || cs.BorderWidth != null
        || cs.BorderTop != null || cs.BorderRight != null || cs.BorderBottom != null || cs.BorderLeft != null;

    // Per-side cell borders; per-side override → uniform fallback. Sides with neither stay borderless
    // (explicit cell borders replace the table grid for that cell, matching the other exporters).
    private static void ApplyExcelCellBorders(IXLCell cell, CellStyleDto cs)
    {
        var b = cell.Style.Border;
        var hasUniform = cs.BorderColor != null || cs.BorderWidth != null;
        XLColor UColor(CellBorderSideDto? side) => ParseColor(side?.Color ?? cs.BorderColor ?? "#000000");
        XLBorderStyleValues UStyle(CellBorderSideDto? side) => BorderStyleFor(side?.Width ?? cs.BorderWidth ?? 1);

        if (cs.BorderTop is not null || hasUniform)    { b.TopBorder = UStyle(cs.BorderTop); b.TopBorderColor = UColor(cs.BorderTop); }
        if (cs.BorderRight is not null || hasUniform)  { b.RightBorder = UStyle(cs.BorderRight); b.RightBorderColor = UColor(cs.BorderRight); }
        if (cs.BorderBottom is not null || hasUniform) { b.BottomBorder = UStyle(cs.BorderBottom); b.BottomBorderColor = UColor(cs.BorderBottom); }
        if (cs.BorderLeft is not null || hasUniform)   { b.LeftBorder = UStyle(cs.BorderLeft); b.LeftBorderColor = UColor(cs.BorderLeft); }
    }

    private static XLBorderStyleValues BorderStyleFor(double widthPt) =>
        widthPt >= 3 ? XLBorderStyleValues.Thick : widthPt >= 2 ? XLBorderStyleValues.Medium : XLBorderStyleValues.Thin;

    private static List<string> RdlMatrixHeaders(Dictionary<string, object> style)
    {
        var headers = new List<string>();
        AddRdlMatrixHeaders(style, "rdlTablixColumnHierarchy", headers);
        AddRdlMatrixHeaders(style, "rdlTablixRowHierarchy", headers);
        return headers;
    }

    private static void AddRdlMatrixHeaders(Dictionary<string, object> style, string key, List<string> headers)
    {
        if (!style.TryGetValue(key, out var value) || value is null) return;

        if (value is JsonElement { ValueKind: JsonValueKind.Array } jsonArray)
        {
            foreach (var item in jsonArray.EnumerateArray())
                AddRdlMatrixHeader(item, headers);
            return;
        }

        if (value is IEnumerable<object> items)
        {
            foreach (var item in items)
                AddRdlMatrixHeader(item, headers);
        }
    }

    private static void AddRdlMatrixHeader(object item, List<string> headers)
    {
        switch (item)
        {
            case JsonElement { ValueKind: JsonValueKind.Object } json:
                var text = JsonProp(json, "headerText") ?? JsonProp(json, "groupName");
                if (!string.IsNullOrWhiteSpace(text)) headers.Add(text);
                break;
            case IReadOnlyDictionary<string, object> dict:
                if ((HeaderValue(dict, "headerText") ?? HeaderValue(dict, "groupName")) is { Length: > 0 } value)
                    headers.Add(value);
                break;
        }
    }

    private static string? JsonProp(JsonElement json, string name) =>
        json.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static string? HeaderValue(IReadOnlyDictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static XLColor ParseColor(string hex)
    {
        try { return XLColor.FromHtml(hex); }
        catch { return XLColor.White; }
    }

    private static string SanitizeSheetName(string name)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var safe    = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
        return safe.Length > 31 ? safe[..31] : safe;
    }

    private static string StripTags(string html) =>
        System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", "").Trim();
}
