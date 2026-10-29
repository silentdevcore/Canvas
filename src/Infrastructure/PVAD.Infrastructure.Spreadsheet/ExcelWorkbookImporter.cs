using PVAD.Core.Contracts;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace PVAD.Infrastructure.Spreadsheet;

/// <summary>
/// Imports an <c>.xlsx</c> file into a <see cref="SpreadsheetDto"/> via ClosedXML, preserving A1 formulas,
/// typed values, number formats, styles, merges, column widths, frozen panes, and defined names.
/// The reverse of <see cref="ExcelWorkbookExporter"/>.
/// </summary>
public sealed class ExcelWorkbookImporter
{
    private sealed record ColumnMetadata(int Index, double? Width, bool Hidden, int OutlineLevel);

    public SpreadsheetDto Import(Stream xlsx, string? fileName = null)
    {
        using var package = new MemoryStream();
        xlsx.CopyTo(package);
        package.Position = 0;
        var columnMetadata = ReadColumnMetadata(package);
        package.Position = 0;
        using var wb = new XLWorkbook(package);
        var dto = new SpreadsheetDto
        {
            Id = Guid.NewGuid().ToString("n"),
            Name = string.IsNullOrWhiteSpace(fileName) ? "Workbook" : Path.GetFileNameWithoutExtension(fileName),
        };

        foreach (var ws in wb.Worksheets)
            dto.Sheets.Add(ReadSheet(ws, columnMetadata.GetValueOrDefault(ws.Name, [])));

        foreach (var nr in wb.NamedRanges)
            dto.DefinedNames.Add(new DefinedNameDto { Name = nr.Name, RefersTo = SafeRefersTo(nr) });

        return dto;
    }

    private static SheetDto ReadSheet(IXLWorksheet ws, IReadOnlyList<ColumnMetadata> columnMetadata)
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

                var nf = cell.Style?.NumberFormat.Format;
                if (!string.IsNullOrEmpty(nf)) dc.NumberFormat = nf;
                dc.Style = ReadStyle(cell);
                if (cell.HasComment) try { dc.Comment = cell.GetComment().Text; } catch { }
                if (cell.HasHyperlink) try { dc.Hyperlink = cell.GetHyperlink().ExternalAddress?.ToString() ?? cell.GetHyperlink().InternalAddress; } catch { }

                if (dc.Type != "empty" || dc.Style is not null || dc.Comment is not null || dc.Hyperlink is not null)
                    sheet.Cells.Add(dc);
            }
        }

        foreach (var mr in ws.MergedRanges)
            sheet.Merges.Add(mr.RangeAddress.ToStringRelative());

        foreach (var col in ws.ColumnsUsed(XLCellsUsedOptions.All, column =>
                     column.IsHidden || column.OutlineLevel > 0 || Math.Abs(column.Width - ws.ColumnWidth) > 0.01))
        {
            sheet.Columns.Add(new SheetColumnDto
            {
                Index = col.ColumnNumber() - 1,
                Width = col.Width,
                Hidden = col.IsHidden,
                OutlineLevel = col.OutlineLevel,
            });
        }
        foreach (var metadata in columnMetadata.Where(value =>
                     !sheet.Columns.Any(existing => existing.Index == value.Index) &&
                     (value.Hidden || value.OutlineLevel > 0)))
            sheet.Columns.Add(new SheetColumnDto
            {
                Index = metadata.Index,
                Width = metadata.Width,
                Hidden = metadata.Hidden,
                OutlineLevel = metadata.OutlineLevel,
            });

        foreach (var row in ws.RowsUsed(XLCellsUsedOptions.All, candidate =>
                     candidate.IsHidden || candidate.OutlineLevel > 0 || Math.Abs(candidate.Height - ws.RowHeight) > 0.01))
        {
            sheet.Rows.Add(new SheetRowDto
            {
                Index = row.RowNumber() - 1,
                Height = row.Height,
                Hidden = row.IsHidden,
                OutlineLevel = row.OutlineLevel,
            });
        }

        try
        {
            sheet.FrozenRows = ws.SheetView.SplitRow;
            sheet.FrozenCols = ws.SheetView.SplitColumn;
        }
        catch { /* frozen panes unavailable */ }

        try { if (ws.AutoFilter is { IsEnabled: true } af) sheet.AutoFilterRange = af.Range.RangeAddress.ToStringRelative(); } catch { }
        try { if (ws.IsProtected) sheet.Protection = new ProtectionDto { Protected = true }; } catch { }
        sheet.PageSetup = ReadPageSetup(ws);
        ReadDataValidations(ws, sheet);
        ReadConditionalFormats(ws, sheet);
        ReadImages(ws, sheet);

        return sheet;
    }

    private static Dictionary<string, IReadOnlyList<ColumnMetadata>> ReadColumnMetadata(Stream package)
    {
        var result = new Dictionary<string, IReadOnlyList<ColumnMetadata>>(StringComparer.OrdinalIgnoreCase);
        using var document = SpreadsheetDocument.Open(package, false);
        var workbookPart = document.WorkbookPart;
        if (workbookPart?.Workbook.Sheets is null) return result;

        foreach (var sheet in workbookPart.Workbook.Sheets.Elements<Sheet>())
        {
            if (sheet.Id?.Value is not { Length: > 0 } relationshipId ||
                workbookPart.GetPartById(relationshipId) is not WorksheetPart worksheetPart)
                continue;

            var columns = new List<ColumnMetadata>();
            foreach (var column in worksheetPart.Worksheet.Elements<Columns>().SelectMany(value => value.Elements<Column>()))
            {
                var minimum = checked((int)(column.Min?.Value ?? 1));
                var maximum = column.Max?.Value is { } maximumValue ? checked((int)maximumValue) : minimum;
                for (var index = minimum; index <= maximum; index++)
                {
                    columns.Add(new ColumnMetadata(
                        index - 1,
                        column.CustomWidth?.Value == true ? column.Width?.Value : null,
                        column.Hidden?.Value ?? false,
                        checked((int)(column.OutlineLevel?.Value ?? 0U))));
                }
            }
            result[sheet.Name?.Value ?? "Sheet1"] = columns;
        }
        return result;
    }

    private static void ReadDataValidations(IXLWorksheet ws, SheetDto sheet)
    {
        foreach (var validation in ws.DataValidations)
        {
            foreach (var range in validation.Ranges)
            {
                var type = validation.AllowedValues switch
                {
                    XLAllowedValues.List => "list",
                    XLAllowedValues.WholeNumber => "wholeNumber",
                    XLAllowedValues.Decimal => "decimal",
                    XLAllowedValues.Date => "date",
                    XLAllowedValues.TextLength => "textLength",
                    _ => null,
                };
                if (type is null) continue;

                sheet.DataValidations.Add(new DataValidationDto
                {
                    Range = range.RangeAddress.ToStringRelative(),
                    Type = type,
                    Operator = MapValidationOperator(validation.Operator),
                    Value1 = type == "list" ? null : EmptyToNull(validation.MinValue),
                    Value2 = type == "list" ? null : EmptyToNull(validation.MaxValue),
                    ListSource = type == "list" ? UnquoteFormula(validation.Value) : null,
                });
            }
        }
    }

    private static void ReadConditionalFormats(IXLWorksheet ws, SheetDto sheet)
    {
        foreach (var format in ws.ConditionalFormats)
        {
            var type = format.ConditionalFormatType switch
            {
                XLConditionalFormatType.CellIs => "cellIs",
                XLConditionalFormatType.ColorScale => "colorScale",
                XLConditionalFormatType.DataBar => "dataBar",
                _ => null,
            };
            if (type is null) continue;

            var values = format.Values.Values.Select(value => value.Value).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            var color = format.Style.Fill.BackgroundColor.ColorType == XLColorType.Color
                ? ToHex(format.Style.Fill.BackgroundColor)
                : format.Colors.Values.FirstOrDefault(colorValue => colorValue.ColorType == XLColorType.Color) is { } firstColor
                    ? ToHex(firstColor)
                    : null;

            foreach (var range in format.Ranges)
            {
                sheet.ConditionalFormats.Add(new ConditionalFormatDto
                {
                    Range = range.RangeAddress.ToStringRelative(),
                    Type = type,
                    Operator = MapConditionalOperator(format.Operator),
                    Value = values.ElementAtOrDefault(0),
                    Value2 = values.ElementAtOrDefault(1),
                    Color = color,
                });
            }
        }
    }

    private static string? MapValidationOperator(XLOperator value) => value switch
    {
        XLOperator.GreaterThan => "greaterThan",
        XLOperator.LessThan => "lessThan",
        XLOperator.EqualTo => "equalTo",
        XLOperator.Between => "between",
        _ => null,
    };

    private static string? MapConditionalOperator(XLCFOperator value) => value switch
    {
        XLCFOperator.GreaterThan => "greaterThan",
        XLCFOperator.LessThan => "lessThan",
        XLCFOperator.Equal => "equalTo",
        XLCFOperator.Between => "between",
        XLCFOperator.Contains => "contains",
        _ => null,
    };

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? UnquoteFormula(string? value)
    {
        var normalized = EmptyToNull(value);
        return normalized is { Length: >= 2 } && normalized[0] == '"' && normalized[^1] == '"'
            ? normalized[1..^1]
            : normalized;
    }

    private static void ReadImages(IXLWorksheet ws, SheetDto sheet)
    {
        foreach (var picture in ws.Pictures)
        {
            try
            {
                using var content = new MemoryStream();
                if (picture.ImageStream.CanSeek)
                    picture.ImageStream.Position = 0;
                picture.ImageStream.CopyTo(content);
                var contentType = picture.Format switch
                {
                    ClosedXML.Excel.Drawings.XLPictureFormat.Jpeg => "image/jpeg",
                    ClosedXML.Excel.Drawings.XLPictureFormat.Png => "image/png",
                    _ => null,
                };
                if (contentType is null)
                    continue;
                sheet.Images.Add(new SpreadsheetImageDto
                {
                    Id = Guid.NewGuid().ToString("n"),
                    FileName = picture.Name,
                    ContentType = contentType,
                    Data = $"data:{contentType};base64,{Convert.ToBase64String(content.ToArray())}",
                    Row = Math.Max(0, picture.TopLeftCell.Address.RowNumber - 1),
                    Col = Math.Max(0, picture.TopLeftCell.Address.ColumnNumber - 1),
                    Width = picture.Width,
                    Height = picture.Height,
                });
            }
            catch
            {
                // A malformed drawing must not prevent the remaining workbook from importing.
            }
        }
    }

    private static PageSetupDto? ReadPageSetup(IXLWorksheet ws)
    {
        try
        {
            var s = ws.PageSetup;
            var dto = new PageSetupDto
            {
                Orientation = s.PageOrientation == XLPageOrientation.Landscape ? "landscape" : "portrait",
                PaperSize = s.PaperSize.ToString().Replace("Paper", "", StringComparison.Ordinal),
                Header = HfText(s.Header.Center),
                Footer = HfText(s.Footer.Center),
                Margins = new MarginsDto
                {
                    Top = s.Margins.Top,
                    Bottom = s.Margins.Bottom,
                    Left = s.Margins.Left,
                    Right = s.Margins.Right,
                },
                RowPageBreaks = s.RowBreaks.Select(value => Math.Max(0, value - 1)).ToList(),
                ColPageBreaks = s.ColumnBreaks.Select(value => Math.Max(0, value - 1)).ToList(),
            };
            if (s.PrintAreas.Any()) dto.PrintArea = s.PrintAreas.First().RangeAddress.ToStringRelative();
            if (s.PagesWide > 0) dto.FitToWidth = s.PagesWide;
            if (s.PagesTall > 0) dto.FitToHeight = s.PagesTall;
            if (s.PagesWide <= 0 && s.PagesTall <= 0 && s.Scale > 0) dto.Scale = s.Scale;
            // Paper size, margins, and scale are meaningful even for an otherwise default portrait sheet.
            return dto;
        }
        catch { return null; }
    }

    // ClosedXML stores header/footer text per page occurrence; AddText defaults vary — try each.
    private static string? HfText(IXLHFItem item)
    {
        foreach (var occ in new[] { XLHFOccurrence.AllPages, XLHFOccurrence.OddPages, XLHFOccurrence.EvenPages, XLHFOccurrence.FirstPage })
        {
            try { if (item.GetText(occ) is { Length: > 0 } t) return t; } catch { }
        }
        return null;
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
