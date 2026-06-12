using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Canvas.Core.Contracts;
using Canvas.Migration.Abstractions;

namespace Canvas.Migration.Stimulsoft;

public sealed class MrtConvertResult
{
    public required DesignExportDto Design { get; init; }
    public required IReadOnlyList<MigrationDiagnostic> Diagnostics { get; init; }
}

/// <summary>
/// Converts a Stimulsoft Reports <c>.mrt</c> report — <c>StiSerializer</c> XML with a <b>banded</b>
/// layout (a <c>&lt;Page&gt;</c> whose <c>&lt;Components&gt;</c> are bands, each band's nested
/// <c>&lt;Components&gt;</c> holding report items) — into a Canvas <see cref="DesignExportDto"/>. Bands
/// carry an explicit <c>&lt;ClientRectangle&gt;</c> position (so this mirrors the FastReport approach of
/// explicit band positions); item rectangles are band-relative. Coordinates are in hundredths of an inch
/// (×0.72 → points). Elements are matched by their <c>type</c> attribute / <see cref="XName.LocalName"/>.
/// </summary>
public sealed class MrtToDesignConverter
{
    private const double Scale = 0.72;   // ClientRectangle units are hundredths of an inch → points
    private const double A4WidthPt = 595, A4HeightPt = 842;

    /// <summary>Detects a Stimulsoft <c>.mrt</c>: root <c>&lt;StiSerializer&gt;</c> (unique).</summary>
    public static bool LooksLikeMrt(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;
        if (!source.TrimStart().StartsWith('<')) return false;
        try
        {
            return XDocument.Parse(source).Root?.Name.LocalName == "StiSerializer";
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
    }

    public MrtConvertResult ConvertAuto(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source cannot be null or empty.", nameof(source));
        return Convert(source);
    }

    public MrtConvertResult Convert(string mrt)
    {
        if (string.IsNullOrWhiteSpace(mrt))
            throw new ArgumentException("Source cannot be null or empty.", nameof(mrt));

        XElement root;
        try { root = XDocument.Parse(mrt).Root ?? throw new ArgumentException("Empty .mrt document."); }
        catch (System.Xml.XmlException ex) { throw new ArgumentException($"Invalid .mrt XML: {ex.Message}", nameof(mrt)); }

        if (root.Name.LocalName != "StiSerializer")
            throw new ArgumentException("Not a Stimulsoft .mrt — expected a root <StiSerializer> element.", nameof(mrt));

        var report = new RawReport { Name = Child(root, "ReportName")?.Value ?? Attr(root, "Name") ?? "Stimulsoft Report" };

        var page = Child(root, "Pages")?.Elements().FirstOrDefault(e => string.Equals(Attr(e, "type"), "Page", StringComparison.Ordinal))
                   ?? Child(root, "Pages")?.Elements().FirstOrDefault();
        ResolvePage(page, report);

        // A page's components are bands; each band's nested components are the report items.
        foreach (var comp in Child(page, "Components")?.Elements() ?? Enumerable.Empty<XElement>())
        {
            var type = Attr(comp, "type") ?? comp.Name.LocalName;
            var (bx, by, _, _) = ParseRect(Child(comp, "ClientRectangle")?.Value);
            if (type.EndsWith("Band", StringComparison.Ordinal))
            {
                report.Bands.Add(new RawBand { Name = NameOf(comp), Type = type });
                ParseItems(Child(comp, "Components"), type, bx, by, report, 0);
            }
            else
            {
                ParseItem(comp, "", bx, by, report, 0);   // loose element directly on the page
            }
        }

        return BuildDesign(report);
    }

    private static void ResolvePage(XElement? page, RawReport report)
    {
        var size = PaperKindSize(Child(page, "PaperSize")?.Value ?? Attr(page, "PaperSize") ?? "");
        report.PageWidthPt = size.W > 0 ? size.W : A4WidthPt;
        report.PageHeightPt = size.H > 0 ? size.H : A4HeightPt;
        if (string.Equals(Child(page, "Orientation")?.Value, "Landscape", StringComparison.OrdinalIgnoreCase))
            (report.PageWidthPt, report.PageHeightPt) = (report.PageHeightPt, report.PageWidthPt);
    }

    // Items inside a band (or Panel); a Panel recurses with its absolute origin so children stay absolute.
    private void ParseItems(XElement? components, string region, double originX, double originY, RawReport report, int depth)
    {
        foreach (var item in components?.Elements() ?? Enumerable.Empty<XElement>())
            ParseItem(item, region, originX, originY, report, depth);
    }

    private void ParseItem(XElement el, string region, double originX, double originY, RawReport report, int depth)
    {
        if (depth > 32) return;
        var type = Attr(el, "type") ?? el.Name.LocalName;
        var (ix, iy, iw, ih) = ParseRect(Child(el, "ClientRectangle")?.Value);
        var absX = originX + ix;   // hundredths of an inch
        var absY = originY + iy;

        var raw = new RawElement
        {
            Name = NameOf(el),
            Type = type,
            Region = region,
            X = absX * Scale, Y = absY * Scale, W = iw * Scale, H = ih * Scale,
            Text = Child(el, "Text")?.Value,
            TextAlign = ParseAlignment(Child(el, "HorAlignment")?.Value),
            ForeColor = ParseColor(Child(el, "TextBrush")?.Value) ?? "#000000",
            BackColor = ParseColor(Child(el, "Brush")?.Value),
        };
        ApplyFont(raw, Child(el, "Font")?.Value);
        if (type is "HorizontalLinePrimitive" or "VerticalLinePrimitive" && ParseColor(Child(el, "Color")?.Value) is { } lc) raw.ForeColor = lc;
        if (type == "Image") raw.ImageDataUrl = ExtractImageDataUrl(el);
        if (type == "BarCode") raw.Symbology = Child(el, "BarCodeType")?.Value ?? Attr(Child(el, "BarCode"), "type");

        report.Elements.Add(raw);

        if (type == "Panel")
            ParseItems(Child(el, "Components"), region, absX, absY, report, depth + 1);
    }

    // ── Build (band positions are explicit, so no height accumulation) ─────────────────────────────

    private static MrtConvertResult BuildDesign(RawReport report)
    {
        var diagnostics = new List<MigrationDiagnostic>();
        var elements = new List<ElementDto>();
        var sharedElements = new List<ElementDto>();
        var mapped = 0;

        foreach (var raw in report.Elements)
        {
            var element = MapControl(raw, raw.X, raw.Y, diagnostics);
            if (element is null) continue;

            diagnostics.Add(Info("CANMIGMRT002", $"'{raw.Name}' ({raw.Type}) → Canvas {element.Type}."));
            if (raw.Text is { } t) ApplyBinding(element, t, diagnostics);

            (raw.Region is "PageHeaderBand" or "PageFooterBand" ? sharedElements : elements).Add(element);
            mapped++;
        }

        elements.Sort((p, q) => p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.X.CompareTo(q.X));
        sharedElements.Sort((p, q) => p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.X.CompareTo(q.X));

        diagnostics.Insert(0, Info("CANMIGMRT001",
            $"Stimulsoft report '{report.Name}' detected — {report.Bands.Count} band(s), {mapped} item(s) mapped."));

        var design = new DesignExportDto
        {
            Id = $"mrt-report-{Guid.NewGuid():N}",
            Name = report.Name,
            Category = "imported",
            Description = "Imported from a Stimulsoft Reports report (.mrt).",
            PageSettings = new PageSettingsDto { Width = report.PageWidthPt, Height = report.PageHeightPt, Unit = "pt" },
            Pages = [new PageDto { Id = "page-1", Elements = elements }],
            SharedElements = sharedElements
        };

        return new MrtConvertResult { Design = design, Diagnostics = diagnostics };
    }

    private static ElementDto? MapControl(RawElement raw, double x, double y, List<MigrationDiagnostic> diagnostics)
    {
        var element = new ElementDto { Id = $"mrt-{raw.Name}", Name = raw.Name, X = x, Y = y, Width = raw.W, Height = raw.H };

        switch (raw.Type)
        {
            case "Text":
                element.Type = "text";
                element.Content = LooksLikeExpr(raw.Text) ? "" : raw.Text ?? "";
                element.Style = BuildTextStyle(raw);
                return element;

            case "HorizontalLinePrimitive":
            case "VerticalLinePrimitive":
                element.Type = "line";
                element.Style = new Dictionary<string, object> { ["color"] = raw.ForeColor };
                if (raw.LineWidth is { } lineW) element.Style["strokeWidth"] = lineW;
                return element;

            case "RectanglePrimitive":
            case "RoundedRectanglePrimitive":
                element.Type = "rect";
                element.Style = new Dictionary<string, object> { ["borderColor"] = raw.ForeColor };
                if (raw.BackColor is { } bg) element.Style["backgroundColor"] = bg;
                return element;

            case "Panel":
                element.Type = "rect";
                element.Style = new Dictionary<string, object> { ["borderColor"] = raw.ForeColor };
                if (raw.BackColor is { } pbg) element.Style["backgroundColor"] = pbg;
                return element;

            case "Image":
                element.Type = "image";
                element.FitMode = "contain";
                if (raw.ImageDataUrl is { } dataUrl)
                    element.Content = dataUrl;
                else
                    diagnostics.Add(Warn("CANMIGMRT012",
                        $"'{raw.Name}' image isn't embeddable from source — inserted an empty image placeholder."));
                return element;

            case "BarCode":
                element.Type = "barcode";
                element.BarcodeValue = LooksLikeExpr(raw.Text) ? "" : raw.Text ?? "";
                element.BarcodeType = BarcodeTypeFromSymbology(raw.Symbology);
                return element;

            case "SubReport":
                diagnostics.Add(Warn("CANMIGMRT011",
                    $"'{raw.Name}' is a sub-report — requires manual migration; inserted a placeholder."));
                return Placeholder(element, $"[Sub-report: {raw.Name} — migrate manually]");

            default:
                diagnostics.Add(Warn("CANMIGMRT011", $"'{raw.Name}' is a {raw.Type} — not supported by Canvas yet; inserted a placeholder."));
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

    private static readonly HashSet<string> SystemVars = new(StringComparer.OrdinalIgnoreCase)
    { "Page", "PageNofM", "TotalPageCount", "Today", "Now", "Time", "Column", "Line", "ReportName", "ReportAlias" };

    private static void ApplyBinding(ElementDto element, string text, List<MigrationDiagnostic> diagnostics)
    {
        if (!LooksLikeExpr(text)) return;   // literal already set as Content/value
        var dotted = Regex.Match(text, @"^\s*\{[A-Za-z_]\w*\.([A-Za-z_]\w*)\}\s*$");
        var single = Regex.Match(text, @"^\s*\{([A-Za-z_]\w*)\}\s*$");
        string? field = dotted.Success ? dotted.Groups[1].Value
            : single.Success && !SystemVars.Contains(single.Groups[1].Value) ? single.Groups[1].Value : null;
        if (field is not null)
        {
            element.Binding = field;
            if (element.Type == "text") element.Content = $"{{{{{field}}}}}";
            else if (element.Type == "barcode") element.BarcodeValue = $"{{{{{field}}}}}";
            diagnostics.Add(Info("CANMIGMRT010", $"'{element.Name}' bound to {text} → Canvas binding '{field}'."));
        }
        else
        {
            element.Expression = text;
            if (string.IsNullOrEmpty(element.Content)) element.Content = text;
            diagnostics.Add(Warn("CANMIGMRT010", $"'{element.Name}' expression '{text}' mapped to Canvas expression — review the syntax."));
        }
    }

    private static bool LooksLikeExpr(string? text) => text is not null && text.Contains('{') && text.Contains('}');

    private static void ApplyFont(RawElement raw, string? value)
    {
        // "Segoe UI,9.75,Bold,Point,False,0" — family, size(pt), style (may itself contain a comma).
        if (string.IsNullOrWhiteSpace(value)) return;
        var parts = value.Split(',');
        if (parts.Length >= 1 && parts[0].Trim() is { Length: > 0 } fam) raw.FontFamily = fam;
        if (parts.Length >= 2 && double.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var size)) raw.FontSize = size;
        var styleText = parts.Length >= 3 ? string.Join(",", parts[2..]) : "";
        raw.Bold = styleText.Contains("Bold", StringComparison.OrdinalIgnoreCase);
        raw.Italic = styleText.Contains("Italic", StringComparison.OrdinalIgnoreCase);
        raw.Underline = styleText.Contains("Underline", StringComparison.OrdinalIgnoreCase);
        raw.Strikeout = styleText.Contains("Strikeout", StringComparison.OrdinalIgnoreCase) || styleText.Contains("Strikethrough", StringComparison.OrdinalIgnoreCase);
    }

    private static (double X, double Y, double W, double H) ParseRect(string? value)
    {
        var n = (value ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(p => double.TryParse(p, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0).ToArray();
        return n.Length >= 4 ? (n[0], n[1], n[2], n[3]) : (0, 0, 0, 0);
    }

    private static string ParseAlignment(string? text)
    {
        text ??= "";
        if (text.Contains("Center", StringComparison.OrdinalIgnoreCase)) return "center";
        if (text.Contains("Right", StringComparison.OrdinalIgnoreCase)) return "right";
        if (text.Contains("Width", StringComparison.OrdinalIgnoreCase) || text.Contains("Justify", StringComparison.OrdinalIgnoreCase)) return "justify";
        return "left";
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
        var candidate = Child(el, "ImageData")?.Value ?? Child(el, "Image")?.Value;
        if (string.IsNullOrWhiteSpace(candidate)) return null;
        if (candidate.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return candidate;
        var b64 = Regex.Replace(candidate, @"\s+", "");
        return b64.Length >= 64 && b64.Length % 4 == 0 && Regex.IsMatch(b64, @"^[A-Za-z0-9+/]+={0,2}$")
            ? $"data:image/png;base64,{b64}" : null;
    }

    // Colours: "[R:G:B]" / "[R:G:B:A]", "solid:Color", named, or "#RRGGBB".
    private static string? ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        if (v.StartsWith("solid:", StringComparison.OrdinalIgnoreCase)) v = v[6..].Trim();
        if (v.Equals("Transparent", StringComparison.OrdinalIgnoreCase) || v.Length == 0) return null;
        if (v.StartsWith('[') && v.EndsWith(']'))
        {
            var c = v[1..^1].Split([':', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
            if (c.Length >= 3)
            {
                var o = c.Length >= 4 ? 1 : 0;   // A:R:G:B → drop alpha
                return $"#{c[o]:X2}{c[o + 1]:X2}{c[o + 2]:X2}";
            }
            return null;
        }
        if (v.StartsWith('#'))
            return v.Length == 4 ? $"#{v[1]}{v[1]}{v[2]}{v[2]}{v[3]}{v[3]}".ToUpperInvariant() : v.ToUpperInvariant();
        return NamedColor(v);
    }

    private static (double W, double H) PaperKindSize(string kind) => kind.Trim().ToLowerInvariant() switch
    {
        "a3" => (842, 1191),
        "a4" => (595, 842),
        "a5" => (420, 595),
        "letter" => (612, 792),
        "legal" => (612, 1008),
        _ => (0, 0)
    };

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
        "orange" => "#FFA500",
        "navy" => "#000080",
        "transparent" => "#00000000",
        _ => "#000000"
    };

    private static string NameOf(XElement el) => Child(el, "Name")?.Value ?? el.Name.LocalName;

    private static string? Attr(XElement? el, string name) => el?.Attribute(name)?.Value;

    private static XElement? Child(XElement? el, string name) =>
        el?.Elements().FirstOrDefault(e => e.Name.LocalName == name);

    private static MigrationDiagnostic Info(string id, string message) =>
        new() { Id = id, Message = message, Severity = MigrationDiagnosticSeverity.Info };

    private static MigrationDiagnostic Warn(string id, string message) =>
        new() { Id = id, Message = message, Severity = MigrationDiagnosticSeverity.Warning };

    // ── Neutral intermediate model ─────────────────────────────────────────────────────────────────

    private sealed class RawReport
    {
        public string Name = "Stimulsoft Report";
        public double PageWidthPt = A4WidthPt, PageHeightPt = A4HeightPt;
        public List<RawBand> Bands = [];
        public List<RawElement> Elements = [];
    }

    private sealed class RawBand
    {
        public required string Name;
        public required string Type;
    }

    private sealed class RawElement
    {
        public required string Name;
        public required string Type;
        public string Region = "";
        public double X, Y, W, H;   // absolute points (band offset already folded in)
        public string? Text;
        public string? FontFamily;
        public double? FontSize;
        public bool Bold, Italic, Underline, Strikeout;
        public string ForeColor = "#000000";
        public string? BackColor;
        public string TextAlign = "left";
        public double? LineWidth;
        public string? ImageDataUrl;
        public string? Symbology;
    }
}
