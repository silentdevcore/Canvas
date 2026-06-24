using System.Globalization;
using System.Text.Json;
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

    // Style name → its report-level <Styles> entry (resolved per Convert call); applied as a base for
    // components that reference it via <ComponentStyle>.
    private readonly Dictionary<string, XElement> _namedStyles = new(StringComparer.Ordinal);
    // Same, for the JSON (Reports.JS) variant: style name → its JSON style object.
    private readonly Dictionary<string, JsonElement> _jsonStyles = new(StringComparer.Ordinal);

    /// <summary>Detects a Stimulsoft <c>.mrt</c>: XML root <c>&lt;StiSerializer&gt;</c>, or the modern
    /// Reports.JS JSON variant (a JSON object carrying Stimulsoft report markers).</summary>
    public static bool LooksLikeMrt(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;
        var trimmed = source.TrimStart();
        if (trimmed.StartsWith('<'))
        {
            try { return XDocument.Parse(source).Root?.Name.LocalName == "StiSerializer"; }
            catch (System.Xml.XmlException) { return false; }
        }
        if (trimmed.StartsWith('{'))
            return LooksLikeJsonMrt(source);
        return false;
    }

    // JSON .mrt (Reports.JS): a JSON object with Stimulsoft report markers (ReportVersion/ReportGuid, or
    // Pages + an "Ident" component discriminator). Conservative so arbitrary JSON isn't misrouted here.
    private static bool LooksLikeJsonMrt(string source)
    {
        try
        {
            using var doc = JsonDocument.Parse(source);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            var root = doc.RootElement;
            return root.TryGetProperty("ReportVersion", out _)
                || root.TryGetProperty("ReportGuid", out _)
                || (root.TryGetProperty("Pages", out _) && source.Contains("\"Ident\"", StringComparison.Ordinal));
        }
        catch (JsonException)
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

        if (mrt.TrimStart().StartsWith('{'))
            return ConvertJson(mrt);   // modern Reports.JS JSON variant

        XElement root;
        try { root = XDocument.Parse(mrt).Root ?? throw new ArgumentException("Empty .mrt document."); }
        catch (System.Xml.XmlException ex) { throw new ArgumentException($"Invalid .mrt XML: {ex.Message}", nameof(mrt)); }

        if (root.Name.LocalName != "StiSerializer")
            throw new ArgumentException("Not a Stimulsoft .mrt — expected a root <StiSerializer> element.", nameof(mrt));

        var report = new RawReport { Name = Child(root, "ReportName")?.Value ?? Attr(root, "Name") ?? "Stimulsoft Report" };

        _namedStyles.Clear();
        foreach (var s in Child(root, "Styles")?.Elements() ?? Enumerable.Empty<XElement>())
            if ((Child(s, "Name")?.Value ?? Attr(s, "Name")) is { Length: > 0 } sn)
                _namedStyles[sn] = s;

        var page = Child(root, "Pages")?.Elements().FirstOrDefault(e => string.Equals(Attr(e, "type"), "Page", StringComparison.Ordinal))
                   ?? Child(root, "Pages")?.Elements().FirstOrDefault();
        ResolvePage(page, report);

        // A page's components are bands; each band's nested components are the report items.
        RawBand? lastGroupHeader = null;
        foreach (var comp in Child(page, "Components")?.Elements() ?? Enumerable.Empty<XElement>())
        {
            var type = Attr(comp, "type") ?? comp.Name.LocalName;
            var (bx, by, _, _) = ParseRect(Child(comp, "ClientRectangle")?.Value);
            if (type.EndsWith("Band", StringComparison.Ordinal))
            {
                var band = new RawBand { Name = NameOf(comp), Type = type };
                // GroupHeaderBand carries the grouping key in <Condition>; a GroupFooterBand pairs with it.
                if (type == "GroupHeaderBand")
                {
                    band.Condition = Child(comp, "Condition")?.Value;
                    band.GroupName = band.Name;
                    lastGroupHeader = band;
                }
                else if (type == "GroupFooterBand")
                {
                    band.Condition = lastGroupHeader?.Condition;
                    band.GroupName = lastGroupHeader?.GroupName ?? band.Name;
                }
                report.Bands.Add(band);
                ParseItems(Child(comp, "Components"), type, bx, by, report, 0, band);
            }
            else
            {
                ParseItem(comp, "", bx, by, report, 0);   // loose element directly on the page
            }
        }

        return BuildDesign(report);
    }

    // ── JSON (Reports.JS) variant ──────────────────────────────────────────────────────────────────
    // Same logical model as the XML path (Pages → bands → items), but components are JSON objects keyed by
    // index, types come from "Ident" (Sti-prefixed), and geometry is in ReportUnit (cm/inch/px) not
    // hundredths-of-inch. Builds the same RawReport so the shared BuildDesign/mapping logic is reused.
    private MrtConvertResult ConvertJson(string json)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new ArgumentException($"Invalid .mrt JSON: {ex.Message}", nameof(json)); }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Not a Stimulsoft .mrt — expected a JSON report object.", nameof(json));

            var report = new RawReport { Name = JStr(root, "ReportName") ?? "Stimulsoft Report" };
            var unit = UnitToPt(JStr(root, "ReportUnit"));

            _jsonStyles.Clear();
            if (JObj(root, "Styles") is { } styles)
                foreach (var s in styles.EnumerateObject())
                    if (JStr(s.Value, "Name") is { Length: > 0 } sn) _jsonStyles[sn] = s.Value;

            var page = FirstJsonPage(root);
            ResolvePageJson(page, report, unit);

            RawBand? lastGroupHeader = null;
            foreach (var comp in JsonComponents(page))
            {
                var type = JsonType(comp);
                var (bx, by, _, _) = ParseRect(JRectString(comp, "ClientRectangle"));
                if (type.EndsWith("Band", StringComparison.Ordinal))
                {
                    var band = new RawBand { Name = JStr(comp, "Name") ?? type, Type = type };
                    if (type == "GroupHeaderBand")
                    {
                        band.Condition = JStr(comp, "Condition");
                        band.GroupName = band.Name;
                        lastGroupHeader = band;
                    }
                    else if (type == "GroupFooterBand")
                    {
                        band.Condition = lastGroupHeader?.Condition;
                        band.GroupName = lastGroupHeader?.GroupName ?? band.Name;
                    }
                    report.Bands.Add(band);
                    foreach (var item in JsonComponents(comp))
                        ParseJsonItem(item, type, bx, by, report, unit, band, 0);
                }
                else
                {
                    ParseJsonItem(comp, "", bx, by, report, unit, null, 0);
                }
            }

            return BuildDesign(report);
        }
    }

    private void ParseJsonItem(JsonElement el, string region, double originX, double originY, RawReport report, double unit, RawBand? group, int depth)
    {
        if (depth > 32) return;
        var type = JsonType(el);
        var (ix, iy, iw, ih) = ParseRect(JRectString(el, "ClientRectangle"));
        var absX = originX + ix;
        var absY = originY + iy;

        var style = _jsonStyles.GetValueOrDefault(JStr(el, "ComponentStyle") ?? "");
        var raw = new RawElement
        {
            Name = JStr(el, "Name") ?? type,
            Type = type,
            Region = region,
            X = absX * unit, Y = absY * unit, W = iw * unit, H = ih * unit,
            Text = JStr(el, "Text"),
            TextAlign = ParseAlignment(JStr(el, "HorAlignment") ?? JStr(style, "HorAlignment")),
            ForeColor = ParseColor(JStr(el, "TextBrush") ?? JStr(style, "TextBrush")) ?? "#000000",
            BackColor = ParseColor(JStr(el, "Brush") ?? JStr(style, "Brush")),
        };
        ApplyFont(raw, JStr(el, "Font") ?? JStr(style, "Font"));
        ParseBorder(raw, JStr(el, "Border") ?? JStr(style, "Border"));
        if (type is "HorizontalLinePrimitive" or "VerticalLinePrimitive" && ParseColor(JStr(el, "Color")) is { } lc) raw.ForeColor = lc;
        if (type == "Image") raw.ImageDataUrl = ExtractJsonImageDataUrl(el);
        if (type == "BarCode") raw.Symbology = JStr(el, "BarCodeType");
        if (group is { Type: "GroupHeaderBand" or "GroupFooterBand" })
        {
            raw.GroupName = group.GroupName;
            raw.GroupRole = group.Type == "GroupFooterBand" ? "footer" : "header";
            raw.GroupCondition = group.Condition;
        }

        report.Elements.Add(raw);

        if (type == "Panel")
            foreach (var child in JsonComponents(el))
                ParseJsonItem(child, region, absX, absY, report, unit, group, depth + 1);
    }

    private static void ResolvePageJson(JsonElement? page, RawReport report, double unit)
    {
        if (page is not { } p) { report.PageWidthPt = A4WidthPt; report.PageHeightPt = A4HeightPt; return; }
        var size = PaperKindSize(JStr(p, "PaperSize") ?? "");
        var w = JNum(p, "PageWidth");
        var h = JNum(p, "PageHeight");
        report.PageWidthPt = size.W > 0 ? size.W : (w is > 0 ? w.Value * unit : A4WidthPt);
        report.PageHeightPt = size.H > 0 ? size.H : (h is > 0 ? h.Value * unit : A4HeightPt);
        if (string.Equals(JStr(p, "Orientation"), "Landscape", StringComparison.OrdinalIgnoreCase))
            (report.PageWidthPt, report.PageHeightPt) = (report.PageHeightPt, report.PageWidthPt);
    }

    private static double UnitToPt(string? unit) => (unit ?? "").Trim().ToLowerInvariant() switch
    {
        "centimeters" => 72.0 / 2.54,
        "millimeters" => 72.0 / 25.4,
        "inches" => 72.0,
        "pixels" => 72.0 / 96.0,
        "hundredthsofinch" => 0.72,
        _ => 72.0 / 2.54,   // Reports.JS default unit is centimeters
    };

    private static JsonElement? FirstJsonPage(JsonElement root)
    {
        if (!root.TryGetProperty("Pages", out var pages)) return null;
        if (pages.ValueKind == JsonValueKind.Object)
            foreach (var p in pages.EnumerateObject()) if (p.Value.ValueKind == JsonValueKind.Object) return p.Value;
        if (pages.ValueKind == JsonValueKind.Array)
            foreach (var p in pages.EnumerateArray()) if (p.ValueKind == JsonValueKind.Object) return p;
        return null;
    }

    private static IEnumerable<JsonElement> JsonComponents(JsonElement? parent)
    {
        if (parent is not { } p || !p.TryGetProperty("Components", out var comps)) return Enumerable.Empty<JsonElement>();
        return comps.ValueKind switch
        {
            JsonValueKind.Object => comps.EnumerateObject().Select(x => x.Value),
            JsonValueKind.Array => comps.EnumerateArray(),
            _ => Enumerable.Empty<JsonElement>(),
        };
    }

    private static string JsonType(JsonElement el)
    {
        var ident = JStr(el, "Ident") ?? "";
        return ident.StartsWith("Sti", StringComparison.Ordinal) ? ident[3..] : ident;
    }

    private static string? JStr(JsonElement el, string prop)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(prop, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }

    private static double? JNum(JsonElement el, string prop)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(prop, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
        return v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static JsonElement? JObj(JsonElement el, string prop) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;

    // ClientRectangle may be a "x,y,w,h" string or a [x,y,w,h] array → normalize to the comma string ParseRect expects.
    private static string? JRectString(JsonElement el, string prop)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(prop, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Array => string.Join(",", v.EnumerateArray().Select(e => e.ToString())),
            _ => null,
        };
    }

    private static string? ExtractJsonImageDataUrl(JsonElement el)
    {
        var b64 = JStr(el, "Image") ?? JStr(el, "ImageData");
        if (string.IsNullOrWhiteSpace(b64)) return null;
        if (b64.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return b64;
        var clean = Regex.Replace(b64, @"\s+", "");
        return clean.Length >= 16 && clean.Length % 4 == 0 && Regex.IsMatch(clean, @"^[A-Za-z0-9+/]+={0,2}$")
            ? $"data:image/png;base64,{clean}" : null;
    }

    private static void ResolvePage(XElement? page, RawReport report)
    {
        // Prefer a named PaperSize; otherwise fall back to explicit PageWidth/PageHeight (hundredths-inch).
        var size = PaperKindSize(Child(page, "PaperSize")?.Value ?? Attr(page, "PaperSize") ?? "");
        report.PageWidthPt = size.W > 0 ? size.W : PageDimPt(page, "PageWidth", A4WidthPt);
        report.PageHeightPt = size.H > 0 ? size.H : PageDimPt(page, "PageHeight", A4HeightPt);
        if (string.Equals(Child(page, "Orientation")?.Value, "Landscape", StringComparison.OrdinalIgnoreCase))
            (report.PageWidthPt, report.PageHeightPt) = (report.PageHeightPt, report.PageWidthPt);
    }

    private static double PageDimPt(XElement? page, string key, double fallback)
    {
        var value = Child(page, key)?.Value ?? Attr(page, key);
        return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) && v > 0
            ? v * Scale
            : fallback;
    }

    // Items inside a band (or Panel); a Panel recurses with its absolute origin so children stay absolute.
    private void ParseItems(XElement? components, string region, double originX, double originY, RawReport report, int depth, RawBand? group = null)
    {
        foreach (var item in components?.Elements() ?? Enumerable.Empty<XElement>())
            ParseItem(item, region, originX, originY, report, depth, group);
    }

    private void ParseItem(XElement el, string region, double originX, double originY, RawReport report, int depth, RawBand? group = null)
    {
        if (depth > 32) return;
        var type = Attr(el, "type") ?? el.Name.LocalName;
        var (ix, iy, iw, ih) = ParseRect(Child(el, "ClientRectangle")?.Value);
        var absX = originX + ix;   // hundredths of an inch
        var absY = originY + iy;

        // A referenced report style supplies defaults the element doesn't set itself.
        var style = _namedStyles.GetValueOrDefault(Child(el, "ComponentStyle")?.Value ?? "");
        var raw = new RawElement
        {
            Name = NameOf(el),
            Type = type,
            Region = region,
            X = absX * Scale, Y = absY * Scale, W = iw * Scale, H = ih * Scale,
            Text = Child(el, "Text")?.Value,
            TextAlign = ParseAlignment(Child(el, "HorAlignment")?.Value ?? Child(style, "HorAlignment")?.Value),
            ForeColor = ParseColor(Child(el, "TextBrush")?.Value ?? Child(style, "TextBrush")?.Value) ?? "#000000",
            BackColor = ParseColor(Child(el, "Brush")?.Value ?? Child(style, "Brush")?.Value),
        };
        ApplyFont(raw, Child(el, "Font")?.Value ?? Child(style, "Font")?.Value);
        ParseBorder(raw, Child(el, "Border")?.Value ?? Child(style, "Border")?.Value);
        if (type is "HorizontalLinePrimitive" or "VerticalLinePrimitive" && ParseColor(Child(el, "Color")?.Value) is { } lc) raw.ForeColor = lc;
        if (type == "Image") raw.ImageDataUrl = ExtractImageDataUrl(el);
        if (type == "BarCode") raw.Symbology = Child(el, "BarCodeType")?.Value ?? Attr(Child(el, "BarCode"), "type");
        if (group is { Type: "GroupHeaderBand" or "GroupFooterBand" })
        {
            raw.GroupName = group.GroupName;
            raw.GroupRole = group.Type == "GroupFooterBand" ? "footer" : "header";
            raw.GroupCondition = group.Condition;
        }

        report.Elements.Add(raw);

        if (type == "Panel")
            ParseItems(Child(el, "Components"), region, absX, absY, report, depth + 1, group);
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
            ApplyGroupRepeatMetadata(element, raw, diagnostics);

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
                ApplyBorderStyle(element.Style, raw);
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

            case "Chart":
            case "CrossTab":
                diagnostics.Add(Warn("CANMIGMRT014",
                    $"'{raw.Name}' is a {raw.Type} — Canvas has no native equivalent; inserted a positioned placeholder for review."));
                return Placeholder(element, $"[{raw.Type}: {raw.Name} — migrate manually]");

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

    // StiBorder serializes as "Sides;Color;Size;Style;…" — e.g. "Top, Bottom;[0:0:0];1;Solid;…".
    // Parse the sides, colour, and size; "None"/empty means no border.
    private static void ParseBorder(RawElement raw, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var parts = value.Split(';', StringSplitOptions.TrimEntries);
        var sides = parts.Length > 0 ? parts[0] : "";
        if (sides.Length == 0 || sides.Equals("None", StringComparison.OrdinalIgnoreCase)) return;
        raw.BorderSides = sides;
        if (parts.Length > 1 && ParseColor(parts[1]) is { } c) raw.BorderColor = c;
        if (parts.Length > 2 && double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var w) && w > 0)
            raw.BorderWidth = w;
    }

    // Apply a parsed border to a style dict: uniform when sides include "All", otherwise per-side keys.
    private static void ApplyBorderStyle(Dictionary<string, object> style, RawElement raw)
    {
        if (raw.BorderSides is not { Length: > 0 } sides) return;
        var color = raw.BorderColor ?? "#000000";
        var width = raw.BorderWidth ?? 1;
        if (sides.Contains("All", StringComparison.OrdinalIgnoreCase))
        {
            style["borderColor"] = color;
            style["borderWidth"] = width;
            return;
        }
        foreach (var side in new[] { "Top", "Right", "Bottom", "Left" })
            if (sides.Contains(side, StringComparison.OrdinalIgnoreCase))
            {
                style[$"border{side}Color"] = color;
                style[$"border{side}Width"] = width;
            }
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

    private static readonly HashSet<string> SystemVars = new(StringComparer.OrdinalIgnoreCase)
    { "Page", "PageNofM", "TotalPageCount", "Today", "Now", "Time", "Column", "Line", "ReportName", "ReportAlias" };

    // Group bands repeat per group key: attach Canvas RepeatDto + group metadata (mirrors the
    // RDL/Jasper/DevExpress/FastReport/Telerik group mapping).
    private static void ApplyGroupRepeatMetadata(ElementDto element, RawElement raw, List<MigrationDiagnostic> diagnostics)
    {
        if (raw.GroupName is null) return;

        var dataPath = GroupDataPath(raw);
        var group = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = raw.GroupName,
            ["role"] = raw.GroupRole ?? "header",
            ["dataPath"] = dataPath,
        };
        if (!string.IsNullOrWhiteSpace(raw.GroupCondition)) group["condition"] = raw.GroupCondition!;

        element.Style ??= [];
        element.Style["mrtGroup"] = group;
        element.Repeat = new RepeatDto { DataPath = dataPath, TemplateId = element.Id };
        diagnostics.Add(Warn("CANMIGMRT013",
            $"'{element.Name}' is in a {raw.GroupRole ?? "group"} group band '{raw.GroupName}' — mapped to Canvas repeat metadata; group runtime semantics need review."));
    }

    // Condition like "{Customers.Country}" → "Country"; otherwise the group name.
    private static string GroupDataPath(RawElement raw)
    {
        if (!string.IsNullOrWhiteSpace(raw.GroupCondition))
        {
            var m = Regex.Match(raw.GroupCondition!, @"\{[A-Za-z_]\w*\.([A-Za-z_]\w*)\}");
            if (m.Success) return SafeDataPath(m.Groups[1].Value);
            var single = Regex.Match(raw.GroupCondition!, @"\{([A-Za-z_]\w*)\}");
            if (single.Success) return SafeDataPath(single.Groups[1].Value);
        }
        return SafeDataPath(raw.GroupName ?? "items");
    }

    private static string SafeDataPath(string value)
    {
        var cleaned = new string(value.Select(ch => char.IsLetterOrDigit(ch) || ch is '_' or '.' ? ch : '_').ToArray()).Trim('_');
        return string.IsNullOrWhiteSpace(cleaned) ? "items" : cleaned;
    }

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
            // Compound expression (multiple fields / functions): normalize every {Source.Field}/{Field}
            // reference to a Canvas {{Field}} token (leaving system variables intact); keep the original.
            var normalized = Regex.Replace(text, @"\{(?:[A-Za-z_]\w*\.)?([A-Za-z_]\w*)\}", m =>
                SystemVars.Contains(m.Groups[1].Value) ? m.Value : $"{{{{{m.Groups[1].Value}}}}}");
            element.Expression = text;
            element.Style ??= [];
            element.Style["mrtExpression"] = text;
            if (element.Type == "barcode") element.BarcodeValue = normalized;
            else if (string.IsNullOrEmpty(element.Content)) element.Content = normalized;
            diagnostics.Add(Warn("CANMIGMRT010", $"'{element.Name}' expression '{text}' mapped to a Canvas template with normalized field references — review the syntax."));
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
        public string? Condition;   // GroupHeaderBand grouping expression
        public string? GroupName;   // group identity shared by a header/footer pair
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
        public string? GroupName;        // set when the element is inside a group header/footer band
        public string? GroupRole;        // "header" | "footer"
        public string? GroupCondition;   // grouping expression, e.g. {Customers.Country}
        public string? BorderSides;      // StiBorder sides token (All / Top, Bottom / …)
        public string? BorderColor;
        public double? BorderWidth;
    }
}
