using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Xml.Linq;
using PXA.Core.Contracts;
using PXA.Migration.Abstractions;

namespace PXA.Migration.Rpx;

public sealed class RpxConvertResult
{
    public required DesignExportDto Design { get; init; }
    public required IReadOnlyList<MigrationDiagnostic> Diagnostics { get; init; }
}

/// <summary>
/// Converts a GrapeCity / MESCIUS ActiveReports <c>.rpx</c> "Section Report" — a <b>banded</b> XML layout
/// (root <c>&lt;Report&gt;</c> with a <c>&lt;Sections&gt;</c> collection of ReportHeader/PageHeader/Detail/
/// PageFooter/… bands, each holding <c>&lt;Controls&gt;</c>) — into a PXA <see cref="DesignExportDto"/>.
/// Unlike RDL this is band-relative, so it mirrors the DevExpress XtraReport band-flatten approach:
/// section heights stack into absolute page coordinates; PageHeader/PageFooter become repeating shared
/// elements. Positions are inches. Elements are matched by <see cref="XName.LocalName"/> (namespace-agnostic).
/// </summary>
public sealed class RpxToDesignConverter
{
    private const double InchToPt = 72.0;
    private const double LetterWidthPt = 612;   // ActiveReports section reports default to Letter
    private const double LetterHeightPt = 792;
    private const int MaxSubreportDepth = 4;

    /// <summary>Detects an ActiveReports <c>.rpx</c> Section Report: root <c>&lt;Report&gt;</c> with a
    /// <c>&lt;Sections&gt;</c> collection and not an RDL (<c>reportdefinition</c>) namespace.</summary>
    public static bool LooksLikeRpx(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;
        var trimmed = source.TrimStart();
        if (!trimmed.StartsWith('<')) return false;  // reject C#/JSON/prose; admit <?xml, <!-- comment -->, <Report
        try
        {
            var root = XDocument.Parse(source).Root;
            return root is not null
                && root.Name.LocalName == "Report"
                && !root.Name.NamespaceName.Contains("reportdefinition", StringComparison.OrdinalIgnoreCase)
                && root.DescendantsAndSelf().Any(e => e.Name.LocalName == "Sections");
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
    }

    public RpxConvertResult ConvertAuto(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source cannot be null or empty.", nameof(source));
        return Convert(source);
    }

    public RpxConvertResult Convert(string rpxXml, IReadOnlyDictionary<string, string>? resources = null)
        => ConvertCore(rpxXml, resources, subreportDepth: 0);

    private RpxConvertResult ConvertCore(
        string rpxXml,
        IReadOnlyDictionary<string, string>? resources,
        int subreportDepth)
    {
        if (string.IsNullOrWhiteSpace(rpxXml))
            throw new ArgumentException("Source cannot be null or empty.", nameof(rpxXml));

        XElement root;
        try { root = XDocument.Parse(rpxXml).Root ?? throw new ArgumentException("Empty RPX document."); }
        catch (System.Xml.XmlException ex) { throw new ArgumentException($"Invalid RPX XML: {ex.Message}", nameof(rpxXml)); }

        if (root.Name.LocalName != "Report")
            throw new ArgumentException("Not an ActiveReports RPX report — expected a root <Report> element.", nameof(rpxXml));

        var report = new RawReport
        {
            Name = Attr(root, "Name") ?? "ActiveReports Section Report",
            Script = ParseScript(root)
        };
        ResolvePageSettings(root, report);

        // A report can repeat a section type (e.g. several GroupHeader/GroupFooter bands); when the
        // optional Name attribute is absent they would all collapse to the type name, so keep band
        // names unique — controls reference their band by this same name.
        var sectionNames = new HashSet<string>(StringComparer.Ordinal);
        var sections = Descendant(root, "Sections");
        foreach (var sectionEl in sections?.Elements() ?? Enumerable.Empty<XElement>())
        {
            var type = sectionEl.Name.LocalName;        // ReportHeader, PageHeader, Detail, …
            var name = UniqueName(Attr(sectionEl, "Name") ?? type, sectionNames);
            report.Bands.Add(new RawBand { Name = name, Type = type, Height = ToInchPt(Attr(sectionEl, "Height")) });

            var controls = sectionEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Controls");
            foreach (var ctrl in controls?.Elements() ?? Enumerable.Empty<XElement>())
            {
                var raw = ParseControl(ctrl, name);
                if (raw is not null) report.Elements.Add(raw);
            }
        }

        return BuildDesign(report, resources, subreportDepth);
    }

    private static void ResolvePageSettings(XElement root, RawReport report)
    {
        var ps = Descendant(root, "PageSettings");
        var size = PaperKindSize(Attr(ps, "PaperKind") ?? Attr(root, "PaperKind") ?? "");
        report.PageWidthPt = size.W > 0 ? size.W : LetterWidthPt;
        report.PageHeightPt = size.H > 0 ? size.H : LetterHeightPt;

        // Margins "Left, Right, Top, Bottom" in inches.
        var m = SplitNumbers(Attr(ps, "Margins"));
        if (m.Length >= 4)
        {
            report.MarginLeftPt = m[0] * InchToPt;
            report.MarginTopPt = m[2] * InchToPt;
            report.MarginBottomPt = m[3] * InchToPt;
        }

        if (string.Equals(Attr(ps, "Orientation"), "Landscape", StringComparison.OrdinalIgnoreCase))
            (report.PageWidthPt, report.PageHeightPt) = (report.PageHeightPt, report.PageWidthPt);
    }

    private static RpxScriptMetadata? ParseScript(XElement root)
    {
        var script = Descendant(root, "Script");
        var source = script?.Value;
        if (string.IsNullOrWhiteSpace(source)) return null;

        source = source.Trim();
        return new RpxScriptMetadata(
            Language: Attr(script, "Language") ?? Attr(script, "ScriptLanguage") ?? Attr(root, "ScriptLanguage"),
            Length: source.Length,
            Sha256: System.Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant(),
            Preview: source.Length <= 400 ? source : source[..400]);
    }

    private static PageSettingsDto BuildPageSettings(RawReport report)
    {
        var settings = new PageSettingsDto { Width = report.PageWidthPt, Height = report.PageHeightPt, Unit = "pt" };
        if (report.Script is not null)
        {
            settings.CustomProperties =
            [
                new CustomDocumentPropertyDto
                {
                    Name = "rpxScript",
                    Type = "text",
                    Value = JsonSerializer.Serialize(new Dictionary<string, object?>
                    {
                        ["language"] = report.Script.Language,
                        ["length"] = report.Script.Length,
                        ["sha256"] = report.Script.Sha256,
                        ["preview"] = report.Script.Preview
                    })
                }
            ];
        }

        return settings;
    }

    private static RawElement? ParseControl(XElement el, string bandName)
    {
        var type = el.Name.LocalName;

        // Geometry: Left/Top/Width/Height (inches); a Line uses X1,Y1,X2,Y2 instead.
        double x, y, w, h;
        if (type == "Line" && Attr(el, "X1") is not null)
        {
            var x1 = ToInchPt(Attr(el, "X1")); var y1 = ToInchPt(Attr(el, "Y1"));
            var x2 = ToInchPt(Attr(el, "X2")); var y2 = ToInchPt(Attr(el, "Y2"));
            x = Math.Min(x1, x2); y = Math.Min(y1, y2);
            w = Math.Abs(x2 - x1); h = Math.Abs(y2 - y1);
        }
        else
        {
            x = ToInchPt(Attr(el, "Left")); y = ToInchPt(Attr(el, "Top"));
            w = ToInchPt(Attr(el, "Width")); h = ToInchPt(Attr(el, "Height"));
        }

        var raw = new RawElement
        {
            Name = Attr(el, "Name") ?? type,
            Type = type,
            Band = bandName,
            X = x, Y = y, W = w, H = h,
            Text = Attr(el, "Text") ?? Attr(el, "Caption"),
            DataField = Attr(el, "DataField"),
            ForeColor = ParseColor(Attr(el, "ForeColor")) ?? "#000000",
            BackColor = ParseColor(Attr(el, "BackColor")),
            TextAlign = ParseAlignment(Attr(el, "Alignment")),
            LineWidth = Attr(el, "LineWeight") is { } lw ? ToDouble(lw) : null,
            LineStyle = Attr(el, "LineStyle"),
            CanGrow = ParseBool(Attr(el, "CanGrow")),
            CanShrink = ParseBool(Attr(el, "CanShrink")),
            OutputFormat = Attr(el, "OutputFormat"),
            PageBreak = Attr(el, "PageBreak") ?? Attr(el, "NewPage") ?? Attr(el, "NewPageBefore") ?? Attr(el, "NewPageAfter"),
            SubreportSource = Attr(el, "ReportName") ?? Attr(el, "Report") ?? Attr(el, "FileName")
                ?? Attr(el, "File") ?? Attr(el, "Path") ?? Attr(el, "Source")
        };
        ApplyFont(raw, el);

        if (type is "Line" or "CrossSectionLine" && ParseColor(Attr(el, "LineColor")) is { } lc) raw.ForeColor = lc;
        if (type is "Shape" or "CrossSectionBox") raw.ShapeKind = ShapeKindFromName(Attr(el, "Style") ?? Attr(el, "ShapeType"));
        if (type == "Picture") raw.ImageDataUrl = ExtractImageDataUrl(el);
        if (type == "Barcode") raw.Symbology = Attr(el, "Style") ?? Attr(el, "Symbology") ?? Attr(el, "BarCodeStyle");
        if (type is "CheckBox") raw.Checked = string.Equals(Attr(el, "Checked"), "True", StringComparison.OrdinalIgnoreCase);

        return raw;
    }

    // ── Band-flatten build (mirrors the DevExpress report converter) ────────────────────────────────

    private RpxConvertResult BuildDesign(
        RawReport report,
        IReadOnlyDictionary<string, string>? resources,
        int subreportDepth)
    {
        var diagnostics = new List<MigrationDiagnostic>();

        var bandByName = report.Bands.ToDictionary(b => b.Name, StringComparer.Ordinal);
        var orderedBands = report.Bands.OrderBy(b => BandOrder(b.Type)).ToList();
        var bandTop = new Dictionary<string, double>(StringComparer.Ordinal);
        var offset = report.MarginTopPt;
        foreach (var band in orderedBands)
        {
            bandTop[band.Name] = offset;
            offset += band.Height;
        }

        var elements = new List<ElementDto>();
        var sharedElements = new List<ElementDto>();
        var mapped = 0;

        foreach (var raw in report.Elements)
        {
            var bandType = raw.Band is not null && bandByName.TryGetValue(raw.Band, out var b) ? b.Type : "";

            double yPt;
            if (bandType == "PageHeader")
                yPt = report.MarginTopPt + raw.Y;
            else if (bandType == "PageFooter")
                yPt = report.PageHeightPt - report.MarginBottomPt
                      - (raw.Band is not null && bandByName.TryGetValue(raw.Band, out var fb) ? fb.Height : 0) + raw.Y;
            else
                yPt = (raw.Band is not null && bandTop.TryGetValue(raw.Band, out var t) ? t : offset) + raw.Y;

            var x = report.MarginLeftPt + raw.X;

            var element = MapControl(raw, x, yPt, diagnostics);
            if (element is null) continue;

            diagnostics.Add(Info("CANMIGRPX002", $"'{raw.Name}' ({raw.Type}) → PXA {element.Type}."));

            if (raw.DataField is { Length: > 0 } field)
            {
                element.Binding = field;
                if (element.Type == "text") element.Content = $"{{{{{field}}}}}";
                if (!string.IsNullOrWhiteSpace(raw.OutputFormat)) element.Formatter = raw.OutputFormat;
                diagnostics.Add(Info("CANMIGRPX010", $"'{raw.Name}' bound to field {field} → PXA binding '{field}'."));
            }

            ApplyBandMetadata(element, raw, bandType, diagnostics);
            ApplyDynamicMetadata(element, raw, diagnostics);
            var pageBoundaryElements = CreatePageBoundaryElements(raw, element);

            if (raw.Type == "SubReport"
                && TryInlineSubreport(raw, element, resources, subreportDepth, diagnostics) is { Count: > 0 } inlined)
            {
                elements.AddRange(inlined);
                elements.AddRange(pageBoundaryElements);
                mapped += inlined.Count + pageBoundaryElements.Count;
                continue;
            }

            var target = bandType is "PageHeader" or "PageFooter" ? sharedElements : elements;
            target.Add(element);
            target.AddRange(pageBoundaryElements);
            mapped += 1 + pageBoundaryElements.Count;
        }

        elements.Sort((p, q) => p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.X.CompareTo(q.X));
        sharedElements.Sort((p, q) => p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.X.CompareTo(q.X));

        if (report.Script is not null)
            diagnostics.Add(Warn("CANMIGRPX018",
                "Report contains embedded script — PXA imports script metadata as a no-op; migrate behaviour manually."));

        diagnostics.Insert(0, Info("CANMIGRPX001",
            $"ActiveReports section report '{report.Name}' detected — {report.Bands.Count} section(s), {mapped} control(s) mapped."));

        var design = new DesignExportDto
        {
            Id = $"rpx-report-{Guid.NewGuid():N}",
            Name = report.Name,
            Category = "imported",
            Description = "Imported from an ActiveReports section report (.rpx).",
            PageSettings = BuildPageSettings(report),
            Pages = [new PageDto { Id = "page-1", Elements = elements }],
            SharedElements = sharedElements
        };

        return new RpxConvertResult { Design = design, Diagnostics = diagnostics };
    }

    private static ElementDto? MapControl(RawElement raw, double x, double y, List<MigrationDiagnostic> diagnostics)
    {
        var element = new ElementDto { Id = $"rpx-{raw.Name}", Name = raw.Name, X = x, Y = y, Width = raw.W, Height = raw.H };

        switch (raw.Type)
        {
            case "Label":
            case "TextBox":
                element.Type = "text";
                element.Content = raw.Text ?? "";
                element.Style = BuildTextStyle(raw);
                return element;

            case "Line":
            case "CrossSectionLine":
                element.Type = "line";
                element.Style = new Dictionary<string, object> { ["color"] = raw.ForeColor };
                if (raw.LineWidth is { } lineW) element.Style["strokeWidth"] = lineW;
                if (DashStyleFromName(raw.LineStyle) is { } dash) element.Style["dashStyle"] = dash;
                if (raw.Type == "CrossSectionLine")
                    element.Style["rpxCrossSection"] = true;
                return element;

            case "Shape":
            case "CrossSectionBox":
                element.Type = raw.ShapeKind switch { "ellipse" => "circle", _ => "rect" };
                element.Style = new Dictionary<string, object> { ["borderColor"] = raw.ForeColor };
                if (raw.BackColor is { } bg) element.Style["backgroundColor"] = bg;
                if (raw.LineWidth is { } borderW) element.Style["borderWidth"] = borderW;
                if (raw.Type == "CrossSectionBox")
                    element.Style["rpxCrossSection"] = true;
                return element;

            case "Picture":
                element.Type = "image";
                element.FitMode = "contain";
                if (raw.ImageDataUrl is { } dataUrl)
                    element.Content = dataUrl;
                else
                    diagnostics.Add(Warn("CANMIGRPX012",
                        $"'{raw.Name}' picture data isn't embeddable from source — inserted an empty image placeholder."));
                return element;

            case "RichTextBox":
                element.Type = "richtext";
                element.HtmlContent = $"<p>{raw.Text ?? ""}</p>";
                return element;

            case "Barcode":
                element.Type = "barcode";
                element.BarcodeValue = raw.DataField is { Length: > 0 } f ? $"{{{{{f}}}}}" : raw.Text ?? "";
                element.BarcodeType = BarcodeTypeFromSymbology(raw.Symbology);
                return element;

            case "CheckBox":
                element.Type = "checkmark";
                element.CheckState = raw.Checked ? "checked" : "empty";
                element.Content = raw.Text ?? "";
                return element;

            case "SubReport":
                diagnostics.Add(Warn("CANMIGRPX011",
                    $"'{raw.Name}' is a sub-report — requires manual migration; inserted a placeholder."));
                return Placeholder(element, $"[Sub-report: {raw.Name} — migrate manually]");

            case "OleObject":
                diagnostics.Add(Warn("CANMIGRPX011",
                    $"'{raw.Name}' is an OLE object — PXA has no native OLE embedding; inserted a placeholder."));
                element = Placeholder(element, $"[OLE object: {raw.Name} — migrate manually]");
                element.Style!["rpxOleObject"] = new Dictionary<string, object?>
                {
                    ["name"] = raw.Name,
                    ["sourceType"] = "OleObject"
                };
                return element;

            default:
                diagnostics.Add(Warn("CANMIGRPX011", $"'{raw.Name}' is a {raw.Type} — not supported by PXA yet; inserted a placeholder."));
                return Placeholder(element, $"[{raw.Type}: migrate manually]");
        }
    }

    private List<ElementDto>? TryInlineSubreport(
        RawElement raw,
        ElementDto parent,
        IReadOnlyDictionary<string, string>? resources,
        int subreportDepth,
        List<MigrationDiagnostic> diagnostics)
    {
        if (resources is null || resources.Count == 0 || subreportDepth >= MaxSubreportDepth)
            return null;

        var sourceName = raw.SubreportSource;
        if (string.IsNullOrWhiteSpace(sourceName))
            return null;

        if (!TryResolveSubreportSource(sourceName, resources, out var matchedKey, out var subreportXml))
            return null;

        RpxConvertResult result;
        try
        {
            result = ConvertCore(subreportXml, resources, subreportDepth + 1);
        }
        catch (ArgumentException ex)
        {
            diagnostics.Add(Warn("CANMIGRPX017",
                $"'{raw.Name}' subreport resource '{matchedKey}' could not be converted: {ex.Message}"));
            return null;
        }

        var imported = new List<ElementDto>();
        foreach (var child in result.Design.Pages.SelectMany(p => p.Elements).Concat(result.Design.SharedElements))
        {
            var clone = CloneElement(child);
            clone.Id = $"{parent.Id}-{clone.Id}";
            clone.X = parent.X + clone.X;
            clone.Y = parent.Y + clone.Y;
            clone.Style ??= [];
            clone.Style["rpxInlinedFromSubreport"] = matchedKey;
            clone.Style["rpxParentSubreport"] = raw.Name;
            imported.Add(clone);
        }

        diagnostics.Add(Info("CANMIGRPX017",
            $"'{raw.Name}' subreport resource '{matchedKey}' inlined with {imported.Count} PXA element(s)."));
        return imported;
    }

    private static bool TryResolveSubreportSource(
        string sourceName,
        IReadOnlyDictionary<string, string> resources,
        out string matchedKey,
        out string source)
    {
        foreach (var candidate in SubreportSourceCandidates(sourceName))
        {
            if (resources.TryGetValue(candidate, out source!))
            {
                matchedKey = candidate;
                return true;
            }
        }

        foreach (var (key, value) in resources)
        {
            if (SubreportSourceCandidates(sourceName).Any(candidate =>
                    string.Equals(Path.GetFileName(key), candidate, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Path.GetFileNameWithoutExtension(key), Path.GetFileNameWithoutExtension(candidate), StringComparison.OrdinalIgnoreCase)))
            {
                matchedKey = key;
                source = value;
                return true;
            }
        }

        matchedKey = "";
        source = "";
        return false;
    }

    private static IEnumerable<string> SubreportSourceCandidates(string sourceName)
    {
        var trimmed = sourceName.Trim().Trim('"', '\'');
        if (trimmed.Length == 0) yield break;

        yield return trimmed;
        yield return Path.GetFileName(trimmed);

        var withoutExtension = Path.GetFileNameWithoutExtension(trimmed);
        if (!string.IsNullOrWhiteSpace(withoutExtension))
        {
            yield return $"{withoutExtension}.rpx";
            yield return withoutExtension;
        }
    }

    private static ElementDto CloneElement(ElementDto element) =>
        JsonSerializer.Deserialize<ElementDto>(JsonSerializer.Serialize(element))!;

    // Keeps an unsupported control visible at its original position/size so the layout isn't silently
    // holed — a muted, captioned block the user can replace in the designer.
    private static ElementDto Placeholder(ElementDto element, string label)
    {
        element.Type = "text";
        element.Content = label;
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
        if (raw.CanGrow == true) style["overflow"] = "visible";
        return style;
    }

    private static void ApplyBandMetadata(ElementDto element, RawElement raw, string bandType, List<MigrationDiagnostic> diagnostics)
    {
        if (element.Style is null) element.Style = [];

        element.Style["rpxBand"] = new Dictionary<string, object?>
        {
            ["name"] = raw.Band,
            ["type"] = bandType
        };

        if (bandType is "GroupHeader" or "GroupFooter")
        {
            var dataPath = SafeRepeatPath(raw.Band is { Length: > 0 } b ? b : $"{bandType}Rows");
            element.Style["rpxGroupRepeat"] = new Dictionary<string, object?>
            {
                ["band"] = raw.Band,
                ["bandType"] = bandType,
                ["dataPath"] = dataPath,
                ["role"] = bandType == "GroupHeader" ? "header" : "footer"
            };
            element.Repeat = new RepeatDto { DataPath = dataPath, TemplateId = element.Id };
            diagnostics.Add(Warn("CANMIGRPX013",
                $"'{raw.Name}' is in {bandType} '{raw.Band}' — mapped to PXA repeat metadata; group runtime semantics need review."));
        }
        else if (bandType == "Detail")
        {
            var dataPath = SafeRepeatPath(raw.Band is { Length: > 0 } b ? b : "DetailRows");
            element.Style["rpxDetailRepeat"] = new Dictionary<string, object?>
            {
                ["band"] = raw.Band,
                ["dataPath"] = dataPath
            };
            element.Repeat = new RepeatDto { DataPath = dataPath, TemplateId = element.Id };
        }
        else if (bandType == "ReportFooter")
        {
            element.Style["rpxReportFooter"] = new Dictionary<string, object?>
            {
                ["band"] = raw.Band,
                ["scope"] = "report-end"
            };
        }
    }

    private static void ApplyDynamicMetadata(ElementDto element, RawElement raw, List<MigrationDiagnostic> diagnostics)
    {
        if (element.Style is null) element.Style = [];

        if (raw.CanGrow == true || raw.CanShrink == true)
        {
            element.Style["rpxAutoSize"] = new Dictionary<string, object?>
            {
                ["canGrow"] = raw.CanGrow,
                ["canShrink"] = raw.CanShrink
            };
            if (raw.CanShrink == true) element.Style["rpxCanShrink"] = true;
            diagnostics.Add(Warn("CANMIGRPX014",
                $"'{raw.Name}' uses CanGrow/CanShrink — PXA imports wrapping/overflow hints, but dynamic band reflow needs review."));
        }

        if (!string.IsNullOrWhiteSpace(raw.OutputFormat))
        {
            element.Style["rpxOutputFormat"] = raw.OutputFormat;
            diagnostics.Add(Warn("CANMIGRPX015",
                $"'{raw.Name}' uses OutputFormat '{raw.OutputFormat}' — preserved as PXA formatter metadata; exact formatting should be reviewed."));
        }

        if (!string.IsNullOrWhiteSpace(raw.PageBreak))
        {
            element.Style["rpxPageBreak"] = raw.PageBreak;
            diagnostics.Add(Warn("CANMIGRPX016",
                $"'{raw.Name}' declares page break/new-page behaviour '{raw.PageBreak}' — preserved as metadata for review."));
        }

        if (raw.Type is "CrossSectionLine" or "CrossSectionBox")
        {
            element.Style["rpxCrossSectionControl"] = raw.Type;
            diagnostics.Add(Warn("CANMIGRPX016",
                $"'{raw.Name}' is a {raw.Type} — mapped visually and preserved as cross-section metadata."));
        }
    }

    private static List<ElementDto> CreatePageBoundaryElements(RawElement raw, ElementDto source)
    {
        var modes = PageBoundaryModes(raw.PageBreak).ToArray();
        if (modes.Length == 0) return [];

        var boundaries = new List<ElementDto>();
        foreach (var mode in modes)
        {
            var y = mode == "start" ? Math.Max(0, source.Y - 10) : source.Y + source.Height + 6;
            var boundary = new ElementDto
            {
                Id = $"{source.Id}-page-{mode}",
                Name = $"{source.Name} page {mode}",
                Type = "pageboundary",
                X = source.X,
                Y = y,
                Width = Math.Max(source.Width, 144),
                Height = 18,
                PageBoundaryMode = mode,
                Content = mode == "end" ? "Page end" : "Page start",
                Style = new Dictionary<string, object>
                {
                    ["color"] = "#7C3AED",
                    ["dashStyle"] = "dashed",
                    ["rpxPageBreak"] = raw.PageBreak!,
                    ["rpxPageBreakFor"] = raw.Name
                }
            };
            boundaries.Add(boundary);
        }

        return boundaries;
    }

    private static IEnumerable<string> PageBoundaryModes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) yield break;
        var normalized = value.Trim().ToLowerInvariant();

        if (normalized is "before" or "start" or "newpagebefore" or "true")
            yield return "start";
        else if (normalized is "after" or "end" or "newpageafter")
            yield return "end";
        else if (normalized.Contains("before") && normalized.Contains("after")
                 || normalized.Contains("start") && normalized.Contains("end"))
        {
            yield return "start";
            yield return "end";
        }
        else if (normalized.Contains("before") || normalized.Contains("start"))
            yield return "start";
        else if (normalized.Contains("after") || normalized.Contains("end"))
            yield return "end";
    }

    private static void ApplyFont(RawElement raw, XElement el)
    {
        // ActiveReports serializes font sub-properties as hyphenated attributes: Font-FamilyName, Font-Size, …
        if (Attr(el, "Font-FamilyName") is { Length: > 0 } family) raw.FontFamily = family;
        if (Attr(el, "Font-Size") is { } size && double.TryParse(size, NumberStyles.Any, CultureInfo.InvariantCulture, out var fs)) raw.FontSize = fs;
        raw.Bold = IsTrue(Attr(el, "Font-Bold"));
        raw.Italic = IsTrue(Attr(el, "Font-Italic"));
        raw.Underline = IsTrue(Attr(el, "Font-Underline"));
        raw.Strikeout = IsTrue(Attr(el, "Font-Strikeout")) || IsTrue(Attr(el, "Font-Strikethrough"));
    }

    // ── Value helpers ──────────────────────────────────────────────────────────────────────────────

    private static string ParseAlignment(string? text)
    {
        text ??= "";
        if (text.Contains("Center", StringComparison.OrdinalIgnoreCase)) return "center";
        if (text.Contains("Right", StringComparison.OrdinalIgnoreCase) || text.Contains("Far", StringComparison.OrdinalIgnoreCase)) return "right";
        if (text.Contains("Justify", StringComparison.OrdinalIgnoreCase)) return "justify";
        return "left";  // Left / Near / General / empty
    }

    private static string? DashStyleFromName(string? lineStyle)
    {
        if (string.IsNullOrEmpty(lineStyle) || lineStyle.Equals("Solid", StringComparison.OrdinalIgnoreCase)) return null;
        if (lineStyle.Contains("Dash", StringComparison.OrdinalIgnoreCase)) return "dashed";
        if (lineStyle.Contains("Dot", StringComparison.OrdinalIgnoreCase)) return "dotted";
        return null;
    }

    private static string ShapeKindFromName(string? style)
    {
        style ??= "";
        if (style.Contains("Ellipse", StringComparison.OrdinalIgnoreCase) || style.Contains("Circle", StringComparison.OrdinalIgnoreCase)) return "ellipse";
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
            ?? el.Elements().FirstOrDefault(e => e.Name.LocalName is "Image" or "ImageData")?.Value;
        if (string.IsNullOrWhiteSpace(candidate)) return null;
        if (candidate.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return candidate;
        var b64 = Regex.Replace(candidate, @"\s+", "");
        return b64.Length >= 16 && b64.Length % 4 == 0 && Regex.IsMatch(b64, @"^[A-Za-z0-9+/]+={0,2}$")
            ? $"data:image/png;base64,{b64}" : null;
    }

    // Colour: named, "#RRGGBB"/"#RGB", "0xAARRGGBB"/"0xRRGGBB", or "R, G, B".
    private static string? ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        if (v.Equals("Transparent", StringComparison.OrdinalIgnoreCase)) return null;
        if (v.StartsWith('#'))
            return v.Length == 4 ? $"#{v[1]}{v[1]}{v[2]}{v[2]}{v[3]}{v[3]}".ToUpperInvariant() : v.ToUpperInvariant();
        if (v.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            var hex = v[2..];
            if (hex.Length == 8) hex = hex[2..];        // drop alpha from AARRGGBB
            return hex.Length == 6 ? $"#{hex.ToUpperInvariant()}" : null;
        }
        var nums = SplitNumbers(v);
        if (nums.Length >= 3)
        {
            var o = nums.Length >= 4 ? 1 : 0;           // A, R, G, B → skip alpha
            return $"#{(int)nums[o]:X2}{(int)nums[o + 1]:X2}{(int)nums[o + 2]:X2}";
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
        _ => "#000000"
    };

    private static int BandOrder(string bandType) => bandType switch
    {
        "ReportHeader" => 0,
        "PageHeader" => 1,
        "GroupHeader" => 2,
        "Detail" => 3,
        "GroupFooter" => 4,
        "ReportFooter" => 5,
        "PageFooter" => 6,
        _ => 100
    };

    private static string UniqueName(string name, HashSet<string> seen)
    {
        if (seen.Add(name)) return name;
        for (var i = 2; ; i++)
        {
            var candidate = $"{name}_{i}";
            if (seen.Add(candidate)) return candidate;
        }
    }

    private static double ToInchPt(string? value) => ToDouble(value) * InchToPt;

    private static double ToDouble(string? value) =>
        double.TryParse((value ?? "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;

    private static double[] SplitNumbers(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([',', ' '], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                   .Select(p => double.TryParse(p, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : double.NaN)
                   .Where(d => !double.IsNaN(d))
                   .ToArray();

    private static bool IsTrue(string? value) => string.Equals(value, "True", StringComparison.OrdinalIgnoreCase);

    private static bool? ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (value.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        return null;
    }

    private static string SafeRepeatPath(string value)
    {
        var safe = Regex.Replace(value, @"[^\w.]+", "");
        return string.IsNullOrWhiteSpace(safe) ? "Rows" : safe;
    }

    private static string? Attr(XElement? el, string name) => el?.Attribute(name)?.Value;

    private static XElement? Descendant(XElement? el, string name) =>
        el?.DescendantsAndSelf().FirstOrDefault(e => e.Name.LocalName == name);

    private static MigrationDiagnostic Info(string id, string message) =>
        new() { Id = id, Message = message, Severity = MigrationDiagnosticSeverity.Info };

    private static MigrationDiagnostic Warn(string id, string message) =>
        new() { Id = id, Message = message, Severity = MigrationDiagnosticSeverity.Warning };

    // ── Neutral intermediate model (band-based) ────────────────────────────────────────────────────

    private sealed class RawReport
    {
        public string Name = "ActiveReports Section Report";
        public double PageWidthPt = LetterWidthPt, PageHeightPt = LetterHeightPt;
        public double MarginLeftPt, MarginTopPt, MarginBottomPt;
        public RpxScriptMetadata? Script;
        public List<RawBand> Bands = [];
        public List<RawElement> Elements = [];
    }

    private sealed record RpxScriptMetadata(string? Language, int Length, string Sha256, string Preview);

    private sealed class RawBand
    {
        public required string Name;
        public required string Type;
        public double Height;   // points
    }

    private sealed class RawElement
    {
        public required string Name;
        public required string Type;
        public string? Band;
        public double X, Y, W, H;   // band-relative, points
        public string? Text;
        public string? DataField;
        public string? FontFamily;
        public double? FontSize;
        public bool Bold, Italic, Underline, Strikeout;
        public string ForeColor = "#000000";
        public string? BackColor;
        public string TextAlign = "left";
        public string? ShapeKind;
        public string? ImageDataUrl;
        public string? Symbology;
        public bool Checked;
        public double? LineWidth;
        public string? LineStyle;
        public bool? CanGrow;
        public bool? CanShrink;
        public string? OutputFormat;
        public string? PageBreak;
        public string? SubreportSource;
    }
}
