using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Canvas.Core.Contracts;
using Canvas.Migration.Abstractions;

namespace Canvas.Migration.Rdl;

public sealed class RdlConvertResult
{
    public required DesignExportDto Design { get; init; }
    public required IReadOnlyList<MigrationDiagnostic> Diagnostics { get; init; }
}

/// <summary>
/// Converts an RDL report — the XML standard emitted by Microsoft SSRS/RDLC and by the Syncfusion /
/// Bold Reports designer (and, later, ActiveReports/DsReport) — into a Canvas <see cref="DesignExportDto"/>
/// the visual designer can open. RDL has no bands: body items are absolutely positioned inside
/// <c>&lt;Body&gt;</c>, while <c>&lt;PageHeader&gt;</c>/<c>&lt;PageFooter&gt;</c> items become repeating
/// shared elements. Lengths are CSS strings ("8.5in", "21cm", "10pt") parsed straight to points.
/// Elements are matched by XML <see cref="XName.LocalName"/> so every RDL schema year (2005/2008/2010/2016)
/// is handled regardless of namespace.
/// </summary>
public sealed class RdlToDesignConverter
{
    private const double A4WidthPt = 595;
    private const double A4HeightPt = 842;
    private const int NestingGuard = 32;

    // Embedded image lookup (Name → data: URL), populated per Convert call.
    private readonly Dictionary<string, string> _embedded = new(StringComparer.Ordinal);

    /// <summary>Detects whether the source is an RDL report (root <c>&lt;Report&gt;</c> in an RDL namespace).</summary>
    public static bool LooksLikeRdl(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;
        var trimmed = source.TrimStart();
        if (!trimmed.StartsWith('<')) return false;  // reject C#/JSON/prose; admit <?xml, <!-- comment -->, <Report
        try
        {
            var root = XDocument.Parse(source).Root;
            return root is not null
                && root.Name.LocalName == "Report"
                && root.Name.NamespaceName.Contains("reportdefinition", StringComparison.OrdinalIgnoreCase);
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
    }

    /// <summary>Validates the input and converts it (symmetry with the DevExpress converter's entry point).</summary>
    public RdlConvertResult ConvertAuto(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source cannot be null or empty.", nameof(source));
        return Convert(source);
    }

    // ── RDL XML → RawReport ────────────────────────────────────────────────────────────────────────

    public RdlConvertResult Convert(string rdlXml)
    {
        if (string.IsNullOrWhiteSpace(rdlXml))
            throw new ArgumentException("Source cannot be null or empty.", nameof(rdlXml));

        XElement root;
        try { root = XDocument.Parse(rdlXml).Root ?? throw new ArgumentException("Empty RDL document."); }
        catch (System.Xml.XmlException ex) { throw new ArgumentException($"Invalid RDL XML: {ex.Message}", nameof(rdlXml)); }

        if (root.Name.LocalName != "Report")
            throw new ArgumentException("Not an RDL report — expected a root <Report> element.", nameof(rdlXml));

        _embedded.Clear();
        CollectEmbeddedImages(root);

        var report = new RawReport
        {
            Name = Attr(root, "Name") ?? Child(root, "Description")?.Value ?? "RDL Report",
            HasCode = Descendant(root, "Code") is not null || Descendant(root, "CodeModules") is not null
        };
        ResolvePage(root, report);

        var body = Descendant(root, "Body");
        if (body is not null)
        {
            report.BodyHeightPt = LengthToPt(Child(body, "Height")?.Value);
            ParseReportItems(Child(body, "ReportItems"), RawRegion.Body, 0, 0, report, 0);
        }

        var header = Descendant(root, "PageHeader");
        if (header is not null)
        {
            report.PageHeaderHeightPt = LengthToPt(Child(header, "Height")?.Value);
            ParseReportItems(Child(header, "ReportItems"), RawRegion.PageHeader, 0, 0, report, 0);
        }

        var footer = Descendant(root, "PageFooter");
        if (footer is not null)
        {
            report.PageFooterHeightPt = LengthToPt(Child(footer, "Height")?.Value);
            ParseReportItems(Child(footer, "ReportItems"), RawRegion.PageFooter, 0, 0, report, 0);
        }

        return BuildDesign(report);
    }

    private void CollectEmbeddedImages(XElement root)
    {
        var container = Descendant(root, "EmbeddedImages");
        foreach (var img in Children(container, "EmbeddedImage"))
        {
            var name = Attr(img, "Name");
            if (string.IsNullOrEmpty(name)) continue;
            var data = NormalizeBase64(Child(img, "ImageData")?.Value);
            if (data is null) continue;
            var mime = Child(img, "MIMEType")?.Value is { Length: > 0 } m ? m : "image/png";
            _embedded[name] = $"data:{mime};base64,{data}";
        }
    }

    private void ResolvePage(XElement root, RawReport report)
    {
        // 2016 nests page geometry under <Page>; 2008/2010 puts it directly under <Report>. A first-match
        // descendant lookup covers both (these names are top-level only in RDL).
        var w = LengthToPt(Descendant(root, "PageWidth")?.Value);
        var h = LengthToPt(Descendant(root, "PageHeight")?.Value);
        report.PageWidthPt = w > 0 ? w : A4WidthPt;
        report.PageHeightPt = h > 0 ? h : A4HeightPt;
        report.MarginLeftPt = LengthToPt(Descendant(root, "LeftMargin")?.Value);
        report.MarginTopPt = LengthToPt(Descendant(root, "TopMargin")?.Value);
        report.MarginRightPt = LengthToPt(Descendant(root, "RightMargin")?.Value);
        report.MarginBottomPt = LengthToPt(Descendant(root, "BottomMargin")?.Value);
    }

    // Add a level of report items, recursing into any Rectangle's nested <ReportItems> (flattening its
    // children to absolute positions by accumulating the rectangle offset, like an XRPanel).
    private void ParseReportItems(XElement? itemsEl, RawRegion region, double originX, double originY, RawReport report, int depth)
    {
        if (itemsEl is null) return;
        if (depth > NestingGuard) { report.DeepNesting = true; return; }

        foreach (var item in itemsEl.Elements())
        {
            var type = item.Name.LocalName;
            var left = LengthToPt(Child(item, "Left")?.Value) + originX;
            var top = LengthToPt(Child(item, "Top")?.Value) + originY;

            var raw = new RawElement
            {
                Name = Attr(item, "Name") ?? type,
                Type = type,
                Region = region,
                X = left,
                Y = top,
                W = LengthToPt(Child(item, "Width")?.Value),
                H = LengthToPt(Child(item, "Height")?.Value)
            };

            switch (type)
            {
                case "Textbox":
                    ParseTextbox(item, raw);
                    break;
                case "Line":
                case "Rectangle":
                    ApplyStyle(raw, Child(item, "Style"));
                    break;
                case "Image":
                    ParseImage(item, raw);
                    break;
                case "Tablix":
                case "Table":
                    ParseTablix(item, raw);
                    break;
                case "CustomReportItem":
                    // RDL-standard custom items (ActiveReports/DsReport barcodes, SSRS charts/gauges/maps).
                    raw.CustomType = Child(item, "Type")?.Value;
                    raw.CustomProps = ParseCustomProperties(item);
                    break;
            }

            report.Elements.Add(raw);

            if (type == "Rectangle")
                ParseReportItems(Child(item, "ReportItems"), region, left, top, report, depth + 1);
        }
    }

    private void ParseTextbox(XElement el, RawElement raw)
    {
        // Textbox-level style (background/align), then paragraph style (align), then run style (font/colour):
        // the most specific wins because ApplyStyle only sets the properties present in each <Style>.
        ApplyStyle(raw, Child(el, "Style"));

        var paragraphs = Child(el, "Paragraphs");
        if (paragraphs is not null)
        {
            var firstParagraph = Children(paragraphs, "Paragraph").FirstOrDefault();
            ApplyStyle(raw, firstParagraph is null ? null : Child(firstParagraph, "Style"));
            var firstRun = paragraphs.Descendants().FirstOrDefault(e => e.Name.LocalName == "TextRun");
            ApplyStyle(raw, firstRun is null ? null : Child(firstRun, "Style"));
        }

        ClassifyValue(raw, ExtractTextboxValue(el));
    }

    // The display value of a Textbox: 2016 concatenates <TextRun><Value>s; 2008 uses a direct <Value>.
    private static string? ExtractTextboxValue(XElement? textbox)
    {
        if (textbox is null) return null;
        var paragraphs = textbox.Elements().FirstOrDefault(e => e.Name.LocalName == "Paragraphs");
        if (paragraphs is not null)
        {
            var runValues = paragraphs.Descendants()
                .Where(e => e.Name.LocalName == "TextRun")
                .Select(r => r.Elements().FirstOrDefault(e => e.Name.LocalName == "Value")?.Value ?? "");
            var joined = string.Concat(runValues);
            return joined.Length > 0 ? joined : null;
        }
        return Child(textbox, "Value")?.Value;
    }

    private void ParseImage(XElement el, RawElement raw)
    {
        var source = Child(el, "Source")?.Value;
        var value = Child(el, "Value")?.Value;
        if (string.Equals(source, "Embedded", StringComparison.OrdinalIgnoreCase)
            && value is not null && _embedded.TryGetValue(value, out var dataUrl))
        {
            raw.ImageDataUrl = dataUrl;
        }
        // External/Database/unresolved embedded → null → placeholder warning in BuildDesign.
    }

    private void ApplyStyle(RawElement raw, XElement? style)
    {
        if (style is null) return;

        if (Child(style, "FontFamily")?.Value is { Length: > 0 } family) raw.FontFamily = family;
        if (LengthToPt(Child(style, "FontSize")?.Value) is var fs and > 0) raw.FontSize = fs;
        if (Child(style, "FontWeight")?.Value is { } fw && IsBoldWeight(fw)) raw.Bold = true;
        if (Child(style, "FontStyle")?.Value is { } fst && fst.Contains("Italic", StringComparison.OrdinalIgnoreCase)) raw.Italic = true;
        if (Child(style, "Color")?.Value is { Length: > 0 } color) raw.ForeColor = NormalizeColor(color);
        if (Child(style, "BackgroundColor")?.Value is { Length: > 0 } bg
            && !bg.Equals("Transparent", StringComparison.OrdinalIgnoreCase)) raw.BackColor = NormalizeColor(bg);
        if (Child(style, "TextAlign")?.Value is { Length: > 0 } align) raw.TextAlign = ParseAlignment(align);
        if (Child(style, "TextDecoration")?.Value is { } deco)
        {
            if (deco.Contains("Underline", StringComparison.OrdinalIgnoreCase)) raw.Underline = true;
            if (deco.Contains("LineThrough", StringComparison.OrdinalIgnoreCase)) raw.Strikeout = true;
        }

        // Border drives Line/Rectangle stroke. Applied last so a Line's border colour wins over any <Color>.
        var border = Child(style, "Border");
        if (border is not null)
        {
            if (Child(border, "Color")?.Value is { Length: > 0 } bc) raw.ForeColor = NormalizeColor(bc);
            if (LengthToPt(Child(border, "Width")?.Value) is var bw and > 0) raw.LineWidth = bw;
            if (Child(border, "Style")?.Value is { Length: > 0 } bs) raw.LineStyle = bs;
        }
    }

    private void ParseTablix(XElement el, RawElement raw)
    {
        var grid = new List<List<string>>();
        var tablixBody = Child(el, "TablixBody");
        XElement? headerRow;

        if (tablixBody is not null)
        {
            // 2016 Tablix
            raw.ColumnWidthsPt = Children(Child(tablixBody, "TablixColumns"), "TablixColumn")
                .Select(c => LengthToPt(Child(c, "Width")?.Value)).Where(w => w > 0).ToArray() is { Length: > 0 } cw ? cw : null;

            var rows = Children(Child(tablixBody, "TablixRows"), "TablixRow").ToList();
            headerRow = rows.FirstOrDefault();
            foreach (var row in rows)
                grid.Add(Children(Child(row, "TablixCells"), "TablixCell")
                    .Select(cell => CellDisplay(ExtractTextboxValue(TablixCellTextbox(cell)))).ToList());
        }
        else
        {
            // 2008 Table
            raw.ColumnWidthsPt = Children(Child(el, "TableColumns"), "TableColumn")
                .Select(c => LengthToPt(Child(c, "Width")?.Value)).Where(w => w > 0).ToArray() is { Length: > 0 } cw ? cw : null;

            var rows = TableRows(Child(el, "Header")).Concat(TableRows(Child(el, "Details"))).ToList();
            headerRow = rows.FirstOrDefault();
            foreach (var row in rows)
                grid.Add(Children(Child(row, "TableCells"), "TableCell")
                    .Select(cell => CellDisplay(ExtractTextboxValue(TableCellTextbox(cell)))).ToList());
        }

        raw.TableCells = grid.Count > 0 ? grid : null;
        raw.ColumnAlignments = headerRow is null ? null : HeaderAlignments(headerRow, tablixBody is not null);
        raw.TablixGroups = ParseTablixGroups(el);
        raw.TablixSorts = ParseTablixSorts(el);
        raw.TablixKeepWithGroups = ParseTablixKeepWithGroups(el);
    }

    private static List<RdlTablixGroup> ParseTablixGroups(XElement tablix)
    {
        var groups = new List<RdlTablixGroup>();
        foreach (var group in tablix.Descendants().Where(e => e.Name.LocalName == "Group"))
        {
            var expressions = Children(Child(group, "GroupExpressions"), "GroupExpression")
                .Select(e => e.Value.Trim())
                .Where(v => v.Length > 0)
                .ToArray();
            groups.Add(new RdlTablixGroup(Attr(group, "Name") ?? "", expressions));
        }
        return groups;
    }

    private static List<string> ParseTablixSorts(XElement tablix) =>
        tablix.Descendants()
            .Where(e => e.Name.LocalName == "SortExpression")
            .Select(e => Child(e, "Value")?.Value.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Cast<string>()
            .ToList();

    private static List<string> ParseTablixKeepWithGroups(XElement tablix) =>
        tablix.Descendants()
            .Where(e => e.Name.LocalName == "KeepWithGroup")
            .Select(e => e.Value.Trim())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static Dictionary<string, string> ParseCustomProperties(XElement item)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cp in Children(Child(item, "CustomProperties"), "CustomProperty"))
        {
            var name = Child(cp, "Name")?.Value;
            if (!string.IsNullOrEmpty(name)) props[name] = Child(cp, "Value")?.Value ?? "";
        }
        return props;
    }

    private static IEnumerable<XElement> TableRows(XElement? section) =>
        section is null ? Enumerable.Empty<XElement>() : Children(Child(section, "TableRows"), "TableRow");

    private static XElement? TablixCellTextbox(XElement cell) =>
        Child(Child(cell, "CellContents"), "Textbox");

    private static XElement? TableCellTextbox(XElement cell) =>
        Children(Child(cell, "ReportItems"), "Textbox").FirstOrDefault();

    private static string[] HeaderAlignments(XElement headerRow, bool tablix)
    {
        var cells = tablix
            ? Children(Child(headerRow, "TablixCells"), "TablixCell").Select(TablixCellTextbox)
            : Children(Child(headerRow, "TableCells"), "TableCell").Select(TableCellTextbox);
        return cells.Select(tb => ParseAlignment(CellAlign(tb))).ToArray();
    }

    private static string? CellAlign(XElement? textbox)
    {
        if (textbox is null) return null;
        var paragraphStyle = Child(Children(Child(textbox, "Paragraphs"), "Paragraph").FirstOrDefault(), "Style");
        return Child(paragraphStyle, "TextAlign")?.Value ?? Child(Child(textbox, "Style"), "TextAlign")?.Value;
    }

    // ── Shared build: RawReport → DesignExportDto ──────────────────────────────────────────────────

    private static RdlConvertResult BuildDesign(RawReport report)
    {
        var diagnostics = new List<MigrationDiagnostic>();
        var elements = new List<ElementDto>();
        var sharedElements = new List<ElementDto>();
        var mapped = 0;

        foreach (var raw in report.Elements)
        {
            var x = report.MarginLeftPt + raw.X;
            var y = raw.Region switch
            {
                RawRegion.PageHeader => report.MarginTopPt + raw.Y,
                RawRegion.PageFooter => report.PageHeightPt - report.MarginBottomPt - report.PageFooterHeightPt + raw.Y,
                _ => report.MarginTopPt + report.PageHeaderHeightPt + raw.Y
            };

            var element = raw.Type is "Tablix" or "Table"
                ? BuildTable(raw, x, y, diagnostics)
                : MapControl(raw, x, y, diagnostics);
            if (element is null) continue;

            diagnostics.Add(Info("CANMIGRDL002", $"'{raw.Name}' ({raw.Type}) → Canvas {element.Type}."));

            if (raw.TextExpression is { } expr)
                ApplyBinding(element, expr, diagnostics);

            (raw.Region is RawRegion.PageHeader or RawRegion.PageFooter ? sharedElements : elements).Add(element);
            mapped++;
        }

        elements.Sort((p, q) => p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.X.CompareTo(q.X));
        sharedElements.Sort((p, q) => p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.X.CompareTo(q.X));

        if (report.HasCode)
            diagnostics.Add(Warn("CANMIGRDL011",
                "Report contains custom <Code> / expressions — Canvas has no scripting; migrate that logic manually."));
        if (report.DeepNesting)
            diagnostics.Add(Warn("CANMIGRDL013",
                "Container nesting exceeded the flatten depth limit — some deeply nested items were skipped."));

        diagnostics.Insert(0, Info("CANMIGRDL001",
            $"RDL report '{report.Name}' detected — {mapped} item(s) mapped."));

        var design = new DesignExportDto
        {
            Id = $"rdl-report-{Guid.NewGuid():N}",
            Name = report.Name,
            Category = "imported",
            Description = "Imported from an RDL (SSRS/RDLC) report.",
            PageSettings = new PageSettingsDto { Width = report.PageWidthPt, Height = report.PageHeightPt, Unit = "pt" },
            Pages = [new PageDto { Id = "page-1", Elements = elements }],
            SharedElements = sharedElements
        };

        return new RdlConvertResult { Design = design, Diagnostics = diagnostics };
    }

    private static ElementDto? MapControl(RawElement raw, double x, double y, List<MigrationDiagnostic> diagnostics)
    {
        var element = new ElementDto { Id = $"rdl-{raw.Name}", Name = raw.Name, X = x, Y = y, Width = raw.W, Height = raw.H };

        switch (raw.Type)
        {
            case "Textbox":
                element.Type = "text";
                element.Content = raw.Text ?? "";
                element.Style = BuildTextStyle(raw);
                return element;

            case "Line":
                element.Type = "line";
                element.Style = new Dictionary<string, object> { ["color"] = raw.ForeColor };
                if (raw.LineWidth is { } lineW) element.Style["strokeWidth"] = lineW;
                if (DashStyleFromName(raw.LineStyle) is { } dash) element.Style["dashStyle"] = dash;
                return element;

            case "Rectangle":
                element.Type = "rect";
                element.Style = new Dictionary<string, object> { ["borderColor"] = raw.ForeColor };
                if (raw.BackColor is { } bg) element.Style["backgroundColor"] = bg;
                if (raw.LineWidth is { } borderW) element.Style["borderWidth"] = borderW;
                return element;

            case "Image":
                element.Type = "image";
                element.FitMode = "contain";
                if (raw.ImageDataUrl is { } dataUrl)
                    element.Content = dataUrl;
                else
                    diagnostics.Add(Warn("CANMIGRDL012",
                        $"'{raw.Name}' image isn't embeddable from source — inserted an empty image placeholder."));
                return element;

            case "CustomReportItem":
                return MapCustomReportItem(raw, element, diagnostics);

            case "Subreport":
                diagnostics.Add(Warn("CANMIGRDL011",
                    $"'{raw.Name}' is a sub-report — requires manual migration; inserted a placeholder."));
                return Placeholder(element, $"[Sub-report: {raw.Name} — migrate manually]");

            default:
                diagnostics.Add(Warn("CANMIGRDL011", $"'{raw.Name}' is a {raw.Type} — not supported by Canvas yet; inserted a placeholder."));
                return Placeholder(element, $"[{raw.Type}: migrate manually]");
        }
    }

    // Keeps an unsupported item visible at its original position/size so the layout isn't silently
    // holed — a muted, captioned block the user can replace in the designer.
    private static ElementDto Placeholder(ElementDto element, string label)
    {
        element.Type = "text";
        element.Content = label;
        element.Binding = null;
        // All keys below are consumed by the PDF text renderer (background fill, dashed border, italic caption).
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

    // RDL <CustomReportItem>: ActiveReports/DsReport serialize barcodes this way (Type + CustomProperties);
    // SSRS uses it for Chart/Gauge/Map/Sparkline. Map barcodes to Canvas barcode/qrcode; warn on the rest.
    private static ElementDto? MapCustomReportItem(RawElement raw, ElementDto element, List<MigrationDiagnostic> diagnostics)
    {
        var props = raw.CustomProps ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var customType = raw.CustomType ?? "";
        var symbology = props.GetValueOrDefault("Symbology") ?? props.GetValueOrDefault("SymbologyType");
        var isBarcode = customType.Contains("Barcode", StringComparison.OrdinalIgnoreCase) || symbology is not null;
        if (!isBarcode)
        {
            var what = customType.Length > 0 ? customType : "Custom item";
            diagnostics.Add(Warn("CANMIGRDL011",
                $"'{raw.Name}' is a custom report item ({what}) — not supported by Canvas yet; inserted a placeholder."));
            return Placeholder(element, $"[{what}: migrate manually]");
        }

        var value = CellDisplay(props.GetValueOrDefault("Value") ?? props.GetValueOrDefault("Text") ?? props.GetValueOrDefault("Code") ?? raw.Text ?? "");
        if (symbology is not null && symbology.Contains("QR", StringComparison.OrdinalIgnoreCase))
        {
            element.Type = "qrcode";
            element.QrValue = value;
        }
        else
        {
            element.Type = "barcode";
            element.BarcodeValue = value;
            element.BarcodeType = BarcodeTypeFromSymbology(symbology);
        }
        return element;
    }

    private static string BarcodeTypeFromSymbology(string? symbology)
    {
        var s = (symbology ?? "").Replace("-", "").Replace("_", "");
        if (s.Contains("Code39", StringComparison.OrdinalIgnoreCase)) return "code39";
        if (s.Contains("EAN13", StringComparison.OrdinalIgnoreCase)) return "ean13";
        if (s.Contains("EAN8", StringComparison.OrdinalIgnoreCase)) return "ean8";
        if (s.Contains("UPCA", StringComparison.OrdinalIgnoreCase)) return "upca";
        if (s.Contains("PDF417", StringComparison.OrdinalIgnoreCase)) return "pdf417";
        return "code128";  // Code128 and anything unrecognised
    }

    private static ElementDto? BuildTable(RawElement raw, double x, double y, List<MigrationDiagnostic> diagnostics)
    {
        if (raw.TableCells is not { Count: > 0 } grid)
        {
            diagnostics.Add(Warn("CANMIGRDL011", $"'{raw.Name}' {raw.Type} has no parseable rows — skipped."));
            return null;
        }

        var columns = grid.Max(r => r.Count);
        if (columns == 0)
        {
            diagnostics.Add(Warn("CANMIGRDL011", $"'{raw.Name}' {raw.Type} has no parseable cells — skipped."));
            return null;
        }

        var cellData = grid
            .Select(r => r.Count == columns ? r.ToArray() : r.Concat(Enumerable.Repeat("", columns - r.Count)).ToArray())
            .ToArray();

        var element = new ElementDto
        {
            Id = $"rdl-{raw.Name}",
            Name = raw.Name,
            Type = "table",
            X = x,
            Y = y,
            Width = raw.W,
            Height = raw.H,
            CellData = cellData,
            ColumnWidths = FitWidths(raw.ColumnWidthsPt, columns, raw.W),
            ColumnAlignments = FitColumns(raw.ColumnAlignments, columns),
            HeaderRow = true
        };
        ApplyTablixMetadata(element, raw, diagnostics);
        return element;
    }

    private static void ApplyTablixMetadata(ElementDto element, RawElement raw, List<MigrationDiagnostic> diagnostics)
    {
        if (raw.TablixGroups is not { Count: > 0 }
            && raw.TablixSorts is not { Count: > 0 }
            && raw.TablixKeepWithGroups is not { Count: > 0 })
            return;

        element.Style ??= [];
        if (raw.TablixGroups is { Count: > 0 })
        {
            element.Style["rdlTablixGroups"] = raw.TablixGroups
                .Select(g => new Dictionary<string, object>
                {
                    ["name"] = g.Name,
                    ["expressions"] = g.Expressions
                })
                .ToArray();
        }
        if (raw.TablixSorts is { Count: > 0 })
            element.Style["rdlTablixSorts"] = raw.TablixSorts.ToArray();
        if (raw.TablixKeepWithGroups is { Count: > 0 })
            element.Style["rdlTablixKeepWithGroup"] = raw.TablixKeepWithGroups.ToArray();

        diagnostics.Add(Warn("CANMIGRDL014",
            $"'{raw.Name}' Tablix grouping/sorting metadata was preserved; Canvas repeat/group semantics still require review."));
    }

    private static double[] FitWidths(double[]? widths, int columns, double totalWidth)
    {
        if (widths is { Length: > 0 } && widths.Length == columns) return widths;
        return Enumerable.Repeat(totalWidth / columns, columns).ToArray();
    }

    private static string[]? FitColumns(string[]? aligns, int columns)
    {
        if (aligns is not { Length: > 0 }) return null;
        if (aligns.Length == columns) return aligns;
        return Enumerable.Range(0, columns).Select(i => i < aligns.Length ? aligns[i] : "left").ToArray();
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

    // A Textbox value as it should appear in static text / table cells: literal, {{field}}, or the raw expression.
    private static string CellDisplay(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (!value.TrimStart().StartsWith('=')) return value;
        var field = SingleFieldMatch(value);
        return field is not null ? $"{{{{{field}}}}}" : value;
    }

    private static void ClassifyValue(RawElement raw, string? value)
    {
        if (string.IsNullOrEmpty(value)) { raw.Text = ""; return; }
        if (!value.TrimStart().StartsWith('=')) { raw.Text = value; return; }
        raw.TextExpression = value;
    }

    private static void ApplyBinding(ElementDto element, string expression, List<MigrationDiagnostic> diagnostics)
    {
        var field = SingleFieldMatch(expression);
        if (field is not null)
        {
            element.Binding = field;
            element.Content = $"{{{{{field}}}}}";
            diagnostics.Add(Info("CANMIGRDL010", $"'{element.Name}' value bound to field {field} → Canvas binding '{field}'."));
        }
        else
        {
            element.Expression = expression;
            if (string.IsNullOrEmpty(element.Content)) element.Content = expression;
            diagnostics.Add(Warn("CANMIGRDL010", $"'{element.Name}' value expression '{expression}' mapped to Canvas expression — review the syntax."));
        }
    }

    private static string? SingleFieldMatch(string expression)
    {
        var m = Regex.Match(expression, @"^\s*=\s*Fields!(\w+)\.Value\s*$");
        return m.Success ? m.Groups[1].Value : null;
    }

    // ── Length & style helpers ─────────────────────────────────────────────────────────────────────

    // CSS length string → points (1pt = 1/72in). RDL sizes carry an explicit unit; unit-less ⇒ points.
    private static double LengthToPt(string? length)
    {
        if (string.IsNullOrWhiteSpace(length)) return 0;
        var m = Regex.Match(length.Trim(), @"^([+-]?[\d.]+)\s*([a-z%]*)$", RegexOptions.IgnoreCase);
        if (!m.Success || !double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
            return 0;
        return m.Groups[2].Value.ToLowerInvariant() switch
        {
            "in" => n * 72.0,
            "cm" => n / 2.54 * 72.0,
            "mm" => n / 25.4 * 72.0,
            "pt" or "" => n,
            "px" => n * 72.0 / 96.0,
            "pc" => n * 12.0,
            _ => 0  // "%" and unknown units aren't absolute geometry
        };
    }

    private static bool IsBoldWeight(string weight)
    {
        if (weight.Contains("Bold", StringComparison.OrdinalIgnoreCase)) return true;
        return int.TryParse(weight, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= 700;
    }

    private static string ParseAlignment(string? text)
    {
        text ??= "";
        if (text.Contains("Center", StringComparison.OrdinalIgnoreCase)) return "center";
        if (text.Contains("Right", StringComparison.OrdinalIgnoreCase)) return "right";
        if (text.Contains("Justify", StringComparison.OrdinalIgnoreCase)) return "justify";
        return "left";  // General/Left/empty
    }

    private static string? DashStyleFromName(string? lineStyle)
    {
        if (string.IsNullOrEmpty(lineStyle) || lineStyle.Equals("Solid", StringComparison.OrdinalIgnoreCase)) return null;
        if (lineStyle.Contains("Dash", StringComparison.OrdinalIgnoreCase)) return "dashed";
        if (lineStyle.Contains("Dot", StringComparison.OrdinalIgnoreCase)) return "dotted";
        return null;
    }

    private static string NormalizeColor(string value)
    {
        var v = value.Trim();
        if (v.StartsWith('#'))
        {
            if (v.Length == 4)  // #RGB → #RRGGBB
                return $"#{v[1]}{v[1]}{v[2]}{v[2]}{v[3]}{v[3]}".ToUpperInvariant();
            return v.ToUpperInvariant();
        }
        return NamedColor(v);
    }

    private static string? NormalizeBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var stripped = Regex.Replace(value, @"\s+", "");
        return stripped.Length >= 16 && stripped.Length % 4 == 0 && Regex.IsMatch(stripped, @"^[A-Za-z0-9+/]+={0,2}$")
            ? stripped : null;
    }

    private static string NamedColor(string name) => name.Trim().ToLowerInvariant() switch
    {
        "white" => "#FFFFFF",
        "black" => "#000000",
        "red" => "#FF0000",
        "green" => "#008000",
        "blue" => "#0000FF",
        "gray" or "grey" => "#808080",
        "darkgray" or "darkgrey" => "#A9A9A9",
        "lightgray" or "lightgrey" => "#D3D3D3",
        "silver" => "#C0C0C0",
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
        "transparent" => "#00000000",
        _ => "#000000"
    };

    // ── XML helpers (namespace-agnostic, by LocalName) ─────────────────────────────────────────────

    private static string? Attr(XElement el, string name) => el.Attribute(name)?.Value;

    private static XElement? Child(XElement? el, string name) =>
        el?.Elements().FirstOrDefault(e => e.Name.LocalName == name);

    private static IEnumerable<XElement> Children(XElement? el, string name) =>
        el is null ? Enumerable.Empty<XElement>() : el.Elements().Where(e => e.Name.LocalName == name);

    private static XElement? Descendant(XElement? el, string name) =>
        el?.DescendantsAndSelf().FirstOrDefault(e => e.Name.LocalName == name);

    private static MigrationDiagnostic Info(string id, string message) =>
        new() { Id = id, Message = message, Severity = MigrationDiagnosticSeverity.Info };

    private static MigrationDiagnostic Warn(string id, string message) =>
        new() { Id = id, Message = message, Severity = MigrationDiagnosticSeverity.Warning };

    // ── Neutral intermediate model ─────────────────────────────────────────────────────────────────

    private enum RawRegion { Body, PageHeader, PageFooter }

    private sealed class RawReport
    {
        public string Name = "RDL Report";
        public double PageWidthPt = A4WidthPt, PageHeightPt = A4HeightPt;
        public double MarginLeftPt, MarginTopPt, MarginRightPt, MarginBottomPt;
        public double BodyHeightPt, PageHeaderHeightPt, PageFooterHeightPt;
        public bool HasCode;
        public bool DeepNesting;
        public List<RawElement> Elements = [];
    }

    private sealed class RawElement
    {
        public required string Name;
        public required string Type;          // RDL LocalName: Textbox/Line/Rectangle/Image/Tablix/Table/Subreport/...
        public RawRegion Region;
        public double X, Y, W, H;             // absolute within region, points
        public string? Text;
        public string? FontFamily;
        public double? FontSize;
        public bool Bold, Italic, Underline, Strikeout;
        public string ForeColor = "#000000";
        public string? BackColor;
        public string TextAlign = "left";
        public string? TextExpression;        // captured "=..." value
        public List<List<string>>? TableCells;
        public string[]? ColumnAlignments;
        public double[]? ColumnWidthsPt;
        public List<RdlTablixGroup>? TablixGroups;
        public List<string>? TablixSorts;
        public List<string>? TablixKeepWithGroups;
        public string? ImageDataUrl;
        public double? LineWidth;
        public string? LineStyle;
        public string? CustomType;                    // <CustomReportItem><Type>
        public Dictionary<string, string>? CustomProps;  // <CustomProperties> name → value
    }

    private sealed record RdlTablixGroup(string Name, string[] Expressions);
}
