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
        CollectNamedStyles(root);

        var report = new RawReport { Name = Attr(root, "Name") ?? "Telerik Report" };
        ResolvePage(root, report);

        // <Report><Items> holds the sections; each section has a Height, an optional <Style>, and <Items>.
        var sectionsContainer = root.Elements().FirstOrDefault(e => e.Name.LocalName == "Items");
        foreach (var section in sectionsContainer?.Elements() ?? Enumerable.Empty<XElement>())
        {
            if (!section.Name.LocalName.EndsWith("Section", StringComparison.Ordinal)) continue;
            var name = Attr(section, "Name") ?? section.Name.LocalName;
            report.Bands.Add(new RawBand
            {
                Name = name,
                Type = section.Name.LocalName,
                HeightPt = LengthToPt(Attr(section, "Height"))
            });

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
                if (Attr(sel, "StyleName") is { Length: > 0 } sn)
                    _namedStyles[sn] = style;
        }
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
            ApplyStyle(raw, _namedStyles.GetValueOrDefault(Attr(item, "StyleName") ?? ""));  // named base
            ApplyStyle(raw, Child(item, "Style"));                                            // inline overrides
            if (type == "Shape") raw.ShapeKind = ShapeKindFromName(Attr(item, "ShapeType") ?? Attr(item, "Shape"));
            if (type == "PictureBox") raw.ImageDataUrl = ExtractImageDataUrl(item);
            if (type == "Barcode") raw.Symbology = Attr(item, "Type") ?? Attr(item, "Symbology") ?? Attr(item, "Encoder");

            report.Elements.Add(raw);

            if (type == "Panel")
                ParseItems(item.Elements().FirstOrDefault(e => e.Name.LocalName == "Items"), sectionName, left, top, report, depth + 1);
        }
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

            if (raw.Value is { } v) ApplyBinding(element, v, diagnostics);

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

            default:
                // Table, CrossTab, Chart, Graph, Map, … — full fidelity is V2.
                diagnostics.Add(Warn("CANMIGTRDX011", $"'{raw.Name}' is a {raw.Type} — not supported by Canvas yet; inserted a placeholder."));
                return Placeholder(element, $"[{raw.Type}: migrate manually]");
        }
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

    private static void ApplyBinding(ElementDto element, string value, List<MigrationDiagnostic> diagnostics)
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
            element.Expression = value;
            if (string.IsNullOrEmpty(element.Content)) element.Content = value;
            diagnostics.Add(Warn("CANMIGTRDX010", $"'{element.Name}' expression '{value}' mapped to Canvas expression — review the syntax."));
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
    }
}
