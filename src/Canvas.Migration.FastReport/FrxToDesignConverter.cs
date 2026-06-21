using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Canvas.Core.Contracts;
using Canvas.Migration.Abstractions;

namespace Canvas.Migration.FastReport;

public sealed class FrxConvertResult
{
    public required DesignExportDto Design { get; init; }
    public required IReadOnlyList<MigrationDiagnostic> Diagnostics { get; init; }
}

/// <summary>
/// Converts a FastReport .NET <c>.frx</c> report — plain, namespace-free XML with a <b>banded</b> layout
/// (root <c>&lt;Report&gt;</c> → <c>&lt;ReportPage&gt;</c> → <c>*Band</c> elements holding objects) — into a
/// Canvas <see cref="DesignExportDto"/>. Like ActiveReports <c>.rpx</c> this is band-relative, so it
/// mirrors the <c>Canvas.Migration.Rpx</c> band-flatten approach. Object geometry is in <b>pixels</b>
/// (96 dpi) and page size in <b>millimetres</b>; sub-properties are dotted attributes
/// (<c>Fill.Color</c>, <c>Border.Color</c>, <c>Font="Tahoma, 9pt, style=Bold"</c>). Elements are matched
/// by <see cref="XName.LocalName"/>.
/// </summary>
public sealed class FrxToDesignConverter
{
    private const double PxToPt = 72.0 / 96.0;    // FastReport object units are screen pixels (96 dpi)
    private const double MmToPt = 72.0 / 25.4;    // ReportPage paper/margins are millimetres
    private const double A4WidthMm = 210, A4HeightMm = 297, DefaultMarginMm = 10;

    /// <summary>Detects a FastReport <c>.frx</c>: root <c>&lt;Report&gt;</c> with a <c>&lt;ReportPage&gt;</c>
    /// child, not an RDL (<c>reportdefinition</c>) namespace and not an ActiveReports <c>&lt;Sections&gt;</c>.</summary>
    public static bool LooksLikeFrx(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;
        if (!source.TrimStart().StartsWith('<')) return false;
        try
        {
            var root = XDocument.Parse(source).Root;
            return root is not null
                && root.Name.LocalName == "Report"
                && !root.Name.NamespaceName.Contains("reportdefinition", StringComparison.OrdinalIgnoreCase)
                && root.DescendantsAndSelf().Any(e => e.Name.LocalName == "ReportPage")
                && !root.DescendantsAndSelf().Any(e => e.Name.LocalName == "Sections");
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
    }

    public FrxConvertResult ConvertAuto(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source cannot be null or empty.", nameof(source));
        return Convert(source);
    }

    public FrxConvertResult Convert(string frxXml)
    {
        if (string.IsNullOrWhiteSpace(frxXml))
            throw new ArgumentException("Source cannot be null or empty.", nameof(frxXml));

        XElement root;
        try { root = XDocument.Parse(frxXml).Root ?? throw new ArgumentException("Empty .frx document."); }
        catch (System.Xml.XmlException ex) { throw new ArgumentException($"Invalid .frx XML: {ex.Message}", nameof(frxXml)); }

        if (root.Name.LocalName != "Report")
            throw new ArgumentException("Not a FastReport .frx — expected a root <Report> element.", nameof(frxXml));

        var report = new RawReport
        {
            Name = Attr(root, "ReportInfo.Name") ?? Attr(root, "Name") ?? "FastReport Report",
            HasScript = HasScript(root)
        };

        var page = Descendant(root, "ReportPage");
        ResolvePage(page, report);

        // Bands are descendants of ReportPage (a ChildBand can nest inside another band); each band's
        // objects are its own non-band children. Band Top is the absolute design position (px).
        var sectionNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var bandEl in (page?.Descendants() ?? Enumerable.Empty<XElement>())
                     .Where(e => e.Name.LocalName.EndsWith("Band", StringComparison.Ordinal)))
        {
            var type = bandEl.Name.LocalName;
            var name = UniqueName(Attr(bandEl, "Name") ?? type, sectionNames);
            report.Bands.Add(new RawBand
            {
                Name = name,
                Type = type,
                TopPt = ToPx(Attr(bandEl, "Top")) * PxToPt,
                HeightPt = ToPx(Attr(bandEl, "Height")) * PxToPt
            });

            foreach (var obj in bandEl.Elements().Where(e => !e.Name.LocalName.EndsWith("Band", StringComparison.Ordinal)))
            {
                var raw = ParseObject(obj, name);
                if (raw is not null) report.Elements.Add(raw);
            }
        }

        return BuildDesign(report);
    }

    private static void ResolvePage(XElement? page, RawReport report)
    {
        var wMm = ToDouble(Attr(page, "PaperWidth"));
        var hMm = ToDouble(Attr(page, "PaperHeight"));
        report.PageWidthPt = (wMm > 0 ? wMm : A4WidthMm) * MmToPt;
        report.PageHeightPt = (hMm > 0 ? hMm : A4HeightMm) * MmToPt;
        report.MarginLeftPt = (Attr(page, "LeftMargin") is { } l ? ToDouble(l) : DefaultMarginMm) * MmToPt;
        report.MarginTopPt = (Attr(page, "TopMargin") is { } t ? ToDouble(t) : DefaultMarginMm) * MmToPt;
        report.MarginBottomPt = (Attr(page, "BottomMargin") is { } b ? ToDouble(b) : DefaultMarginMm) * MmToPt;

        if (string.Equals(Attr(page, "Landscape"), "true", StringComparison.OrdinalIgnoreCase))
            (report.PageWidthPt, report.PageHeightPt) = (report.PageHeightPt, report.PageWidthPt);
    }

    private static RawElement? ParseObject(XElement el, string bandName)
    {
        var type = el.Name.LocalName;

        var raw = new RawElement
        {
            Name = Attr(el, "Name") ?? type,
            Type = type,
            Band = bandName,
            X = ToPx(Attr(el, "Left")) * PxToPt,
            Y = ToPx(Attr(el, "Top")) * PxToPt,
            W = ToPx(Attr(el, "Width")) * PxToPt,
            H = ToPx(Attr(el, "Height")) * PxToPt,
            Text = Attr(el, "Text"),
            ForeColor = ParseColor(Attr(el, "TextFill.Color")) ?? "#000000",
            BackColor = ParseColor(Attr(el, "Fill.Color")),
            TextAlign = ParseAlignment(Attr(el, "HorzAlign")),
            BorderColor = ParseColor(Attr(el, "Border.Color")),
            BorderWidth = Attr(el, "Border.Width") is { } bw ? ToDouble(bw) : null,
            BorderLines = Attr(el, "Border.Lines"),
            DataColumn = Attr(el, "DataColumn")
        };
        ApplyFont(raw, Attr(el, "Font"));

        if (type == "LineObject") raw.ForeColor = ParseColor(Attr(el, "Border.Color")) ?? raw.ForeColor;
        if (type == "ShapeObject") raw.ShapeKind = ShapeKindFromName(Attr(el, "Shape"));
        if (type == "PictureObject") raw.ImageDataUrl = ExtractImageDataUrl(el);
        if (type == "BarcodeObject") raw.Symbology = Attr(el, "Barcode") ?? Attr(el, "Symbology");
        if (type == "CheckBoxObject") raw.Checked = string.Equals(Attr(el, "Checked"), "true", StringComparison.OrdinalIgnoreCase);
        if (type == "TableObject") ParseTable(el, raw);

        return raw;
    }

    // FastReport TableObject holds <TableColumn Width> then <TableRow Height> each with <TableCell Text>.
    // Flatten to a row-major grid; honour ColSpan by padding empties so columns stay aligned.
    private static void ParseTable(XElement el, RawElement raw)
    {
        var columns = el.Elements().Where(e => e.Name.LocalName == "TableColumn").ToList();
        raw.ColumnWidthsPt = columns.Count > 0
            ? columns.Select(c => ToPx(Attr(c, "Width")) * PxToPt).ToArray()
            : null;

        var grid = new List<List<string>>();
        var cellStyles = new List<CellStyleDto>();
        string[]? aligns = null;
        var rowIndex = 0;
        foreach (var row in el.Elements().Where(e => e.Name.LocalName == "TableRow"))
        {
            var cells = new List<string>();
            var rowAligns = new List<string>();
            var colIndex = 0;
            foreach (var cell in row.Elements().Where(e => e.Name.LocalName == "TableCell"))
            {
                if (ExtractFrxCellStyle(cell, rowIndex, colIndex) is { } cs) cellStyles.Add(cs);
                cells.Add(CellDisplay(Attr(cell, "Text")));
                rowAligns.Add(ParseAlignment(Attr(cell, "HorzAlign")));
                var span = Math.Max(1, (int)ToDouble(Attr(cell, "ColSpan")));
                for (var i = 1; i < span; i++) { cells.Add(""); rowAligns.Add("left"); }
                colIndex += span;
            }
            if (cells.Count == 0) continue;
            grid.Add(cells);
            aligns ??= rowAligns.ToArray();
            rowIndex++;
        }

        raw.TableCells = grid.Count > 0 ? grid : null;
        raw.CellStyles = cellStyles.Count > 0 ? cellStyles : null;
        raw.ColumnAlignments = aligns;
        raw.TableHasHeader = grid.Count > 1;
    }

    // Per-cell style from a FastReport TableCell: fill/text colour, alignment, font, and borders
    // (Border.Lines "All" → uniform; otherwise the listed sides).
    private static CellStyleDto? ExtractFrxCellStyle(XElement cell, int row, int col)
    {
        var cs = new CellStyleDto { Row = row, Col = col };
        var any = false;

        if (ParseColor(Attr(cell, "Fill.Color")) is { } bg)     { cs.BackgroundColor = bg; any = true; }
        if (ParseColor(Attr(cell, "TextFill.Color")) is { } fc) { cs.Color = fc; any = true; }
        if (Attr(cell, "HorzAlign") is { Length: > 0 } ha)      { cs.TextAlign = ParseAlignment(ha); any = true; }

        if (Attr(cell, "Font") is { Length: > 0 } font)
        {
            var tmp = new RawElement { Name = "", Type = "" };
            ApplyFont(tmp, font);
            cs.FontFamily = tmp.FontFamily;
            cs.FontSize = tmp.FontSize;
            if (tmp.Bold) cs.Bold = true;
            if (tmp.Italic) cs.Italic = true;
            any = true;
        }

        if (SplitNumbers(Attr(cell, "Padding")).DefaultIfEmpty(0).Max() is var padPx and > 0)
        { cs.Padding = padPx * PxToPt; any = true; }

        if (Attr(cell, "Border.Lines") is { Length: > 0 } lines)
        {
            var color = ParseColor(Attr(cell, "Border.Color")) ?? "#000000";
            var width = Attr(cell, "Border.Width") is { } bw ? ToDouble(bw) : 1;
            CellBorderSideDto Side() => new() { Color = color, Width = width };
            if (lines.Contains("All", StringComparison.OrdinalIgnoreCase))
            { cs.BorderColor = color; cs.BorderWidth = width; }
            else
            {
                if (lines.Contains("Top", StringComparison.OrdinalIgnoreCase))    cs.BorderTop = Side();
                if (lines.Contains("Right", StringComparison.OrdinalIgnoreCase))   cs.BorderRight = Side();
                if (lines.Contains("Bottom", StringComparison.OrdinalIgnoreCase))  cs.BorderBottom = Side();
                if (lines.Contains("Left", StringComparison.OrdinalIgnoreCase))    cs.BorderLeft = Side();
            }
            any = true;
        }

        return any ? cs : null;
    }

    private static double[] SplitNumbers(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([',', ' '], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                   .Select(p => double.TryParse(p, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0)
                   .ToArray();

    // A table cell value: a single [Source.Column] becomes a Canvas binding token; anything else is literal.
    private static string CellDisplay(string? text)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0) return "";
        var m = Regex.Match(text, @"^\[([\w.]+)\]$");
        return m.Success ? $"{{{{{LastSegment(m.Groups[1].Value)}}}}}" : text;
    }

    // ── Band-flatten build (mirrors Canvas.Migration.Rpx) ──────────────────────────────────────────

    private static FrxConvertResult BuildDesign(RawReport report)
    {
        var diagnostics = new List<MigrationDiagnostic>();
        var bandByName = new Dictionary<string, RawBand>(StringComparer.Ordinal);
        foreach (var b in report.Bands) bandByName.TryAdd(b.Name, b);

        var elements = new List<ElementDto>();
        var sharedElements = new List<ElementDto>();
        var mapped = 0;

        foreach (var raw in report.Elements)
        {
            var band = raw.Band is not null && bandByName.TryGetValue(raw.Band, out var b) ? b : null;
            var bandType = band?.Type ?? "";

            double yPt = bandType switch
            {
                "PageHeaderBand" => report.MarginTopPt + raw.Y,
                "PageFooterBand" => report.PageHeightPt - report.MarginBottomPt - (band?.HeightPt ?? 0) + raw.Y,
                _ => report.MarginTopPt + (band?.TopPt ?? 0) + raw.Y
            };
            var x = report.MarginLeftPt + raw.X;

            var element = MapControl(raw, x, yPt, diagnostics);
            if (element is null) continue;

            diagnostics.Add(Info("CANMIGFRX002", $"'{raw.Name}' ({raw.Type}) → Canvas {element.Type}."));

            if (raw.TextExpression is { } expr)
                ApplyBinding(element, expr, diagnostics);
            else if (raw.DataColumn is { Length: > 0 } col)
            {
                element.Binding = LastSegment(col);
                if (element.Type == "barcode") element.BarcodeValue = $"{{{{{LastSegment(col)}}}}}";
                else if (element.Type == "text") element.Content = $"{{{{{LastSegment(col)}}}}}";
                diagnostics.Add(Info("CANMIGFRX010", $"'{raw.Name}' bound to {col} → Canvas binding '{LastSegment(col)}'."));
            }

            (bandType is "PageHeaderBand" or "PageFooterBand" ? sharedElements : elements).Add(element);
            mapped++;
        }

        elements.Sort((p, q) => p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.X.CompareTo(q.X));
        sharedElements.Sort((p, q) => p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.X.CompareTo(q.X));

        if (report.HasScript)
            diagnostics.Add(Warn("CANMIGFRX011",
                "Report contains script/event handlers — Canvas has no scripting; migrate that logic manually."));

        diagnostics.Insert(0, Info("CANMIGFRX001",
            $"FastReport '{report.Name}' detected — {report.Bands.Count} band(s), {mapped} object(s) mapped."));

        var design = new DesignExportDto
        {
            Id = $"frx-report-{Guid.NewGuid():N}",
            Name = report.Name,
            Category = "imported",
            Description = "Imported from a FastReport .NET report (.frx).",
            PageSettings = new PageSettingsDto { Width = report.PageWidthPt, Height = report.PageHeightPt, Unit = "pt" },
            Pages = [new PageDto { Id = "page-1", Elements = elements }],
            SharedElements = sharedElements
        };

        return new FrxConvertResult { Design = design, Diagnostics = diagnostics };
    }

    private static ElementDto? MapControl(RawElement raw, double x, double y, List<MigrationDiagnostic> diagnostics)
    {
        var element = new ElementDto { Id = $"frx-{raw.Name}", Name = raw.Name, X = x, Y = y, Width = raw.W, Height = raw.H };

        switch (raw.Type)
        {
            case "TextObject":
                element.Type = "text";
                ClassifyText(raw, element);
                element.Style = BuildTextStyle(raw);
                return element;

            case "LineObject":
                element.Type = "line";
                element.Style = new Dictionary<string, object> { ["color"] = raw.ForeColor };
                if (raw.BorderWidth is { } lineW) element.Style["strokeWidth"] = lineW;
                return element;

            case "ShapeObject":
                element.Type = raw.ShapeKind == "ellipse" ? "circle" : "rect";
                element.Style = new Dictionary<string, object> { ["borderColor"] = raw.BorderColor ?? raw.ForeColor };
                if (raw.BackColor is { } bg) element.Style["backgroundColor"] = bg;
                if (raw.BorderWidth is { } borderW) element.Style["borderWidth"] = borderW;
                return element;

            case "PictureObject":
                element.Type = "image";
                element.FitMode = "contain";
                if (raw.ImageDataUrl is { } dataUrl)
                    element.Content = dataUrl;
                else
                    diagnostics.Add(Warn("CANMIGFRX012",
                        $"'{raw.Name}' picture data isn't embeddable from source — inserted an empty image placeholder."));
                return element;

            case "BarcodeObject":
                element.Type = "barcode";
                element.BarcodeValue = raw.Text ?? "";
                element.BarcodeType = BarcodeTypeFromSymbology(raw.Symbology);
                return element;

            case "CheckBoxObject":
                element.Type = "checkmark";
                element.CheckState = raw.Checked ? "checked" : "empty";
                return element;

            case "RichObject":
                element.Type = "richtext";
                element.HtmlContent = $"<p>{raw.Text ?? ""}</p>";
                return element;

            case "SubreportObject":
                diagnostics.Add(Warn("CANMIGFRX011",
                    $"'{raw.Name}' is a sub-report — requires manual migration; inserted a placeholder."));
                return Placeholder(element, $"[Sub-report: {raw.Name} — migrate manually]");

            case "TableObject":
                return MapTable(raw, element, diagnostics);

            default:
                diagnostics.Add(Warn("CANMIGFRX011", $"'{raw.Name}' is a {raw.Type} — not supported by Canvas yet; inserted a placeholder."));
                return Placeholder(element, $"[{raw.Type}: migrate manually]");
        }
    }

    private static ElementDto MapTable(RawElement raw, ElementDto element, List<MigrationDiagnostic> diagnostics)
    {
        if (raw.TableCells is not { Count: > 0 } grid)
        {
            diagnostics.Add(Warn("CANMIGFRX011", $"'{raw.Name}' TableObject has no parseable rows — inserted a placeholder."));
            return Placeholder(element, $"[Table: {raw.Name} — migrate manually]");
        }

        var columns = grid.Max(r => r.Count);
        var cellData = grid
            .Select(r => r.Count == columns ? r.ToArray() : r.Concat(Enumerable.Repeat("", columns - r.Count)).ToArray())
            .ToArray();

        element.Type = "table";
        element.CellData = cellData;
        element.ColumnWidths = FitWidths(raw.ColumnWidthsPt, columns, raw.W);
        element.ColumnAlignments = FitToColumns(raw.ColumnAlignments, columns);
        element.HeaderRow = raw.TableHasHeader;
        element.CellStyles = raw.CellStyles is { Count: > 0 } cs ? cs.ToArray() : null;

        diagnostics.Add(Info("CANMIGFRX013",
            $"'{raw.Name}' FastReport TableObject was mapped to a Canvas table ({grid.Count} row(s) × {columns} column(s))."));
        return element;
    }

    // Reconcile parsed column widths (pixels→pt) with the actual column count: truncate extras, and
    // distribute the table's remaining width across any columns that lacked an explicit <TableColumn>.
    private static double[]? FitWidths(double[]? widths, int columns, double totalPt)
    {
        if (widths is null || widths.Length == 0 || columns <= 0) return null;
        if (widths.Length == columns) return widths;
        if (widths.Length > columns) return widths.Take(columns).ToArray();
        var pad = columns - widths.Length;
        var remaining = Math.Max(0, totalPt - widths.Sum());
        var each = pad > 0 ? remaining / pad : 0;
        return widths.Concat(Enumerable.Repeat(each, pad)).ToArray();
    }

    private static string[]? FitToColumns(string[]? aligns, int columns)
    {
        if (aligns is null || aligns.Length == 0 || columns <= 0) return null;
        if (aligns.Length == columns) return aligns;
        if (aligns.Length > columns) return aligns.Take(columns).ToArray();
        return aligns.Concat(Enumerable.Repeat("left", columns - aligns.Length)).ToArray();
    }

    private static ElementDto Placeholder(ElementDto element, string label)
    {
        element.Type = "text";
        element.Content = label;
        element.Binding = null;
        element.Style = new Dictionary<string, object>
        {
            ["backgroundColor"] = "#F0F0F0",
            ["borderColor"] = "#BBBBBB",
            ["borderWidth"] = 1.0,
            ["borderStyle"] = "dashed",
            ["color"] = "#888888",
            ["textAlign"] = "center",
            ["fontStyle"] = "italic"
        };
        return element;
    }

    private static Dictionary<string, object> BuildTextStyle(RawElement raw)
    {
        var style = new Dictionary<string, object> { ["color"] = raw.ForeColor };
        if (raw.FontFamily is not null) style["fontFamily"] = raw.FontFamily;
        if (raw.FontSize is { } size) style["fontSize"] = size;
        if (raw.Bold) style["fontWeight"] = "bold";
        if (raw.Italic) style["fontStyle"] = "italic";
        var decoration = string.Join(" ", new[] { raw.Underline ? "underline" : null, raw.Strikeout ? "line-through" : null }.Where(s => s is not null));
        if (decoration.Length > 0) style["textDecoration"] = decoration;
        if (raw.BackColor is { } bg) style["backgroundColor"] = bg;
        style["textAlign"] = raw.TextAlign;
        return style;
    }

    // FastReport TextObject.Text is literal text, a single [Source.Column] field, or a script expression.
    private static void ClassifyText(RawElement raw, ElementDto element)
    {
        var text = raw.Text ?? "";
        if (text.Length == 0) { element.Content = ""; return; }
        var single = Regex.Match(text, @"^\s*\[([\w.]+)\]\s*$");
        if (single.Success && !text.Contains(' '))
        {
            raw.TextExpression = single.Groups[1].Value;   // resolved into a binding in BuildDesign
            element.Content = text;
        }
        else if (text.Contains('[') && text.Contains(']'))
        {
            raw.TextExpression = text;                      // mixed/complex → expression + warning
            element.Content = text;
        }
        else
        {
            element.Content = text;                         // literal
        }
    }

    private static void ApplyBinding(ElementDto element, string expression, List<MigrationDiagnostic> diagnostics)
    {
        var single = Regex.Match(expression, @"^[\w.]+$");
        if (single.Success)
        {
            var field = LastSegment(expression);
            element.Binding = field;
            element.Content = $"{{{{{field}}}}}";
            diagnostics.Add(Info("CANMIGFRX010", $"'{element.Name}' bound to [{expression}] → Canvas binding '{field}'."));
        }
        else
        {
            element.Expression = expression;
            if (string.IsNullOrEmpty(element.Content)) element.Content = expression;
            diagnostics.Add(Warn("CANMIGFRX010", $"'{element.Name}' expression '{expression}' mapped to Canvas expression — review the syntax."));
        }
    }

    private static string LastSegment(string path)
    {
        var dot = path.LastIndexOf('.');
        return dot >= 0 ? path[(dot + 1)..] : path;
    }

    private static void ApplyFont(RawElement raw, string? value)
    {
        // "Tahoma, 9.75pt, style=Bold, Italic, Underline"
        if (string.IsNullOrWhiteSpace(value)) return;
        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 1) raw.FontFamily = parts[0];
        foreach (var part in parts)
        {
            var m = Regex.Match(part, @"([\d.]+)\s*pt", RegexOptions.IgnoreCase);
            if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var size))
                raw.FontSize = size;
        }
        raw.Bold = value.Contains("Bold", StringComparison.OrdinalIgnoreCase);
        raw.Italic = value.Contains("Italic", StringComparison.OrdinalIgnoreCase);
        raw.Underline = value.Contains("Underline", StringComparison.OrdinalIgnoreCase);
        raw.Strikeout = value.Contains("Strikeout", StringComparison.OrdinalIgnoreCase) || value.Contains("Strikethrough", StringComparison.OrdinalIgnoreCase);
    }

    private static string ParseAlignment(string? text)
    {
        text ??= "";
        if (text.Contains("Center", StringComparison.OrdinalIgnoreCase)) return "center";
        if (text.Contains("Right", StringComparison.OrdinalIgnoreCase)) return "right";
        if (text.Contains("Justify", StringComparison.OrdinalIgnoreCase)) return "justify";
        return "left";
    }

    private static string ShapeKindFromName(string? shape)
    {
        shape ??= "";
        if (shape.Contains("Ellipse", StringComparison.OrdinalIgnoreCase) || shape.Contains("Circle", StringComparison.OrdinalIgnoreCase)) return "ellipse";
        return "rect";
    }

    private static string BarcodeTypeFromSymbology(string? symbology)
    {
        var s = (symbology ?? "").Replace("-", "").Replace("_", "");
        if (s.Contains("Code39", StringComparison.OrdinalIgnoreCase)) return "code39";
        if (s.Contains("EAN13", StringComparison.OrdinalIgnoreCase)) return "ean13";
        if (s.Contains("EAN8", StringComparison.OrdinalIgnoreCase)) return "ean8";
        if (s.Contains("UPCA", StringComparison.OrdinalIgnoreCase)) return "upca";
        if (s.Contains("PDF417", StringComparison.OrdinalIgnoreCase)) return "pdf417";
        return "code128";
    }

    private static string? ExtractImageDataUrl(XElement el)
    {
        var candidate = Attr(el, "Image") ?? Attr(el, "ImageData")
            ?? el.Elements().FirstOrDefault(e => e.Name.LocalName is "Image" or "ImageData")?.Value
            ?? (string.IsNullOrWhiteSpace(el.Value) ? null : el.Value);
        if (string.IsNullOrWhiteSpace(candidate)) return null;
        if (candidate.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return candidate;
        var b64 = Regex.Replace(candidate, @"\s+", "");
        return b64.Length >= 16 && b64.Length % 4 == 0 && Regex.IsMatch(b64, @"^[A-Za-z0-9+/]+={0,2}$")
            ? $"data:image/png;base64,{b64}" : null;
    }

    // Colour: .NET named (WhiteSmoke, Maroon, …), "#RRGGBB"/"#RGB", or ARGB int / "A,R,G,B".
    private static string? ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        if (v.Equals("Transparent", StringComparison.OrdinalIgnoreCase)) return null;
        if (v.StartsWith('#'))
            return v.Length == 4 ? $"#{v[1]}{v[1]}{v[2]}{v[2]}{v[3]}{v[3]}".ToUpperInvariant() : v.ToUpperInvariant();
        var nums = v.Split([',', ' '], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (nums.Length >= 3 && nums.All(n => int.TryParse(n, out _)))
        {
            var c = nums.Select(int.Parse).ToArray();
            var o = c.Length >= 4 ? 1 : 0;   // A,R,G,B → drop alpha
            return $"#{c[o]:X2}{c[o + 1]:X2}{c[o + 2]:X2}";
        }
        if (long.TryParse(v, out var argb) && argb > 0)   // packed ARGB integer
            return $"#{(argb >> 16) & 0xFF:X2}{(argb >> 8) & 0xFF:X2}{argb & 0xFF:X2}";
        return NamedColor(v);
    }

    private static string NamedColor(string name) => name.Trim().ToLowerInvariant() switch
    {
        "white" => "#FFFFFF",
        "whitesmoke" => "#F5F5F5",
        "black" => "#000000",
        "red" => "#FF0000",
        "green" => "#008000",
        "blue" => "#0000FF",
        "gray" or "grey" => "#808080",
        "darkgray" or "darkgrey" => "#A9A9A9",
        "lightgray" or "lightgrey" => "#D3D3D3",
        "dimgray" or "dimgrey" => "#696969",
        "silver" => "#C0C0C0",
        "gainsboro" => "#DCDCDC",
        "yellow" => "#FFFF00",
        "orange" => "#FFA500",
        "navy" => "#000080",
        "maroon" => "#800000",
        "teal" => "#008080",
        "olive" => "#808000",
        "lime" => "#00FF00",
        "aqua" or "cyan" => "#00FFFF",
        "fuchsia" or "magenta" => "#FF00FF",
        "purple" => "#800080",
        "pink" => "#FFC0CB",
        "brown" => "#A52A2A",
        "gold" => "#FFD700",
        "darkblue" => "#00008B",
        "darkgreen" => "#006400",
        "darkred" => "#8B0000",
        "royalblue" => "#4169E1",
        "steelblue" => "#4682B4",
        "lightblue" => "#ADD8E6",
        "transparent" => "#00000000",
        _ => "#000000"
    };

    // ── helpers ────────────────────────────────────────────────────────────────────────────────────

    private static bool HasScript(XElement root) =>
        root.DescendantsAndSelf().Any(e => e.Name.LocalName == "ScriptText")
        || (Attr(root, "ScriptText") is { Length: > 0 });

    private static string UniqueName(string name, HashSet<string> seen)
    {
        if (seen.Add(name)) return name;
        for (var i = 2; ; i++)
        {
            var candidate = $"{name}_{i}";
            if (seen.Add(candidate)) return candidate;
        }
    }

    private static double ToPx(string? value) => ToDouble(value);

    private static double ToDouble(string? value) =>
        double.TryParse((value ?? "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;

    private static string? Attr(XElement? el, string name) => el?.Attribute(name)?.Value;

    private static XElement? Descendant(XElement? el, string name) =>
        el?.DescendantsAndSelf().FirstOrDefault(e => e.Name.LocalName == name);

    private static MigrationDiagnostic Info(string id, string message) =>
        new() { Id = id, Message = message, Severity = MigrationDiagnosticSeverity.Info };

    private static MigrationDiagnostic Warn(string id, string message) =>
        new() { Id = id, Message = message, Severity = MigrationDiagnosticSeverity.Warning };

    // ── Neutral intermediate model ─────────────────────────────────────────────────────────────────

    private sealed class RawReport
    {
        public string Name = "FastReport Report";
        public double PageWidthPt = A4WidthMm * MmToPt, PageHeightPt = A4HeightMm * MmToPt;
        public double MarginLeftPt, MarginTopPt, MarginBottomPt;
        public bool HasScript;
        public List<RawBand> Bands = [];
        public List<RawElement> Elements = [];
    }

    private sealed class RawBand
    {
        public required string Name;
        public required string Type;
        public double TopPt;     // absolute design position (points)
        public double HeightPt;
    }

    private sealed class RawElement
    {
        public required string Name;
        public required string Type;
        public string? Band;
        public double X, Y, W, H;   // points; X/Y are object-within-band
        public string? Text;
        public string? DataColumn;
        public string? TextExpression;
        public string? FontFamily;
        public double? FontSize;
        public bool Bold, Italic, Underline, Strikeout;
        public string ForeColor = "#000000";
        public string? BackColor;
        public string? BorderColor;
        public double? BorderWidth;
        public string? BorderLines;
        public string TextAlign = "left";
        public string? ShapeKind;
        public string? ImageDataUrl;
        public string? Symbology;
        public bool Checked;
        public List<List<string>>? TableCells;
        public List<CellStyleDto>? CellStyles;
        public double[]? ColumnWidthsPt;
        public string[]? ColumnAlignments;
        public bool TableHasHeader;
    }
}
