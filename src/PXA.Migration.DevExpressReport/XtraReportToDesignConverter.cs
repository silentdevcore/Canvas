using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PXA.Core.Contracts;
using PXA.Migration.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PXA.Migration.DevExpressReport;

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
        => ConvertAuto(source, resources: null);

    /// <summary>Detects whether the source is a <c>.repx</c> XML layout (vs a C# class) and converts it.</summary>
    public XtraReportConvertResult ConvertAuto(string source, IReadOnlyDictionary<string, string>? resources)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source cannot be null or empty.", nameof(source));
        return LooksLikeRepx(source) ? ConvertRepx(source) : Convert(source, resources);
    }

    private static bool LooksLikeRepx(string source)
    {
        var trimmed = source.TrimStart();
        return trimmed.StartsWith("<?xml", StringComparison.Ordinal)
            || trimmed.StartsWith("<XtraReportsLayoutSerializer", StringComparison.Ordinal);
    }

    // ── C# Report Designer class → RawReport ───────────────────────────────────────────────────────

    public XtraReportConvertResult Convert(string sourceCode)
        => Convert(sourceCode, resources: null);

    public XtraReportConvertResult Convert(string sourceCode, IReadOnlyDictionary<string, string>? resources)
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
        var multiColumnModes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is not MemberAccessExpressionSyntax left) continue;
            var leftText = left.ToString();
            var multiColumnMatch = Regex.Match(leftText, @"(?:this\.)?(?<band>\w+)\.MultiColumn\.Mode$");
            if (multiColumnMatch.Success)
                multiColumnModes[multiColumnMatch.Groups["band"].Value] = NameOf(assignment.Right);

            var receiver = ReceiverName(left.Expression);
            if (receiver is null) continue;
            if (!props.TryGetValue(receiver, out var bag))
                props[receiver] = bag = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal);
            bag[left.Name.Identifier.ValueText] = assignment.Right;
        }

        var controlBand = new Dictionary<string, string>(StringComparer.Ordinal);
        var controlBindings = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var boundOther = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var tableRows = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var rowCells = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var bandOrder = new Dictionary<string, int>(StringComparer.Ordinal);
        var bandParent = new Dictionary<string, string>(StringComparer.Ordinal);
        var groupFields = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var sortFields = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var controlOrder = new Dictionary<string, int>(StringComparer.Ordinal);
        var nextBandOrder = 0;
        var nextControlOrder = 0;
        foreach (var inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (inv.Expression is not MemberAccessExpressionSyntax call) continue;
            if (call.Name.Identifier.ValueText is not ("Add" or "AddRange")) continue;
            if (call.Expression is not MemberAccessExpressionSyntax collection) continue;
            var owner = ReceiverName(collection.Expression);
            if (owner is null) continue;

            switch (collection.Name.Identifier.ValueText)
            {
                case "Bands":
                    foreach (var band in ExtractControlNames(inv.ArgumentList))
                    {
                        bandOrder.TryAdd(band, nextBandOrder++);
                        if (owner != "this") bandParent[band] = owner;
                    }
                    break;
                case "Controls":
                    foreach (var ctrl in ExtractControlNames(inv.ArgumentList))
                    {
                        controlBand[ctrl] = owner;
                        controlOrder.TryAdd(ctrl, nextControlOrder++);
                    }
                    break;
                case "Rows":
                    (tableRows.TryGetValue(owner, out var rl) ? rl : tableRows[owner] = []).AddRange(ExtractControlNames(inv.ArgumentList));
                    break;
                case "Cells":
                    (rowCells.TryGetValue(owner, out var cl) ? cl : rowCells[owner] = []).AddRange(ExtractControlNames(inv.ArgumentList));
                    break;
                case "ExpressionBindings" or "DataBindings":
                    CaptureBindings(owner, inv.ArgumentList, controlBindings, boundOther, resources);
                    break;
                case "GroupFields":
                    (groupFields.TryGetValue(owner, out var gl) ? gl : groupFields[owner] = []).AddRange(ExtractFieldNames(inv.ArgumentList));
                    break;
                case "SortFields":
                    (sortFields.TryGetValue(owner, out var sl) ? sl : sortFields[owner] = []).AddRange(ExtractFieldNames(inv.ArgumentList));
                    break;
            }
        }

        var unitScale = ResolveUnitScale(NameOf(GetProp(props, "this", "ReportUnit")));
        var (marginLeft, marginTop, marginBottom) = ResolveMarginsCSharp(GetProp(props, "this", "Margins"));
        var (pageW, pageH) = ResolvePageSizeCSharp(props, unitScale);
        var styles = BuildControlStylesCSharp(fieldTypes, props);

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
                report.Bands.Add(new RawBand
                {
                    Name = name,
                    Type = type,
                    Height = ToNumber(GetProp(props, name, "HeightF")),
                    Order = bandOrder.GetValueOrDefault(name, int.MaxValue),
                    Parent = bandParent.GetValueOrDefault(name),
                    GroupFields = groupFields.GetValueOrDefault(name) ?? [],
                    SortFields = sortFields.GetValueOrDefault(name) ?? [],
                    MultiColumnMode = multiColumnModes.GetValueOrDefault(name)
                });
                continue;
            }
            if (type is "XRTableRow" or "XRTableCell") continue;
            if (!IsKnownControl(type) && !IsUnsupportedControl(type)) continue;

            var bag = props.TryGetValue(name, out var b) ? b : [];
            var (locX, locY) = ParsePoint(bag.GetValueOrDefault("LocationF") ?? bag.GetValueOrDefault("LocationFloat"));
            var (sizeW, sizeH) = ParseSize(bag.GetValueOrDefault("SizeF"));
            var styleName = ParseString(bag.GetValueOrDefault("StyleName"));
            styles.TryGetValue(styleName ?? "", out var controlStyle);
            var imageExpr = bag.GetValueOrDefault("ImageSource") ?? bag.GetValueOrDefault("Image");

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
                FontFamily = controlStyle?.FontFamily,
                FontSize = controlStyle?.FontSize,
                Bold = controlStyle?.Bold ?? false,
                Italic = controlStyle?.Italic ?? false,
                Underline = controlStyle?.Underline ?? false,
                Strikeout = controlStyle?.Strikeout ?? false,
                ForeColor = bag.ContainsKey("ForeColor") ? ParseColor(bag["ForeColor"]) : controlStyle?.ForeColor ?? "#000000",
                BackColor = bag.ContainsKey("BackColor") ? ParseColor(bag["BackColor"]) : controlStyle?.BackColor,
                BorderColor = bag.ContainsKey("BorderColor") ? ParseColor(bag["BorderColor"]) : controlStyle?.BorderColor,
                Borders = ParseBorders(bag.GetValueOrDefault("Borders")) ?? controlStyle?.Borders,
                BorderWidth = bag.ContainsKey("BorderWidth") ? ToNumber(bag["BorderWidth"]) : controlStyle?.BorderWidth,
                TextAlign = bag.ContainsKey("TextAlignment") ? ParseAlignment(NameOf(bag["TextAlignment"])) : controlStyle?.TextAlign ?? "left",
                BindingExpressions = controlBindings.GetValueOrDefault(name),
                UnmappedBindingProperties = boundOther.GetValueOrDefault(name),
                LineWidth = bag.ContainsKey("LineWidth") ? ToNumber(bag["LineWidth"]) : null,
                LineStyle = NameOf(bag.GetValueOrDefault("LineStyle")) is { Length: > 0 } ls ? ls : null,
                LineDirection = NameOf(bag.GetValueOrDefault("LineDirection")) is { Length: > 0 } ld ? ld : null,
                ImageDataUrl = ExtractImageDataUrlCSharp(imageExpr, resources),
                ImageResourceKey = ExtractResourceGetStringKey(imageExpr),
                Padding = ParsePadding(bag.GetValueOrDefault("Padding")) ?? controlStyle?.Padding,
                CanGrow = BoolValue(bag.GetValueOrDefault("CanGrow")),
                CanShrink = BoolValue(bag.GetValueOrDefault("CanShrink")),
                Multiline = BoolValue(bag.GetValueOrDefault("Multiline")),
                WordWrap = BoolValue(bag.GetValueOrDefault("WordWrap")),
                KeepTogether = BoolValue(bag.GetValueOrDefault("KeepTogether")),
                AnchorHorizontal = NameOf(bag.GetValueOrDefault("AnchorHorizontal")),
                AnchorVertical = NameOf(bag.GetValueOrDefault("AnchorVertical")),
                TextFitMode = NameOf(bag.GetValueOrDefault("TextFitMode")),
                TextTrimming = NameOf(bag.GetValueOrDefault("TextTrimming")),
                Order = controlOrder.GetValueOrDefault(name, int.MaxValue)
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
        AddRepxBands(bandsContainer?.Elements() ?? Enumerable.Empty<XElement>(), parent: null, report);

        return BuildDesign(report);
    }

    private static void AddRepxBands(IEnumerable<XElement> bandEls, string? parent, RawReport report)
    {
        var order = report.Bands.Count;
        foreach (var bandEl in bandEls)
        {
            var bandType = SimpleTypeOf(Attr(bandEl, "ControlType"));
            var bandName = Attr(bandEl, "Name") ?? $"{bandType}{order + 1}";
            report.Bands.Add(new RawBand
            {
                Name = bandName,
                Type = bandType,
                Height = ToDouble(Attr(bandEl, "HeightF")),
                Order = order++,
                Parent = parent,
                GroupFields = ExtractFieldNamesXml(bandEl, "GroupFields"),
                SortFields = ExtractFieldNamesXml(bandEl, "SortFields"),
                MultiColumnMode = Attr(bandEl, "MultiColumn.Mode") ?? Attr(bandEl, "MultiColumnMode")
            });

            var controls = bandEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Controls");
            AddRepxControls(controls?.Elements() ?? Enumerable.Empty<XElement>(), bandName, report);

            var nestedBands = bandEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Bands");
            AddRepxBands(nestedBands?.Elements() ?? Enumerable.Empty<XElement>(), bandName, report);
        }
    }

    // Add a level of controls, recursing into any nested <Controls> (e.g. an XRPanel's children),
    // which reference their parent control as the container (resolved to a band in BuildDesign).
    private static void AddRepxControls(IEnumerable<XElement> controlEls, string container, RawReport report)
    {
        foreach (var ctrlEl in controlEls)
        {
            var raw = ParseRepxControl(ctrlEl, container);
            if (raw is not null) report.Elements.Add(raw);

            var nested = ctrlEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Controls");
            if (nested is not null)
                AddRepxControls(nested.Elements(), raw?.Name ?? container, report);
        }
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
            BackColor = Attr(el, "BackColor") is { } bc ? ParseColorString(bc) : null,
            BorderColor = Attr(el, "BorderColor") is { } borderColor ? ParseColorString(borderColor) : null,
            Borders = ParseBorders(Attr(el, "Borders")),
            TextAlign = ParseAlignment(Attr(el, "TextAlignment")),
            LineWidth = Attr(el, "LineWidth") is { } lw ? ToDouble(lw) : null,
            BorderWidth = Attr(el, "BorderWidth") is { } borderWidth ? ToDouble(borderWidth) : null,
            LineStyle = Attr(el, "LineStyle"),
            LineDirection = Attr(el, "LineDirection"),
            CanGrow = ParseBool(Attr(el, "CanGrow")),
            CanShrink = ParseBool(Attr(el, "CanShrink")),
            Multiline = ParseBool(Attr(el, "Multiline")),
            WordWrap = ParseBool(Attr(el, "WordWrap")),
            KeepTogether = ParseBool(Attr(el, "KeepTogether")),
            AnchorHorizontal = Attr(el, "AnchorHorizontal"),
            AnchorVertical = Attr(el, "AnchorVertical"),
            TextFitMode = Attr(el, "TextFitMode"),
            TextTrimming = Attr(el, "TextTrimming")
        };
        ApplyFontString(raw, Attr(el, "Font"));

        // <ExpressionBindings><ItemN PropertyName="Text" Expression="[X]" /></ExpressionBindings>
        foreach (var bindEl in BindingElements(el))
        {
            var property = Attr(bindEl, "PropertyName") ?? Attr(bindEl, "Property") ?? Attr(bindEl, "Name");
            var expression = Attr(bindEl, "Expression") ?? Attr(bindEl, "DataMember") ?? Attr(bindEl, "DataField");
            if (string.IsNullOrWhiteSpace(property) || string.IsNullOrWhiteSpace(expression)) continue;
            if (CanMapBindingProperty(type, property))
            {
                raw.BindingExpressions ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                raw.BindingExpressions[property] = expression;
            }
            else
            {
                raw.UnmappedBindingProperties ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                raw.UnmappedBindingProperties.Add(property);
            }
        }

        if (type == "XRShape") raw.ShapeKind = ShapeKindFromName(ShapeTypeXml(el));
        if (type == "XRCheckBox") raw.CheckState = CheckStateXml(el);
        if (type == "XRPictureBox") raw.ImageDataUrl = ExtractImageDataUrl(el);

        // XRTable: <Rows><ItemN ControlType="XRTableRow"><Cells><ItemN ControlType="XRTableCell" Text="..."/></Cells></ItemN></Rows>
        if (type == "XRTable")
        {
            var rowsEl = el.Elements().FirstOrDefault(e => e.Name.LocalName == "Rows");
            var grid = new List<List<string>>();
            var cellStyles = new List<CellStyleDto>();
            var rowIndex = 0;
            foreach (var rowEl in rowsEl?.Elements() ?? Enumerable.Empty<XElement>())
            {
                var cellsEl = rowEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Cells");
                var cellEls = (cellsEl?.Elements() ?? Enumerable.Empty<XElement>()).ToList();
                grid.Add(cellEls.Select(c => Attr(c, "Text") ?? "").ToList());
                for (var colIndex = 0; colIndex < cellEls.Count; colIndex++)
                    if (ExtractCellStyle(cellEls[colIndex], rowIndex, colIndex) is { } cs) cellStyles.Add(cs);
                rowIndex++;
            }
            raw.TableCells = grid.Count > 0 ? grid : null;
            raw.CellStyles = cellStyles.Count > 0 ? cellStyles : null;

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
        var bandTop = BuildBandTops(report);
        var offset = bandTop.Count > 0
            ? report.Bands.Max(b => bandTop.GetValueOrDefault(b.Name) + b.Height)
            : report.MarginTop;

        // Controls nested inside an XRPanel reference the panel (not a band) as their container;
        // walk up to the owning band, accumulating the panel offsets, so positions stay absolute.
        var elementByName = new Dictionary<string, RawElement>(StringComparer.Ordinal);
        foreach (var e in report.Elements) elementByName[e.Name] = e;

        var pageHeightUnits = report.PageHeightPt / report.UnitScale;
        var elements = new List<ElementDto>();
        var sharedElements = new List<ElementDto>();
        var controlCount = 0;

        foreach (var raw in report.Elements.OrderBy(e => e.Order).ThenBy(e => e.Name, StringComparer.Ordinal))
        {
            var (bandName, offsetX, offsetY) = ResolveContainer(raw, bandByName, elementByName);
            var bandType = bandName is not null && bandByName.TryGetValue(bandName, out var band) ? band.Type : "";
            var effX = raw.X + offsetX;
            var effY = raw.Y + offsetY;

            double yUnits;
            if (bandType == "PageHeaderBand")
                yUnits = report.MarginTop + effY;
            else if (bandType == "PageFooterBand")
                yUnits = pageHeightUnits - report.MarginBottom - (bandByName.TryGetValue(bandName!, out var fb) ? fb.Height : 0) + effY;
            else
                yUnits = (bandName is not null && bandTop.TryGetValue(bandName, out var t) ? t : offset) + effY;

            var x = (report.MarginLeft + effX) * report.UnitScale;
            var y = yUnits * report.UnitScale;
            var w = raw.W * report.UnitScale;
            var h = raw.H * report.UnitScale;

            var element = raw.Type == "XRTable"
                ? BuildTable(raw, x, y, w, h, diagnostics)
                : MapControl(raw, x, y, w, h, diagnostics);
            if (element is null) continue;

            if (bandType == "ReportFooterBand")
                element.PageScope = "last";

            diagnostics.Add(Info("CANMIGDEVREP002", $"'{raw.Name}' ({raw.Type}) → Canvas {element.Type}."));

            var ownerBand = bandName is not null && bandByName.TryGetValue(bandName, out var ob) ? ob : null;
            // Aggregates in a group footer/header scope to the current group's row subset ($group),
            // which DesignLayoutPlanner injects per group at expansion time.
            var aggDataset = ownerBand?.Type is "GroupHeaderBand" or "GroupFooterBand"
                ? ExpressionTranslator.GroupScopeToken : null;
            ApplyBindings(element, raw, diagnostics, aggDataset);
            if (ownerBand is not null)
                ApplyGroupRepeatMetadata(element, ownerBand);
            AddLayoutDiagnostics(raw, diagnostics);

            (bandType is "PageHeaderBand" or "PageFooterBand" ? sharedElements : elements).Add(element);
            controlCount++;
        }

        if (report.HasScripts)
            diagnostics.Add(Warn("CANMIGDEVREP012",
                "Report contains scripts/event handlers — Canvas has no scripting; migrate that logic manually."));

        if (report.Bands.Any(b => b.Type == "DetailReportBand"))
            diagnostics.Add(Warn("CANMIGDEVREP014",
                "Report contains DetailReportBand/sub-detail bands — layout is flattened, but data-repeat semantics must be wired in Canvas templates."));

        foreach (var groupBand in report.Bands.Where(b => b.Type is "GroupHeaderBand" or "GroupFooterBand"))
        {
            var fields = FormatFields(groupBand.GroupFields, "group");
            var sorts = FormatFields(groupBand.SortFields, "sort");
            diagnostics.Add(Warn("CANMIGDEVREP015",
                $"'{groupBand.Name}' ({groupBand.Type}) layout was imported; {fields}; {sorts}; group repeat/sort semantics must be wired in Canvas templates."));
        }

        foreach (var multiColumnBand in report.Bands.Where(b => !string.IsNullOrWhiteSpace(b.MultiColumnMode)))
        {
            diagnostics.Add(Warn("CANMIGDEVREP022",
                $"'{multiColumnBand.Name}' uses MultiColumn mode '{multiColumnBand.MultiColumnMode}' — Canvas flattens the band layout; review repeated column flow manually."));
        }

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

    private static string FormatFields(IReadOnlyList<string> fields, string label) =>
        fields.Count > 0 ? $"{label} field(s): {string.Join(", ", fields)}" : $"no {label} fields detected";

    private static Dictionary<string, double> BuildBandTops(RawReport report)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        var rootBands = OrderedBands(report.Bands.Where(b => b.Parent is null)).ToList();
        var childrenByParent = report.Bands
            .Where(b => b.Parent is not null)
            .GroupBy(b => b.Parent!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => OrderedBands(g).ToList(), StringComparer.Ordinal);

        var hasTopMargin = report.Bands.Any(b => b.Parent is null && b.Type == "TopMarginBand");
        var rootStart = hasTopMargin ? 0 : report.MarginTop;
        StackBands(rootBands, rootStart);
        return result;

        double StackBands(IReadOnlyList<RawBand> bands, double start)
        {
            var cursor = start;
            foreach (var band in bands)
            {
                result[band.Name] = cursor;
                var childStart = cursor + band.Height;
                var childEnd = StackBands(childrenByParent.GetValueOrDefault(band.Name) ?? [], childStart);
                cursor = Math.Max(childEnd, childStart);
            }
            return cursor;
        }

        static IOrderedEnumerable<RawBand> OrderedBands(IEnumerable<RawBand> bands) => bands
            .OrderBy(b => b.Order == int.MaxValue ? 1 : 0)
            .ThenBy(b => b.Order)
            .ThenBy(b => BandOrder(b.Type));
    }

    // Walk a control's container chain up to the owning band, accumulating panel offsets.
    private static (string? Band, double OffsetX, double OffsetY) ResolveContainer(
        RawElement raw, Dictionary<string, RawBand> bandByName, Dictionary<string, RawElement> elementByName)
    {
        var name = raw.Band;
        double ox = 0, oy = 0;
        var guard = 0;
        while (name is not null && !bandByName.ContainsKey(name) && guard++ < 32)
        {
            if (elementByName.TryGetValue(name, out var parent) && !ReferenceEquals(parent, raw))
            {
                ox += parent.X;
                oy += parent.Y;
                name = parent.Band;
            }
            else
            {
                name = null;
            }
        }
        return (name, ox, oy);
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
                element.Style = new Dictionary<string, object>
                {
                    ["color"] = raw.ForeColor,
                    ["backgroundColor"] = raw.ForeColor
                };
                if (raw.LineWidth is { } lineW) element.Style["strokeWidth"] = lineW;
                if (DashStyleFromName(raw.LineStyle) is { } dash) element.Style["dashStyle"] = dash;
                NormalizeLineGeometry(element, raw);
                return element;

            case "XRShape" or "XRPanel":
                // XRShape carries a shape kind: ellipse → circle, line → line, otherwise a rectangle.
                element.Type = raw.ShapeKind switch { "ellipse" => "circle", "line" => "line", "arrow" => "arrow", _ => "rect" };
                element.Style = new Dictionary<string, object> { ["borderColor"] = raw.BorderColor ?? raw.ForeColor };
                if (raw.BackColor is { } shapeBg) element.Style["backgroundColor"] = shapeBg;
                if ((raw.LineWidth ?? raw.BorderWidth) is { } borderW) element.Style["borderWidth"] = borderW;
                ApplyBorderStyle(element.Style, raw);
                if (element.Type == "arrow")
                {
                    element.Style["color"] = raw.BorderColor ?? raw.ForeColor;
                    element.Style["strokeWidth"] = raw.LineWidth ?? raw.BorderWidth ?? 1.0;
                    element.ArrowDirection = "right";
                    element.EndMarker = "arrow";
                    diagnostics.Add(Warn("CANMIGDEVREP019",
                        $"'{raw.Name}' XRShape arrow was imported as a Canvas arrow; review direction/head style."));
                }
                return element;

            case "XRPictureBox":
                element.Type = "image";
                element.FitMode = "contain";
                if (raw.ImageResourceKey is { } resourceKey)
                {
                    element.Style = new Dictionary<string, object>
                    {
                        ["devExpressImageResourceKey"] = resourceKey
                    };
                }
                if (raw.ImageDataUrl is { } dataUrl)
                {
                    element.Content = dataUrl;  // embedded image survives the import
                }
                else if (raw.ImageResourceKey is { } key)
                {
                    diagnostics.Add(Warn("CANMIGDEVREP021",
                        $"'{raw.Name}' image is stored in designer resources as '{key}' — include the .resx/resource payload to embed it automatically."));
                }
                else if (!raw.HasAnyBinding("ImageSource", "Image", "ImageUrl", "Value"))
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

            case "XRChart":
                element.Type = "chart";
                element.ChartType = "bar";
                element.ChartData = CreatePlaceholderChartData(raw.Name);
                element.Style = PlaceholderStyle("#1d4ed8");
                diagnostics.Add(Warn("CANMIGDEVREP018",
                    $"'{raw.Name}' is an XRChart — inserted an editable Canvas chart placeholder; wire the original series/data manually."));
                return element;

            case "XRGauge" or "XRPivotGrid":
                element.Type = "text";
                element.Content = $"{raw.Type[2..]}: {raw.Name}";
                element.Style = PlaceholderStyle(raw.Type == "XRGauge" ? "#7c3aed" : "#0f766e");
                diagnostics.Add(Warn("CANMIGDEVREP018",
                    $"'{raw.Name}' is a {raw.Type} — inserted a positioned placeholder; native mapping is still manual."));
                return element;

            case "XRSubreport":
                element.Type = "subsection";
                element.Content = $"Subreport: {raw.Name}";
                element.Style = PlaceholderStyle("#475569");
                diagnostics.Add(Warn("CANMIGDEVREP012",
                    $"'{raw.Name}' is a sub-report — inserted a positioned placeholder; migrate the nested report content manually."));
                return element;

            default:
                diagnostics.Add(Warn("CANMIGDEVREP011", $"'{raw.Name}' is a {raw.Type} — not supported by Canvas yet; skipped."));
                return null;
        }
    }

    private static Dictionary<string, object> CreatePlaceholderChartData(string name) => new()
    {
        ["labels"] = new[] { "Series A", "Series B", "Series C" },
        ["datasets"] = new object[]
        {
            new Dictionary<string, object>
            {
                ["label"] = name,
                ["data"] = new[] { 35, 68, 52 },
                ["backgroundColor"] = "#2563eb"
            }
        }
    };

    private static Dictionary<string, object> PlaceholderStyle(string color) => new()
    {
        ["color"] = color,
        ["backgroundColor"] = "#f8fafc",
        ["borderColor"] = color,
        ["borderWidth"] = 1.0,
        ["borderStyle"] = "dashed",
        ["textAlign"] = "center",
        ["whiteSpace"] = "pre-wrap"
    };

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
            HeaderRow = true,
            CellStyles = raw.CellStyles is { Count: > 0 } cs ? cs.ToArray() : null
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
        var decoration = string.Join(" ", new[] { raw.Underline ? "underline" : null, raw.Strikeout ? "line-through" : null }.Where(s => s is not null));
        if (decoration.Length > 0) style["textDecoration"] = decoration;
        if (raw.BackColor is { } bg) style["backgroundColor"] = bg;
        if (raw.Padding is { } padding)
        {
            style["paddingLeft"] = padding.Left;
            style["paddingRight"] = padding.Right;
            style["paddingTop"] = padding.Top;
            style["paddingBottom"] = padding.Bottom;
        }
        style["textAlign"] = raw.TextAlign;
        ApplyBorderStyle(style, raw);
        if (raw.Multiline == true || raw.WordWrap == true)
            style["whiteSpace"] = "pre-wrap";
        else if (raw.WordWrap == false)
            style["whiteSpace"] = "nowrap";
        if (raw.CanGrow == true) style["overflow"] = "visible";
        if (raw.CanShrink == true) style["devExpressCanShrink"] = true;
        if (!string.IsNullOrWhiteSpace(raw.TextFitMode)) style["devExpressTextFitMode"] = raw.TextFitMode;
        if (!string.IsNullOrWhiteSpace(raw.TextTrimming)) style["devExpressTextTrimming"] = raw.TextTrimming;
        if (raw.KeepTogether == true) style["devExpressKeepTogether"] = true;
        if (NormalizeAnchor(raw.AnchorHorizontal) is { } ah) style["devExpressAnchorHorizontal"] = ah;
        if (NormalizeAnchor(raw.AnchorVertical) is { } av)
        {
            style["devExpressAnchorVertical"] = av;
            if (av.Contains("Bottom", StringComparison.OrdinalIgnoreCase)) style["verticalAlign"] = "bottom";
        }
        return style;
    }

    private static void ApplyBorderStyle(Dictionary<string, object> style, RawElement raw)
    {
        if (raw.BorderColor is { } color)
            style["borderColor"] = color;

        if (raw.Borders is not { Count: > 0 } borders)
        {
            if (raw.BorderWidth is { } width)
                style["borderWidth"] = width;
            return;
        }

        style["devExpressBorders"] = string.Join(", ", borders.OrderBy(side => side, StringComparer.OrdinalIgnoreCase));

        if (borders.Contains("None"))
        {
            style["borderWidth"] = 0d;
            return;
        }

        var borderWidth = raw.BorderWidth ?? raw.LineWidth ?? 1d;
        var borderColor = raw.BorderColor ?? raw.ForeColor;

        if (borders.Contains("All"))
        {
            style["borderWidth"] = borderWidth;
            style["borderColor"] = borderColor;
            return;
        }

        foreach (var side in borders)
        {
            style[$"border{side}Width"] = borderWidth;
            style[$"border{side}Color"] = borderColor;
        }
    }

    private static void AddLayoutDiagnostics(RawElement raw, List<MigrationDiagnostic> diagnostics)
    {
        if (raw.CanGrow == true || raw.CanShrink == true)
        {
            diagnostics.Add(Warn("CANMIGDEVREP016",
                $"'{raw.Name}' uses CanGrow/CanShrink — Canvas imports wrapping/overflow hints, but dynamic band reflow must be reviewed."));
        }

        if (!string.IsNullOrWhiteSpace(raw.AnchorHorizontal) || !string.IsNullOrWhiteSpace(raw.AnchorVertical))
        {
            diagnostics.Add(Warn("CANMIGDEVREP017",
                $"'{raw.Name}' uses DevExpress anchoring ({raw.AnchorHorizontal}/{raw.AnchorVertical}) — imported as metadata; review responsive positioning in Canvas."));
        }

        if (!string.IsNullOrWhiteSpace(raw.TextFitMode) || !string.IsNullOrWhiteSpace(raw.TextTrimming))
        {
            diagnostics.Add(Warn("CANMIGDEVREP023",
                $"'{raw.Name}' uses TextFitMode/TextTrimming — imported as metadata; review text overflow/shrink behaviour manually."));
        }
    }

    private static string? NormalizeAnchor(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Equals("None", StringComparison.OrdinalIgnoreCase) ? null : value;

    private static void NormalizeLineGeometry(ElementDto element, RawElement raw)
    {
        var strokeWidth = Math.Max(raw.LineWidth ?? 1, 0.5);
        var hasExplicitDirection = !string.IsNullOrWhiteSpace(raw.LineDirection);
        var direction = NormalizeLineDirection(raw.LineDirection, element.Width, element.Height);
        element.Style ??= [];
        element.Style["lineDirection"] = direction;

        switch (direction)
        {
            case "vertical":
            {
                var centerX = element.X + element.Width / 2;
                element.X = centerX - strokeWidth / 2;
                element.Width = strokeWidth;
                if (element.Height <= 0) element.Height = strokeWidth;
                break;
            }
            case "horizontal" when hasExplicitDirection:
            {
                var centerY = element.Y + element.Height / 2;
                element.Y = centerY - strokeWidth / 2;
                element.Height = strokeWidth;
                if (element.Width <= 0) element.Width = strokeWidth;
                break;
            }
            case "horizontal":
                if (element.Height <= 0) element.Height = strokeWidth;
                if (element.Width <= 0) element.Width = strokeWidth;
                break;
            case "backSlant":
            case "slant":
                if (element.Width <= 0) element.Width = strokeWidth;
                if (element.Height <= 0) element.Height = strokeWidth;
                break;
        }
    }

    private static string NormalizeLineDirection(string? direction, double width, double height)
    {
        if (!string.IsNullOrWhiteSpace(direction))
        {
            if (direction.Contains("Vertical", StringComparison.OrdinalIgnoreCase)) return "vertical";
            if (direction.Contains("BackSlant", StringComparison.OrdinalIgnoreCase)) return "backSlant";
            if (direction.Contains("Slant", StringComparison.OrdinalIgnoreCase)) return "slant";
            if (direction.Contains("Horizontal", StringComparison.OrdinalIgnoreCase)) return "horizontal";
        }

        return height > width ? "vertical" : "horizontal";
    }

    private static void ApplyBindings(ElementDto element, RawElement raw, List<MigrationDiagnostic> diagnostics,
        string? dataSetPath = null)
    {
        if (raw.BindingExpressions is not null)
        {
            if (raw.BindingExpressions.TryGetValue("Visible", out var visibleExpression))
            {
                element.VisibleExpression = visibleExpression;
                diagnostics.Add(Warn("CANMIGDEVREP020",
                    $"'{raw.Name}' Visible expression '{visibleExpression}' was preserved on Canvas visibleExpression; wire runtime evaluation if needed."));
            }

            foreach (var property in BindingPriority(element.Type))
            {
                if (raw.BindingExpressions.TryGetValue(property, out var expression))
                    ApplyBinding(element, property, expression, diagnostics, dataSetPath);
            }
        }

        if (raw.UnmappedBindingProperties is { Count: > 0 })
        {
            var props = string.Join(", ", raw.UnmappedBindingProperties.Order(StringComparer.OrdinalIgnoreCase));
            diagnostics.Add(Warn("CANMIGDEVREP010",
                $"'{raw.Name}' has unmapped data binding(s) for {props} — re-bind them in Canvas."));
        }
    }

    private static IEnumerable<string> BindingPriority(string elementType) => elementType switch
    {
        "barcode" => ["Text", "Value", "BarcodeValue"],
        "image" => ["ImageSource", "Image", "ImageUrl", "Value"],
        "checkmark" => ["CheckState", "Checked", "Value", "Text"],
        _ => ["Text", "Value"]
    };

    private static void ApplyBinding(ElementDto element, string property, string expression, List<MigrationDiagnostic> diagnostics,
        string? dataSetPath = null)
    {
        var single = Regex.Match(expression, @"^\s*\[(\w+)\]\s*$");
        if (single.Success)
        {
            var field = single.Groups[1].Value;
            element.Binding = field;
            var placeholder = $"{{{{{field}}}}}";
            switch (element.Type)
            {
                case "text" or "richtext":
                    element.Content = placeholder;
                    break;
                case "barcode":
                    element.BarcodeValue = placeholder;
                    break;
                case "image":
                    element.Content = placeholder;
                    break;
                case "checkmark":
                    element.FieldName ??= field;
                    break;
            }
            diagnostics.Add(Info("CANMIGDEVREP010", $"'{element.Name}' {property} bound to field [{field}] → Canvas binding '{field}'."));
        }
        else
        {
            // Normalize [Field] refs to {{Field}} for readable content, and translate the expression to
            // executable PXA grammar for the preview engine; raw preserved for review.
            var normalized = Regex.Replace(expression, @"\[([^\]]+)\]", m =>
            {
                var n = m.Groups[1].Value.Trim();
                var dot = n.LastIndexOf('.');
                return $"{{{{{(dot >= 0 ? n[(dot + 1)..] : n)}}}}}";
            });
            element.Expression = ExpressionTranslator.TranslateDevExpress(expression, dataSetPath) ?? expression;
            element.Style ??= [];
            element.Style["devExpressExpression"] = expression;
            if (string.IsNullOrEmpty(element.Content)) element.Content = normalized;
            diagnostics.Add(Warn("CANMIGDEVREP010", $"'{element.Name}' {property} expression '{expression}' mapped to a Canvas template with normalized field references — review the syntax."));
        }
    }

    // ── C# value extraction (ExpressionSyntax) ─────────────────────────────────────────────────────

    private static (double X, double Y) ParsePoint(ExpressionSyntax? expr)
        => expr is ObjectCreationExpressionSyntax { ArgumentList.Arguments: { Count: >= 2 } a }
            ? (ToNumber(a[0].Expression), ToNumber(a[1].Expression)) : (0, 0);

    private static (double W, double H) ParseSize(ExpressionSyntax? expr)
        => expr is ObjectCreationExpressionSyntax { ArgumentList.Arguments: { Count: >= 2 } a }
            ? (ToNumber(a[0].Expression), ToNumber(a[1].Expression)) : (0, 0);

    private static RawPadding? ParsePadding(ExpressionSyntax? expr)
        => expr is ObjectCreationExpressionSyntax { ArgumentList.Arguments: { Count: >= 4 } a }
            ? new RawPadding(
                ToNumber(a[0].Expression),
                ToNumber(a[1].Expression),
                ToNumber(a[2].Expression),
                ToNumber(a[3].Expression))
            : null;

    private static string? ParseString(ExpressionSyntax? expr)
        => expr is LiteralExpressionSyntax { Token.Value: string s } ? s : null;

    private static string? ParseString(ExpressionSyntax? expr, IReadOnlyDictionary<string, string>? resources)
    {
        if (ParseString(expr) is { } literal) return literal;
        var key = ExtractResourceGetStringKey(expr);
        return key is not null && resources?.TryGetValue(key, out var value) == true ? value : null;
    }

    private static string? ExtractResourceGetStringKey(ExpressionSyntax? expr)
    {
        if (expr is InvocationExpressionSyntax invocation &&
            invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "GetString" } &&
            invocation.ArgumentList.Arguments is { Count: >= 1 } args)
            return ParseString(args[0].Expression);

        if (expr is ObjectCreationExpressionSyntax { ArgumentList.Arguments: { Count: >= 2 } imageArgs })
            return ExtractResourceGetStringKey(imageArgs[1].Expression);

        return null;
    }

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
        raw.Underline = text.Contains("Underline", StringComparison.Ordinal);
        raw.Strikeout = text.Contains("Strikeout", StringComparison.Ordinal) || text.Contains("Strikethrough", StringComparison.Ordinal);
    }

    private static void ApplyFontCSharp(RawStyle raw, ExpressionSyntax? fontExpr)
    {
        if (fontExpr is not ObjectCreationExpressionSyntax font) return;
        var args = font.ArgumentList?.Arguments;
        if (args is { Count: >= 1 } && ParseString(args.Value[0].Expression) is { } family) raw.FontFamily = family;
        if (args is { Count: >= 2 }) raw.FontSize = ToNumber(args.Value[1].Expression);
        var text = font.ToString();
        raw.Bold = text.Contains("Bold", StringComparison.Ordinal);
        raw.Italic = text.Contains("Italic", StringComparison.Ordinal);
        raw.Underline = text.Contains("Underline", StringComparison.Ordinal);
        raw.Strikeout = text.Contains("Strikeout", StringComparison.Ordinal) || text.Contains("Strikethrough", StringComparison.Ordinal);
    }

    private static Dictionary<string, RawStyle> BuildControlStylesCSharp(
        Dictionary<string, string> fieldTypes,
        Dictionary<string, Dictionary<string, ExpressionSyntax>> props)
    {
        var styles = new Dictionary<string, RawStyle>(StringComparer.Ordinal);
        foreach (var (fieldName, type) in fieldTypes)
        {
            if (type != "XRControlStyle" || !props.TryGetValue(fieldName, out var bag)) continue;

            var style = new RawStyle
            {
                ForeColor = bag.ContainsKey("ForeColor") ? ParseColor(bag["ForeColor"]) : null,
                BackColor = bag.ContainsKey("BackColor") ? ParseColor(bag["BackColor"]) : null,
                BorderColor = bag.ContainsKey("BorderColor") ? ParseColor(bag["BorderColor"]) : null,
                Borders = ParseBorders(bag.GetValueOrDefault("Borders")),
                BorderWidth = bag.ContainsKey("BorderWidth") ? ToNumber(bag["BorderWidth"]) : null,
                TextAlign = bag.ContainsKey("TextAlignment") ? ParseAlignment(NameOf(bag["TextAlignment"])) : null,
                Padding = ParsePadding(bag.GetValueOrDefault("Padding"))
            };
            ApplyFontCSharp(style, bag.GetValueOrDefault("Font"));

            styles[fieldName] = style;
            if (ParseString(bag.GetValueOrDefault("Name")) is { Length: > 0 } styleName)
                styles[styleName] = style;
        }

        return styles;
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
        raw.Underline = value.Contains("Underline", StringComparison.OrdinalIgnoreCase);
        raw.Strikeout = value.Contains("Strikeout", StringComparison.OrdinalIgnoreCase) || value.Contains("Strikethrough", StringComparison.OrdinalIgnoreCase);
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

    private static string? DashStyleFromName(string? lineStyle)
    {
        if (string.IsNullOrEmpty(lineStyle) || lineStyle.Equals("Solid", StringComparison.OrdinalIgnoreCase)) return null;
        if (lineStyle.Contains("Dash", StringComparison.OrdinalIgnoreCase)) return "dashed";
        if (lineStyle.Contains("Dot", StringComparison.OrdinalIgnoreCase)) return "dotted";
        return null;
    }

    private static string ShapeKindFromName(string shapeType)
    {
        if (shapeType.Contains("Ellipse", StringComparison.OrdinalIgnoreCase)) return "ellipse";
        if (shapeType.Contains("Arrow", StringComparison.OrdinalIgnoreCase)) return "arrow";
        if (shapeType.Contains("Line", StringComparison.OrdinalIgnoreCase)) return "line";
        return "rect";
    }

    private static HashSet<string>? ParseBorders(ExpressionSyntax? expr) => ParseBorders(expr?.ToString());

    // Group bands repeat per group key: attach Canvas RepeatDto + group metadata so the band's controls
    // can be wired as a repeating template (mirrors the RDL/Jasper group-repeat mapping).
    private static void ApplyGroupRepeatMetadata(ElementDto element, RawBand band)
    {
        if (band.Type is not ("GroupHeaderBand" or "GroupFooterBand")) return;

        var role = band.Type == "GroupFooterBand" ? "footer" : "header";
        var dataPath = GroupDataPath(band);
        var group = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = band.Name,
            ["role"] = role,
            ["band"] = band.Name,
            ["dataPath"] = dataPath,
        };
        if (band.GroupFields is { Count: > 0 }) group["fields"] = band.GroupFields.ToArray();
        if (band.SortFields is { Count: > 0 }) group["sorts"] = band.SortFields.ToArray();

        element.Style ??= [];
        element.Style["devExpressGroup"] = group;
        element.Repeat = new RepeatDto { DataPath = dataPath, TemplateId = element.Id };
    }

    private static string GroupDataPath(RawBand band)
    {
        var field = band.GroupFields.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f));
        var basis = !string.IsNullOrWhiteSpace(field) ? field! : band.Name;
        // GroupFields may carry a " (SortOrder)" annotation — use only the field name for the data path.
        var paren = basis.IndexOf(" (", StringComparison.Ordinal);
        if (paren >= 0) basis = basis[..paren];
        return SafeDataPath(basis);
    }

    private static string SafeDataPath(string value)
    {
        var cleaned = new string(value.Select(ch => char.IsLetterOrDigit(ch) || ch is '_' or '.' ? ch : '_').ToArray()).Trim('_');
        return string.IsNullOrWhiteSpace(cleaned) ? "items" : cleaned;
    }

    // Per-cell style from an XRTableCell: fore/back colour, alignment, font, and borders
    // (Borders "All" → uniform; otherwise the listed sides). Padding is XRControl-specific and skipped.
    private static CellStyleDto? ExtractCellStyle(XElement cell, int row, int col)
    {
        var cs = new CellStyleDto { Row = row, Col = col };
        var any = false;

        if (Attr(cell, "BackColor") is { } bg)        { cs.BackgroundColor = ParseColorString(bg); any = true; }
        if (Attr(cell, "ForeColor") is { } fc)        { cs.Color = ParseColorString(fc); any = true; }
        if (Attr(cell, "TextAlignment") is { Length: > 0 } ta) { cs.TextAlign = ParseAlignment(ta); any = true; }

        if (Attr(cell, "Font") is { Length: > 0 } font)
        {
            var tmp = new RawElement { Name = "", Type = "" };
            ApplyFontString(tmp, font);
            cs.FontFamily = tmp.FontFamily;
            cs.FontSize = tmp.FontSize;
            if (tmp.Bold) cs.Bold = true;
            if (tmp.Italic) cs.Italic = true;
            any = true;
        }

        if (ParseBorders(Attr(cell, "Borders")) is { Count: > 0 } borders && !(borders.Count == 1 && borders.Contains("None")))
        {
            var color = Attr(cell, "BorderColor") is { } bcv ? ParseColorString(bcv) : "#000000";
            var width = double.TryParse(Attr(cell, "BorderWidth"), NumberStyles.Any, CultureInfo.InvariantCulture, out var w) && w > 0 ? w : 1;
            CellBorderSideDto Side() => new() { Color = color, Width = width };
            if (borders.Contains("All")) { cs.BorderColor = color; cs.BorderWidth = width; }
            else
            {
                if (borders.Contains("Top"))    cs.BorderTop = Side();
                if (borders.Contains("Right"))  cs.BorderRight = Side();
                if (borders.Contains("Bottom")) cs.BorderBottom = Side();
                if (borders.Contains("Left"))   cs.BorderLeft = Side();
            }
            any = true;
        }

        return any ? cs : null;
    }

    private static HashSet<string>? ParseBorders(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var borders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var side in new[] { "None", "All", "Left", "Top", "Right", "Bottom" })
        {
            if (value.Contains(side, StringComparison.OrdinalIgnoreCase))
                borders.Add(side);
        }

        return borders.Count > 0 ? borders : null;
    }

    private static string CreationTypeName(ExpressionSyntax? expr) =>
        expr is ObjectCreationExpressionSyntax c ? SimpleName(c.Type) : "";

    private static string? CheckStateCSharp(Dictionary<string, ExpressionSyntax> bag)
    {
        if (NameOf(bag.GetValueOrDefault("CheckBoxState")) == "Checked") return "checked";
        if (bag.GetValueOrDefault("Checked") is LiteralExpressionSyntax { Token.Value: true }) return "checked";
        return "empty";
    }

    private static bool? BoolValue(ExpressionSyntax? expr) =>
        expr is LiteralExpressionSyntax { Token.Value: bool b } ? b : null;

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

    private static bool? ParseBool(string? value)
    {
        if (bool.TryParse(value, out var b)) return b;
        return null;
    }

    private static List<string> ExtractFieldNamesXml(XElement bandEl, string containerName)
    {
        var fieldsEl = bandEl.Elements().FirstOrDefault(e => e.Name.LocalName == containerName);
        var fields = new List<string>();
        foreach (var item in fieldsEl?.Elements() ?? Enumerable.Empty<XElement>())
        {
            var name = Attr(item, "FieldName") ?? Attr(item, "Name") ?? Attr(item, "Field");
            var order = Attr(item, "SortOrder") ?? Attr(item, "Order") ?? Attr(item, "SortDirection");
            if (!string.IsNullOrWhiteSpace(name))
                fields.Add(string.IsNullOrWhiteSpace(order) ? name : $"{name} ({order})");
        }
        return fields;
    }

    private static IEnumerable<XElement> BindingElements(XElement el)
    {
        foreach (var containerName in new[] { "ExpressionBindings", "DataBindings" })
        {
            var container = el.Elements().FirstOrDefault(e => e.Name.LocalName == containerName);
            foreach (var item in container?.Elements() ?? Enumerable.Empty<XElement>())
                yield return item;
        }
    }

    private static bool CanMapBindingProperty(string controlType, string property) =>
        property switch
        {
            "Visible" => true,
            "Text" => controlType is "XRLabel" or "XRPageInfo" or "XRRichText" or "XRBarCode" or "XRCheckBox",
            "Value" or "BarcodeValue" => controlType == "XRBarCode",
            "ImageSource" or "Image" or "ImageUrl" => controlType == "XRPictureBox",
            "CheckState" or "Checked" => controlType == "XRCheckBox",
            _ => false
        };

    // Best-effort extraction of an embedded picture's base64 payload from a .repx XRPictureBox.
    private static string? ExtractImageDataUrl(XElement el)
    {
        var candidate = Attr(el, "ImageSource") ?? Attr(el, "ImageData") ?? Attr(el, "Image");
        if (candidate is null)
        {
            var child = el.Elements().FirstOrDefault(e => e.Name.LocalName is "ImageSource" or "Image");
            candidate = child is null ? null : (Attr(child, "ImageData") ?? Attr(child, "Base64") ?? child.Value);
        }
        return ImageDataUrlFromCandidate(candidate);
    }

    private static bool IsLikelyBase64(string value) =>
        value.Length >= 64 && value.Length % 4 == 0 && Regex.IsMatch(value, @"^[A-Za-z0-9+/]+={0,2}$");

    private static string? ExtractImageDataUrlCSharp(ExpressionSyntax? expr, IReadOnlyDictionary<string, string>? resources)
    {
        string? candidate = expr switch
        {
            LiteralExpressionSyntax { Token.Value: string s } => s,
            ObjectCreationExpressionSyntax { ArgumentList.Arguments: { Count: >= 2 } args } => ParseString(args[1].Expression, resources),
            _ => null
        };

        if (candidate is null
            && ExtractResourceGetStringKey(expr) is { } key
            && resources?.TryGetValue(key, out var resourceValue) == true)
        {
            candidate = resourceValue;
        }

        return ImageDataUrlFromCandidate(candidate);
    }

    private static string? ImageDataUrlFromCandidate(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return null;
        if (candidate.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return candidate;

        var pipe = candidate.LastIndexOf('|');
        var b64 = (pipe >= 0 ? candidate[(pipe + 1)..] : candidate).Trim();
        return IsLikelyBase64(b64) ? $"data:image/png;base64,{b64}" : null;
    }

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
        "XRPanel" or "XRPictureBox" or "XRBarCode" or "XRRichText" or "XRTable" or
        "XRChart" or "XRGauge" or "XRPivotGrid";

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
        Dictionary<string, Dictionary<string, string>> controlBindings,
        Dictionary<string, HashSet<string>> boundOther,
        IReadOnlyDictionary<string, string>? resources)
    {
        foreach (var creation in args.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            var typeName = SimpleName(creation.Type);
            var a = creation.ArgumentList?.Arguments;
            if (a is not { Count: >= 2 }) continue;

            string? property = null, expression = null;
            if (typeName == "ExpressionBinding")
            {
                var hasEvent = a.Value.Count >= 3;
                property = ParseString(a.Value[hasEvent ? 1 : 0].Expression, resources);
                expression = ParseString(a.Value[hasEvent ? 2 : 1].Expression, resources);
            }
            else if (typeName == "Binding")
            {
                property = ParseString(a.Value[0].Expression, resources);
                var field = a.Value.Count >= 3 ? ParseString(a.Value[2].Expression, resources) : null;
                if (field is not null) expression = $"[{field}]";
            }

            if (property is null || expression is null) continue;
            if (KnownBindableProperty(property))
            {
                if (!controlBindings.TryGetValue(owner, out var bag))
                    controlBindings[owner] = bag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                bag[property] = expression;
            }
            else
            {
                if (!boundOther.TryGetValue(owner, out var props))
                    boundOther[owner] = props = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                props.Add(property);
            }
        }
    }

    private static bool KnownBindableProperty(string property) =>
        property is "Text" or "Value" or "BarcodeValue" or "ImageSource" or "Image" or "ImageUrl" or "CheckState" or "Checked" or "Visible";

    private static IEnumerable<string> ExtractFieldNames(ArgumentListSyntax args)
    {
        foreach (var creation in args.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            var typeName = SimpleName(creation.Type);
            if (typeName is not ("GroupField" or "GroupFieldInfo" or "SortField")) continue;
            var a = creation.ArgumentList?.Arguments;
            if (a is not { Count: >= 1 }) continue;
            var field = ParseString(a.Value[0].Expression);
            if (string.IsNullOrWhiteSpace(field)) continue;
            var order = a.Value.Count >= 2 ? NameOf(a.Value[1].Expression) : "";
            yield return string.IsNullOrWhiteSpace(order) ? field : $"{field} ({order})";
        }
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
        public int Order = int.MaxValue;
        public string? Parent;
        public List<string> GroupFields = [];
        public List<string> SortFields = [];
        public string? MultiColumnMode;
    }

    private sealed class RawStyle
    {
        public string? FontFamily;
        public double? FontSize;
        public bool Bold, Italic, Underline, Strikeout;
        public string? ForeColor;
        public string? BackColor;
        public string? BorderColor;
        public HashSet<string>? Borders;
        public double? BorderWidth;
        public string? TextAlign;
        public RawPadding? Padding;
    }

    private sealed record RawPadding(double Left, double Right, double Top, double Bottom);

    private sealed class RawElement
    {
        public required string Name;
        public required string Type;
        public string? Band;
        public double X, Y, W, H;            // band-relative, report units
        public string? Text;
        public string? FontFamily;
        public double? FontSize;
        public bool Bold, Italic, Underline, Strikeout;
        public string ForeColor = "#000000";
        public string? BackColor;   // null = no explicit background
        public string? BorderColor;
        public HashSet<string>? Borders;
        public double? BorderWidth;
        public RawPadding? Padding;
        public string TextAlign = "left";
        public Dictionary<string, string>? BindingExpressions;
        public HashSet<string>? UnmappedBindingProperties;
        public List<List<string>>? TableCells;
        public List<CellStyleDto>? CellStyles;
        public string[]? ColumnAlignments;  // per-column alignment from the header row (XRTable)
        public string? ShapeKind;     // "ellipse" | "line" | "arrow" | "rect" (XRShape)
        public string? CheckState;    // "checked" | "empty" (XRCheckBox)
        public string? ImageDataUrl;  // data: URL for an embedded XRPictureBox image
        public string? ImageResourceKey;
        public double? LineWidth;     // XRLine/XRShape stroke/border width
        public string? LineStyle;     // XRLine dash style (Solid/Dash/Dot/...)
        public string? LineDirection; // XRLine direction (Horizontal/Vertical/Slant/BackSlant)
        public bool? CanGrow;
        public bool? CanShrink;
        public bool? Multiline;
        public bool? WordWrap;
        public bool? KeepTogether;
        public string? AnchorHorizontal;
        public string? AnchorVertical;
        public string? TextFitMode;
        public string? TextTrimming;
        public int Order = int.MaxValue;

        public bool HasAnyBinding(params string[] properties) =>
            BindingExpressions is not null && properties.Any(p => BindingExpressions.ContainsKey(p));
    }
}
