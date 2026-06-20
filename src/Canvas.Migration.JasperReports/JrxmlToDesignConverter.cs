using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Canvas.Core.Contracts;
using Canvas.Migration.Abstractions;

namespace Canvas.Migration.JasperReports;

public sealed class JrxmlConvertResult
{
    public required DesignExportDto Design { get; init; }
    public required IReadOnlyList<MigrationDiagnostic> Diagnostics { get; init; }
}

/// <summary>
/// Converts a JasperReports <c>.jrxml</c> report — namespaced XML
/// (<c>http://jasperreports.sourceforge.net/jasperreports</c>) with a <b>banded</b> layout
/// (<c>&lt;title&gt;</c>/<c>&lt;pageHeader&gt;</c>/<c>&lt;detail&gt;</c>/… each wrapping a
/// <c>&lt;band&gt;</c>) — into a Canvas <see cref="DesignExportDto"/>. JasperReports coordinates are in
/// points (pixels = 1/72in), so no unit scaling. Bands stack and flatten to absolute page coordinates
/// (mirroring <c>Canvas.Migration.Rpx</c>); named <c>&lt;style&gt;</c> elements are resolved.
/// Elements are matched by <see cref="XName.LocalName"/> (namespace-agnostic).
/// </summary>
public sealed class JrxmlToDesignConverter
{
    private const double A4WidthPt = 595, A4HeightPt = 842;

    // Section element name → canonical stacking order (smaller = higher on the page).
    private static readonly string[] SectionOrder =
    [
        "background", "title", "pageHeader", "columnHeader", "detail",
        "columnFooter", "pageFooter", "lastPageFooter", "summary",
    ];

    // style name → its <style> element from the report, populated per Convert call.
    private readonly Dictionary<string, XElement> _namedStyles = new(StringComparer.Ordinal);

    /// <summary>Detects a JasperReports <c>.jrxml</c>: root LocalName is <c>jasperReport</c> (unique).</summary>
    public static bool LooksLikeJrxml(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;
        if (!source.TrimStart().StartsWith('<')) return false;
        try
        {
            return XDocument.Parse(source).Root?.Name.LocalName == "jasperReport";
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
    }

    public JrxmlConvertResult ConvertAuto(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source cannot be null or empty.", nameof(source));
        return Convert(source);
    }

    public JrxmlConvertResult Convert(string jrxml)
    {
        if (string.IsNullOrWhiteSpace(jrxml))
            throw new ArgumentException("Source cannot be null or empty.", nameof(jrxml));

        XElement root;
        try { root = XDocument.Parse(jrxml).Root ?? throw new ArgumentException("Empty .jrxml document."); }
        catch (System.Xml.XmlException ex) { throw new ArgumentException($"Invalid .jrxml XML: {ex.Message}", nameof(jrxml)); }

        if (root.Name.LocalName != "jasperReport")
            throw new ArgumentException("Not a JasperReports .jrxml — expected a root <jasperReport> element.", nameof(jrxml));

        _namedStyles.Clear();
        foreach (var style in Children(root, "style"))
            if (Attr(style, "name") is { Length: > 0 } sn)
                _namedStyles[sn] = style;

        var report = new RawReport
        {
            Name = Attr(root, "name") ?? "JasperReports Report",
            PageWidthPt = ToDouble(Attr(root, "pageWidth")) is var pw and > 0 ? pw : A4WidthPt,
            PageHeightPt = ToDouble(Attr(root, "pageHeight")) is var ph and > 0 ? ph : A4HeightPt,
            MarginLeftPt = ToDouble(Attr(root, "leftMargin")),
            MarginTopPt = ToDouble(Attr(root, "topMargin")),
            MarginBottomPt = ToDouble(Attr(root, "bottomMargin")),
        };
        ParseReportDataDeclarations(root, report);
        if (string.Equals(Attr(root, "orientation"), "Landscape", StringComparison.OrdinalIgnoreCase))
            (report.PageWidthPt, report.PageHeightPt) = (report.PageHeightPt, report.PageWidthPt);

        // Walk sections in canonical order; each holds one or more <band>s of elements.
        foreach (var sectionType in SectionOrder)
        {
            if (sectionType == "background") continue;   // watermark-style; skip
            var section = Child(root, sectionType);
            if (section is null) continue;
            var bandIndex = 0;
            foreach (var band in Children(section, "band"))
            {
                var name = $"{sectionType}-{bandIndex++}";
                report.Bands.Add(new RawBand { Name = name, Type = sectionType, HeightPt = ToDouble(Attr(band, "height")) });
                ParseElements(band, name, 0, 0, report, 0);
            }
        }

        return BuildDesign(report);
    }

    // Parse a band's (or frame's) visual elements; a frame recurses with its offset so children stay absolute.
    private void ParseElements(XElement container, string bandName, double originX, double originY, RawReport report, int depth)
    {
        if (depth > 32) return;
        foreach (var el in container.Elements())
        {
            var re = Child(el, "reportElement");
            if (re is null) continue;   // not a visual element (property/expression/etc.)

            var type = el.Name.LocalName;
            var x = ToDouble(Attr(re, "x")) + originX;
            var y = ToDouble(Attr(re, "y")) + originY;

            var raw = new RawElement
            {
                Name = Attr(re, "key") ?? Attr(re, "uuid") ?? $"{type}{report.Elements.Count}",
                Type = type,
                Band = bandName,
                X = x, Y = y,
                W = ToDouble(Attr(re, "width")),
                H = ToDouble(Attr(re, "height")),
            };
            ApplyStyle(raw, _namedStyles.GetValueOrDefault(Attr(re, "style") ?? ""));   // named base
            if (ParseColor(Attr(re, "forecolor")) is { } fc) raw.ForeColor = fc;        // inline overrides
            if (ParseColor(Attr(re, "backcolor")) is { } bc && !string.Equals(Attr(re, "mode"), "Transparent", StringComparison.OrdinalIgnoreCase)) raw.BackColor = bc;

            var textEl = Child(el, "textElement");
            if (textEl is not null)
            {
                if (Attr(textEl, "textAlignment") is { Length: > 0 } al) raw.TextAlign = ParseAlignment(al);
                ApplyFont(raw, Child(textEl, "font"));
            }
            ApplyBox(raw, Child(el, "box"));

            switch (type)
            {
                case "staticText":
                    raw.Text = Child(el, "text")?.Value?.Trim();
                    break;
                case "textField":
                    raw.Expression = Child(el, "textFieldExpression")?.Value?.Trim();
                    break;
                case "image":
                    raw.ImageDataUrl = ExtractImageDataUrl(el);
                    break;
                case "componentElement":
                    ParseComponentElement(el, raw);
                    break;
                case "crosstab":
                    ParseCrosstab(el, raw);
                    break;
                case "line":
                case "rectangle":
                case "ellipse":
                    var pen = Child(Child(el, "graphicElement"), "pen");
                    if (ParseColor(Attr(pen, "lineColor")) is { } lc) raw.ForeColor = lc;
                    if (ToDouble(Attr(pen, "lineWidth")) is var lw and > 0) raw.LineWidth = lw;
                    break;
            }

            report.Elements.Add(raw);

            if (type == "frame")
                ParseElements(el, bandName, x, y, report, depth + 1);
        }
    }

    // ── Section-flatten build ──────────────────────────────────────────────────────────────────────

    private static JrxmlConvertResult BuildDesign(RawReport report)
    {
        var diagnostics = new List<MigrationDiagnostic>();

        var bandByName = new Dictionary<string, RawBand>(StringComparer.Ordinal);
        var bandTop = new Dictionary<string, double>(StringComparer.Ordinal);
        var offset = report.MarginTopPt;
        foreach (var band in report.Bands)   // already in canonical order
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
            var band = raw.Band is not null && bandByName.TryGetValue(raw.Band, out var b) ? b : null;
            var bandType = band?.Type ?? "";

            double yPt = bandType switch
            {
                "pageHeader" => report.MarginTopPt + raw.Y,
                "pageFooter" or "lastPageFooter" => report.PageHeightPt - report.MarginBottomPt - (band?.HeightPt ?? 0) + raw.Y,
                _ => (raw.Band is not null && bandTop.TryGetValue(raw.Band, out var t) ? t : offset) + raw.Y,
            };
            var x = report.MarginLeftPt + raw.X;

            var element = MapControl(raw, x, yPt, diagnostics);
            if (element is null) continue;

            diagnostics.Add(Info("CANMIGJRXML002", $"'{raw.Name}' ({raw.Type}) → Canvas {element.Type}."));

            if (raw.Type == "textField" && raw.Expression is { } expr) ApplyBinding(element, expr, diagnostics);

            (bandType is "pageHeader" or "pageFooter" or "lastPageFooter" ? sharedElements : elements).Add(element);
            mapped++;
        }

        elements.Sort((p, q) => p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.X.CompareTo(q.X));
        sharedElements.Sort((p, q) => p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.X.CompareTo(q.X));

        diagnostics.Insert(0, Info("CANMIGJRXML001",
            $"JasperReports report '{report.Name}' detected — {report.Bands.Count} band(s), {mapped} element(s) mapped."));

        var design = new DesignExportDto
        {
            Id = $"jrxml-report-{Guid.NewGuid():N}",
            Name = report.Name,
            Category = "imported",
            Description = "Imported from a JasperReports report (.jrxml).",
            PageSettings = BuildPageSettings(report, diagnostics),
            Pages = [new PageDto { Id = "page-1", Elements = elements }],
            SharedElements = sharedElements
        };

        return new JrxmlConvertResult { Design = design, Diagnostics = diagnostics };
    }

    private static PageSettingsDto BuildPageSettings(RawReport report, List<MigrationDiagnostic> diagnostics)
    {
        var settings = new PageSettingsDto { Width = report.PageWidthPt, Height = report.PageHeightPt, Unit = "pt" };
        var customProperties = new List<CustomDocumentPropertyDto>();

        AddCustomJson(customProperties, "jrxmlParameters", report.Parameters);
        AddCustomJson(customProperties, "jrxmlFields", report.Fields);
        AddCustomJson(customProperties, "jrxmlVariables", report.Variables);
        AddCustomJson(customProperties, "jrxmlSubDatasets", report.SubDatasets);
        AddCustomJson(customProperties, "jrxmlQuery", report.Query);

        if (customProperties.Count > 0)
        {
            settings.CustomProperties = customProperties;
            diagnostics.Add(Warn("CANMIGJRXML015",
                "JasperReports data declarations were preserved in PageSettings.CustomProperties; Canvas does not evaluate JRXML datasets/queries yet."));
        }

        return settings;
    }

    private static ElementDto? MapControl(RawElement raw, double x, double y, List<MigrationDiagnostic> diagnostics)
    {
        var element = new ElementDto { Id = $"jrxml-{raw.Name}", Name = raw.Name, X = x, Y = y, Width = raw.W, Height = raw.H };

        switch (raw.Type)
        {
            case "staticText":
            case "textField":
                element.Type = "text";
                element.Content = raw.Type == "staticText" ? raw.Text ?? "" : (LooksLikeExpr(raw.Expression) ? "" : raw.Expression ?? "");
                element.Style = BuildTextStyle(raw);
                return element;

            case "line":
                element.Type = "line";
                element.Style = new Dictionary<string, object> { ["color"] = raw.ForeColor };
                if (raw.LineWidth is { } lineW) element.Style["strokeWidth"] = lineW;
                return element;

            case "rectangle":
            case "ellipse":
                element.Type = raw.Type == "ellipse" ? "circle" : "rect";
                element.Style = new Dictionary<string, object> { ["borderColor"] = raw.ForeColor };
                if (raw.BackColor is { } bg) element.Style["backgroundColor"] = bg;
                if (raw.LineWidth is { } bw) element.Style["borderWidth"] = bw;
                ApplyBorderStyle(element.Style, raw);
                return element;

            case "frame":
                element.Type = "rect";
                element.Style = new Dictionary<string, object> { ["borderColor"] = raw.ForeColor };
                if (raw.BackColor is { } fbg) element.Style["backgroundColor"] = fbg;
                ApplyBorderStyle(element.Style, raw);
                return element;

            case "image":
                element.Type = "image";
                element.FitMode = "contain";
                if (raw.ImageDataUrl is { } dataUrl)
                    element.Content = dataUrl;
                else
                    diagnostics.Add(Warn("CANMIGJRXML012",
                        $"'{raw.Name}' image isn't embeddable from source — inserted an empty image placeholder."));
                return element;

            case "subreport":
                diagnostics.Add(Warn("CANMIGJRXML011",
                    $"'{raw.Name}' is a sub-report — requires manual migration; inserted a placeholder."));
                return Placeholder(element, $"[Sub-report: {raw.Name} — migrate manually]");

            case "componentElement" when raw.ComponentKind == "barcode":
                return MapBarcodeComponent(raw, element, diagnostics);

            case "componentElement" when raw.ComponentKind == "table":
                return MapTableComponent(raw, element, diagnostics);

            case "componentElement":
            case "crosstab":
                return MapComponentPlaceholder(raw, element, diagnostics);

            default:
                // componentElement (barcodes/charts), crosstab, … — full fidelity is V2.
                diagnostics.Add(Warn("CANMIGJRXML011", $"'{raw.Name}' is a {raw.Type} — not supported by Canvas yet; inserted a placeholder."));
                return Placeholder(element, $"[{raw.Type}: migrate manually]");
        }
    }

    private static ElementDto MapBarcodeComponent(RawElement raw, ElementDto element, List<MigrationDiagnostic> diagnostics)
    {
        var value = ExpressionDisplay(raw.ComponentValue);
        var barcodeType = BarcodeTypeFromSymbology(raw.ComponentType);
        if (barcodeType == "qrcode")
        {
            element.Type = "qrcode";
            element.QrValue = value;
        }
        else
        {
            element.Type = "barcode";
            element.BarcodeValue = value;
            element.BarcodeType = barcodeType;
        }

        element.Style = new Dictionary<string, object>
        {
            ["jrxmlComponentType"] = raw.ComponentType ?? "barcode"
        };
        if (raw.ComponentMetadata is { Count: > 0 })
            element.Style["jrxmlComponent"] = raw.ComponentMetadata;

        diagnostics.Add(Info("CANMIGJRXML013",
            $"'{raw.Name}' JasperReports barcode component mapped to Canvas {element.Type}."));
        return element;
    }

    private static ElementDto MapTableComponent(RawElement raw, ElementDto element, List<MigrationDiagnostic> diagnostics)
    {
        element.Type = "table";
        element.CellData = raw.TableRows?.Select(row => row.ToArray()).ToArray() ?? [];
        element.ColumnWidths = raw.TableColumnWidths?.ToArray();
        element.HeaderRow = raw.TableHasHeader;
        element.Style = new Dictionary<string, object>
        {
            ["jrxmlComponentType"] = raw.ComponentType ?? "table"
        };
        if (raw.ComponentMetadata is { Count: > 0 })
            element.Style["jrxmlTable"] = raw.ComponentMetadata;

        diagnostics.Add(Warn("CANMIGJRXML014",
            $"'{raw.Name}' JasperReports table component was mapped to a Canvas table with preserved dataset/cell metadata; review repeat/data semantics."));
        return element;
    }

    private static ElementDto MapComponentPlaceholder(RawElement raw, ElementDto element, List<MigrationDiagnostic> diagnostics)
    {
        var label = raw.ComponentKind switch
        {
            "chart" => "Chart",
            "crosstab" => "Crosstab",
            _ => "Component"
        };
        var caption = raw.ComponentCaption ?? raw.ComponentType ?? raw.Name;
        var placeholder = Placeholder(element, $"[{label}: {ExpressionDisplay(caption)}]");
        placeholder.Style ??= [];
        placeholder.Style["jrxmlComponentType"] = raw.ComponentType ?? raw.Type;
        if (raw.ComponentMetadata is { Count: > 0 })
            placeholder.Style["jrxmlComponent"] = raw.ComponentMetadata;

        diagnostics.Add(Warn("CANMIGJRXML011",
            $"'{raw.Name}' JasperReports {label.ToLowerInvariant()} component metadata was preserved on a positioned placeholder; review manually."));
        return placeholder;
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
        ApplyBorderStyle(style, raw);
        return style;
    }

    private void ApplyStyle(RawElement raw, XElement? style)
    {
        if (style is null) return;
        if (ParseColor(Attr(style, "forecolor")) is { } fc) raw.ForeColor = fc;
        if (ParseColor(Attr(style, "backcolor")) is { } bc) raw.BackColor = bc;
        if (Attr(style, "hAlign") is { Length: > 0 } al) raw.TextAlign = ParseAlignment(al);
        ApplyFont(raw, Child(style, "font"));
        ApplyBox(raw, Child(style, "box"));
        // JasperReports also exposes font attributes directly on <style>.
        ApplyFontAttrs(raw, style);
    }

    private static void ApplyBox(RawElement raw, XElement? box)
    {
        if (box is null)
            return;

        ApplyPen(raw, "all", Child(box, "pen"));
        ApplyPen(raw, "top", Child(box, "topPen"));
        ApplyPen(raw, "left", Child(box, "leftPen"));
        ApplyPen(raw, "bottom", Child(box, "bottomPen"));
        ApplyPen(raw, "right", Child(box, "rightPen"));
    }

    private static void ApplyPen(RawElement raw, string side, XElement? pen)
    {
        if (pen is null)
            return;

        var target = raw.Borders.GetValueOrDefault(side) ?? new BorderStyle();
        if (ToDouble(Attr(pen, "lineWidth")) is var width and > 0)
            target.Width = width;
        if (ParseColor(Attr(pen, "lineColor")) is { } color)
            target.Color = color;
        if (Attr(pen, "lineStyle") is { Length: > 0 } lineStyle)
            target.Style = lineStyle;
        raw.Borders[side] = target;
    }

    private static void ApplyBorderStyle(Dictionary<string, object> style, RawElement raw)
    {
        if (raw.Borders.TryGetValue("all", out var all))
        {
            if (all.Width is { } width)
                style["borderWidth"] = width;
            if (all.Color is { } color)
                style["borderColor"] = color;
            if (DashStyleFromPen(all.Style) is { } dash)
                style["borderStyle"] = dash;
        }

        foreach (var side in new[] { "top", "left", "bottom", "right" })
        {
            if (!raw.Borders.TryGetValue(side, out var border))
                continue;
            var suffix = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(side);
            if (border.Width is { } width)
                style[$"border{suffix}Width"] = width;
            if (border.Color is { } color)
                style[$"border{suffix}Color"] = color;
            if (DashStyleFromPen(border.Style) is { } dash)
                style[$"border{suffix}Style"] = dash;
        }
    }

    private static string? DashStyleFromPen(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (value.Contains("Dash", StringComparison.OrdinalIgnoreCase))
            return "dashed";
        if (value.Contains("Dot", StringComparison.OrdinalIgnoreCase))
            return "dotted";
        return "solid";
    }

    private static void ParseComponentElement(XElement el, RawElement raw)
    {
        var component = el.Elements().FirstOrDefault(child => child.Name.LocalName != "reportElement");
        if (component is null)
            return;

        var componentName = component.Name.LocalName;
        var type = Attr(component, "type") ?? Attr(component, "barcodeType") ?? componentName;
        raw.ComponentType = type;
        raw.ComponentMetadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Component"] = componentName,
            ["Type"] = type
        };

        if (componentName.Equals("table", StringComparison.OrdinalIgnoreCase))
        {
            ParseTableComponent(component, raw);
            return;
        }

        if (LooksLikeBarcodeComponent(componentName, type))
        {
            raw.ComponentKind = "barcode";
            raw.ComponentValue = component.Descendants()
                .FirstOrDefault(desc => desc.Name.LocalName is "codeExpression" or "messageExpression" or "textExpression")
                ?.Value.Trim();
            AddText(raw.ComponentMetadata, "ValueExpression", raw.ComponentValue);
            return;
        }

        raw.ComponentKind = LooksLikeChartComponent(componentName, type) ? "chart" : "component";
        raw.ComponentCaption = FirstDescendantValue(component, "titleExpression", "title", "captionExpression", "caption");
        AddText(raw.ComponentMetadata, "Caption", raw.ComponentCaption);
        AddText(raw.ComponentMetadata, "DatasetName", component.Descendants()
            .Select(desc => Attr(desc, "subDataset") ?? Attr(desc, "datasetName"))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)));

        var expressions = ComponentExpressions(component).ToArray();
        if (expressions.Length > 0)
            raw.ComponentMetadata["Expressions"] = expressions;
    }

    private static void ParseTableComponent(XElement table, RawElement raw)
    {
        raw.ComponentKind = "table";
        raw.ComponentType = "table";

        if (Child(table, "datasetRun") is { } datasetRun)
        {
            AddText(raw.ComponentMetadata, "DatasetName", Attr(datasetRun, "subDataset"));
            var parameters = Children(datasetRun, "datasetParameter")
                .Select(parameter =>
                {
                    var p = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Name"] = Attr(parameter, "name") ?? ""
                    };
                    AddText(p, "Expression", Child(parameter, "datasetParameterExpression")?.Value.Trim());
                    return p;
                })
                .Where(parameter => parameter.Values.Any(value => value?.ToString()?.Length > 0))
                .ToArray();
            if (parameters.Length > 0)
                raw.ComponentMetadata!["Parameters"] = parameters;
            AddText(raw.ComponentMetadata, "ConnectionExpression", Child(datasetRun, "connectionExpression")?.Value.Trim());
            AddText(raw.ComponentMetadata, "DataSourceExpression", Child(datasetRun, "dataSourceExpression")?.Value.Trim());
        }

        var columns = Children(table, "column").ToList();
        raw.TableColumnWidths = columns.Select(column => ToDouble(Attr(column, "width"))).ToList();
        raw.TableRows = [];

        var headers = columns.Select(column => TableCellText(Child(column, "columnHeader"))).ToList();
        if (headers.Any(text => !string.IsNullOrWhiteSpace(text)))
        {
            raw.TableRows.Add(headers);
            raw.TableHasHeader = true;
        }

        var details = columns.Select(column => TableCellText(Child(column, "detailCell"))).ToList();
        if (details.Any(text => !string.IsNullOrWhiteSpace(text)))
            raw.TableRows.Add(details);

        raw.ComponentMetadata!["ColumnCount"] = columns.Count;
        raw.ComponentMetadata["HasHeader"] = raw.TableHasHeader;
        var expressions = ComponentExpressions(table).ToArray();
        if (expressions.Length > 0)
            raw.ComponentMetadata["Expressions"] = expressions;
    }

    private static string TableCellText(XElement? cell)
    {
        if (cell is null)
            return "";
        if (cell.Descendants().FirstOrDefault(desc => desc.Name.LocalName == "staticText") is { } staticText)
            return Child(staticText, "text")?.Value.Trim() ?? "";
        if (cell.Descendants().FirstOrDefault(desc => desc.Name.LocalName == "textField") is { } textField)
            return ExpressionDisplay(Child(textField, "textFieldExpression")?.Value.Trim());
        return "";
    }

    private static void ParseCrosstab(XElement el, RawElement raw)
    {
        raw.ComponentKind = "crosstab";
        raw.ComponentType = "crosstab";
        raw.ComponentCaption = Attr(el, "name") ?? raw.Name;
        raw.ComponentMetadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Component"] = "crosstab",
            ["Type"] = "crosstab",
            ["RowGroupCount"] = el.Descendants().Count(desc => desc.Name.LocalName == "rowGroup"),
            ["ColumnGroupCount"] = el.Descendants().Count(desc => desc.Name.LocalName == "columnGroup"),
            ["MeasureCount"] = el.Descendants().Count(desc => desc.Name.LocalName == "measure")
        };
        var expressions = ComponentExpressions(el).ToArray();
        if (expressions.Length > 0)
            raw.ComponentMetadata["Expressions"] = expressions;
    }

    private static void ParseReportDataDeclarations(XElement root, RawReport report)
    {
        report.Parameters = Children(root, "parameter").Select(ParseNamedDeclaration).ToList();
        report.Fields = Children(root, "field").Select(ParseNamedDeclaration).ToList();
        report.Variables = Children(root, "variable").Select(ParseVariableDeclaration).ToList();
        report.SubDatasets = Children(root, "subDataset").Select(ParseSubDatasetDeclaration).ToList();
        report.Query = ParseQuery(Child(root, "queryString"));
    }

    private static Dictionary<string, object> ParseNamedDeclaration(XElement element)
    {
        var item = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = Attr(element, "name") ?? ""
        };
        AddText(item, "Class", Attr(element, "class"));
        AddText(item, "IsForPrompting", Attr(element, "isForPrompting"));
        AddText(item, "Description", Child(element, "parameterDescription")?.Value.Trim());
        AddText(item, "DefaultValueExpression", Child(element, "defaultValueExpression")?.Value.Trim());
        return item;
    }

    private static Dictionary<string, object> ParseVariableDeclaration(XElement element)
    {
        var item = ParseNamedDeclaration(element);
        AddText(item, "Calculation", Attr(element, "calculation"));
        AddText(item, "VariableExpression", Child(element, "variableExpression")?.Value.Trim());
        AddText(item, "InitialValueExpression", Child(element, "initialValueExpression")?.Value.Trim());
        return item;
    }

    private static Dictionary<string, object> ParseSubDatasetDeclaration(XElement element)
    {
        var item = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = Attr(element, "name") ?? ""
        };
        AddText(item, "Uuid", Attr(element, "uuid"));
        var parameters = Children(element, "parameter").Select(ParseNamedDeclaration).ToArray();
        if (parameters.Length > 0)
            item["Parameters"] = parameters;
        var fields = Children(element, "field").Select(ParseNamedDeclaration).ToArray();
        if (fields.Length > 0)
            item["Fields"] = fields;
        if (ParseQuery(Child(element, "queryString")) is { Count: > 0 } query)
            item["Query"] = query;
        return item;
    }

    private static Dictionary<string, object> ParseQuery(XElement? query)
    {
        var item = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (query is null)
            return item;
        AddText(item, "Language", Attr(query, "language"));
        AddText(item, "Text", query.Value.Trim());
        return item;
    }

    private static void AddCustomJson(List<CustomDocumentPropertyDto> properties, string name, object? value)
    {
        var include = value switch
        {
            null => false,
            IReadOnlyCollection<Dictionary<string, object>> collection => collection.Count > 0,
            IReadOnlyDictionary<string, object> dictionary => dictionary.Count > 0,
            _ => true
        };
        if (!include)
            return;

        properties.Add(new CustomDocumentPropertyDto
        {
            Name = name,
            Type = "text",
            Value = JsonSerializer.Serialize(value)
        });
    }

    private static void ApplyFont(RawElement raw, XElement? font)
    {
        if (font is null) return;
        ApplyFontAttrs(raw, font);
    }

    private static void ApplyFontAttrs(RawElement raw, XElement el)
    {
        if (Attr(el, "fontName") is { Length: > 0 } fam) raw.FontFamily = fam;
        if (ToDouble(Attr(el, "size") ?? Attr(el, "fontSize")) is var fs and > 0) raw.FontSize = fs;
        if (IsTrue(Attr(el, "isBold"))) raw.Bold = true;
        if (IsTrue(Attr(el, "isItalic"))) raw.Italic = true;
        if (IsTrue(Attr(el, "isUnderline"))) raw.Underline = true;
        if (IsTrue(Attr(el, "isStrikeThrough"))) raw.Strikeout = true;
    }

    private static void ApplyBinding(ElementDto element, string expression, List<MigrationDiagnostic> diagnostics)
    {
        var single = Regex.Match(expression, @"^\s*\$F\{(\w+)\}\s*$");
        if (single.Success)
        {
            var field = single.Groups[1].Value;
            element.Binding = field;
            element.Content = $"{{{{{field}}}}}";
            diagnostics.Add(Info("CANMIGJRXML010", $"'{element.Name}' bound to $F{{{field}}} → Canvas binding '{field}'."));
        }
        else if (LooksLikeExpr(expression))
        {
            element.Expression = expression;
            if (string.IsNullOrEmpty(element.Content)) element.Content = expression;
            diagnostics.Add(Warn("CANMIGJRXML010", $"'{element.Name}' expression '{expression}' mapped to Canvas expression — review the syntax."));
        }
        else
        {
            // A quoted literal like "Total:" — strip the surrounding quotes.
            element.Content = expression.Trim('"');
        }
    }

    // A JasperReports textField expression that references data/params/vars/functions (vs a bare literal).
    private static bool LooksLikeExpr(string? value) =>
        value is not null && (value.Contains("$F{") || value.Contains("$P{") || value.Contains("$V{")
            || value.Contains('+') || value.Contains('(') );

    private static string ExpressionDisplay(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return "";

        var field = Regex.Match(expression, @"^\s*\$F\{(\w+)\}\s*$");
        if (field.Success)
            return $"{{{{{field.Groups[1].Value}}}}}";

        return expression.Trim().Trim('"');
    }

    private static void AddText(Dictionary<string, object>? target, string key, string? value)
    {
        if (target is not null && !string.IsNullOrWhiteSpace(value))
            target[key] = value.Trim();
    }

    private static string? FirstDescendantValue(XElement el, params string[] names) =>
        el.Descendants()
            .FirstOrDefault(desc => names.Any(name => desc.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase)))
            ?.Value.Trim();

    private static IEnumerable<Dictionary<string, object>> ComponentExpressions(XElement el)
    {
        foreach (var expression in el.Descendants().Where(desc =>
            desc.Name.LocalName.EndsWith("Expression", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(desc.Value)))
        {
            yield return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Name"] = expression.Name.LocalName,
                ["Value"] = expression.Value.Trim()
            };
        }
    }

    private static bool LooksLikeBarcodeComponent(string componentName, string? type)
    {
        var value = $"{componentName} {type}".Replace("-", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal);
        return value.Contains("barbecue", StringComparison.OrdinalIgnoreCase)
            || value.Contains("barcode", StringComparison.OrdinalIgnoreCase)
            || value.Contains("code128", StringComparison.OrdinalIgnoreCase)
            || value.Contains("code39", StringComparison.OrdinalIgnoreCase)
            || value.Contains("ean", StringComparison.OrdinalIgnoreCase)
            || value.Contains("upc", StringComparison.OrdinalIgnoreCase)
            || value.Contains("qr", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeChartComponent(string componentName, string? type)
    {
        var value = $"{componentName} {type}".Replace("-", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal);
        return value.Contains("chart", StringComparison.OrdinalIgnoreCase)
            || value.Contains("sparkline", StringComparison.OrdinalIgnoreCase)
            || value.Contains("pie", StringComparison.OrdinalIgnoreCase)
            || value.Contains("bar", StringComparison.OrdinalIgnoreCase)
            || value.Contains("line", StringComparison.OrdinalIgnoreCase);
    }

    private static string BarcodeTypeFromSymbology(string? symbology)
    {
        var s = (symbology ?? "").Replace("-", "").Replace("_", "");
        if (s.Contains("QR", StringComparison.OrdinalIgnoreCase)) return "qrcode";
        if (s.Contains("Code39", StringComparison.OrdinalIgnoreCase)) return "code39";
        if (s.Contains("EAN13", StringComparison.OrdinalIgnoreCase)) return "ean13";
        if (s.Contains("EAN8", StringComparison.OrdinalIgnoreCase)) return "ean8";
        if (s.Contains("UPCA", StringComparison.OrdinalIgnoreCase)) return "upca";
        if (s.Contains("UPCE", StringComparison.OrdinalIgnoreCase)) return "upce";
        return "code128";
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────────

    private static string ParseAlignment(string? text)
    {
        text ??= "";
        if (text.Contains("Center", StringComparison.OrdinalIgnoreCase)) return "center";
        if (text.Contains("Right", StringComparison.OrdinalIgnoreCase)) return "right";
        if (text.Contains("Justif", StringComparison.OrdinalIgnoreCase)) return "justify";
        return "left";
    }

    private static string? ExtractImageDataUrl(XElement el)
    {
        var expr = Child(el, "imageExpression")?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(expr)) return null;
        var inner = expr.Trim('"');
        if (inner.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return inner;
        var b64 = Regex.Replace(inner, @"\s+", "");
        return b64.Length >= 64 && b64.Length % 4 == 0 && Regex.IsMatch(b64, @"^[A-Za-z0-9+/]+={0,2}$")
            ? $"data:image/png;base64,{b64}" : null;   // a path/expression isn't embeddable → placeholder
    }

    private static string? ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        if (v.StartsWith('#'))
            return v.Length == 4 ? $"#{v[1]}{v[1]}{v[2]}{v[2]}{v[3]}{v[3]}".ToUpperInvariant() : v.ToUpperInvariant();
        var nums = v.Split([',', ' '], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (nums.Length >= 3 && nums.All(n => int.TryParse(n, out _)))
        {
            var c = nums.Select(int.Parse).ToArray();
            return $"#{c[0]:X2}{c[1]:X2}{c[2]:X2}";
        }
        return NamedColor(v);
    }

    private static string NamedColor(string name) => name.Trim().ToLowerInvariant() switch
    {
        "white" => "#FFFFFF",
        "black" => "#000000",
        "red" => "#FF0000",
        "green" => "#008000",
        "blue" => "#0000FF",
        "gray" or "grey" => "#808080",
        "lightgray" or "lightgrey" => "#D3D3D3",
        "darkgray" or "darkgrey" => "#A9A9A9",
        "yellow" => "#FFFF00",
        "cyan" => "#00FFFF",
        "magenta" => "#FF00FF",
        "orange" => "#FFA500",
        "pink" => "#FFC0CB",
        _ => "#000000"
    };

    private static bool IsTrue(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static double ToDouble(string? value) =>
        double.TryParse((value ?? "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;

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
        public string Name = "JasperReports Report";
        public double PageWidthPt = A4WidthPt, PageHeightPt = A4HeightPt;
        public double MarginLeftPt, MarginTopPt, MarginBottomPt;
        public List<Dictionary<string, object>> Parameters = [];
        public List<Dictionary<string, object>> Fields = [];
        public List<Dictionary<string, object>> Variables = [];
        public List<Dictionary<string, object>> SubDatasets = [];
        public Dictionary<string, object> Query = [];
        public List<RawBand> Bands = [];
        public List<RawElement> Elements = [];
    }

    private sealed class RawBand
    {
        public required string Name;
        public required string Type;
        public double HeightPt;
    }

    private sealed class RawElement
    {
        public required string Name;
        public required string Type;
        public string? Band;
        public double X, Y, W, H;   // points; X/Y are band-relative (frame offset already folded in)
        public string? Text;
        public string? Expression;
        public string? FontFamily;
        public double? FontSize;
        public bool Bold, Italic, Underline, Strikeout;
        public string ForeColor = "#000000";
        public string? BackColor;
        public string TextAlign = "left";
        public double? LineWidth;
        public string? ImageDataUrl;
        public Dictionary<string, BorderStyle> Borders = new(StringComparer.OrdinalIgnoreCase);
        public string? ComponentKind;
        public string? ComponentType;
        public string? ComponentValue;
        public string? ComponentCaption;
        public Dictionary<string, object>? ComponentMetadata;
        public List<List<string>>? TableRows;
        public List<double>? TableColumnWidths;
        public bool TableHasHeader;
    }

    private sealed class BorderStyle
    {
        public double? Width;
        public string? Color;
        public string? Style;
    }
}
