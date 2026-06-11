using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Canvas.Core.Contracts;
using Canvas.Migration.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Canvas.Migration.DevExpressReport;

public sealed class XtraReportConvertResult
{
    public required DesignExportDto Design { get; init; }
    public required IReadOnlyList<MigrationDiagnostic> Diagnostics { get; init; }
}

/// <summary>
/// Converts a DevExpress XtraReport — either a C# Report Designer class or a serialized
/// <c>.repx</c> XML layout — into a Canvas <see cref="DesignExportDto"/> the visual designer can open.
/// Both front-ends produce a neutral <see cref="RawReport"/> that the shared builder flattens (band
/// stacking → absolute coordinates), unit-converts, and maps to Canvas elements.
/// </summary>
public sealed class XtraReportToDesignConverter
{
    private const double A4WidthPt = 595;
    private const double A4HeightPt = 842;

    /// <summary>Detects whether the source is a <c>.repx</c> XML layout (vs a C# class) and converts it.</summary>
    public XtraReportConvertResult ConvertAuto(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source cannot be null or empty.", nameof(source));
        return LooksLikeRepx(source) ? ConvertRepx(source) : Convert(source);
    }

    private static bool LooksLikeRepx(string source)
    {
        var trimmed = source.TrimStart();
        return trimmed.StartsWith("<?xml", StringComparison.Ordinal)
            || trimmed.StartsWith("<XtraReportsLayoutSerializer", StringComparison.Ordinal);
    }

    // ── C# Report Designer class → RawReport ───────────────────────────────────────────────────────

    public XtraReportConvertResult Convert(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));

        var root = CSharpSyntaxTree.ParseText(sourceCode).GetCompilationUnitRoot();
        var reportClass = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault(IsXtraReportClass);

        var fieldTypes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            var typeName = SimpleName(field.Declaration.Type);
            foreach (var v in field.Declaration.Variables)
                fieldTypes[v.Identifier.ValueText] = typeName;
        }

        var props = new Dictionary<string, Dictionary<string, ExpressionSyntax>>(StringComparer.Ordinal);
        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is not MemberAccessExpressionSyntax left) continue;
            var receiver = ReceiverName(left.Expression);
            if (receiver is null) continue;
            if (!props.TryGetValue(receiver, out var bag))
                props[receiver] = bag = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal);
            bag[left.Name.Identifier.ValueText] = assignment.Right;
        }

        var controlBand = new Dictionary<string, string>(StringComparer.Ordinal);
        var controlTextExpr = new Dictionary<string, string>(StringComparer.Ordinal);
        var boundOther = new HashSet<string>(StringComparer.Ordinal);
        var tableRows = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var rowCells = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (inv.Expression is not MemberAccessExpressionSyntax call) continue;
            if (call.Name.Identifier.ValueText is not ("Add" or "AddRange")) continue;
            if (call.Expression is not MemberAccessExpressionSyntax collection) continue;
            var owner = ReceiverName(collection.Expression);
            if (owner is null) continue;

            switch (collection.Name.Identifier.ValueText)
            {
                case "Controls":
                    foreach (var ctrl in ExtractControlNames(inv.ArgumentList)) controlBand[ctrl] = owner;
                    break;
                case "Rows":
                    (tableRows.TryGetValue(owner, out var rl) ? rl : tableRows[owner] = []).AddRange(ExtractControlNames(inv.ArgumentList));
                    break;
                case "Cells":
                    (rowCells.TryGetValue(owner, out var cl) ? cl : rowCells[owner] = []).AddRange(ExtractControlNames(inv.ArgumentList));
                    break;
                case "ExpressionBindings" or "DataBindings":
                    CaptureBindings(owner, inv.ArgumentList, controlTextExpr, boundOther);
                    break;
            }
        }

        var unitScale = ResolveUnitScale(NameOf(GetProp(props, "this", "ReportUnit")));
        var (marginLeft, marginTop, marginBottom) = ResolveMarginsCSharp(GetProp(props, "this", "Margins"));
        var (pageW, pageH) = ResolvePageSizeCSharp(props, unitScale);

        var report = new RawReport
        {
            Name = reportClass?.Identifier.ValueText ?? "DevExpress Report",
            UnitScale = unitScale,
            MarginLeft = marginLeft,
            MarginTop = marginTop,
            MarginBottom = marginBottom,
            PageWidthPt = pageW,
            PageHeightPt = pageH,
            HasScripts = root.DescendantNodes().OfType<AssignmentExpressionSyntax>()
                .Any(a => a.Left is MemberAccessExpressionSyntax m && m.ToString().Contains(".Scripts.", StringComparison.Ordinal))
        };

        foreach (var (name, type) in fieldTypes)
        {
            if (type.EndsWith("Band", StringComparison.Ordinal))
            {
                report.Bands.Add(new RawBand { Name = name, Type = type, Height = ToNumber(GetProp(props, name, "HeightF")) });
                continue;
            }
            if (type is "XRTableRow" or "XRTableCell") continue;
            if (!IsKnownControl(type) && !IsUnsupportedControl(type)) continue;

            var bag = props.TryGetValue(name, out var b) ? b : [];
            var (locX, locY) = ParsePoint(bag.GetValueOrDefault("LocationF"));
            var (sizeW, sizeH) = ParseSize(bag.GetValueOrDefault("SizeF"));

            var el = new RawElement
            {
                Name = name,
                Type = type,
                Band = controlBand.GetValueOrDefault(name),
                X = locX,
                Y = locY,
                W = sizeW,
                H = sizeH,
                Text = ParseString(bag.GetValueOrDefault("Text")),
                ForeColor = ParseColor(bag.GetValueOrDefault("ForeColor")),
                BackColor = ParseColor(bag.GetValueOrDefault("BackColor")),
                TextAlign = ParseAlignment(NameOf(bag.GetValueOrDefault("TextAlignment"))),
                TextExpression = controlTextExpr.GetValueOrDefault(name),
                HasUnmappedBinding = boundOther.Contains(name)
            };
            ApplyFontCSharp(el, bag.GetValueOrDefault("Font"));
            if (type == "XRTable")
            {
                el.TableCells = BuildTableCellsCSharp(name, tableRows, rowCells, props);
                el.ColumnAlignments = HeaderAlignmentsCSharp(name, tableRows, rowCells, props);
            }
            if (type == "XRShape") el.ShapeKind = ShapeKindFromName(CreationTypeName(bag.GetValueOrDefault("Shape")));
            if (type == "XRCheckBox") el.CheckState = CheckStateCSharp(bag);
            report.Elements.Add(el);
        }

        return BuildDesign(report);
    }

    // ── .repx XML layout → RawReport ───────────────────────────────────────────────────────────────

    public XtraReportConvertResult ConvertRepx(string repxXml)
    {
        if (string.IsNullOrWhiteSpace(repxXml))
            throw new ArgumentException("Source cannot be null or empty.", nameof(repxXml));

        XElement root;
        try { root = XDocument.Parse(repxXml).Root ?? throw new ArgumentException("Empty .repx document."); }
        catch (System.Xml.XmlException ex) { throw new ArgumentException($"Invalid .repx XML: {ex.Message}", nameof(repxXml)); }

        var unitScale = ResolveUnitScale(Attr(root, "Measurement") ?? Attr(root, "ReportUnit") ?? "");
        var (marginLeft, marginTop, marginBottom) = ResolveMarginsXml(Attr(root, "Margins"));
        var (pageW, pageH) = ResolvePageSizeXml(root, unitScale);

        var report = new RawReport
        {
            Name = Attr(root, "Name") ?? "DevExpress Report",
            UnitScale = unitScale,
            MarginLeft = marginLeft,
            MarginTop = marginTop,
            MarginBottom = marginBottom,
            PageWidthPt = pageW,
            PageHeightPt = pageH,
            HasScripts = root.Descendants().Any(e => e.Name.LocalName == "Scripts")
                || root.DescendantsAndSelf().Attributes("Scripts").Any(a => !string.IsNullOrWhiteSpace(a.Value))
        };

        // <Bands><ItemN ControlType="...DetailBand,..." HeightF="..."><Controls>...</Controls></ItemN></Bands>
        var bandsContainer = root.Elements().FirstOrDefault(e => e.Name.LocalName == "Bands");
        foreach (var bandEl in bandsContainer?.Elements() ?? Enumerable.Empty<XElement>())
        {
            var bandType = SimpleTypeOf(Attr(bandEl, "ControlType"));
            var bandName = Attr(bandEl, "Name") ?? bandType;
            report.Bands.Add(new RawBand { Name = bandName, Type = bandType, Height = ToDouble(Attr(bandEl, "HeightF")) });

            var controls = bandEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Controls");
            foreach (var ctrlEl in controls?.Elements() ?? Enumerable.Empty<XElement>())
            {
                var raw = ParseRepxControl(ctrlEl, bandName);
                if (raw is not null) report.Elements.Add(raw);
            }
        }

        return BuildDesign(report);
    }

    private static RawElement? ParseRepxControl(XElement el, string bandName)
    {
        var type = SimpleTypeOf(Attr(el, "ControlType"));
        if (!IsKnownControl(type) && !IsUnsupportedControl(type)) return null;

        var (locX, locY) = ParseCommaPair(Attr(el, "LocationFloat") ?? Attr(el, "LocationF"));
        var (sizeW, sizeH) = ParseCommaPair(Attr(el, "SizeF") ?? Attr(el, "Size"));

        var raw = new RawElement
        {
            Name = Attr(el, "Name") ?? type,
            Type = type,
            Band = bandName,
            X = locX,
            Y = locY,
            W = sizeW,
            H = sizeH,
            Text = Attr(el, "Text"),
            ForeColor = ParseColorString(Attr(el, "ForeColor")),
            BackColor = ParseColorString(Attr(el, "BackColor")),
            TextAlign = ParseAlignment(Attr(el, "TextAlignment"))
        };
        ApplyFontString(raw, Attr(el, "Font"));

        // <ExpressionBindings><ItemN PropertyName="Text" Expression="[X]" /></ExpressionBindings>
        var bindings = el.Elements().FirstOrDefault(e => e.Name.LocalName == "ExpressionBindings");
        foreach (var bindEl in bindings?.Elements() ?? Enumerable.Empty<XElement>())
        {
            if (string.Equals(Attr(bindEl, "PropertyName"), "Text", StringComparison.Ordinal))
                raw.TextExpression = Attr(bindEl, "Expression");
            else
                raw.HasUnmappedBinding = true;
        }

        if (type == "XRShape") raw.ShapeKind = ShapeKindFromName(ShapeTypeXml(el));
        if (type == "XRCheckBox") raw.CheckState = CheckStateXml(el);
        if (type == "XRPictureBox") raw.ImageDataUrl = ExtractImageDataUrl(el);

        // XRTable: <Rows><ItemN ControlType="XRTableRow"><Cells><ItemN ControlType="XRTableCell" Text="..."/></Cells></ItemN></Rows>
        if (type == "XRTable")
        {
            var rowsEl = el.Elements().FirstOrDefault(e => e.Name.LocalName == "Rows");
            var grid = new List<List<string>>();
            foreach (var rowEl in rowsEl?.Elements() ?? Enumerable.Empty<XElement>())
            {
                var cellsEl = rowEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Cells");
                var cells = (cellsEl?.Elements() ?? Enumerable.Empty<XElement>())
                    .Select(c => Attr(c, "Text") ?? "").ToList();
                grid.Add(cells);
            }
            raw.TableCells = grid.Count > 0 ? grid : null;

            var headerCells = rowsEl?.Elements().FirstOrDefault()
                ?.Elements().FirstOrDefault(e => e.Name.LocalName == "Cells")?.Elements();
            raw.ColumnAlignments = headerCells?.Select(c => ParseAlignment(Attr(c, "TextAlignment"))).ToArray();
        }

        return raw;
    }

    // ── Shared build: RawReport → DesignExportDto ──────────────────────────────────────────────────

    private static XtraReportConvertResult BuildDesign(RawReport report)
    {
        var diagnostics = new List<MigrationDiagnostic>();

        var bandByName = report.Bands.ToDictionary(b => b.Name, StringComparer.Ordinal);
        var orderedBands = report.Bands.OrderBy(b => BandOrder(b.Type)).ToList();
        var hasTopMargin = report.Bands.Any(b => b.Type == "TopMarginBand");
        var bandTop = new Dictionary<string, double>(StringComparer.Ordinal);
        var offset = hasTopMargin ? 0 : report.MarginTop;
        foreach (var band in orderedBands)
        {
            bandTop[band.Name] = offset;
            offset += band.Height;
        }

        var pageHeightUnits = report.PageHeightPt / report.UnitScale;
        var elements = new List<ElementDto>();
        var sharedElements = new List<ElementDto>();
        var controlCount = 0;

        foreach (var raw in report.Elements)
        {
            var bandType = raw.Band is not null && bandByName.TryGetValue(raw.Band, out var band) ? band.Type : "";

            double yUnits;
            if (bandType == "PageHeaderBand")
                yUnits = report.MarginTop + raw.Y;
            else if (bandType == "PageFooterBand")
                yUnits = pageHeightUnits - report.MarginBottom - (bandByName.TryGetValue(raw.Band!, out var fb) ? fb.Height : 0) + raw.Y;
            else
                yUnits = (raw.Band is not null && bandTop.TryGetValue(raw.Band, out var t) ? t : offset) + raw.Y;

            var x = (report.MarginLeft + raw.X) * report.UnitScale;
            var y = yUnits * report.UnitScale;
            var w = raw.W * report.UnitScale;
            var h = raw.H * report.UnitScale;

            var element = raw.Type == "XRTable"
                ? BuildTable(raw, x, y, w, h, diagnostics)
                : MapControl(raw, x, y, w, h, diagnostics);
            if (element is null) continue;

            diagnostics.Add(Info("CANMIGDEVREP002", $"'{raw.Name}' ({raw.Type}) → Canvas {element.Type}."));

            if (raw.TextExpression is { } expr)
                ApplyBinding(element, expr, diagnostics);
            else if (raw.HasUnmappedBinding)
                diagnostics.Add(Warn("CANMIGDEVREP010",
                    $"'{raw.Name}' has a non-text data binding that wasn't mapped — re-bind it in Canvas."));

            (bandType is "PageHeaderBand" or "PageFooterBand" ? sharedElements : elements).Add(element);
            controlCount++;
        }

        elements.Sort((p, q) => p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.X.CompareTo(q.X));
        sharedElements.Sort((p, q) => p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.X.CompareTo(q.X));

        if (report.HasScripts)
            diagnostics.Add(Warn("CANMIGDEVREP012",
                "Report contains scripts/event handlers — Canvas has no scripting; migrate that logic manually."));

        diagnostics.Insert(0, Info("CANMIGDEVREP001",
            $"XtraReport '{report.Name}' detected — {report.Bands.Count} band(s), {controlCount} control(s) mapped."));

        var design = new DesignExportDto
        {
            Id = $"devexpress-report-{Guid.NewGuid():N}",
            Name = report.Name,
            Category = "imported",
            Description = "Imported from a DevExpress XtraReport.",
            PageSettings = new PageSettingsDto { Width = report.PageWidthPt, Height = report.PageHeightPt, Unit = "pt" },
            Pages = [new PageDto { Id = "page-1", Elements = elements }],
            SharedElements = sharedElements
        };

        return new XtraReportConvertResult { Design = design, Diagnostics = diagnostics };
    }

    private static ElementDto? MapControl(RawElement raw, double x, double y, double w, double h, List<MigrationDiagnostic> diagnostics)
    {
        var element = new ElementDto { Id = $"xr-{raw.Name}", Name = raw.Name, X = x, Y = y, Width = w, Height = h };

        switch (raw.Type)
        {
            case "XRLabel" or "XRPageInfo":
                element.Type = "text";
                element.Content = raw.Text ?? "";
                element.Style = BuildTextStyle(raw);
                return element;

            case "XRCheckBox":
                element.Type = "checkmark";
                element.CheckState = raw.CheckState ?? "empty";
                element.Content = raw.Text ?? "";
                return element;

            case "XRLine":
                element.Type = "line";
                element.Style = new Dictionary<string, object> { ["color"] = raw.ForeColor };
                return element;

            case "XRShape" or "XRPanel":
                // XRShape carries a shape kind: ellipse → circle, line → line, otherwise a rectangle.
                element.Type = raw.ShapeKind switch { "ellipse" => "circle", "line" => "line", _ => "rect" };
                element.Style = new Dictionary<string, object> { ["borderColor"] = raw.ForeColor, ["backgroundColor"] = raw.BackColor };
                return element;

            case "XRPictureBox":
                element.Type = "image";
                element.FitMode = "contain";
                if (raw.ImageDataUrl is { } dataUrl)
                {
                    element.Content = dataUrl;  // embedded image survives the import
                }
                else
                {
                    diagnostics.Add(Warn("CANMIGDEVREP013",
                        $"'{raw.Name}' picture data isn't embeddable from source — inserted an empty image placeholder."));
                }
                return element;

            case "XRBarCode":
                element.Type = "barcode";
                element.BarcodeValue = raw.Text ?? "";
                element.BarcodeType = "code128";
                return element;

            case "XRRichText":
                element.Type = "richtext";
                element.HtmlContent = $"<p>{raw.Text ?? ""}</p>";
                return element;

            case "XRSubreport":
                diagnostics.Add(Warn("CANMIGDEVREP012",
                    $"'{raw.Name}' is a sub-report — requires manual migration; skipped."));
                return null;

            default:
                diagnostics.Add(Warn("CANMIGDEVREP011", $"'{raw.Name}' is a {raw.Type} — not supported by Canvas yet; skipped."));
                return null;
        }
    }

    private static ElementDto? BuildTable(RawElement raw, double x, double y, double w, double h, List<MigrationDiagnostic> diagnostics)
    {
        if (raw.TableCells is not { Count: > 0 } grid)
        {
            diagnostics.Add(Warn("CANMIGDEVREP011", $"'{raw.Name}' XRTable has no parseable rows — skipped."));
            return null;
        }

        var columns = grid.Max(r => r.Count);
        if (columns == 0)
        {
            diagnostics.Add(Warn("CANMIGDEVREP011", $"'{raw.Name}' XRTable has no parseable cells — skipped."));
            return null;
        }

        var cellData = grid
            .Select(r => r.Count == columns ? r.ToArray() : r.Concat(Enumerable.Repeat("", columns - r.Count)).ToArray())
            .ToArray();

        return new ElementDto
        {
            Id = $"xr-{raw.Name}",
            Name = raw.Name,
            Type = "table",
            X = x,
            Y = y,
            Width = w,
            Height = h,
            CellData = cellData,
            ColumnWidths = Enumerable.Repeat(w / columns, columns).ToArray(),
            ColumnAlignments = FitColumns(raw.ColumnAlignments, columns),
            HeaderRow = true
        };
    }

    // Pad/truncate the captured header alignments to the table's column count (default "left").
    private static string[]? FitColumns(string[]? aligns, int columns)
    {
        if (aligns is not { Length: > 0 }) return null;
        if (aligns.Length == columns) return aligns;
        return Enumerable.Range(0, columns).Select(i => i < aligns.Length ? aligns[i] : "left").ToArray();
    }

    private static string[]? HeaderAlignmentsCSharp(
        string name,
        Dictionary<string, List<string>> tableRows,
        Dictionary<string, List<string>> rowCells,
        Dictionary<string, Dictionary<string, ExpressionSyntax>> props)
    {
        if (!tableRows.TryGetValue(name, out var rows) || rows.Count == 0) return null;
        if (!rowCells.TryGetValue(rows[0], out var cells)) return null;
        return cells.Select(cell =>
            props.TryGetValue(cell, out var bag) ? ParseAlignment(NameOf(bag.GetValueOrDefault("TextAlignment"))) : "left")
            .ToArray();
    }

    private static Dictionary<string, object> BuildTextStyle(RawElement raw)
    {
        var style = new Dictionary<string, object> { ["color"] = raw.ForeColor };
        if (raw.FontFamily is not null) style["fontFamily"] = raw.FontFamily;
        if (raw.FontSize is { } size) style["fontSize"] = size;
        if (raw.Bold) style["fontWeight"] = "bold";
        if (raw.Italic) style["fontStyle"] = "italic";
        style["textAlign"] = raw.TextAlign;
        return style;
    }

    private static void ApplyBinding(ElementDto element, string expression, List<MigrationDiagnostic> diagnostics)
    {
        var single = Regex.Match(expression, @"^\s*\[(\w+)\]\s*$");
        if (single.Success)
        {
            var field = single.Groups[1].Value;
            element.Binding = field;
            element.Content = $"{{{{{field}}}}}";
            diagnostics.Add(Info("CANMIGDEVREP010", $"'{element.Name}' Text bound to field [{field}] → Canvas binding '{field}'."));
        }
        else
        {
            element.Expression = expression;
            if (string.IsNullOrEmpty(element.Content)) element.Content = expression;
            diagnostics.Add(Warn("CANMIGDEVREP010", $"'{element.Name}' Text expression '{expression}' mapped to Canvas expression — review the syntax."));
        }
    }

    // ── C# value extraction (ExpressionSyntax) ─────────────────────────────────────────────────────

    private static (double X, double Y) ParsePoint(ExpressionSyntax? expr)
        => expr is ObjectCreationExpressionSyntax { ArgumentList.Arguments: { Count: >= 2 } a }
            ? (ToNumber(a[0].Expression), ToNumber(a[1].Expression)) : (0, 0);

    private static (double W, double H) ParseSize(ExpressionSyntax? expr)
        => expr is ObjectCreationExpressionSyntax { ArgumentList.Arguments: { Count: >= 2 } a }
            ? (ToNumber(a[0].Expression), ToNumber(a[1].Expression)) : (0, 0);

    private static string? ParseString(ExpressionSyntax? expr)
        => expr is LiteralExpressionSyntax { Token.Value: string s } ? s : null;

    private static string ParseColor(ExpressionSyntax? expr)
    {
        switch (expr)
        {
            case MemberAccessExpressionSyntax ma:
                return NamedColor(ma.Name.Identifier.ValueText);
            case InvocationExpressionSyntax inv when inv.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "FromArgb" }:
            {
                var args = inv.ArgumentList.Arguments;
                if (args.Count is 3 or 4)
                {
                    var o = args.Count == 4 ? 1 : 0;
                    return HexColor((int)ToNumber(args[o].Expression), (int)ToNumber(args[o + 1].Expression), (int)ToNumber(args[o + 2].Expression));
                }
                break;
            }
        }
        return "#000000";
    }

    private static void ApplyFontCSharp(RawElement raw, ExpressionSyntax? fontExpr)
    {
        if (fontExpr is not ObjectCreationExpressionSyntax font) return;
        var args = font.ArgumentList?.Arguments;
        if (args is { Count: >= 1 } && ParseString(args.Value[0].Expression) is { } family) raw.FontFamily = family;
        if (args is { Count: >= 2 }) raw.FontSize = ToNumber(args.Value[1].Expression);
        var text = font.ToString();
        raw.Bold = text.Contains("Bold", StringComparison.Ordinal);
        raw.Italic = text.Contains("Italic", StringComparison.Ordinal);
    }

    private static List<List<string>>? BuildTableCellsCSharp(
        string name,
        Dictionary<string, List<string>> tableRows,
        Dictionary<string, List<string>> rowCells,
        Dictionary<string, Dictionary<string, ExpressionSyntax>> props)
    {
        if (!tableRows.TryGetValue(name, out var rows)) return null;
        return rows.Select(row =>
            (rowCells.TryGetValue(row, out var cells) ? cells : new List<string>())
                .Select(cell => props.TryGetValue(cell, out var bag) && bag.TryGetValue("Text", out var t) ? ParseString(t) ?? "" : "")
                .ToList()).ToList();
    }

    private static (double Left, double Top, double Bottom) ResolveMarginsCSharp(ExpressionSyntax? margins)
        => margins is ObjectCreationExpressionSyntax { ArgumentList.Arguments: { Count: >= 4 } a }
            ? (ToNumber(a[0].Expression), ToNumber(a[2].Expression), ToNumber(a[3].Expression)) : (100, 100, 100);

    private static (double Width, double Height) ResolvePageSizeCSharp(
        Dictionary<string, Dictionary<string, ExpressionSyntax>> props, double unitScale)
    {
        var size = PaperKindSize(NameOf(GetProp(props, "this", "PaperKind")));
        if (size.W <= 0)
        {
            var w = ToNumber(GetProp(props, "this", "PageWidth"));
            var h = ToNumber(GetProp(props, "this", "PageHeight"));
            size = w > 0 && h > 0 ? (w * unitScale, h * unitScale) : (A4WidthPt, A4HeightPt);
        }
        if (GetProp(props, "this", "Landscape") is LiteralExpressionSyntax { Token.Value: true }) size = (size.H, size.W);
        return size;
    }

    // ── XML value extraction (strings) ─────────────────────────────────────────────────────────────

    private static (double Left, double Top, double Bottom) ResolveMarginsXml(string? margins)
    {
        // "left, right, top, bottom"
        var parts = SplitNumbers(margins);
        return parts.Length >= 4 ? (parts[0], parts[2], parts[3]) : (100, 100, 100);
    }

    private static (double Width, double Height) ResolvePageSizeXml(XElement root, double unitScale)
    {
        var size = PaperKindSize(Attr(root, "PaperKind") ?? "");
        if (size.W <= 0)
        {
            var w = ToDouble(Attr(root, "PageWidth"));
            var h = ToDouble(Attr(root, "PageHeight"));
            size = w > 0 && h > 0 ? (w * unitScale, h * unitScale) : (A4WidthPt, A4HeightPt);
        }
        if (string.Equals(Attr(root, "Landscape"), "true", StringComparison.OrdinalIgnoreCase)) size = (size.H, size.W);
        return size;
    }

    private static (double X, double Y) ParseCommaPair(string? value)
    {
        var n = SplitNumbers(value);
        return n.Length >= 2 ? (n[0], n[1]) : (0, 0);
    }

    private static string ParseColorString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "#000000";
        var nums = SplitNumbers(value);
        if (nums.Length >= 3)
        {
            // "R, G, B" or "A, R, G, B"
            var o = nums.Length >= 4 ? 1 : 0;
            return HexColor((int)nums[o], (int)nums[o + 1], (int)nums[o + 2]);
        }
        return NamedColor(value.Trim());
    }

    private static void ApplyFontString(RawElement raw, string? value)
    {
        // "Tahoma, 9.75pt, style=Bold, Italic"
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
    }

    // ── Shared (input-agnostic) ────────────────────────────────────────────────────────────────────

    private static string ParseAlignment(string? text)
    {
        text ??= "";
        if (text.Contains("Center", StringComparison.Ordinal)) return "center";
        if (text.Contains("Right", StringComparison.Ordinal)) return "right";
        if (text.Contains("Justify", StringComparison.Ordinal)) return "justify";
        return "left";
    }

    private static string ShapeKindFromName(string shapeType)
    {
        if (shapeType.Contains("Ellipse", StringComparison.Ordinal)) return "ellipse";
        if (shapeType.Contains("Line", StringComparison.Ordinal)) return "line";
        return "rect";
    }

    private static string CreationTypeName(ExpressionSyntax? expr) =>
        expr is ObjectCreationExpressionSyntax c ? SimpleName(c.Type) : "";

    private static string? CheckStateCSharp(Dictionary<string, ExpressionSyntax> bag)
    {
        if (NameOf(bag.GetValueOrDefault("CheckBoxState")) == "Checked") return "checked";
        if (bag.GetValueOrDefault("Checked") is LiteralExpressionSyntax { Token.Value: true }) return "checked";
        return "empty";
    }

    private static string ShapeTypeXml(XElement el)
    {
        var shape = el.Elements().FirstOrDefault(e => e.Name.LocalName == "Shape");
        if (shape is null) return "";
        return SimpleTypeOf(Attr(shape, "ControlType")
            ?? shape.Elements().Select(c => Attr(c, "ControlType")).FirstOrDefault(v => v is not null));
    }

    private static string? CheckStateXml(XElement el)
    {
        if (string.Equals(Attr(el, "CheckBoxState"), "Checked", StringComparison.OrdinalIgnoreCase)) return "checked";
        if (string.Equals(Attr(el, "Checked"), "true", StringComparison.OrdinalIgnoreCase)) return "checked";
        return "empty";
    }

    // Best-effort extraction of an embedded picture's base64 payload from a .repx XRPictureBox.
    private static string? ExtractImageDataUrl(XElement el)
    {
        var candidate = Attr(el, "ImageSource") ?? Attr(el, "ImageData") ?? Attr(el, "Image");
        if (candidate is null)
        {
            var child = el.Elements().FirstOrDefault(e => e.Name.LocalName is "ImageSource" or "Image");
            candidate = child is null ? null : (Attr(child, "ImageData") ?? Attr(child, "Base64") ?? child.Value);
        }
        if (string.IsNullOrWhiteSpace(candidate)) return null;
        if (candidate.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return candidate;

        // DevExpress sometimes prefixes the payload, e.g. "Image|<base64>".
        var pipe = candidate.LastIndexOf('|');
        var b64 = (pipe >= 0 ? candidate[(pipe + 1)..] : candidate).Trim();
        return IsLikelyBase64(b64) ? $"data:image/png;base64,{b64}" : null;
    }

    private static bool IsLikelyBase64(string value) =>
        value.Length >= 64 && value.Length % 4 == 0 && Regex.IsMatch(value, @"^[A-Za-z0-9+/]+={0,2}$");

    private static (double W, double H) PaperKindSize(string kind) => kind switch
    {
        "A3" => (842, 1191),
        "A4" => (595, 842),
        "A5" => (420, 595),
        "Letter" => (612, 792),
        "Legal" => (612, 1008),
        "Tabloid" or "Ledger" => (792, 1224),
        _ => (0, 0)
    };

    private static double ResolveUnitScale(string reportUnit) => reportUnit switch
    {
        "Pixels" => 72.0 / 96.0,
        "TenthsOfAMillimeter" => 0.1 * 72.0 / 25.4,
        "HundredthsOfAnInch" => 0.72,
        _ => 0.72
    };

    private static string HexColor(int r, int g, int b) => $"#{r:X2}{g:X2}{b:X2}";

    private static string NamedColor(string name) => name switch
    {
        "White" => "#FFFFFF",
        "Black" => "#000000",
        "Red" => "#FF0000",
        "Green" => "#008000",
        "Blue" => "#0000FF",
        "Gray" or "Grey" => "#808080",
        "DarkGray" or "DarkGrey" => "#A9A9A9",
        "LightGray" or "LightGrey" => "#D3D3D3",
        "Yellow" => "#FFFF00",
        "Orange" => "#FFA500",
        "Navy" => "#000080",
        "Transparent" => "#00000000",
        _ => "#000000"
    };

    private static int BandOrder(string bandType) => bandType switch
    {
        "TopMarginBand" => 0,
        "ReportHeaderBand" => 1,
        "PageHeaderBand" => 2,
        "GroupHeaderBand" => 3,
        "DetailBand" => 4,
        "DetailReportBand" => 5,
        "GroupFooterBand" => 6,
        "ReportFooterBand" => 7,
        "PageFooterBand" => 8,
        "BottomMarginBand" => 9,
        _ => 100
    };

    private static bool IsKnownControl(string type) => type is
        "XRLabel" or "XRPageInfo" or "XRCheckBox" or "XRLine" or "XRShape" or
        "XRPanel" or "XRPictureBox" or "XRBarCode" or "XRRichText" or "XRTable";

    private static bool IsUnsupportedControl(string type) =>
        type.StartsWith("XR", StringComparison.Ordinal) && !type.EndsWith("Band", StringComparison.Ordinal);

    private static double[] SplitNumbers(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([',', ' '], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                   .Select(p => double.TryParse(p.TrimEnd('F', 'f', 'D', 'd'), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : double.NaN)
                   .Where(d => !double.IsNaN(d))
                   .ToArray();

    private static double ToDouble(string? value) =>
        double.TryParse((value ?? "").TrimEnd('F', 'f'), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;

    private static string? Attr(XElement el, string name) => el.Attribute(name)?.Value;

    private static string SimpleTypeOf(string? controlType)
    {
        // "DevExpress.XtraReports.UI.XRLabel, DevExpress.XtraReports.v23.1, ..." → "XRLabel"
        if (string.IsNullOrWhiteSpace(controlType)) return "";
        var typeName = controlType.Split(',')[0].Trim();
        var dot = typeName.LastIndexOf('.');
        return dot >= 0 ? typeName[(dot + 1)..] : typeName;
    }

    // ── Roslyn helpers ─────────────────────────────────────────────────────────────────────────────

    private static bool IsXtraReportClass(ClassDeclarationSyntax cls) =>
        cls.BaseList?.Types.Any(t => SimpleName(t.Type) == "XtraReport") == true;

    private static string? ReceiverName(ExpressionSyntax expr) => expr switch
    {
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } ma => ma.Name.Identifier.ValueText,
        IdentifierNameSyntax id => id.Identifier.ValueText,
        ThisExpressionSyntax => "this",
        _ => null
    };

    private static string NameOf(ExpressionSyntax? expr) =>
        expr is MemberAccessExpressionSyntax ma ? ma.Name.Identifier.ValueText : "";

    private static void CaptureBindings(
        string owner, ArgumentListSyntax args,
        Dictionary<string, string> controlTextExpr, HashSet<string> boundOther)
    {
        var captured = false;
        foreach (var creation in args.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            var typeName = SimpleName(creation.Type);
            var a = creation.ArgumentList?.Arguments;
            if (a is not { Count: >= 2 }) continue;

            string? property = null, expression = null;
            if (typeName == "ExpressionBinding")
            {
                var hasEvent = a.Value.Count >= 3;
                property = ParseString(a.Value[hasEvent ? 1 : 0].Expression);
                expression = ParseString(a.Value[hasEvent ? 2 : 1].Expression);
            }
            else if (typeName == "Binding")
            {
                property = ParseString(a.Value[0].Expression);
                var field = a.Value.Count >= 3 ? ParseString(a.Value[2].Expression) : null;
                if (field is not null) expression = $"[{field}]";
            }

            if (property is null || expression is null) continue;
            if (string.Equals(property, "Text", StringComparison.Ordinal)) { controlTextExpr[owner] = expression; captured = true; }
        }

        if (!captured) boundOther.Add(owner);
    }

    private static IEnumerable<string> ExtractControlNames(ArgumentListSyntax args)
    {
        foreach (var arg in args.Arguments)
        {
            switch (arg.Expression)
            {
                case ImplicitArrayCreationExpressionSyntax { Initializer: { } init }:
                    foreach (var e in init.Expressions) if (ReceiverName(e) is { } n) yield return n;
                    break;
                case ArrayCreationExpressionSyntax { Initializer: { } init }:
                    foreach (var e in init.Expressions) if (ReceiverName(e) is { } n) yield return n;
                    break;
                default:
                    if (ReceiverName(arg.Expression) is { } single) yield return single;
                    break;
            }
        }
    }

    private static ExpressionSyntax? GetProp(
        Dictionary<string, Dictionary<string, ExpressionSyntax>> props, string owner, string prop)
        => props.TryGetValue(owner, out var bag) && bag.TryGetValue(prop, out var e) ? e : null;

    private static string SimpleName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText,
        QualifiedNameSyntax qn => qn.Right.Identifier.ValueText,
        _ => type.ToString()
    };

    private static double ToNumber(ExpressionSyntax? e)
    {
        switch (e)
        {
            case null: return 0;
            case LiteralExpressionSyntax { Token.Value: IConvertible c }:
                try { return System.Convert.ToDouble(c, CultureInfo.InvariantCulture); } catch { break; }
            case PrefixUnaryExpressionSyntax u when u.IsKind(SyntaxKind.UnaryMinusExpression):
                return -ToNumber(u.Operand);
        }
        var text = (e.ToString() ?? "").TrimEnd('F', 'f', 'D', 'd', 'M', 'm');
        return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;
    }

    private static MigrationDiagnostic Info(string id, string message) =>
        new() { Id = id, Message = message, Severity = MigrationDiagnosticSeverity.Info };

    private static MigrationDiagnostic Warn(string id, string message) =>
        new() { Id = id, Message = message, Severity = MigrationDiagnosticSeverity.Warning };

    // ── Neutral intermediate model ─────────────────────────────────────────────────────────────────

    private sealed class RawReport
    {
        public string Name = "DevExpress Report";
        public double UnitScale = 0.72;
        public double MarginLeft = 100, MarginTop = 100, MarginBottom = 100;
        public double PageWidthPt = A4WidthPt, PageHeightPt = A4HeightPt;
        public bool HasScripts;
        public List<RawBand> Bands = [];
        public List<RawElement> Elements = [];
    }

    private sealed class RawBand
    {
        public required string Name;
        public required string Type;
        public double Height;
    }

    private sealed class RawElement
    {
        public required string Name;
        public required string Type;
        public string? Band;
        public double X, Y, W, H;            // band-relative, report units
        public string? Text;
        public string? FontFamily;
        public double? FontSize;
        public bool Bold, Italic;
        public string ForeColor = "#000000";
        public string BackColor = "#000000";
        public string TextAlign = "left";
        public string? TextExpression;
        public bool HasUnmappedBinding;
        public List<List<string>>? TableCells;
        public string[]? ColumnAlignments;  // per-column alignment from the header row (XRTable)
        public string? ShapeKind;     // "ellipse" | "line" | "rect" (XRShape)
        public string? CheckState;    // "checked" | "empty" (XRCheckBox)
        public string? ImageDataUrl;  // data: URL for an embedded XRPictureBox image
    }
}
