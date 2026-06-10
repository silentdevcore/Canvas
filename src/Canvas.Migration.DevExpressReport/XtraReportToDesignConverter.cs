using System.Globalization;
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
/// Converts a C# DevExpress XtraReport class (Report Designer output) into a Canvas
/// <see cref="DesignExportDto"/> that the visual designer can open. Handles static-content reports:
/// it flattens band-relative control positions to absolute page coordinates and converts report units
/// to points. Data bindings, sub-reports and scripts are reported as diagnostics, not converted.
/// </summary>
public sealed class XtraReportToDesignConverter
{
    private const double A4WidthPt = 595;
    private const double A4HeightPt = 842;

    public XtraReportConvertResult Convert(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));

        var diagnostics = new List<MigrationDiagnostic>();
        var root = CSharpSyntaxTree.ParseText(sourceCode).GetCompilationUnitRoot();

        var reportClass = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(IsXtraReportClass);

        // varName → simple type name (controls and bands).
        var fieldTypes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            var typeName = SimpleName(field.Declaration.Type);
            foreach (var v in field.Declaration.Variables)
                fieldTypes[v.Identifier.ValueText] = typeName;
        }

        // varName → { property → value expression }.
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

        // bandName → [controlName...], and controls that carry data bindings.
        var bandControls = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var controlBand = new Dictionary<string, string>(StringComparer.Ordinal);
        var boundControls = new HashSet<string>(StringComparer.Ordinal);
        // XRTable structure: tableName → [rowName...], rowName → [cellName...].
        var tableRows = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var rowCells = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (inv.Expression is not MemberAccessExpressionSyntax call) continue;
            var method = call.Name.Identifier.ValueText;
            if (method is not ("Add" or "AddRange")) continue;
            if (call.Expression is not MemberAccessExpressionSyntax collection) continue;
            var collectionName = collection.Name.Identifier.ValueText;
            var owner = ReceiverName(collection.Expression);
            if (owner is null) continue;

            switch (collectionName)
            {
                case "Controls": // band.Controls.Add(...) / AddRange(...)
                {
                    var list = bandControls.TryGetValue(owner, out var l) ? l : (bandControls[owner] = []);
                    foreach (var ctrl in ExtractControlNames(inv.ArgumentList))
                    {
                        list.Add(ctrl);
                        controlBand[ctrl] = owner;
                    }
                    break;
                }
                case "Rows": // xrTable.Rows.AddRange(...)
                {
                    var list = tableRows.TryGetValue(owner, out var l) ? l : (tableRows[owner] = []);
                    list.AddRange(ExtractControlNames(inv.ArgumentList));
                    break;
                }
                case "Cells": // xrTableRow.Cells.AddRange(...)
                {
                    var list = rowCells.TryGetValue(owner, out var l) ? l : (rowCells[owner] = []);
                    list.AddRange(ExtractControlNames(inv.ArgumentList));
                    break;
                }
                case "ExpressionBindings" or "DataBindings":
                    boundControls.Add(owner);
                    break;
            }
        }

        var unitScale = ResolveUnitScale(GetProp(props, "this", "ReportUnit"));
        var (marginLeft, marginTop) = ResolveMargins(GetProp(props, "this", "Margins"));

        // --- Band flattening: absolute top (report units) per band -----------------------------------
        var bands = fieldTypes.Where(kv => kv.Value.EndsWith("Band", StringComparison.Ordinal))
            .Select(kv => kv.Key)
            .OrderBy(name => BandOrder(fieldTypes[name]))
            .ToList();

        var hasTopMargin = bands.Any(b => fieldTypes[b] == "TopMarginBand");
        var bandTop = new Dictionary<string, double>(StringComparer.Ordinal);
        var offset = hasTopMargin ? 0 : marginTop;
        foreach (var band in bands)
        {
            bandTop[band] = offset;
            offset += ToNumber(GetProp(props, band, "HeightF"));
        }

        // --- Build elements ---------------------------------------------------------------------------
        var elements = new List<ElementDto>();
        var controlCount = 0;
        foreach (var (name, type) in fieldTypes)
        {
            if (type.EndsWith("Band", StringComparison.Ordinal)) continue;
            // Table rows/cells are consumed by their XRTable, not emitted as standalone elements.
            if (type is "XRTableRow" or "XRTableCell") continue;
            if (!IsKnownControl(type) && !IsUnsupportedControl(type)) continue;

            var bag = props.TryGetValue(name, out var b) ? b : new Dictionary<string, ExpressionSyntax>();
            var (locX, locY) = ParsePoint(bag.GetValueOrDefault("LocationF"));
            var (sizeW, sizeH) = ParseSize(bag.GetValueOrDefault("SizeF"));

            var bandOffset = controlBand.TryGetValue(name, out var bandName) && bandTop.TryGetValue(bandName, out var t)
                ? t
                : offset;

            var x = (marginLeft + locX) * unitScale;
            var y = (bandOffset + locY) * unitScale;
            var w = sizeW * unitScale;
            var h = sizeH * unitScale;

            var element = type == "XRTable"
                ? BuildTable(name, x, y, w, h, tableRows, rowCells, props, diagnostics)
                : MapControl(name, type, bag, x, y, w, h, diagnostics);
            if (element is null) continue;

            if (boundControls.Contains(name))
            {
                diagnostics.Add(Warn("CANMIGDEVREP010",
                    $"'{name}' has a data binding/expression — the bound value was dropped; static text kept. Re-bind in Canvas."));
            }

            elements.Add(element);
            controlCount++;
        }

        elements.Sort((p, q) => p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.X.CompareTo(q.X));

        var reportName = reportClass?.Identifier.ValueText ?? "DevExpress Report";
        diagnostics.Insert(0, Info("CANMIGDEVREP001",
            $"XtraReport '{reportName}' detected — {bands.Count} band(s), {controlCount} control(s) mapped."));

        var design = new DesignExportDto
        {
            Id = $"devexpress-report-{Guid.NewGuid():N}",
            Name = reportName,
            Category = "imported",
            Description = "Imported from a DevExpress XtraReport.",
            PageSettings = new PageSettingsDto { Width = A4WidthPt, Height = A4HeightPt, Unit = "pt" },
            Pages = [new PageDto { Id = "page-1", Elements = elements }]
        };

        return new XtraReportConvertResult { Design = design, Diagnostics = diagnostics };
    }

    // ── Control mapping ──────────────────────────────────────────────────────────────────────────

    private static ElementDto? MapControl(
        string name, string type, Dictionary<string, ExpressionSyntax> bag,
        double x, double y, double w, double h, List<MigrationDiagnostic> diagnostics)
    {
        var element = new ElementDto { Id = $"xr-{name}", Name = name, X = x, Y = y, Width = w, Height = h };

        switch (type)
        {
            case "XRLabel":
            case "XRPageInfo":
            case "XRCheckBox":
                element.Type = "text";
                element.Content = ParseString(bag.GetValueOrDefault("Text")) ?? "";
                element.Style = BuildTextStyle(bag);
                return element;

            case "XRLine":
                element.Type = "line";
                element.Style = new Dictionary<string, object> { ["color"] = ParseColor(bag.GetValueOrDefault("ForeColor")) };
                return element;

            case "XRShape":
            case "XRPanel":
                element.Type = "rect";
                element.Style = new Dictionary<string, object>
                {
                    ["borderColor"] = ParseColor(bag.GetValueOrDefault("ForeColor")),
                    ["backgroundColor"] = ParseColor(bag.GetValueOrDefault("BackColor"))
                };
                return element;

            case "XRPictureBox":
                element.Type = "image";
                element.FitMode = "contain";
                diagnostics.Add(Warn("CANMIGDEVREP013",
                    $"'{name}' picture data isn't embeddable from source — inserted an empty image placeholder."));
                return element;

            case "XRBarCode":
                element.Type = "barcode";
                element.BarcodeValue = ParseString(bag.GetValueOrDefault("Text")) ?? "";
                element.BarcodeType = "code128";
                return element;

            case "XRRichText":
                element.Type = "richtext";
                element.HtmlContent = $"<p>{ParseString(bag.GetValueOrDefault("Text")) ?? ""}</p>";
                return element;

            default:
                diagnostics.Add(Warn("CANMIGDEVREP011",
                    $"'{name}' is a {type} — not supported by Canvas yet; skipped."));
                return null;
        }
    }

    // Build a Canvas table element from an XRTable's row/cell structure (cell .Text → cell content).
    private static ElementDto? BuildTable(
        string name, double x, double y, double w, double h,
        Dictionary<string, List<string>> tableRows,
        Dictionary<string, List<string>> rowCells,
        Dictionary<string, Dictionary<string, ExpressionSyntax>> props,
        List<MigrationDiagnostic> diagnostics)
    {
        if (!tableRows.TryGetValue(name, out var rows) || rows.Count == 0)
        {
            diagnostics.Add(Warn("CANMIGDEVREP011", $"'{name}' XRTable has no parseable rows — skipped."));
            return null;
        }

        var grid = new List<string[]>();
        foreach (var row in rows)
        {
            var cells = rowCells.TryGetValue(row, out var c) ? c : [];
            grid.Add(cells.Select(cell =>
                props.TryGetValue(cell, out var bag) && bag.TryGetValue("Text", out var text)
                    ? ParseString(text) ?? ""
                    : "").ToArray());
        }

        var columns = grid.Max(r => r.Length);
        if (columns == 0)
        {
            diagnostics.Add(Warn("CANMIGDEVREP011", $"'{name}' XRTable has no parseable cells — skipped."));
            return null;
        }

        // Pad ragged rows so every row has the same column count.
        var cellData = grid
            .Select(r => r.Length == columns ? r : r.Concat(Enumerable.Repeat("", columns - r.Length)).ToArray())
            .ToArray();

        return new ElementDto
        {
            Id = $"xr-{name}",
            Name = name,
            Type = "table",
            X = x,
            Y = y,
            Width = w,
            Height = h,
            CellData = cellData,
            ColumnWidths = Enumerable.Repeat(w / columns, columns).ToArray(),
            HeaderRow = true
        };
    }

    private static Dictionary<string, object> BuildTextStyle(Dictionary<string, ExpressionSyntax> bag)
    {
        var style = new Dictionary<string, object> { ["color"] = ParseColor(bag.GetValueOrDefault("ForeColor")) };

        if (bag.GetValueOrDefault("Font") is ObjectCreationExpressionSyntax font)
        {
            var args = font.ArgumentList?.Arguments;
            if (args is { Count: >= 1 } && ParseString(args.Value[0].Expression) is { } family)
                style["fontFamily"] = family;
            if (args is { Count: >= 2 })
                style["fontSize"] = ToNumber(args.Value[1].Expression);
            var fontText = font.ToString();
            if (fontText.Contains("Bold", StringComparison.Ordinal)) style["fontWeight"] = "bold";
            if (fontText.Contains("Italic", StringComparison.Ordinal)) style["fontStyle"] = "italic";
        }

        style["textAlign"] = ParseAlignment(bag.GetValueOrDefault("TextAlignment"));
        return style;
    }

    // ── Value parsing ────────────────────────────────────────────────────────────────────────────

    private static string ParseAlignment(ExpressionSyntax? expr)
    {
        var text = expr?.ToString() ?? "";
        if (text.Contains("Center", StringComparison.Ordinal)) return "center";
        if (text.Contains("Right", StringComparison.Ordinal)) return "right";
        if (text.Contains("Justify", StringComparison.Ordinal)) return "justify";
        return "left";
    }

    private static (double X, double Y) ParsePoint(ExpressionSyntax? expr)
    {
        if (expr is ObjectCreationExpressionSyntax c && c.ArgumentList is { Arguments.Count: >= 2 } a)
            return (ToNumber(a.Arguments[0].Expression), ToNumber(a.Arguments[1].Expression));
        return (0, 0);
    }

    private static (double W, double H) ParseSize(ExpressionSyntax? expr)
    {
        if (expr is ObjectCreationExpressionSyntax c && c.ArgumentList is { Arguments.Count: >= 2 } a)
            return (ToNumber(a.Arguments[0].Expression), ToNumber(a.Arguments[1].Expression));
        return (0, 0);
    }

    private static string? ParseString(ExpressionSyntax? expr)
        => expr is LiteralExpressionSyntax { Token.Value: string s } ? s : null;

    private static string ParseColor(ExpressionSyntax? expr)
    {
        switch (expr)
        {
            case MemberAccessExpressionSyntax ma:
                return NamedColor(ma.Name.Identifier.ValueText);
            case InvocationExpressionSyntax inv when inv.Expression is MemberAccessExpressionSyntax m &&
                                                     m.Name.Identifier.ValueText == "FromArgb":
            {
                var args = inv.ArgumentList.Arguments;
                // FromArgb(r,g,b) or FromArgb(a,r,g,b) — take the last three.
                if (args.Count is 3 or 4)
                {
                    var off = args.Count == 4 ? 1 : 0;
                    int R = (int)ToNumber(args[off].Expression);
                    int G = (int)ToNumber(args[off + 1].Expression);
                    int B = (int)ToNumber(args[off + 2].Expression);
                    return $"#{R:X2}{G:X2}{B:X2}";
                }
                break;
            }
        }
        return "#000000";
    }

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

    private static double ToNumber(ExpressionSyntax? e)
    {
        switch (e)
        {
            case null:
                return 0;
            case LiteralExpressionSyntax { Token.Value: IConvertible c }:
                try { return System.Convert.ToDouble(c, CultureInfo.InvariantCulture); }
                catch { break; }
            case PrefixUnaryExpressionSyntax u when u.IsKind(SyntaxKind.UnaryMinusExpression):
                return -ToNumber(u.Operand);
        }

        var text = (e.ToString() ?? "").TrimEnd('F', 'f', 'D', 'd', 'M', 'm');
        return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;
    }

    // ── Report-level resolution ────────────────────────────────────────────────────────────────────

    private static double ResolveUnitScale(ExpressionSyntax? reportUnit)
    {
        var name = reportUnit is MemberAccessExpressionSyntax ma ? ma.Name.Identifier.ValueText : "";
        return name switch
        {
            "Pixels" => 72.0 / 96.0,            // 96 DPI → points
            "TenthsOfAMillimeter" => 0.1 * 72.0 / 25.4,
            "HundredthsOfAnInch" => 0.72,
            _ => 0.72                            // default report unit
        };
    }

    private static (double Left, double Top) ResolveMargins(ExpressionSyntax? margins)
    {
        // new Margins(left, right, top, bottom) / new DXMargins(...)
        if (margins is ObjectCreationExpressionSyntax c && c.ArgumentList is { Arguments.Count: >= 4 } a)
            return (ToNumber(a.Arguments[0].Expression), ToNumber(a.Arguments[2].Expression));
        return (100, 100); // DevExpress default 1-inch margins (hundredths-of-inch units)
    }

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
        "XRPanel" or "XRPictureBox" or "XRBarCode" or "XRRichText";

    private static bool IsUnsupportedControl(string type) =>
        type.StartsWith("XR", StringComparison.Ordinal) && !type.EndsWith("Band", StringComparison.Ordinal);

    // ── Syntax helpers ─────────────────────────────────────────────────────────────────────────────

    private static bool IsXtraReportClass(ClassDeclarationSyntax cls) =>
        cls.BaseList?.Types.Any(t => SimpleName(t.Type) == "XtraReport") == true;

    private static string? ReceiverName(ExpressionSyntax expr) => expr switch
    {
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } ma => ma.Name.Identifier.ValueText,
        IdentifierNameSyntax id => id.Identifier.ValueText,
        ThisExpressionSyntax => "this",
        _ => null
    };

    private static IEnumerable<string> ExtractControlNames(ArgumentListSyntax args)
    {
        foreach (var arg in args.Arguments)
        {
            switch (arg.Expression)
            {
                case ImplicitArrayCreationExpressionSyntax iac when iac.Initializer is not null:
                    foreach (var e in iac.Initializer.Expressions)
                        if (ReceiverName(e) is { } n) yield return n;
                    break;
                case ArrayCreationExpressionSyntax ac when ac.Initializer is not null:
                    foreach (var e in ac.Initializer.Expressions)
                        if (ReceiverName(e) is { } n) yield return n;
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

    private static MigrationDiagnostic Info(string id, string message) =>
        new() { Id = id, Message = message, Severity = MigrationDiagnosticSeverity.Info };

    private static MigrationDiagnostic Warn(string id, string message) =>
        new() { Id = id, Message = message, Severity = MigrationDiagnosticSeverity.Warning };
}
