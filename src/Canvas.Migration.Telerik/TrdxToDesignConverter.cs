using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Canvas.Core.Contracts;
using Canvas.Migration.Abstractions;

namespace Canvas.Migration.Telerik;

public sealed class TrdxConvertResult
{
    public required DesignExportDto Design { get; init; }
    public required IReadOnlyList<MigrationDiagnostic> Diagnostics { get; init; }
}

/// <summary>
/// Converts a Telerik Reporting <c>.trdx</c> report — namespaced XML
/// (<c>http://schemas.telerik.com/reporting/…</c>) with a <b>sectioned</b> layout — into a Canvas
/// <see cref="DesignExportDto"/>. It is a hybrid: geometry is in CSS-like Unit strings
/// (<c>"8.1in"</c>, like <c>Canvas.Migration.Rdl</c>) while sections (<c>PageHeaderSection</c>,
/// <c>DetailSection</c>, …) stack and flatten to absolute page coordinates (like
/// <c>Canvas.Migration.Rpx</c>). Named styles are resolved from the report's <c>&lt;StyleSheet&gt;</c>.
/// Elements are matched by <see cref="XName.LocalName"/> (namespace-agnostic).
/// </summary>
public sealed class TrdxToDesignConverter
{
    private const double A4WidthPt = 595, A4HeightPt = 842;

    // StyleName → its <Style> element from the report <StyleSheet>, populated per Convert call.
    private readonly Dictionary<string, XElement> _namedStyles = new(StringComparer.Ordinal);
    // Control-type name → <Style> from a TypeSelector StyleRule (applies to every control of that type).
    private readonly Dictionary<string, XElement> _typeStyles = new(StringComparer.Ordinal);

    /// <summary>Detects a Telerik <c>.trdx</c>: root <c>&lt;Report&gt;</c> in a Telerik reporting namespace.</summary>
    public static bool LooksLikeTrdx(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;
        if (!source.TrimStart().StartsWith('<')) return false;
        try
        {
            var root = XDocument.Parse(source).Root;
            return root is not null
                && root.Name.LocalName == "Report"
                && root.Name.NamespaceName.Contains("telerik.com/reporting", StringComparison.OrdinalIgnoreCase);
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
    }

    public TrdxConvertResult ConvertAuto(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source cannot be null or empty.", nameof(source));
        return Convert(source);
    }

    public TrdxConvertResult Convert(string trdxXml)
    {
        if (string.IsNullOrWhiteSpace(trdxXml))
            throw new ArgumentException("Source cannot be null or empty.", nameof(trdxXml));

        XElement root;
        try { root = XDocument.Parse(trdxXml).Root ?? throw new ArgumentException("Empty .trdx document."); }
        catch (System.Xml.XmlException ex) { throw new ArgumentException($"Invalid .trdx XML: {ex.Message}", nameof(trdxXml)); }

        if (root.Name.LocalName != "Report")
            throw new ArgumentException("Not a Telerik .trdx — expected a root <Report> element.", nameof(trdxXml));

        _namedStyles.Clear();
        _typeStyles.Clear();
        CollectNamedStyles(root);

        var report = new RawReport { Name = Attr(root, "Name") ?? "Telerik Report" };
        ResolvePage(root, report);

        // <Report><Items> holds the sections; each section has a Height, an optional <Style>, and <Items>.
        var sectionsContainer = root.Elements().FirstOrDefault(e => e.Name.LocalName == "Items");
        RawBand? lastGroupHeader = null;
        foreach (var section in sectionsContainer?.Elements() ?? Enumerable.Empty<XElement>())
        {
            if (!section.Name.LocalName.EndsWith("Section", StringComparison.Ordinal)) continue;
            var type = section.Name.LocalName;
            var name = Attr(section, "Name") ?? type;
            var band = new RawBand
            {
                Name = name,
                Type = type,
                HeightPt = LengthToPt(Attr(section, "Height"))
            };
            // GroupHeaderSection carries the grouping expression; a following GroupFooterSection pairs with it.
            if (type == "GroupHeaderSection")
            {
                band.Condition = GroupingExpression(section);
                band.GroupName = name;
                lastGroupHeader = band;
            }
            else if (type == "GroupFooterSection")
            {
                band.Condition = lastGroupHeader?.Condition;
                band.GroupName = lastGroupHeader?.GroupName ?? name;
            }
            report.Bands.Add(band);

            var items = section.Elements().FirstOrDefault(e => e.Name.LocalName == "Items");
            ParseItems(items, name, 0, 0, report, 0);
        }

        return BuildDesign(report);
    }

    private void CollectNamedStyles(XElement root)
    {
        var sheet = root.Elements().FirstOrDefault(e => e.Name.LocalName == "StyleSheet");
        foreach (var rule in Children(sheet, "StyleRule"))
        {
            var style = Child(rule, "Style");
            if (style is null) continue;
            var selectors = Child(rule, "Selectors");
            foreach (var sel in Children(selectors, "StyleSelector"))
            {
                if (Attr(sel, "StyleName") is { Length: > 0 } sn)
                    _namedStyles[sn] = style;
                // TypeSelector: a Type attribute applies the rule to every control of that type. Stored by
                // the simple type name (e.g. "Telerik.Reporting.TextBox" → "TextBox").
                else if (Attr(sel, "Type") is { Length: > 0 } type)
                    _typeStyles[SimpleTypeName(type)] = style;
            }
        }
    }

    private static string SimpleTypeName(string type)
    {
        var name = type.Split(',')[0].Trim();
        var dot = name.LastIndexOf('.');
        return dot >= 0 ? name[(dot + 1)..] : name;
    }

    private void ResolvePage(XElement root, RawReport report)
    {
        var ps = root.DescendantsAndSelf().FirstOrDefault(e => e.Name.LocalName == "PageSettings");
        var kind = Child(ps, "PaperKind")?.Value ?? Attr(ps, "PaperKind");
        var size = PaperKindSize(kind ?? "");
        report.PageWidthPt = size.W > 0 ? size.W : A4WidthPt;
        report.PageHeightPt = size.H > 0 ? size.H : A4HeightPt;

        // Margins: a <Margins>/<MarginsU> element (or attribute) with Left/Top/Right/Bottom Unit strings.
        var margins = ps?.DescendantsAndSelf().FirstOrDefault(e => e.Name.LocalName is "Margins" or "MarginsU");
        report.MarginLeftPt = LengthToPt(Attr(margins, "Left"));
        report.MarginTopPt = LengthToPt(Attr(margins, "Top"));
        report.MarginBottomPt = LengthToPt(Attr(margins, "Bottom"));

        if (string.Equals(Child(ps, "Landscape")?.Value ?? Attr(ps, "Landscape"), "true", StringComparison.OrdinalIgnoreCase))
            (report.PageWidthPt, report.PageHeightPt) = (report.PageHeightPt, report.PageWidthPt);
    }

    // Parse a section's (or Panel's) <Items>; a Panel recurses with its offset so children stay absolute.
    private void ParseItems(XElement? itemsEl, string sectionName, double originX, double originY, RawReport report, int depth)
    {
        if (itemsEl is null || depth > 32) return;
        foreach (var item in itemsEl.Elements())
        {
            var type = item.Name.LocalName;
            var left = LengthToPt(Attr(item, "Left")) + originX;
            var top = LengthToPt(Attr(item, "Top")) + originY;

            var raw = new RawElement
            {
                Name = Attr(item, "Name") ?? type,
                Type = type,
                Section = sectionName,
                X = left, Y = top,
                W = LengthToPt(Attr(item, "Width")),
                H = LengthToPt(Attr(item, "Height")),
                Value = Attr(item, "Value") ?? Child(item, "Value")?.Value
            };
            ApplyStyle(raw, _typeStyles.GetValueOrDefault(type));                             // TypeSelector base
            ApplyStyle(raw, _namedStyles.GetValueOrDefault(Attr(item, "StyleName") ?? ""));  // named override
            ApplyStyle(raw, Child(item, "Style"));                                            // inline override
            if (type == "Shape") raw.ShapeKind = ShapeKindFromName(Attr(item, "ShapeType") ?? Attr(item, "Shape"));
            if (type == "PictureBox") raw.ImageDataUrl = ExtractImageDataUrl(item);
            if (type == "Barcode") raw.Symbology = Attr(item, "Type") ?? Attr(item, "Symbology") ?? Attr(item, "Encoder");
            if (type is "Table" or "Crosstab" or "CrossTab") ParseTable(item, raw);

            report.Elements.Add(raw);

            if (type == "Panel")
                ParseItems(item.Elements().FirstOrDefault(e => e.Name.LocalName == "Items"), sectionName, left, top, report, depth + 1);
        }
    }

    // Telerik Table/CrossTab: column widths from <TableBodyColumn Width>, content items from the table's
    // own <Items>, each anchored to a cell via attached properties (e.g. Table.CellRowIndex/…). The trdx
    // cell-anchoring serialization is not verifiable against local samples, so this reads anchors
    // prefix-agnostically and falls back to sequential left-to-right fill when no anchors are present —
    // worst case the data is surfaced in order rather than lost behind a placeholder.
    private void ParseTable(XElement tableEl, RawElement raw)
    {
        var body = Child(tableEl, "Body") ?? tableEl;
        var colWidths = body.Descendants().Where(e => e.Name.LocalName == "TableBodyColumn")
            .Select(c => LengthToPt(Attr(c, "Width"))).Where(w => w > 0).ToList();
        var declaredRows = body.Descendants().Count(e => e.Name.LocalName == "TableBodyRow");
        var declaredCols = colWidths.Count;

        var itemsEl = tableEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Items");
        var content = (itemsEl?.Elements() ?? Enumerable.Empty<XElement>())
            .Where(e => e.Name.LocalName != "Items")
            .Select(item => (Text: CellDisplay(ItemValue(item)), Anchor: ReadCellAnchor(item), Item: item))
            .ToList();
        if (content.Count == 0) return;

        List<List<string>> grid;
        var cellStyles = new List<CellStyleDto>();
        if (content.Any(c => c.Anchor.HasPosition))
        {
            var rows = Math.Max(declaredRows, content.Where(c => c.Anchor.HasPosition).Max(c => c.Anchor.Row + 1));
            var cols = Math.Max(declaredCols, content.Where(c => c.Anchor.HasPosition).Max(c => c.Anchor.Col + 1));
            grid = NewGrid(rows, cols);
            foreach (var (text, anchor, item) in content)
                if (anchor.HasPosition && anchor.Row < rows && anchor.Col < cols)
                {
                    grid[anchor.Row][anchor.Col] = text;
                    if (ExtractCellStyle(item, anchor.Row, anchor.Col) is { } cs) cellStyles.Add(cs);
                }
        }
        else
        {
            var cols = Math.Max(1, declaredCols);
            var rows = (int)Math.Ceiling(content.Count / (double)cols);
            grid = NewGrid(rows, cols);
            for (var i = 0; i < content.Count; i++)
            {
                grid[i / cols][i % cols] = content[i].Text;
                if (ExtractCellStyle(content[i].Item, i / cols, i % cols) is { } cs) cellStyles.Add(cs);
            }
        }

        raw.TableCells = grid;
        raw.CellStyles = cellStyles.Count > 0 ? cellStyles : null;
        raw.TableColumnWidthsPt = colWidths.Count > 0 ? colWidths.ToArray() : null;
        raw.TableHasHeader = grid.Count > 1;
    }

    // Per-cell style from a Telerik content item, reusing the named+inline style resolution. Only
    // non-default properties are emitted so CellStyles stays sparse.
    private CellStyleDto? ExtractCellStyle(XElement item, int row, int col)
    {
        var tmp = new RawElement { Name = "", Type = "" };
        ApplyStyle(tmp, _namedStyles.GetValueOrDefault(Attr(item, "StyleName") ?? ""));
        ApplyStyle(tmp, Child(item, "Style"));

        var cs = new CellStyleDto { Row = row, Col = col };
        var any = false;
        if (tmp.BackColor is { } bg)                 { cs.BackgroundColor = bg; any = true; }
        if (tmp.ForeColor != "#000000")              { cs.Color = tmp.ForeColor; any = true; }
        if (tmp.TextAlign != "left")                 { cs.TextAlign = tmp.TextAlign; any = true; }
        if (tmp.BorderColor is not null || tmp.BorderWidth is not null)
        { cs.BorderColor = tmp.BorderColor; cs.BorderWidth = tmp.BorderWidth; any = true; }
        if (tmp.FontFamily is { } ff)                { cs.FontFamily = ff; any = true; }
        if (tmp.FontSize is { } fs)                  { cs.FontSize = fs; any = true; }
        if (tmp.Bold)                                { cs.Bold = true; any = true; }
        if (tmp.Italic)                              { cs.Italic = true; any = true; }
        return any ? cs : null;
    }

    private static List<List<string>> NewGrid(int rows, int cols) =>
        Enumerable.Range(0, rows).Select(_ => Enumerable.Repeat("", cols).ToList()).ToList();

    private string? ItemValue(XElement item) => Attr(item, "Value") ?? Child(item, "Value")?.Value;

    // A cell value: a single =Fields.X expression becomes a Canvas binding token; otherwise literal text.
    private static string CellDisplay(string? value)
    {
        value = (value ?? "").Trim();
        if (value.Length == 0) return "";
        var m = Regex.Match(value, @"^=\s*Fields\.(\w+)(?:\.Value)?$");
        return m.Success ? $"{{{{{m.Groups[1].Value}}}}}" : value;
    }

    // Read attached cell-position properties regardless of owner prefix (Table.* / Crosstab.* / bare),
    // whether serialized as attributes or child elements.
    private static (int Row, int Col, bool HasPosition) ReadCellAnchor(XElement item)
    {
        int row = 0, col = 0; var has = false;
        void Consume(string name, string? value)
        {
            if (value is null) return;
            if (name.EndsWith("CellRowIndex", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var r)) { row = r; has = true; }
            else if (name.EndsWith("CellColumnIndex", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var c)) { col = c; has = true; }
        }
        foreach (var a in item.Attributes()) Consume(a.Name.LocalName, a.Value);
        foreach (var e in item.Elements()) Consume(e.Name.LocalName, e.Value);
        return (row, col, has);
    }

    // The grouping expression on a GroupHeaderSection: <Groupings><Grouping Expression="=Fields.X"/> or
    // a nested <Expression> element.
    private static string? GroupingExpression(XElement section)
    {
        var grouping = section.Descendants().FirstOrDefault(e => e.Name.LocalName == "Grouping");
        if (grouping is null) return null;
        return Attr(grouping, "Expression") ?? Child(grouping, "Expression")?.Value;
    }

    // Group sections repeat per group key: attach Canvas RepeatDto + group metadata so the section's items
    // can be wired as a repeating template (mirrors the RDL/Jasper/DevExpress/FastReport group mapping).
    private static void ApplyGroupRepeatMetadata(ElementDto element, RawBand band, List<MigrationDiagnostic> diagnostics)
    {
        if (band.Type is not ("GroupHeaderSection" or "GroupFooterSection")) return;

        var role = band.Type == "GroupFooterSection" ? "footer" : "header";
        var dataPath = GroupDataPath(band);
        var group = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = band.GroupName ?? band.Name,
            ["role"] = role,
            ["band"] = band.Name,
            ["dataPath"] = dataPath,
        };
        if (!string.IsNullOrWhiteSpace(band.Condition)) group["condition"] = band.Condition!;

        element.Style ??= [];
        element.Style["trdxGroup"] = group;
        element.Style["groupField"] = dataPath;   // generic key the planner reads to inject $group
        element.Repeat = new RepeatDto { DataPath = dataPath, TemplateId = element.Id };
        diagnostics.Add(Warn("CANMIGTRDX014",
            $"'{element.Name}' is in {band.Type} '{band.GroupName ?? band.Name}' — mapped to Canvas repeat metadata; group runtime semantics need review."));
    }

    // Grouping expression like "=Fields.Region" → "Region"; otherwise the group/section name.
    private static string GroupDataPath(RawBand band)
    {
        if (!string.IsNullOrWhiteSpace(band.Condition))
        {
            var m = Regex.Match(band.Condition!, @"Fields\.(\w+)");
            if (m.Success) return SafeDataPath(m.Groups[1].Value);
        }
        return SafeDataPath(band.GroupName ?? band.Name);
    }

    private static string SafeDataPath(string value)
    {
        var cleaned = new string(value.Select(ch => char.IsLetterOrDigit(ch) || ch is '_' or '.' ? ch : '_').ToArray()).Trim('_');
        return string.IsNullOrWhiteSpace(cleaned) ? "items" : cleaned;
    }

    // ── Section-flatten build ──────────────────────────────────────────────────────────────────────

    private static TrdxConvertResult BuildDesign(RawReport report)
    {
        var diagnostics = new List<MigrationDiagnostic>();

        var orderedBands = report.Bands.OrderBy(b => SectionOrder(b.Type)).ToList();
        var bandTop = new Dictionary<string, double>(StringComparer.Ordinal);
        var bandByName = new Dictionary<string, RawBand>(StringComparer.Ordinal);
        var offset = report.MarginTopPt;
        foreach (var band in orderedBands)
        {
            bandByName.TryAdd(band.Name, band);
            bandTop[band.Name] = offset;
            offset += band.HeightPt;
        }

        var elements = new List<ElementDto>();
        var sharedElements = new List<ElementDto>();
        var mapped = 0;

        foreach (var raw in report.Elements)
        {
            var band = raw.Section is not null && bandByName.TryGetValue(raw.Section, out var b) ? b : null;
            var bandType = band?.Type ?? "";

            double yPt = bandType switch
            {
                "PageHeaderSection" => report.MarginTopPt + raw.Y,
                "PageFooterSection" => report.PageHeightPt - report.MarginBottomPt - (band?.HeightPt ?? 0) + raw.Y,
                _ => (raw.Section is not null && bandTop.TryGetValue(raw.Section, out var t) ? t : offset) + raw.Y
            };
            var x = report.MarginLeftPt + raw.X;

            var element = MapControl(raw, x, yPt, diagnostics);
            if (element is null) continue;

            diagnostics.Add(Info("CANMIGTRDX002", $"'{raw.Name}' ({raw.Type}) → Canvas {element.Type}."));

            // Aggregates in a group section scope to the current group's row subset ($group),
            // injected per group by DesignLayoutPlanner; other sections have no aggregate scope.
            var aggDataset = bandType is "GroupHeaderSection" or "GroupFooterSection"
                ? ExpressionTranslator.GroupScopeToken : null;
            if (raw.Value is { } v) ApplyBinding(element, v, diagnostics, aggDataset);
            if (band is not null) ApplyGroupRepeatMetadata(element, band, diagnostics);

            (bandType is "PageHeaderSection" or "PageFooterSection" ? sharedElements : elements).Add(element);
            mapped++;
        }

        elements.Sort((p, q) => p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.X.CompareTo(q.X));
        sharedElements.Sort((p, q) => p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.X.CompareTo(q.X));

        diagnostics.Insert(0, Info("CANMIGTRDX001",
            $"Telerik report '{report.Name}' detected — {report.Bands.Count} section(s), {mapped} item(s) mapped."));

        var design = new DesignExportDto
        {
            Id = $"trdx-report-{Guid.NewGuid():N}",
            Name = report.Name,
            Category = "imported",
            Description = "Imported from a Telerik Reporting report (.trdx).",
            PageSettings = new PageSettingsDto { Width = report.PageWidthPt, Height = report.PageHeightPt, Unit = "pt" },
            Pages = [new PageDto { Id = "page-1", Elements = elements }],
            SharedElements = sharedElements
        };

        return new TrdxConvertResult { Design = design, Diagnostics = diagnostics };
    }

    private static ElementDto? MapControl(RawElement raw, double x, double y, List<MigrationDiagnostic> diagnostics)
    {
        var element = new ElementDto { Id = $"trdx-{raw.Name}", Name = raw.Name, X = x, Y = y, Width = raw.W, Height = raw.H };

        switch (raw.Type)
        {
            case "TextBox":
                element.Type = "text";
                element.Content = LooksLikeExpression(raw.Value) ? "" : raw.Value ?? "";
                element.Style = BuildTextStyle(raw);
                return element;

            case "HtmlTextBox":
                element.Type = "richtext";
                element.HtmlContent = raw.Value ?? "";
                return element;

            case "Shape":
                element.Type = raw.ShapeKind == "ellipse" ? "circle" : "rect";
                element.Style = new Dictionary<string, object> { ["borderColor"] = raw.BorderColor ?? raw.ForeColor };
                if (raw.BackColor is { } bg) element.Style["backgroundColor"] = bg;
                if (raw.BorderWidth is { } bw) element.Style["borderWidth"] = bw;
                return element;

            case "Panel":
                element.Type = "rect";
                element.Style = new Dictionary<string, object> { ["borderColor"] = raw.BorderColor ?? "#D1D5DB" };
                if (raw.BackColor is { } pbg) element.Style["backgroundColor"] = pbg;
                if (raw.BorderWidth is { } pbw) element.Style["borderWidth"] = pbw;
                return element;

            case "PictureBox":
                element.Type = "image";
                element.FitMode = "contain";
                if (raw.ImageDataUrl is { } dataUrl)
                    element.Content = dataUrl;
                else
                    diagnostics.Add(Warn("CANMIGTRDX012",
                        $"'{raw.Name}' picture data isn't embeddable from source — inserted an empty image placeholder."));
                return element;

            case "Barcode":
                element.Type = "barcode";
                element.BarcodeValue = LooksLikeExpression(raw.Value) ? "" : raw.Value ?? "";
                element.BarcodeType = BarcodeTypeFromSymbology(raw.Symbology);
                return element;

            case "SubReport":
                diagnostics.Add(Warn("CANMIGTRDX011",
                    $"'{raw.Name}' is a sub-report — requires manual migration; inserted a placeholder."));
                return Placeholder(element, $"[Sub-report: {raw.Name} — migrate manually]");

            case "Table":
            case "Crosstab":
            case "CrossTab":
                return MapTable(raw, element, diagnostics);

            default:
                // Chart, Graph, Map, … — full fidelity is V2.
                diagnostics.Add(Warn("CANMIGTRDX011", $"'{raw.Name}' is a {raw.Type} — not supported by Canvas yet; inserted a placeholder."));
                return Placeholder(element, $"[{raw.Type}: migrate manually]");
        }
    }

    private static ElementDto MapTable(RawElement raw, ElementDto element, List<MigrationDiagnostic> diagnostics)
    {
        if (raw.TableCells is not { Count: > 0 } grid)
        {
            diagnostics.Add(Warn("CANMIGTRDX011", $"'{raw.Name}' {raw.Type} has no parseable cells — inserted a placeholder."));
            return Placeholder(element, $"[{raw.Type}: {raw.Name} — migrate manually]");
        }

        var columns = grid.Max(r => r.Count);
        var cellData = grid
            .Select(r => r.Count == columns ? r.ToArray() : r.Concat(Enumerable.Repeat("", columns - r.Count)).ToArray())
            .ToArray();

        element.Type = "table";
        element.CellData = cellData;
        element.ColumnWidths = FitWidths(raw.TableColumnWidthsPt, columns, raw.W);
        element.HeaderRow = raw.TableHasHeader;
        element.CellStyles = raw.CellStyles is { Count: > 0 } cs ? cs.ToArray() : null;

        diagnostics.Add(Warn("CANMIGTRDX013",
            $"'{raw.Name}' Telerik {raw.Type} was mapped to a Canvas table ({grid.Count} row(s) × {columns} column(s)); cell anchoring/grouping is best-effort — review."));
        return element;
    }

    private static double[]? FitWidths(double[]? widths, int columns, double totalPt)
    {
        if (widths is null || widths.Length == 0 || columns <= 0) return null;
        if (widths.Length == columns) return widths;
        if (widths.Length > columns) return widths.Take(columns).ToArray();
        var pad = columns - widths.Length;
        var each = pad > 0 ? Math.Max(0, totalPt - widths.Sum()) / pad : 0;
        return widths.Concat(Enumerable.Repeat(each, pad)).ToArray();
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

    private void ApplyStyle(RawElement raw, XElement? style)
    {
        if (style is null) return;

        var font = Child(style, "Font");
        if (font is not null)
        {
            if (Attr(font, "Name") is { Length: > 0 } fam) raw.FontFamily = fam;
            if (LengthToPt(Attr(font, "Size")) is var fs and > 0) raw.FontSize = fs;
            if (IsTrue(Attr(font, "Bold"))) raw.Bold = true;
            if (IsTrue(Attr(font, "Italic"))) raw.Italic = true;
            if (IsTrue(Attr(font, "Underline"))) raw.Underline = true;
            if (IsTrue(Attr(font, "Strikeout")) || IsTrue(Attr(font, "Strikethrough"))) raw.Strikeout = true;
        }

        if (ParseColor(Attr(style, "Color") ?? Child(style, "Color")?.Value ?? Attr(Child(style, "Color"), "Default")) is { } c) raw.ForeColor = c;
        if (ParseColor(Attr(style, "BackgroundColor") ?? Attr(Child(style, "BackgroundColor"), "Default")) is { } bg) raw.BackColor = bg;
        if (ParseColor(Attr(Child(style, "BorderColor"), "Default") ?? Attr(style, "BorderColor")) is { } bc) raw.BorderColor = bc;
        if (LengthToPt(Attr(Child(style, "BorderWidth"), "Default")) is var bw and > 0) raw.BorderWidth = bw;
        if ((Attr(style, "TextAlign") ?? Attr(style, "HorizontalAlign")) is { Length: > 0 } al) raw.TextAlign = ParseAlignment(al);
    }

    private static void ApplyBinding(ElementDto element, string value, List<MigrationDiagnostic> diagnostics,
        string? dataSetPath = null)
    {
        if (!LooksLikeExpression(value)) return;   // literal already set as Content
        var single = Regex.Match(value, @"^\s*=\s*Fields\.(\w+)(?:\.Value)?\s*$");
        if (single.Success)
        {
            var field = single.Groups[1].Value;
            element.Binding = field;
            if (element.Type == "text") element.Content = $"{{{{{field}}}}}";
            else if (element.Type == "barcode") element.BarcodeValue = $"{{{{{field}}}}}";
            diagnostics.Add(Info("CANMIGTRDX010", $"'{element.Name}' bound to Fields.{field} → Canvas binding '{field}'."));
        }
        else
        {
            // Compound expression (multiple fields / functions): normalize every Fields.X reference to a
            // Canvas {{X}} token so it renders as a readable template; keep the original for review.
            var normalized = Regex.Replace(value.TrimStart().TrimStart('=').Trim(),
                @"Fields\.(\w+)(?:\.Value)?", m => $"{{{{{m.Groups[1].Value}}}}}");
            // Telerik uses RDL grammar with dot field refs (Fields.X.Value); rewrite to the bang form the
            // RDL translator expects, then translate (IIf/operators + Sum/Avg/... aggregates). Raw kept.
            var rdlish = Regex.Replace(value, @"Fields\.(\w+)(?:\.Value)?", m => $"Fields!{m.Groups[1].Value}.Value");
            element.Expression = ExpressionTranslator.TranslateRdl(rdlish, dataSetPath) ?? value;
            element.Style ??= [];
            element.Style["trdxExpression"] = value;
            if (element.Type == "barcode") element.BarcodeValue = normalized;
            else if (string.IsNullOrEmpty(element.Content)) element.Content = normalized;
            diagnostics.Add(Warn("CANMIGTRDX010", $"'{element.Name}' expression '{value}' mapped to a Canvas template with normalized field references — review the syntax."));
        }
    }

    private static bool LooksLikeExpression(string? value) => value is not null && value.TrimStart().StartsWith('=');

    // ── helpers ────────────────────────────────────────────────────────────────────────────────────

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
            _ => 0
        };
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
        if (s.Contains("QR", StringComparison.OrdinalIgnoreCase)) return "qrcode";
        return "code128";
    }

    private static string? ExtractImageDataUrl(XElement el)
    {
        var candidate = Attr(el, "Image") ?? Attr(el, "ImageData")
            ?? el.Descendants().FirstOrDefault(e => e.Name.LocalName is "Base64" or "ImageData")?.Value;
        if (string.IsNullOrWhiteSpace(candidate)) return null;
        if (candidate.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return candidate;
        var b64 = Regex.Replace(candidate, @"\s+", "");
        return b64.Length >= 16 && b64.Length % 4 == 0 && Regex.IsMatch(b64, @"^[A-Za-z0-9+/]+={0,2}$")
            ? $"data:image/png;base64,{b64}" : null;
    }

    // Telerik colours: "R, G, B", named, or "#RRGGBB".
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
            var o = c.Length >= 4 ? 1 : 0;
            return $"#{c[o]:X2}{c[o + 1]:X2}{c[o + 2]:X2}";
        }
        return NamedColor(v);
    }

    private static (double W, double H) PaperKindSize(string kind) => kind.Trim().ToLowerInvariant() switch
    {
        "a3" => (842, 1191),
        "a4" => (595, 842),
        "a5" => (420, 595),
        "letter" => (612, 792),
        "legal" => (612, 1008),
        "tabloid" or "ledger" => (792, 1224),
        _ => (0, 0)
    };

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
        "silver" => "#C0C0C0",
        "yellow" => "#FFFF00",
        "orange" => "#FFA500",
        "navy" => "#000080",
        "maroon" => "#800000",
        "teal" => "#008080",
        "purple" => "#800080",
        "transparent" => "#00000000",
        _ => "#000000"
    };

    private static int SectionOrder(string type) => type switch
    {
        "ReportHeaderSection" => 0,
        "PageHeaderSection" => 1,
        "GroupHeaderSection" => 2,
        "DetailSection" => 3,
        "GroupFooterSection" => 4,
        "ReportFooterSection" => 5,
        "PageFooterSection" => 6,
        _ => 100
    };

    private static bool IsTrue(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static string? Attr(XElement? el, string name) => el?.Attribute(name)?.Value;

    private static XElement? Child(XElement? el, string name) =>
        el?.Elements().FirstOrDefault(e => e.Name.LocalName == name);

    private static IEnumerable<XElement> Children(XElement? el, string name) =>
        el is null ? Enumerable.Empty<XElement>() : el.Elements().Where(e => e.Name.LocalName == name);

    private static MigrationDiagnostic Info(string id, string message) =>
        new() { Id = id, Message = message, Severity = MigrationDiagnosticSeverity.Info };

    private static MigrationDiagnostic Warn(string id, string message) =>
        new() { Id = id, Message = message, Severity = MigrationDiagnosticSeverity.Warning };

    // ── Neutral intermediate model ─────────────────────────────────────────────────────────────────

    private sealed class RawReport
    {
        public string Name = "Telerik Report";
        public double PageWidthPt = A4WidthPt, PageHeightPt = A4HeightPt;
        public double MarginLeftPt, MarginTopPt, MarginBottomPt;
        public List<RawBand> Bands = [];
        public List<RawElement> Elements = [];
    }

    private sealed class RawBand
    {
        public required string Name;
        public required string Type;
        public double HeightPt;
        public string? Condition;   // GroupHeaderSection grouping expression
        public string? GroupName;   // group identity shared by a header/footer pair
    }

    private sealed class RawElement
    {
        public required string Name;
        public required string Type;
        public string? Section;
        public double X, Y, W, H;   // points; X/Y are section-relative (Panel-offset already folded in)
        public string? Value;
        public string? FontFamily;
        public double? FontSize;
        public bool Bold, Italic, Underline, Strikeout;
        public string ForeColor = "#000000";
        public string? BackColor;
        public string? BorderColor;
        public double? BorderWidth;
        public string TextAlign = "left";
        public string? ShapeKind;
        public string? ImageDataUrl;
        public string? Symbology;
        public List<List<string>>? TableCells;
        public List<CellStyleDto>? CellStyles;
        public double[]? TableColumnWidthsPt;
        public bool TableHasHeader;
    }
}
