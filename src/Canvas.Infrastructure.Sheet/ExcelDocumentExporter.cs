using Canvas.Core.Abstractions;
using Canvas.Core.Contracts;
using ClosedXML.Excel;

namespace Canvas.Infrastructure.Sheet;

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

        for (int r = 0; r < cellData.Length; r++)
        {
            var row = cellData[r] ?? [];
            for (int c = 0; c < row.Length; c++)
            {
                var cell = ws.Cell(r + 1, c + 1);
                cell.Value = row[c] ?? "";

                var isHdr = hasHdr && r == 0;
                if (isHdr)
                {
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = hdrBg;
                }
                else if (zebraClr is not null && r % 2 == 1)
                {
                    cell.Style.Fill.BackgroundColor = zebraClr;
                }

                if (border > 0)
                {
                    var bc = ParseColor(s.GetStr("borderColor", "#000000"));
                    cell.Style.Border.OutsideBorder      = XLBorderStyleValues.Thin;
                    cell.Style.Border.OutsideBorderColor = bc;
                }

                var aligns = el.ColumnAlignments;
                if (aligns is not null && aligns.Length > c)
                {
                    cell.Style.Alignment.Horizontal = aligns[c] switch
                    {
                        "center" => XLAlignmentHorizontalValues.Center,
                        "right"  => XLAlignmentHorizontalValues.Right,
                        _        => XLAlignmentHorizontalValues.Left,
                    };
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
