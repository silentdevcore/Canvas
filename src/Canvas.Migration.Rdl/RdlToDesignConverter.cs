using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Net;
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
        report.ReportParameters = ParseReportParameters(root);
        report.ReportParametersLayout = ParseReportParametersLayout(root);

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
            ParseVisibility(item, raw);
            ParsePaginationMetadata(item, raw);
            raw.Filters = ParseFilters(item);
            raw.NavigationMetadata = ParseNavigationMetadata(item);

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
                    ParseTablix(item, raw, report);
                    break;
                case "Chart":
                    ParseNativeChart(item, raw);
                    break;
                case "Map":
                    ParseMap(item, raw);
                    break;
                case "GaugePanel":
                    ParseGaugePanel(item, raw);
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
            raw.RichTextHtml = BuildRichTextHtml(paragraphs);
        }

        ClassifyValue(raw, ExtractTextboxValue(el));
    }

    private static string? BuildRichTextHtml(XElement paragraphs)
    {
        var paragraphParts = new List<string>();
        var hasMultipleRuns = false;

        foreach (var paragraph in Children(paragraphs, "Paragraph"))
        {
            var runs = paragraph.Descendants().Where(e => e.Name.LocalName == "TextRun").ToList();
            if (runs.Count > 1) hasMultipleRuns = true;
            if (runs.Count == 0) continue;

            var spans = new List<string>();
            foreach (var run in runs)
            {
                var display = CellDisplay(Child(run, "Value")?.Value);
                var encoded = WebUtility.HtmlEncode(display);
                var style = RichTextRunStyle(Child(run, "Style"));
                if (style.Length > 0)
                {
                    spans.Add($"""<span style="{style}">{encoded}</span>""");
                }
                else
                {
                    spans.Add(encoded);
                }
            }

            var paragraphStyle = RichTextParagraphStyle(Child(paragraph, "Style"));
            paragraphParts.Add(paragraphStyle.Length > 0
                ? $"""<p style="{paragraphStyle}">{string.Concat(spans)}</p>"""
                : $"<p>{string.Concat(spans)}</p>");
        }

        return hasMultipleRuns ? string.Concat(paragraphParts) : null;
    }

    private static string RichTextParagraphStyle(XElement? style)
    {
        if (Child(style, "TextAlign")?.Value is not { Length: > 0 } align) return "";
        return $"text-align:{ParseAlignment(align)}";
    }

    private static string RichTextRunStyle(XElement? style)
    {
        var parts = new List<string>();
        if (Child(style, "FontFamily")?.Value is { Length: > 0 } family)
            parts.Add($"font-family:{CssValue(family)}");
        if (LengthToPt(Child(style, "FontSize")?.Value) is var size and > 0)
            parts.Add($"font-size:{size.ToString("0.###", CultureInfo.InvariantCulture)}pt");
        if (Child(style, "FontWeight")?.Value is { } weight && IsBoldWeight(weight))
            parts.Add("font-weight:bold");
        if (Child(style, "FontStyle")?.Value is { } fontStyle && fontStyle.Contains("Italic", StringComparison.OrdinalIgnoreCase))
            parts.Add("font-style:italic");
        if (Child(style, "Color")?.Value is { Length: > 0 } color)
            parts.Add($"color:{NormalizeColor(color)}");
        if (Child(style, "TextDecoration")?.Value is { } deco)
        {
            var decorations = string.Join(" ", new[] {
                deco.Contains("Underline", StringComparison.OrdinalIgnoreCase) ? "underline" : null,
                deco.Contains("LineThrough", StringComparison.OrdinalIgnoreCase) ? "line-through" : null
            }.Where(v => v is not null));
            if (decorations.Length > 0) parts.Add($"text-decoration:{decorations}");
        }
        return string.Join(";", parts);
    }

    private static string CssValue(string value) => value.Contains(' ') ? $"'{value.Replace("'", "\\'", StringComparison.Ordinal)}'" : value;

    // The display value of a Textbox: 2016 concatenates <TextRun><Value>s; 2008 uses a direct <Value>.
    private static string? ExtractTextboxValue(XElement? textbox)
    {
        if (textbox is null) return null;
        var paragraphs = textbox.Elements().FirstOrDefault(e => e.Name.LocalName == "Paragraphs");
        if (paragraphs is not null)
        {
            var runs = paragraphs.Descendants()
                .Where(e => e.Name.LocalName == "TextRun")
                .ToList();
            var runValues = runs
                .Select(r =>
                {
                    var value = r.Elements().FirstOrDefault(e => e.Name.LocalName == "Value")?.Value ?? "";
                    return runs.Count > 1 ? CellDisplay(value) : value;
                });
            var joined = string.Concat(runValues);
            return joined.Length > 0 ? joined : null;
        }
        return Child(textbox, "Value")?.Value;
    }

    private void ParseImage(XElement el, RawElement raw)
    {
        var source = Child(el, "Source")?.Value;
        var value = Child(el, "Value")?.Value;
        raw.ImageSource = source;
        raw.ImageValue = value;
        if (string.Equals(source, "Embedded", StringComparison.OrdinalIgnoreCase)
            && value is not null && _embedded.TryGetValue(value, out var dataUrl))
        {
            raw.ImageDataUrl = dataUrl;
        }
        // External/Database/unresolved embedded → null → placeholder warning in BuildDesign.
    }

    private static void ParseVisibility(XElement el, RawElement raw)
    {
        var hidden = Child(Child(el, "Visibility"), "Hidden")?.Value.Trim();
        if (string.IsNullOrWhiteSpace(hidden)) return;

        if (bool.TryParse(hidden, out var hiddenValue))
        {
            raw.Hidden = hiddenValue;
            return;
        }

        raw.HiddenExpression = NormalizeRdlExpression(hidden);
    }

    private static void ParseNativeChart(XElement el, RawElement raw)
    {
        raw.CustomType = "Chart";
        raw.CustomProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var seriesList = ChartSeriesItems(el).ToList();
        if (seriesList.Count > 0)
        {
            var first = seriesList[0];
            AddText(raw.CustomProps, "SeriesName", Attr(first, "Name"));
            AddText(raw.CustomProps, "ChartType", Child(first, "Type")?.Value);
            AddText(raw.CustomProps, "Value", ChartSeriesY(first));
            raw.ChartSeries = seriesList.Select(series => new RdlChartSeries(
                Attr(series, "Name") ?? "Series",
                Child(series, "Type")?.Value ?? "",
                ChartSeriesY(series),
                ChartSeriesX(series),
                ChartSeriesSize(series))).ToList();
        }

        AddText(raw.CustomProps, "Category", el.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "ChartCategoryHierarchy")?
            .Descendants().FirstOrDefault(e => e.Name.LocalName == "Label")?.Value);
        AddText(raw.CustomProps, "Title", el.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "ChartTitle")?
            .Descendants().FirstOrDefault(e => e.Name.LocalName == "Caption")?.Value);
        AddText(raw.CustomProps, "DataSetName", Child(el, "DataSetName")?.Value);
    }

    private void ParseGaugePanel(XElement el, RawElement raw)
    {
        ApplyStyle(raw, Child(el, "Style"));

        var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        AddText(metadata, "DataSetName", Child(el, "DataSetName")?.Value);

        var radialGauges = Children(Child(el, "RadialGauges"), "RadialGauge").ToList();
        var linearGauges = Children(Child(el, "LinearGauges"), "LinearGauge").ToList();
        if (radialGauges.Count > 0) metadata["GaugeType"] = "Radial";
        else if (linearGauges.Count > 0) metadata["GaugeType"] = "Linear";

        var gauges = radialGauges.Concat(linearGauges).Select(ParseGaugeSummary).ToArray();
        if (gauges.Length > 0)
            metadata["Gauges"] = gauges;

        var labels = Children(Child(el, "GaugeLabels"), "GaugeLabel")
            .Select(label => new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Name"] = Attr(label, "Name") ?? "GaugeLabel",
                ["Text"] = Child(label, "Text")?.Value ?? "",
                ["ParentItem"] = Child(label, "ParentItem")?.Value ?? ""
            })
            .Where(label => ((string)label["Text"]).Length > 0 || ((string)label["ParentItem"]).Length > 0)
            .ToArray();
        if (labels.Length > 0)
            metadata["Labels"] = labels;

        raw.GaugePanelMetadata = metadata;
    }

    private void ParseMap(XElement el, RawElement raw)
    {
        ApplyStyle(raw, Child(el, "Style"));

        var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        AddText(metadata, "ToolTip", Child(el, "ToolTip")?.Value);

        var layers = Children(Child(el, "MapLayers"), "MapPolygonLayer")
            .Concat(Children(Child(el, "MapLayers"), "MapPointLayer"))
            .Concat(Children(Child(el, "MapLayers"), "MapLineLayer"))
            .Select(ParseMapLayerSummary)
            .ToArray();
        if (layers.Length > 0)
            metadata["Layers"] = layers;

        var dataRegions = Children(Child(el, "MapDataRegions"), "MapDataRegion")
            .Select(region =>
            {
                var r = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Name"] = Attr(region, "Name") ?? "MapDataRegion"
                };
                AddText(r, "DataSetName", Child(region, "DataSetName")?.Value);
                var group = Descendant(region, "Group");
                AddText(r, "GroupName", group is null ? null : Attr(group, "Name"));
                var filters = ParseFilters(region);
                if (filters.Count > 0)
                    r["Filters"] = filters.ToArray();
                return r;
            })
            .ToArray();
        if (dataRegions.Length > 0)
            metadata["DataRegions"] = dataRegions;

        if (Child(el, "MapViewport") is { } viewport)
            metadata["Viewport"] = ParseMapViewportSummary(viewport);

        var legends = Children(Child(el, "MapLegends"), "MapLegend")
            .Select(legend => Attr(legend, "Name") ?? "MapLegend")
            .ToArray();
        if (legends.Length > 0)
            metadata["Legends"] = legends;

        var titles = Children(Child(el, "MapTitles"), "MapTitle")
            .Select(title =>
            {
                var t = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Name"] = Attr(title, "Name") ?? "MapTitle"
                };
                AddText(t, "Text", Child(title, "Text")?.Value);
                return t;
            })
            .ToArray();
        if (titles.Length > 0)
            metadata["Titles"] = titles;

        if (Child(el, "MapDistanceScale") is not null)
            metadata["HasDistanceScale"] = true;
        if (Child(el, "MapColorScale") is not null)
            metadata["HasColorScale"] = true;

        raw.MapMetadata = metadata;
    }

    private static Dictionary<string, object> ParseMapLayerSummary(XElement layer)
    {
        var summary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = Attr(layer, "Name") ?? layer.Name.LocalName,
            ["Kind"] = layer.Name.LocalName
        };
        AddText(summary, "MapDataRegionName", Child(layer, "MapDataRegionName")?.Value);

        var bindings = Children(Child(layer, "MapBindingFieldPairs"), "MapBindingFieldPair")
            .Select(pair =>
            {
                var b = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                AddText(b, "FieldName", Child(pair, "FieldName")?.Value);
                AddText(b, "BindingExpression", Child(pair, "BindingExpression")?.Value);
                return b;
            })
            .Where(b => b.Count > 0)
            .ToArray();
        if (bindings.Length > 0)
            summary["BindingFieldPairs"] = bindings;

        var fields = Children(Child(layer, "MapFieldDefinitions"), "MapFieldDefinition")
            .Select(field =>
            {
                var f = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                AddText(f, "Name", Child(field, "Name")?.Value);
                AddText(f, "DataType", Child(field, "DataType")?.Value);
                return f;
            })
            .Where(f => f.Count > 0)
            .ToArray();
        if (fields.Length > 0)
            summary["FieldDefinitions"] = fields;

        var ruleKinds = layer.Descendants()
            .Where(e => e.Name.LocalName.StartsWith("Map", StringComparison.Ordinal)
                && e.Name.LocalName.EndsWith("Rule", StringComparison.Ordinal))
            .Select(e => e.Name.LocalName)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ruleKinds.Length > 0)
            summary["RuleKinds"] = ruleKinds;

        var spatialElementCount =
            Children(Child(layer, "MapPolygons"), "MapPolygon").Count()
            + Children(Child(layer, "MapPoints"), "MapPoint").Count()
            + Children(Child(layer, "MapLines"), "MapLine").Count();
        if (spatialElementCount > 0)
            summary["SpatialElementCount"] = spatialElementCount;

        return summary;
    }

    private static Dictionary<string, object> ParseMapViewportSummary(XElement viewport)
    {
        var summary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        AddText(summary, "CoordinateSystem", Child(viewport, "MapCoordinateSystem")?.Value);
        AddText(summary, "Projection", Child(viewport, "MapProjection")?.Value);
        AddText(summary, "MaximumZoom", Child(viewport, "MaximumZoom")?.Value);

        if (Child(viewport, "MapCustomView") is { } customView)
        {
            var view = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            AddText(view, "CenterX", Child(customView, "CenterX")?.Value);
            AddText(view, "CenterY", Child(customView, "CenterY")?.Value);
            AddText(view, "Zoom", Child(customView, "Zoom")?.Value);
            if (view.Count > 0)
                summary["CustomView"] = view;
        }

        return summary;
    }

    private static Dictionary<string, object> ParseGaugeSummary(XElement gauge)
    {
        var summary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = Attr(gauge, "Name") ?? gauge.Name.LocalName,
            ["Kind"] = gauge.Name.LocalName
        };

        var scales = gauge.Descendants()
            .Where(e => e.Name.LocalName is "RadialScale" or "LinearScale")
            .Select(ParseGaugeScaleSummary)
            .ToArray();
        if (scales.Length > 0)
            summary["Scales"] = scales;

        return summary;
    }

    private static Dictionary<string, object> ParseGaugeScaleSummary(XElement scale)
    {
        var summary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = Attr(scale, "Name") ?? scale.Name.LocalName,
            ["Kind"] = scale.Name.LocalName
        };
        AddText(summary, "MinimumValue", Child(Child(scale, "MinimumValue"), "Value")?.Value);
        AddText(summary, "MaximumValue", Child(Child(scale, "MaximumValue"), "Value")?.Value);
        AddText(summary, "Interval", Child(scale, "Interval")?.Value);

        var pointers = scale.Descendants()
            .Where(e => e.Name.LocalName is "RadialPointer" or "LinearPointer")
            .Select(pointer =>
            {
                var p = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Name"] = Attr(pointer, "Name") ?? pointer.Name.LocalName,
                    ["Kind"] = pointer.Name.LocalName
                };
                AddText(p, "Type", Child(pointer, "Type")?.Value);
                AddText(p, "MarkerStyle", Child(pointer, "MarkerStyle")?.Value);
                AddText(p, "Value", Child(Child(pointer, "GaugeInputValue"), "Value")?.Value);
                if (Descendant(Child(pointer, "Style"), "BackgroundColor")?.Value is { Length: > 0 } color)
                    p["BackgroundColor"] = color.Trim();
                return p;
            })
            .ToArray();
        if (pointers.Length > 0)
            summary["Pointers"] = pointers;

        var ranges = Children(Child(scale, "ScaleRanges"), "ScaleRange")
            .Select(range =>
            {
                var r = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Name"] = Attr(range, "Name") ?? "ScaleRange"
                };
                AddText(r, "StartValue", Child(Child(range, "StartValue"), "Value")?.Value);
                AddText(r, "EndValue", Child(Child(range, "EndValue"), "Value")?.Value);
                if (Descendant(Child(range, "Style"), "BackgroundColor")?.Value is { Length: > 0 } color)
                    r["BackgroundColor"] = color.Trim();
                return r;
            })
            .ToArray();
        if (ranges.Length > 0)
            summary["Ranges"] = ranges;

        return summary;
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

    private void ParseTablix(XElement el, RawElement raw, RawReport report)
    {
        var grid = new List<List<string>>();
        var tablixBody = Child(el, "TablixBody");
        XElement? headerRow;
        raw.DataSetName = Child(el, "DataSetName")?.Value.Trim();

        if (tablixBody is not null)
        {
            // 2016 Tablix
            raw.ColumnWidthsPt = ParseColumnWidths(Children(Child(tablixBody, "TablixColumns"), "TablixColumn"), raw.W);

            var rows = Children(Child(tablixBody, "TablixRows"), "TablixRow").ToList();
            headerRow = rows.FirstOrDefault();
            foreach (var row in rows)
                grid.Add(Children(Child(row, "TablixCells"), "TablixCell")
                    .Select(cell => CellDisplay(ExtractTextboxValue(TablixCellTextbox(cell)))).ToList());

            raw.TablixColumnHierarchy = ParseTablixHierarchy(el, "TablixColumnHierarchy");
            raw.TablixRowHierarchy = ParseTablixHierarchy(el, "TablixRowHierarchy");
            raw.TableHeaderRow = DetermineTablixHeaderRow(raw.TablixRowHierarchy);
            raw.TablixGroups = ParseTablixGroups(el);
            raw.TablixSorts = ParseTablixSorts(el);
            raw.TablixKeepWithGroups = ParseTablixKeepWithGroups(el);
            raw.TablixGroupFilters = ParseTablixGroupFilters(el);
            raw.TablixNavigationMetadata = ParseTablixNavigationMetadata(el);
            ExtractNestedTablixItems(raw, report, rows, true);
        }
        else
        {
            // 2008 Table
            raw.ColumnWidthsPt = ParseColumnWidths(Children(Child(el, "TableColumns"), "TableColumn"), raw.W);

            var rows = TableRows(Child(el, "Header")).Concat(TableRows(Child(el, "Details"))).ToList();
            headerRow = rows.FirstOrDefault();
            foreach (var row in rows)
                grid.Add(Children(Child(row, "TableCells"), "TableCell")
                    .Select(cell => CellDisplay(ExtractTextboxValue(TableCellTextbox(cell)))).ToList());

            raw.TableHeaderRow = Child(el, "Header") is not null;
            raw.TablixGroups = ParseTablixGroups(el);
            raw.TablixSorts = ParseTablixSorts(el);
            raw.TablixKeepWithGroups = ParseTablixKeepWithGroups(el);
            raw.TablixGroupFilters = ParseTablixGroupFilters(el);
            raw.TablixNavigationMetadata = ParseTablixNavigationMetadata(el);
            ExtractNestedTablixItems(raw, report, rows, false);
        }

        raw.TableCells = grid.Count > 0 ? grid : null;
        raw.ColumnAlignments = headerRow is null ? null : HeaderAlignments(headerRow, tablixBody is not null);
        raw.PaginationMetadata["TablixMemberRepeatOnNewPage"] = ParseTablixMemberValues(el, "RepeatOnNewPage");
        raw.PaginationMetadata["TablixMemberKeepTogether"] = ParseTablixMemberValues(el, "KeepTogether");
        raw.PaginationMetadata["TablixMemberFixedData"] = ParseTablixMemberValues(el, "FixedData");
    }

    private void ExtractNestedTablixItems(RawElement table, RawReport report, IReadOnlyList<XElement> rows, bool tablix)
    {
        if (rows.Count == 0) return;

        var columnCount = rows
            .Select(row => tablix ? Children(Child(row, "TablixCells"), "TablixCell").Count() : Children(Child(row, "TableCells"), "TableCell").Count())
            .DefaultIfEmpty(0)
            .Max();
        if (columnCount == 0) return;

        var widths = FitWidths(table.ColumnWidthsPt, columnCount, table.W);
        var rowHeights = rows.Select(row => LengthToPt(Child(row, "Height")?.Value)).ToArray();

        var y = 0.0;
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var cells = (tablix ? Children(Child(rows[rowIndex], "TablixCells"), "TablixCell") : Children(Child(rows[rowIndex], "TableCells"), "TableCell")).ToList();
            var x = 0.0;
            for (var columnIndex = 0; columnIndex < cells.Count; columnIndex++)
            {
                var cellItems = NestedCellItems(cells[columnIndex], tablix)
                    .Where(item => item.Name.LocalName != "Textbox")
                    .ToList();
                foreach (var item in cellItems)
                {
                    var nested = ParseNestedCellItem(item, table, report, cells[columnIndex], rowIndex, columnIndex, x, y);
                    if (nested is null) continue;
                    report.Elements.Add(nested);
                    table.TablixNestedItemNames.Add(nested.Name);
                    table.TablixNestedItemLayouts.Add(NestedCellItemLayout(table, nested));
                }

                x += columnIndex < widths.Length ? widths[columnIndex] : 0;
            }

            y += rowIndex < rowHeights.Length && rowHeights[rowIndex] > 0 ? rowHeights[rowIndex] : 0;
        }
    }

    private RawElement? ParseNestedCellItem(
        XElement item,
        RawElement table,
        RawReport report,
        XElement cell,
        int rowIndex,
        int columnIndex,
        double cellX,
        double cellY)
    {
        var type = item.Name.LocalName;
        var cellContents = Child(cell, "CellContents");
        var raw = new RawElement
        {
            Name = Attr(item, "Name") ?? $"{table.Name}_r{rowIndex}_c{columnIndex}_{type}",
            Type = type,
            Region = table.Region,
            X = table.X + cellX + LengthToPt(Child(item, "Left")?.Value),
            Y = table.Y + cellY + LengthToPt(Child(item, "Top")?.Value),
            W = LengthToPt(Child(item, "Width")?.Value),
            H = LengthToPt(Child(item, "Height")?.Value),
            ParentTablixName = table.Name,
            ParentTablixRow = rowIndex,
            ParentTablixColumn = columnIndex,
            ParentTablixRowSpan = IntValue(Child(cellContents, "RowSpan")?.Value),
            ParentTablixColumnSpan = IntValue(Child(cellContents, "ColSpan")?.Value),
            ParentTablixRepeatScope = ParentTablixRepeatScope(table, rowIndex, columnIndex)
        };
        ParseVisibility(item, raw);
        ParsePaginationMetadata(item, raw);
        raw.Filters = ParseFilters(item);
        raw.NavigationMetadata = ParseNavigationMetadata(item);

        switch (type)
        {
            case "Line":
            case "Rectangle":
                ApplyStyle(raw, Child(item, "Style"));
                break;
            case "Image":
                ParseImage(item, raw);
                break;
            case "Chart":
                ParseNativeChart(item, raw);
                break;
            case "Map":
                ParseMap(item, raw);
                break;
            case "GaugePanel":
                ParseGaugePanel(item, raw);
                break;
            case "Tablix":
            case "Table":
                ParseTablix(item, raw, report);
                break;
            case "CustomReportItem":
                raw.CustomType = Child(item, "Type")?.Value;
                raw.CustomProps = ParseCustomProperties(item);
                break;
            default:
                return null;
        }

        return raw;
    }

    private static Dictionary<string, object> ParentTablixRepeatScope(RawElement table, int rowIndex, int columnIndex)
    {
        var scope = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["parent"] = table.Name,
            ["row"] = rowIndex,
            ["column"] = columnIndex
        };

        if (table.TablixRowHierarchy is { Count: > 0 })
            scope["rowHierarchy"] = table.TablixRowHierarchy.Select(TablixMemberMetadataStyle).ToArray();
        if (table.TablixColumnHierarchy is { Count: > 0 })
            scope["columnHierarchy"] = table.TablixColumnHierarchy.Select(TablixMemberMetadataStyle).ToArray();
        var groups = RepeatScopeGroups(table).ToArray();
        if (groups.Length > 0)
            scope["groups"] = groups;

        return scope;
    }

    private static IEnumerable<Dictionary<string, object>> RepeatScopeGroups(RawElement table)
    {
        var members = (table.TablixRowHierarchy ?? [])
            .Concat(table.TablixColumnHierarchy ?? [])
            .Where(m => !m.IsStatic && !string.IsNullOrWhiteSpace(m.GroupName));

        foreach (var member in members)
        {
            var item = new Dictionary<string, object>
            {
                ["name"] = member.GroupName!
            };
            if (member.GroupExpressions.Length > 0)
                item["expressions"] = member.GroupExpressions;
            if (member.SortExpressions.Length > 0)
                item["sortExpressions"] = member.SortExpressions;
            yield return item;
        }
    }

    private static int? IntValue(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : null;

    private static IEnumerable<XElement> NestedCellItems(XElement cell, bool tablix)
    {
        if (tablix)
        {
            var contents = Child(cell, "CellContents");
            return contents is null ? Enumerable.Empty<XElement>() : contents.Elements();
        }

        return Child(cell, "ReportItems")?.Elements() ?? Enumerable.Empty<XElement>();
    }

    private static void ParsePaginationMetadata(XElement item, RawElement raw)
    {
        var metadata = raw.PaginationMetadata;
        AddText(metadata, "PageName", Child(item, "PageName")?.Value);
        AddText(metadata, "KeepTogether", Child(item, "KeepTogether")?.Value);
        AddText(metadata, "RepeatWith", Child(item, "RepeatWith")?.Value);
        AddText(metadata, "RepeatOnNewPage", Child(item, "RepeatOnNewPage")?.Value);
        AddText(metadata, "FixedData", Child(item, "FixedData")?.Value);

        var pageBreak = Child(item, "PageBreak");
        if (pageBreak is not null)
        {
            AddText(metadata, "PageBreak.BreakLocation", Child(pageBreak, "BreakLocation")?.Value);
            AddText(metadata, "PageBreak.Disabled", Child(pageBreak, "Disabled")?.Value);
            AddText(metadata, "PageBreak.ResetPageNumber", Child(pageBreak, "ResetPageNumber")?.Value);
        }
    }

    private static List<Dictionary<string, object>> ParseReportParameters(XElement root)
    {
        var parameters = new List<Dictionary<string, object>>();
        foreach (var parameter in Children(Child(root, "ReportParameters"), "ReportParameter"))
        {
            var p = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Name"] = Attr(parameter, "Name") ?? "Parameter"
            };
            AddText(p, "DataType", Child(parameter, "DataType")?.Value);
            AddText(p, "Prompt", Child(parameter, "Prompt")?.Value);
            AddText(p, "Nullable", Child(parameter, "Nullable")?.Value);
            AddText(p, "AllowBlank", Child(parameter, "AllowBlank")?.Value);
            AddText(p, "MultiValue", Child(parameter, "MultiValue")?.Value);
            AddText(p, "Hidden", Child(parameter, "Hidden")?.Value);
            AddText(p, "UsedInQuery", Child(parameter, "UsedInQuery")?.Value);
            AddText(p, "DefaultValue", ParameterValueSummary(Child(parameter, "DefaultValue")));
            AddText(p, "ValidValues", ParameterValueSummary(Child(parameter, "ValidValues")));
            parameters.Add(p);
        }
        return parameters;
    }

    private static List<Dictionary<string, object>> ParseReportParametersLayout(XElement root)
    {
        var layout = new List<Dictionary<string, object>>();
        foreach (var item in Descendant(root, "ReportParametersLayout")?.Descendants()
            .Where(e => e.Name.LocalName == "ParameterName") ?? Enumerable.Empty<XElement>())
        {
            var entry = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["ParameterName"] = item.Value.Trim()
            };
            layout.Add(entry);
        }
        return layout;
    }

    private static string? ParameterValueSummary(XElement? container)
    {
        if (container is null) return null;
        var dataSetRef = Descendant(container, "DataSetReference");
        if (dataSetRef is not null)
        {
            var parts = new[]
            {
                Child(dataSetRef, "DataSetName")?.Value,
                Child(dataSetRef, "ValueField")?.Value,
                Child(dataSetRef, "LabelField")?.Value
            }.Where(v => !string.IsNullOrWhiteSpace(v));
            return $"DataSetReference:{string.Join("|", parts)}";
        }

        var values = container.Descendants()
            .Where(e => e.Name.LocalName == "Value")
            .Select(e => e.Value.Trim())
            .Where(v => v.Length > 0)
            .ToArray();
        return values.Length > 0 ? string.Join("; ", values) : null;
    }

    private static List<Dictionary<string, object>> ParseFilters(XElement el)
    {
        var filters = new List<Dictionary<string, object>>();
        foreach (var filter in Children(Child(el, "Filters"), "Filter"))
        {
            var f = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            AddText(f, "FilterExpression", Child(filter, "FilterExpression")?.Value);
            AddText(f, "Operator", Child(filter, "Operator")?.Value);
            var values = Children(Child(filter, "FilterValues"), "FilterValue")
                .Select(v => v.Value.Trim())
                .Where(v => v.Length > 0)
                .ToArray();
            if (values.Length > 0)
                f["FilterValues"] = values;
            if (f.Count > 0)
                filters.Add(f);
        }
        return filters;
    }

    private static Dictionary<string, object>? ParseNavigationMetadata(XElement item)
    {
        var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        AddText(metadata, "Bookmark", Child(item, "Bookmark")?.Value);
        AddText(metadata, "DocumentMapLabel", Child(item, "DocumentMapLabel")?.Value);

        var actions = ParseActions(Child(item, "ActionInfo"));
        if (actions.Count > 0)
            metadata["Actions"] = actions.ToArray();

        return metadata.Count > 0 ? metadata : null;
    }

    private static List<Dictionary<string, object>> ParseActions(XElement? actionInfo)
    {
        var actions = new List<Dictionary<string, object>>();
        foreach (var action in Children(Child(actionInfo, "Actions"), "Action"))
        {
            var a = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            AddText(a, "Hyperlink", Child(action, "Hyperlink")?.Value);
            AddText(a, "BookmarkLink", Child(action, "BookmarkLink")?.Value);

            if (Child(action, "Drillthrough") is { } drillthrough)
            {
                var drill = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                AddText(drill, "ReportName", Child(drillthrough, "ReportName")?.Value);
                var parameters = Children(Child(drillthrough, "Parameters"), "Parameter")
                    .Select(parameter =>
                    {
                        var p = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Name"] = Attr(parameter, "Name") ?? ""
                        };
                        AddText(p, "Value", Child(parameter, "Value")?.Value);
                        return p;
                    })
                    .ToArray();
                if (parameters.Length > 0)
                    drill["Parameters"] = parameters;
                if (drill.Count > 0)
                    a["Drillthrough"] = drill;
            }

            if (a.Count > 0)
                actions.Add(a);
        }
        return actions;
    }

    private static List<Dictionary<string, object>> ParseTablixNavigationMetadata(XElement tablix)
    {
        var result = new List<Dictionary<string, object>>();
        foreach (var group in tablix.Descendants().Where(e => e.Name.LocalName == "Group"))
        {
            var item = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            AddText(item, "GroupName", Attr(group, "Name"));
            AddText(item, "DocumentMapLabel", Child(group, "DocumentMapLabel")?.Value);
            AddText(item, "Bookmark", Child(group, "Bookmark")?.Value);
            if (item.Count > 1)
                result.Add(item);
        }

        foreach (var visibility in tablix.Descendants().Where(e => e.Name.LocalName == "Visibility"))
        {
            if (Child(visibility, "ToggleItem")?.Value is not { Length: > 0 } toggleItem)
                continue;
            result.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["ToggleItem"] = toggleItem.Trim()
            });
        }

        return result;
    }

    private static string[] ParseTablixMemberValues(XElement tablix, string name) =>
        tablix.Descendants()
            .Where(e => e.Name.LocalName == "TablixMember")
            .Select(m => Child(m, name)?.Value.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static List<RdlTablixMemberMetadata> ParseTablixHierarchy(XElement tablix, string hierarchyName)
    {
        var hierarchy = Child(tablix, hierarchyName);
        var rootMembers = Child(hierarchy, "TablixMembers");
        var members = new List<RdlTablixMemberMetadata>();
        AddTablixMembers(rootMembers, members, level: 0, parentPath: "");
        return members;
    }

    private static void AddTablixMembers(
        XElement? membersElement,
        List<RdlTablixMemberMetadata> result,
        int level,
        string parentPath)
    {
        if (membersElement is null) return;

        var members = Children(membersElement, "TablixMember").ToList();
        for (var index = 0; index < members.Count; index++)
        {
            var member = members[index];
            var group = Child(member, "Group");
            var groupExpressions = Children(Child(group, "GroupExpressions"), "GroupExpression")
                .Select(e => e.Value.Trim())
                .Where(v => v.Length > 0)
                .ToArray();
            var sortExpressions = member.Descendants()
                .Where(e => e.Name.LocalName == "SortExpression")
                .Select(e => Child(e, "Value")?.Value.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Cast<string>()
                .ToArray();
            var header = Child(member, "TablixHeader");
            var path = string.IsNullOrEmpty(parentPath) ? index.ToString(CultureInfo.InvariantCulture) : $"{parentPath}.{index}";
            result.Add(new RdlTablixMemberMetadata(
                Level: level,
                Index: index,
                Path: path,
                IsStatic: group is null,
                GroupName: group is null ? null : Attr(group, "Name"),
                GroupExpressions: groupExpressions,
                SortExpressions: sortExpressions,
                KeepWithGroup: Child(member, "KeepWithGroup")?.Value.Trim(),
                RepeatOnNewPage: Child(member, "RepeatOnNewPage")?.Value.Trim(),
                FixedData: Child(member, "FixedData")?.Value.Trim(),
                HeaderText: CellDisplay(ExtractTextboxValue(TablixHeaderTextbox(header))),
                HeaderSizePt: LengthToPt(Child(header, "Size")?.Value)));

            AddTablixMembers(Child(member, "TablixMembers"), result, level + 1, path);
        }
    }

    private static bool? DetermineTablixHeaderRow(IReadOnlyList<RdlTablixMemberMetadata>? rowHierarchy)
    {
        if (rowHierarchy is not { Count: > 0 }) return null;

        var topLevel = rowHierarchy.Where(m => m.Level == 0).OrderBy(m => m.Index).ToList();
        if (topLevel.Count == 0) return null;

        var firstGroupIndex = topLevel.FindIndex(m => !m.IsStatic);
        var candidates = firstGroupIndex >= 0
            ? topLevel.Where(m => m.IsStatic && m.Index < firstGroupIndex)
            : topLevel.Where(m => m.IsStatic);

        return candidates.Any(m =>
            string.Equals(m.KeepWithGroup, "After", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(m.HeaderText));
    }

    private static void AddText(Dictionary<string, object> target, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            target[key] = value.Trim();
    }

    private static void AddText(Dictionary<string, string> target, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            target[key] = value.Trim();
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

    private static List<Dictionary<string, object>> ParseTablixGroupFilters(XElement tablix)
    {
        var result = new List<Dictionary<string, object>>();
        foreach (var group in tablix.Descendants().Where(e => e.Name.LocalName == "Group"))
        {
            var filters = ParseFilters(group);
            if (filters.Count == 0) continue;
            result.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["GroupName"] = Attr(group, "Name") ?? "",
                ["Filters"] = filters.ToArray()
            });
        }
        return result;
    }

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

    private static XElement? TablixHeaderTextbox(XElement? header) =>
        Child(Child(header, "CellContents"), "Textbox");

    private static XElement? TableCellTextbox(XElement cell) =>
        Children(Child(cell, "ReportItems"), "Textbox").FirstOrDefault();

    private static IEnumerable<XElement> ChartSeriesItems(XElement chart) =>
        chart.Descendants().Where(e => e.Name.LocalName == "ChartSeries");

    private static string? ChartSeriesY(XElement series) =>
        series.Descendants().FirstOrDefault(e => e.Name.LocalName == "Y")?.Value;

    private static string? ChartSeriesX(XElement series) =>
        series.Descendants().FirstOrDefault(e => e.Name.LocalName == "X")?.Value;

    private static string? ChartSeriesSize(XElement series) =>
        series.Descendants().FirstOrDefault(e => e.Name.LocalName == "Size")?.Value;

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
            ApplyVisibility(element, raw, diagnostics);
            ApplyPaginationMetadata(element, raw, diagnostics);
            ApplyNestedTablixMetadata(element, raw, diagnostics);
            ApplyFilterMetadata(element, raw, diagnostics);
            ApplyNavigationMetadata(element, raw, diagnostics);

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
            PageSettings = BuildPageSettings(report, diagnostics),
            Pages = [new PageDto { Id = "page-1", Elements = elements }],
            SharedElements = sharedElements
        };

        return new RdlConvertResult { Design = design, Diagnostics = diagnostics };
    }

    private static PageSettingsDto BuildPageSettings(RawReport report, List<MigrationDiagnostic> diagnostics)
    {
        var settings = new PageSettingsDto { Width = report.PageWidthPt, Height = report.PageHeightPt, Unit = "pt" };
        var customProperties = new List<CustomDocumentPropertyDto>();

        if (report.ReportParameters.Count > 0)
        {
            customProperties.Add(new CustomDocumentPropertyDto
            {
                Name = "rdlReportParameters",
                Type = "text",
                Value = JsonSerializer.Serialize(report.ReportParameters)
            });
            diagnostics.Add(Warn("CANMIGRDL024",
                "RDL report parameters were preserved in PageSettings.CustomProperties['rdlReportParameters']; Canvas has no native report-parameter UI yet."));
        }

        if (report.ReportParametersLayout.Count > 0)
        {
            customProperties.Add(new CustomDocumentPropertyDto
            {
                Name = "rdlReportParametersLayout",
                Type = "text",
                Value = JsonSerializer.Serialize(report.ReportParametersLayout)
            });
        }

        settings.CustomProperties = customProperties.Count > 0 ? customProperties : null;
        return settings;
    }

    private static ElementDto? MapControl(RawElement raw, double x, double y, List<MigrationDiagnostic> diagnostics)
    {
        var element = new ElementDto { Id = $"rdl-{raw.Name}", Name = raw.Name, X = x, Y = y, Width = raw.W, Height = raw.H };

        switch (raw.Type)
        {
            case "Textbox":
                if (raw.RichTextHtml is { Length: > 0 } html)
                {
                    element.Type = "richtext";
                    element.HtmlContent = html;
                    element.Content = raw.Text ?? "";
                    element.Style = BuildTextStyle(raw);
                    diagnostics.Add(Warn("CANMIGRDL016",
                        $"'{raw.Name}' contains multiple/styled text runs — imported as Canvas richtext; review inline formatting."));
                }
                else
                {
                    element.Type = "text";
                    element.Content = raw.Text ?? "";
                    element.Style = BuildTextStyle(raw);
                }
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
                {
                    element.Content = dataUrl;
                }
                else if (MapImageReference(element, raw, diagnostics))
                {
                    return element;
                }
                else
                {
                    diagnostics.Add(Warn("CANMIGRDL012",
                        $"'{raw.Name}' image isn't embeddable from source — inserted an empty image placeholder."));
                }
                return element;

            case "CustomReportItem":
                return MapCustomReportItem(raw, element, diagnostics);

            case "Chart":
                return MapRdlChart(raw, element, diagnostics);

            case "Map":
                return MapNativeMap(raw, element, diagnostics);

            case "GaugePanel":
                return MapGaugePanel(raw, element, diagnostics);

            case "Subreport":
                diagnostics.Add(Warn("CANMIGRDL011",
                    $"'{raw.Name}' is a sub-report — requires manual migration; inserted a placeholder."));
                return Placeholder(element, $"[Sub-report: {raw.Name} — migrate manually]");

            default:
                diagnostics.Add(Warn("CANMIGRDL011", $"'{raw.Name}' is a {raw.Type} — not supported by Canvas yet; inserted a placeholder."));
                return Placeholder(element, $"[{raw.Type}: migrate manually]");
        }
    }

    private static ElementDto MapNativeMap(RawElement raw, ElementDto element, List<MigrationDiagnostic> diagnostics)
    {
        element.Type = "text";
        element.Content = $"[Map: {raw.Name} — migrate manually]";
        element.Style = new Dictionary<string, object>
        {
            ["color"] = "#0F172A",
            ["backgroundColor"] = raw.BackColor ?? "#EFF6FF",
            ["borderColor"] = raw.ForeColor,
            ["borderStyle"] = "dashed",
            ["fontStyle"] = "italic",
            ["rdlCustomItemType"] = "Map"
        };
        if (raw.LineWidth is { } borderW)
            element.Style["borderWidth"] = borderW;
        if (raw.MapMetadata is { Count: > 0 })
            element.Style["rdlMap"] = raw.MapMetadata;

        diagnostics.Add(Warn("CANMIGRDL022",
            $"'{raw.Name}' native RDL Map metadata was preserved on a positioned placeholder; Canvas has no native map element yet."));
        if (MapMetadataHasFilters(raw.MapMetadata))
            diagnostics.Add(Warn("CANMIGRDL025",
                $"'{raw.Name}' MapDataRegion filters were preserved as metadata; Canvas does not evaluate report filters yet."));
        return element;
    }

    private static bool MapMetadataHasFilters(IReadOnlyDictionary<string, object>? metadata)
    {
        if (metadata is null || !metadata.TryGetValue("DataRegions", out var regionsObj)
            || regionsObj is not Dictionary<string, object>[] regions)
            return false;

        return regions.Any(region => region.ContainsKey("Filters"));
    }

    private static ElementDto MapGaugePanel(RawElement raw, ElementDto element, List<MigrationDiagnostic> diagnostics)
    {
        var gaugeType = raw.GaugePanelMetadata?.GetValueOrDefault("GaugeType")?.ToString() ?? "Gauge";
        var value = FirstGaugePointerValue(raw.GaugePanelMetadata);

        element.Type = "text";
        element.Content = string.IsNullOrWhiteSpace(value)
            ? $"[{gaugeType} Gauge: {raw.Name} — migrate manually]"
            : $"[{gaugeType} Gauge: {CellDisplay(value)}]";
        element.Style = new Dictionary<string, object>
        {
            ["color"] = "#0F172A",
            ["backgroundColor"] = raw.BackColor ?? "#F8FAFC",
            ["borderColor"] = raw.ForeColor,
            ["borderStyle"] = "dashed",
            ["fontStyle"] = "italic",
            ["rdlCustomItemType"] = "GaugePanel"
        };
        if (raw.LineWidth is { } borderW)
            element.Style["borderWidth"] = borderW;
        if (raw.GaugePanelMetadata is { Count: > 0 })
            element.Style["rdlGaugePanel"] = raw.GaugePanelMetadata;

        diagnostics.Add(Warn("CANMIGRDL021",
            $"'{raw.Name}' native RDL GaugePanel metadata was preserved on a positioned placeholder; Canvas has no native gauge element yet."));
        return element;
    }

    private static string? FirstGaugePointerValue(IReadOnlyDictionary<string, object>? metadata)
    {
        if (metadata is null || !metadata.TryGetValue("Gauges", out var gaugesObj) || gaugesObj is not Dictionary<string, object>[] gauges)
            return null;

        foreach (var gauge in gauges)
        {
            if (!gauge.TryGetValue("Scales", out var scalesObj) || scalesObj is not Dictionary<string, object>[] scales)
                continue;
            foreach (var scale in scales)
            {
                if (!scale.TryGetValue("Pointers", out var pointersObj) || pointersObj is not Dictionary<string, object>[] pointers)
                    continue;
                foreach (var pointer in pointers)
                {
                    if (pointer.TryGetValue("Value", out var value) && value?.ToString() is { Length: > 0 } text)
                        return text;
                }
            }
        }

        return null;
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

    private static bool MapImageReference(ElementDto element, RawElement raw, List<MigrationDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(raw.ImageValue)) return false;

        if (string.Equals(raw.ImageSource, "External", StringComparison.OrdinalIgnoreCase))
        {
            element.Content = raw.ImageValue;
            element.Style = new Dictionary<string, object>
            {
                ["rdlImageSource"] = "External"
            };
            diagnostics.Add(Warn("CANMIGRDL012",
                $"'{raw.Name}' external image reference was preserved; verify fetch/security behaviour in Canvas."));
            return true;
        }

        if (string.Equals(raw.ImageSource, "Database", StringComparison.OrdinalIgnoreCase))
        {
            var field = SingleFieldMatch(raw.ImageValue);
            if (field is not null)
            {
                element.Binding = field;
                element.Content = $"{{{{{field}}}}}";
            }
            else
            {
                element.Expression = raw.ImageValue;
                element.Content = raw.ImageValue;
            }

            element.Style = new Dictionary<string, object>
            {
                ["rdlImageSource"] = "Database"
            };
            diagnostics.Add(Warn("CANMIGRDL012",
                $"'{raw.Name}' database image source was preserved as binding/expression; verify runtime image data mapping."));
            return true;
        }

        return false;
    }

    private static void ApplyNestedTablixMetadata(ElementDto element, RawElement raw, List<MigrationDiagnostic> diagnostics)
    {
        if (raw.ParentTablixName is not { Length: > 0 } parent) return;

        element.Style ??= [];
        element.Style["rdlParentTablix"] = parent;
        element.Style["rdlParentTablixRow"] = raw.ParentTablixRow ?? 0;
        element.Style["rdlParentTablixColumn"] = raw.ParentTablixColumn ?? 0;
        if (raw.ParentTablixRowSpan is { } rowSpan)
            element.Style["rdlParentTablixRowSpan"] = rowSpan;
        if (raw.ParentTablixColumnSpan is { } columnSpan)
            element.Style["rdlParentTablixColumnSpan"] = columnSpan;
        if (raw.ParentTablixRepeatScope is { Count: > 0 })
            element.Style["rdlParentTablixRepeatScope"] = raw.ParentTablixRepeatScope;
        var repeat = RdlRepeatMetadata(raw);
        if (repeat is not null)
        {
            element.Style["rdlRepeat"] = repeat;
            if (repeat.TryGetValue("dataPath", out var dataPath) && dataPath is string { Length: > 0 } path)
            {
                element.Repeat = new RepeatDto
                {
                    DataPath = path,
                    TemplateId = element.Id
                };
            }
        }
        diagnostics.Add(Warn("CANMIGRDL023",
            $"'{raw.Name}' was extracted from Tablix '{parent}' cell [{raw.ParentTablixRow},{raw.ParentTablixColumn}] as a separate positioned element; RDL repeat scope was mapped to Canvas repeat metadata for review."));
    }

    private static Dictionary<string, object>? RdlRepeatMetadata(RawElement raw)
    {
        if (raw.ParentTablixRepeatScope is not { Count: > 0 } scope)
            return null;

        var groups = RepeatScopeGroups(scope).ToArray();
        var dataPath = RepeatDataPath(raw, groups);
        var repeat = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["source"] = "rdlTablix",
            ["parent"] = raw.ParentTablixName ?? "",
            ["row"] = raw.ParentTablixRow ?? 0,
            ["column"] = raw.ParentTablixColumn ?? 0,
            ["dataPath"] = dataPath,
            ["itemAlias"] = "item",
            ["indexAlias"] = "index"
        };
        if (!string.IsNullOrWhiteSpace(raw.DataSetName))
            repeat["dataSetName"] = raw.DataSetName!;
        if (groups.Length > 0)
            repeat["groups"] = groups;
        return repeat;
    }

    private static Dictionary<string, object> NestedCellItemLayout(RawElement table, RawElement nested)
    {
        var layout = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = nested.Name,
            ["type"] = nested.Type,
            ["row"] = nested.ParentTablixRow ?? 0,
            ["column"] = nested.ParentTablixColumn ?? 0,
            ["x"] = nested.X - table.X,
            ["y"] = nested.Y - table.Y,
            ["width"] = nested.W,
            ["height"] = nested.H
        };
        if (nested.ParentTablixRowSpan is { } rowSpan)
            layout["rowSpan"] = rowSpan;
        if (nested.ParentTablixColumnSpan is { } columnSpan)
            layout["columnSpan"] = columnSpan;
        if (nested.Hidden is { } hidden)
            layout["hidden"] = hidden;
        if (!string.IsNullOrWhiteSpace(nested.HiddenExpression))
            layout["hiddenExpression"] = nested.HiddenExpression!;
        if (nested.ParentTablixRepeatScope is { Count: > 0 })
            layout["repeatScope"] = nested.ParentTablixRepeatScope;
        if (RdlRepeatMetadata(nested) is { } repeat)
            layout["repeat"] = repeat;
        return layout;
    }

    private static IEnumerable<Dictionary<string, object>> RepeatScopeGroups(Dictionary<string, object> scope)
    {
        if (!scope.TryGetValue("groups", out var value) || value is null)
            yield break;

        if (value is IEnumerable<Dictionary<string, object>> typedGroups)
        {
            foreach (var group in typedGroups)
                yield return group;
            yield break;
        }

        if (value is IEnumerable<object> objectGroups)
        {
            foreach (var item in objectGroups)
            {
                if (item is Dictionary<string, object> dict)
                    yield return dict;
            }
        }
    }

    private static string RepeatDataPath(RawElement raw, IReadOnlyList<Dictionary<string, object>> groups)
    {
        if (!string.IsNullOrWhiteSpace(raw.DataSetName))
            return SafeRepeatPath(raw.DataSetName!);

        var groupName = groups
            .Select(g => g.TryGetValue("name", out var name) ? name?.ToString() : null)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
        if (!string.IsNullOrWhiteSpace(groupName))
            return SafeRepeatPath(groupName!);

        return SafeRepeatPath($"{raw.ParentTablixName ?? raw.Name}Rows");
    }

    private static string SafeRepeatPath(string value)
    {
        var cleaned = new string(value
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '_' or '.' ? ch : '_')
            .ToArray())
            .Trim('_');
        return string.IsNullOrWhiteSpace(cleaned) ? "items" : cleaned;
    }

    private static void ApplyFilterMetadata(ElementDto element, RawElement raw, List<MigrationDiagnostic> diagnostics)
    {
        if (raw.Filters.Count == 0) return;

        element.Style ??= [];
        element.Style["rdlFilters"] = raw.Filters.ToArray();
        diagnostics.Add(Warn("CANMIGRDL025",
            $"'{raw.Name}' RDL filters were preserved as metadata; Canvas does not evaluate report filters yet."));
    }

    private static void ApplyNavigationMetadata(ElementDto element, RawElement raw, List<MigrationDiagnostic> diagnostics)
    {
        if (raw.NavigationMetadata is not { Count: > 0 }) return;

        element.Style ??= [];
        element.Style["rdlNavigation"] = raw.NavigationMetadata;
        if (TryResolveNavigationHref(raw.NavigationMetadata, out var href))
        {
            element.Type = "link";
            element.Href = href;
            element.LinkTarget = href.StartsWith("#", StringComparison.Ordinal) ? "_self" : "_blank";
            element.Style["rdlNavigationMappedToLink"] = true;
        }
        diagnostics.Add(Warn("CANMIGRDL026",
            $"'{raw.Name}' RDL navigation/action metadata was preserved and mapped to a Canvas link when possible."));
    }

    private static bool TryResolveNavigationHref(Dictionary<string, object> navigation, out string href)
    {
        href = "";
        if (navigation.TryGetValue("Actions", out var actionsObj) && actionsObj is IEnumerable<object> actions)
        {
            foreach (var action in actions)
            {
                if (action is not IReadOnlyDictionary<string, object> dict)
                    continue;
                if (HeaderValue(dict, "Hyperlink") is { Length: > 0 } hyperlink)
                {
                    href = hyperlink;
                    return true;
                }
                if (HeaderValue(dict, "BookmarkLink") is { Length: > 0 } bookmark)
                {
                    href = $"#{Uri.EscapeDataString(bookmark)}";
                    return true;
                }
                if (dict.TryGetValue("Drillthrough", out var drillObj)
                    && drillObj is IReadOnlyDictionary<string, object> drill
                    && HeaderValue(drill, "ReportName") is { Length: > 0 } reportName)
                {
                    href = DrillthroughHref(reportName, drill);
                    return true;
                }
            }
        }
        if (HeaderValue(navigation, "Bookmark") is { Length: > 0 } ownBookmark)
        {
            href = $"#{Uri.EscapeDataString(ownBookmark)}";
            return true;
        }
        return false;
    }

    private static string DrillthroughHref(string reportName, IReadOnlyDictionary<string, object> drill)
    {
        var href = reportName.StartsWith("/", StringComparison.Ordinal) ? reportName : $"/{reportName}";
        var query = new List<string>();
        if (drill.TryGetValue("Parameters", out var parametersObj) && parametersObj is IEnumerable<object> parameters)
        {
            foreach (var parameter in parameters)
            {
                if (parameter is not IReadOnlyDictionary<string, object> dict)
                    continue;
                var name = HeaderValue(dict, "Name");
                var value = HeaderValue(dict, "Value");
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
                    continue;
                query.Add($"{Uri.EscapeDataString(name)}={DrillthroughParameterValue(value)}");
            }
        }
        return query.Count == 0 ? href : $"{href}?{string.Join("&", query)}";
    }

    private static string DrillthroughParameterValue(string value)
    {
        var display = CellDisplay(value);
        return display.StartsWith("{{", StringComparison.Ordinal) && display.EndsWith("}}", StringComparison.Ordinal)
            ? display
            : Uri.EscapeDataString(display);
    }

    private static string? HeaderValue(IReadOnlyDictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static ElementDto MapRdlChart(RawElement raw, ElementDto element, List<MigrationDiagnostic> diagnostics)
    {
        var props = raw.CustomProps ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        element.Type = "chart";
        element.ChartType = ChartTypeFromRdl(props.GetValueOrDefault("ChartType"));
        element.ChartData = CreateRdlChartData(raw.Name, props, raw.ChartSeries);
        element.Style = RdlCustomItemStyle("Chart", props);
        diagnostics.Add(Warn("CANMIGRDL017",
            $"'{raw.Name}' RDL chart was imported as an editable Canvas chart placeholder; review series/category/value bindings."));
        return element;
    }

    // RDL <CustomReportItem>: ActiveReports/DsReport serialize barcodes this way (Type + CustomProperties);
    // SSRS uses it for Chart/Gauge/Map/Sparkline. Map barcodes and charts where possible; keep the
    // rest visible with metadata so users can finish the migration in the designer.
    private static ElementDto? MapCustomReportItem(RawElement raw, ElementDto element, List<MigrationDiagnostic> diagnostics)
    {
        var props = raw.CustomProps ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var customType = raw.CustomType ?? "";
        var symbology = props.GetValueOrDefault("Symbology") ?? props.GetValueOrDefault("SymbologyType");
        var isBarcode = customType.Contains("Barcode", StringComparison.OrdinalIgnoreCase) || symbology is not null;
        if (!isBarcode)
        {
            if (customType.Contains("Chart", StringComparison.OrdinalIgnoreCase))
                return MapRdlChart(raw, element, diagnostics);

            if (customType.Contains("Gauge", StringComparison.OrdinalIgnoreCase))
            {
                var gauge = Placeholder(element, $"[Gauge: {raw.Name} — migrate manually]");
                gauge.Style ??= [];
                foreach (var (key, styleValue) in RdlCustomItemStyle("Gauge", props))
                    gauge.Style[key] = styleValue;
                diagnostics.Add(Warn("CANMIGRDL018",
                    $"'{raw.Name}' RDL gauge metadata was preserved on a positioned placeholder; Canvas has no native gauge element yet."));
                return gauge;
            }

            if (customType.Contains("Shape", StringComparison.OrdinalIgnoreCase) || props.ContainsKey("ShapeType"))
                return MapRdlShape(raw, element, props, diagnostics);

            if (IsDocumentCustomItem(customType))
                return MapRdlDocumentCustomItem(raw, element, customType, props, diagnostics);

            if (IsSignatureCustomItem(customType))
                return MapRdlSignatureCustomItem(raw, element, customType, props, diagnostics);

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

    private static bool IsDocumentCustomItem(string customType) =>
        customType.Contains("htmldocument", StringComparison.OrdinalIgnoreCase)
        || customType.Contains("pdfdocument", StringComparison.OrdinalIgnoreCase);

    private static bool IsSignatureCustomItem(string customType) =>
        customType.Contains("ESignature", StringComparison.OrdinalIgnoreCase)
        || customType.Contains("PDFSignature", StringComparison.OrdinalIgnoreCase)
        || customType.Contains("Signature", StringComparison.OrdinalIgnoreCase);

    private static ElementDto MapRdlDocumentCustomItem(
        RawElement raw,
        ElementDto element,
        string customType,
        IReadOnlyDictionary<string, string> props,
        List<MigrationDiagnostic> diagnostics)
    {
        var isPdf = customType.Contains("pdf", StringComparison.OrdinalIgnoreCase);
        var itemType = isPdf ? "PdfDocument" : "HtmlDocument";
        var label = isPdf ? "PDF document" : "HTML document";
        var document = Placeholder(element, $"[{label}: {raw.Name} - migrate manually]");
        document.Style ??= [];
        foreach (var (key, styleValue) in RdlCustomItemStyle(itemType, props, truncateLargeValues: true))
            document.Style[key] = styleValue;
        document.Style["rdlDocumentKind"] = isPdf ? "pdf" : "html";
        if (props.GetValueOrDefault("Source") is { Length: > 0 } source)
            document.Style["rdlDocumentSource"] = source;
        if (props.GetValueOrDefault("Sizing") is { Length: > 0 } sizing)
            document.Style["rdlDocumentSizing"] = sizing;

        diagnostics.Add(Warn("CANMIGRDL027",
            $"'{raw.Name}' RDL document custom item was preserved as a positioned placeholder; Canvas has no native embedded document item yet."));
        return document;
    }

    private static ElementDto MapRdlSignatureCustomItem(
        RawElement raw,
        ElementDto element,
        string customType,
        IReadOnlyDictionary<string, string> props,
        List<MigrationDiagnostic> diagnostics)
    {
        var isPdfSignature = customType.Contains("PDF", StringComparison.OrdinalIgnoreCase);
        element.Type = "signature";
        element.SignatureLabel = isPdfSignature ? "PDF Signature" : "Electronic Signature";
        element.Style = RdlCustomItemStyle(isPdfSignature ? "PDFSignature" : "ESignature", props, truncateLargeValues: true);
        element.Style["rdlSignatureKind"] = isPdfSignature ? "pdf" : "electronic";

        diagnostics.Add(Warn("CANMIGRDL028",
            $"'{raw.Name}' RDL signature custom item was mapped to a Canvas signature placeholder; review signing/certificate semantics."));
        return element;
    }

    private static ElementDto MapRdlShape(
        RawElement raw,
        ElementDto element,
        IReadOnlyDictionary<string, string> props,
        List<MigrationDiagnostic> diagnostics)
    {
        var shapeType = props.GetValueOrDefault("ShapeType") ?? props.GetValueOrDefault("Shape") ?? "Rectangle";
        var normalizedShape = shapeType.Trim();

        element.Type = normalizedShape switch
        {
            var s when s.Contains("Ellipse", StringComparison.OrdinalIgnoreCase) => "circle",
            var s when s.Contains("Arrow", StringComparison.OrdinalIgnoreCase) => "arrow",
            _ => "rect"
        };

        element.Style = RdlCustomItemStyle("Shape", props);
        element.Style["rdlShapeType"] = normalizedShape;

        if (ColorProp(props, "FillColor") is { } fill)
            element.Style["backgroundColor"] = fill;
        if (ColorProp(props, "LineColor") is { } line)
        {
            element.Style["borderColor"] = line;
            element.Style["color"] = line;
        }
        if (NumberProp(props, "LineWidth") is { } lineWidth)
        {
            element.Style["borderWidth"] = lineWidth;
            element.Style["strokeWidth"] = lineWidth;
        }
        if (DashStyleFromName(props.GetValueOrDefault("LineStyle")) is { } dash)
            element.Style["dashStyle"] = dash;
        if (NumberProp(props, "RotationAngle") is { } rotation)
            element.Style["rotation"] = rotation;

        if (element.Type == "arrow")
        {
            element.ArrowDirection = ArrowDirectionFromShape(normalizedShape);
            element.EndMarker = "arrow";
        }

        diagnostics.Add(Warn("CANMIGRDL020",
            $"'{raw.Name}' RDL shape custom item was imported as a Canvas {element.Type}; review geometry and rotation."));
        return element;
    }

    private static Dictionary<string, object> CreateRdlChartData(string name, IReadOnlyDictionary<string, string> props, IReadOnlyList<RdlChartSeries>? series)
    {
        var category = props.GetValueOrDefault("Category") ?? props.GetValueOrDefault("CategoryExpression") ?? "Category";
        var value = props.GetValueOrDefault("Value") ?? props.GetValueOrDefault("ValueExpression") ?? "Value";
        var chartSeries = series is { Count: > 0 }
            ? series
            : [new RdlChartSeries(props.GetValueOrDefault("SeriesName") ?? name, props.GetValueOrDefault("ChartType") ?? "", value, null, null)];
        var data = new Dictionary<string, object>
        {
            ["labels"] = new[] { CellDisplay(category) },
            ["datasets"] = chartSeries
                .Select((s, i) => new Dictionary<string, object>
                {
                    ["label"] = string.IsNullOrWhiteSpace(s.Name) ? $"{name} {i + 1}" : s.Name,
                    ["data"] = new[] { 1 },
                    ["backgroundColor"] = ChartColor(i)
                })
                .ToArray(),
            ["rdlCategoryExpression"] = category,
            ["rdlValueExpression"] = value,
            ["rdlSeries"] = chartSeries
                .Select(s => new Dictionary<string, object>
                {
                    ["name"] = s.Name,
                    ["type"] = s.Type,
                    ["x"] = s.XExpression ?? "",
                    ["y"] = s.YExpression ?? "",
                    ["size"] = s.SizeExpression ?? ""
                })
                .ToArray()
        };
        if (props.GetValueOrDefault("Title") is { Length: > 0 } title)
            data["rdlTitle"] = title;
        if (props.GetValueOrDefault("DataSetName") is { Length: > 0 } dataSetName)
            data["rdlDataSetName"] = dataSetName;
        return data;
    }

    private static string ChartTypeFromRdl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "bar";
        if (value.Contains("Line", StringComparison.OrdinalIgnoreCase)) return "line";
        if (value.Contains("Area", StringComparison.OrdinalIgnoreCase)) return "line";
        if (value.Contains("Scatter", StringComparison.OrdinalIgnoreCase) || value.Contains("Bubble", StringComparison.OrdinalIgnoreCase)) return "line";
        if (value.Contains("Polar", StringComparison.OrdinalIgnoreCase) || value.Contains("Radar", StringComparison.OrdinalIgnoreCase)) return "line";
        if (value.Contains("Pie", StringComparison.OrdinalIgnoreCase) || value.Contains("Doughnut", StringComparison.OrdinalIgnoreCase)) return "pie";
        if (value.Contains("Shape", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Funnel", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Pyramid", StringComparison.OrdinalIgnoreCase))
            return "pie";
        if (value.Contains("Range", StringComparison.OrdinalIgnoreCase)) return "bar";
        return "bar";
    }

    private static string ChartColor(int index) =>
        new[] { "#2563eb", "#16a34a", "#dc2626", "#9333ea", "#ea580c", "#0891b2" }[index % 6];

    private static Dictionary<string, object> RdlCustomItemStyle(
        string itemType,
        IReadOnlyDictionary<string, string> props,
        bool truncateLargeValues = false)
    {
        var style = new Dictionary<string, object>
        {
            ["rdlCustomItemType"] = itemType
        };
        if (props.Count > 0)
            style["rdlCustomProperties"] = props.ToDictionary(
                kvp => kvp.Key,
                kvp => RdlCustomPropertyValue(kvp.Value, truncateLargeValues),
                StringComparer.OrdinalIgnoreCase);
        return style;
    }

    private static object RdlCustomPropertyValue(string value, bool truncateLargeValues)
    {
        const int maxLength = 512;
        if (!truncateLargeValues || value.Length <= maxLength)
            return value;

        return $"{value[..maxLength]}...[truncated {value.Length - maxLength} chars]";
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
            HeaderRow = raw.TableHeaderRow ?? true
        };
        ApplyTablixMetadata(element, raw, diagnostics);
        if (raw.TablixNestedItemNames.Count > 0)
        {
            element.Style ??= [];
            element.Style["rdlExtractedCellItems"] = raw.TablixNestedItemNames.ToArray();
            if (raw.TablixNestedItemLayouts.Count > 0)
                element.Style["rdlExtractedCellItemLayouts"] = raw.TablixNestedItemLayouts.ToArray();
            diagnostics.Add(Warn("CANMIGRDL023",
                $"'{raw.Name}' contains non-text Tablix cell items that were extracted as separate positioned elements with structured cell-layout metadata."));
        }
        if (raw.TablixNavigationMetadata is { Count: > 0 })
        {
            element.Style ??= [];
            element.Style["rdlTablixNavigation"] = raw.TablixNavigationMetadata.ToArray();
            diagnostics.Add(Warn("CANMIGRDL026",
                $"'{raw.Name}' Tablix navigation/document-map metadata was preserved; Canvas does not execute drilldown/document-map behaviour yet."));
        }
        return element;
    }

    private static void ApplyTablixMetadata(ElementDto element, RawElement raw, List<MigrationDiagnostic> diagnostics)
    {
        if (raw.TablixGroups is not { Count: > 0 }
            && raw.TablixSorts is not { Count: > 0 }
            && raw.TablixKeepWithGroups is not { Count: > 0 }
            && raw.TablixGroupFilters is not { Count: > 0 }
            && raw.TablixRowHierarchy is not { Count: > 0 }
            && raw.TablixColumnHierarchy is not { Count: > 0 })
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
        if (raw.TablixGroupFilters is { Count: > 0 })
            element.Style["rdlTablixGroupFilters"] = raw.TablixGroupFilters.ToArray();
        if (raw.TablixRowHierarchy is { Count: > 0 })
            element.Style["rdlTablixRowHierarchy"] = raw.TablixRowHierarchy.Select(TablixMemberMetadataStyle).ToArray();
        if (raw.TablixColumnHierarchy is { Count: > 0 })
            element.Style["rdlTablixColumnHierarchy"] = raw.TablixColumnHierarchy.Select(TablixMemberMetadataStyle).ToArray();
        if (raw.TableHeaderRow is not null)
            element.Style["rdlHeaderRowFromHierarchy"] = raw.TableHeaderRow.Value;

        diagnostics.Add(Warn("CANMIGRDL014",
            $"'{raw.Name}' Tablix grouping/sorting metadata was preserved; Canvas repeat/group semantics still require review."));
        if (raw.TablixGroupFilters is { Count: > 0 })
            diagnostics.Add(Warn("CANMIGRDL025",
                $"'{raw.Name}' Tablix group filters were preserved as metadata; Canvas does not evaluate report filters yet."));
        if (raw.TablixRowHierarchy is { Count: > 0 } || raw.TablixColumnHierarchy is { Count: > 0 })
            diagnostics.Add(Warn("CANMIGRDL029",
                $"'{raw.Name}' Tablix row/column hierarchy headers were preserved as metadata; Canvas has limited native matrix/group header rendering."));
    }

    private static Dictionary<string, object> TablixMemberMetadataStyle(RdlTablixMemberMetadata member)
    {
        var item = new Dictionary<string, object>
        {
            ["level"] = member.Level,
            ["index"] = member.Index,
            ["path"] = member.Path,
            ["isStatic"] = member.IsStatic
        };
        AddText(item, "groupName", member.GroupName);
        if (member.GroupExpressions.Length > 0)
            item["groupExpressions"] = member.GroupExpressions;
        if (member.SortExpressions.Length > 0)
            item["sortExpressions"] = member.SortExpressions;
        AddText(item, "keepWithGroup", member.KeepWithGroup);
        AddText(item, "repeatOnNewPage", member.RepeatOnNewPage);
        AddText(item, "fixedData", member.FixedData);
        AddText(item, "headerText", member.HeaderText);
        if (member.HeaderSizePt is > 0)
            item["headerSizePt"] = member.HeaderSizePt.Value;
        return item;
    }

    private static double[] FitWidths(double[]? widths, int columns, double totalWidth)
    {
        if (widths is { Length: > 0 } && widths.Length == columns) return widths;
        return Enumerable.Repeat(totalWidth / columns, columns).ToArray();
    }

    private static double[]? ParseColumnWidths(IEnumerable<XElement> columns, double totalWidth)
    {
        var specs = columns.Select(c => Child(c, "Width")?.Value).ToList();
        if (specs.Count == 0) return null;

        var widths = new double[specs.Count];
        var relativeWeights = new double[specs.Count];
        var unresolved = new List<int>();
        var fixedWidth = 0.0;
        var totalWeight = 0.0;

        for (var i = 0; i < specs.Count; i++)
        {
            var spec = specs[i]?.Trim();
            if (string.IsNullOrWhiteSpace(spec))
            {
                unresolved.Add(i);
                continue;
            }

            if (TryParsePercent(spec, out var percent))
            {
                widths[i] = totalWidth * percent / 100.0;
                fixedWidth += widths[i];
                continue;
            }

            if (TryParseRelativeWeight(spec, out var weight))
            {
                relativeWeights[i] = weight;
                totalWeight += weight;
                continue;
            }

            var absolute = LengthToPt(spec);
            if (absolute > 0)
            {
                widths[i] = absolute;
                fixedWidth += absolute;
            }
            else
            {
                unresolved.Add(i);
            }
        }

        var remaining = Math.Max(totalWidth - fixedWidth, 0);
        if (totalWeight > 0)
        {
            for (var i = 0; i < relativeWeights.Length; i++)
            {
                if (relativeWeights[i] <= 0) continue;
                widths[i] = remaining * relativeWeights[i] / totalWeight;
            }
        }

        if (unresolved.Count > 0)
        {
            var used = widths.Sum();
            var unresolvedWidth = Math.Max(totalWidth - used, 0);
            var fallback = unresolvedWidth > 0 ? unresolvedWidth / unresolved.Count : totalWidth / specs.Count;
            foreach (var index in unresolved)
                widths[index] = fallback;
        }

        return widths.All(w => w > 0) ? widths : null;
    }

    private static bool TryParsePercent(string value, out double percent)
    {
        percent = 0;
        var trimmed = value.Trim();
        if (!trimmed.EndsWith('%')) return false;

        return double.TryParse(trimmed[..^1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out percent)
            && percent > 0;
    }

    private static bool TryParseRelativeWeight(string value, out double weight)
    {
        weight = 0;
        var trimmed = value.Trim();
        if (!trimmed.EndsWith('*')) return false;

        var number = trimmed[..^1].Trim();
        if (number.Length == 0)
        {
            weight = 1;
            return true;
        }

        return double.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out weight)
            && weight > 0;
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

    private static void ApplyVisibility(ElementDto element, RawElement raw, List<MigrationDiagnostic> diagnostics)
    {
        if (raw.Hidden is { } hidden)
            element.Hidden = hidden;

        if (raw.HiddenExpression is not { Length: > 0 } hiddenExpression) return;

        element.VisibleExpression = InvertHiddenExpression(hiddenExpression);
        diagnostics.Add(Warn("CANMIGRDL015",
            $"'{raw.Name}' RDL Hidden expression '{hiddenExpression}' was mapped to Canvas visibleExpression; review runtime semantics."));
    }

    private static void ApplyPaginationMetadata(ElementDto element, RawElement raw, List<MigrationDiagnostic> diagnostics)
    {
        var metadata = raw.PaginationMetadata
            .Where(kvp => kvp.Value switch
            {
                string[] arr => arr.Length > 0,
                string s => s.Length > 0,
                _ => true
            })
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);

        if (metadata.Count == 0) return;

        element.Style ??= [];
        element.Style["rdlPagination"] = metadata;
        diagnostics.Add(Warn("CANMIGRDL019",
            $"'{raw.Name}' RDL pagination/repeat metadata was preserved; Canvas pagination behaviour requires review."));
    }

    private static string InvertHiddenExpression(string hiddenExpression) =>
        $"IIF({hiddenExpression}, False, True)";

    private static string NormalizeRdlExpression(string expression)
    {
        var normalized = expression.Trim();
        if (normalized.StartsWith('='))
            normalized = normalized[1..].Trim();
        return Regex.Replace(normalized, @"Fields!(\w+)\.Value", "[$1]", RegexOptions.IgnoreCase);
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

    private static string ArrowDirectionFromShape(string shapeType)
    {
        if (shapeType.Contains("Left", StringComparison.OrdinalIgnoreCase)) return "left";
        if (shapeType.Contains("Up", StringComparison.OrdinalIgnoreCase)) return "up";
        if (shapeType.Contains("Down", StringComparison.OrdinalIgnoreCase)) return "down";
        return "right";
    }

    private static string? ColorProp(IReadOnlyDictionary<string, string> props, string key)
    {
        if (!props.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)) return null;
        if (value.Equals("Transparent", StringComparison.OrdinalIgnoreCase)) return "transparent";
        return NormalizeColor(value);
    }

    private static double? NumberProp(IReadOnlyDictionary<string, string> props, string key)
    {
        if (!props.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)) return null;
        var length = LengthToPt(value);
        if (length > 0) return length;
        return double.TryParse(value.Trim().TrimEnd('F', 'f', 'D', 'd'), NumberStyles.Any, CultureInfo.InvariantCulture, out var n)
            ? n
            : null;
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
        public List<Dictionary<string, object>> ReportParameters = [];
        public List<Dictionary<string, object>> ReportParametersLayout = [];
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
        public string? RichTextHtml;
        public bool? Hidden;
        public string? HiddenExpression;
        public Dictionary<string, object> PaginationMetadata = new(StringComparer.Ordinal);
        public List<Dictionary<string, object>> Filters = [];
        public Dictionary<string, object>? NavigationMetadata;
        public List<List<string>>? TableCells;
        public string[]? ColumnAlignments;
        public double[]? ColumnWidthsPt;
        public List<RdlTablixGroup>? TablixGroups;
        public List<string>? TablixSorts;
        public List<string>? TablixKeepWithGroups;
        public List<Dictionary<string, object>>? TablixGroupFilters;
        public List<Dictionary<string, object>>? TablixNavigationMetadata;
        public List<RdlTablixMemberMetadata>? TablixRowHierarchy;
        public List<RdlTablixMemberMetadata>? TablixColumnHierarchy;
        public List<string> TablixNestedItemNames = [];
        public List<Dictionary<string, object>> TablixNestedItemLayouts = [];
        public string? DataSetName;
        public bool? TableHeaderRow;
        public string? ImageDataUrl;
        public string? ImageSource;
        public string? ImageValue;
        public double? LineWidth;
        public string? LineStyle;
        public string? CustomType;                    // <CustomReportItem><Type>
        public Dictionary<string, string>? CustomProps;  // <CustomProperties> name → value
        public List<RdlChartSeries>? ChartSeries;
        public Dictionary<string, object>? GaugePanelMetadata;
        public Dictionary<string, object>? MapMetadata;
        public string? ParentTablixName;
        public int? ParentTablixRow;
        public int? ParentTablixColumn;
        public int? ParentTablixRowSpan;
        public int? ParentTablixColumnSpan;
        public Dictionary<string, object>? ParentTablixRepeatScope;
    }

    private sealed record RdlTablixGroup(string Name, string[] Expressions);
    private sealed record RdlTablixMemberMetadata(
        int Level,
        int Index,
        string Path,
        bool IsStatic,
        string? GroupName,
        string[] GroupExpressions,
        string[] SortExpressions,
        string? KeepWithGroup,
        string? RepeatOnNewPage,
        string? FixedData,
        string? HeaderText,
        double? HeaderSizePt);
    private sealed record RdlChartSeries(string Name, string Type, string? YExpression, string? XExpression, string? SizeExpression);
}
