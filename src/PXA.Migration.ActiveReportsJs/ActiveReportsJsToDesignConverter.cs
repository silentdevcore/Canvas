using System.Globalization;
using System.Text.Json;
using PXA.Core.Contracts;
using PXA.Migration.Abstractions;

namespace PXA.Migration.ActiveReportsJs;

public sealed class ActiveReportsJsConvertResult
{
    public required DesignExportDto Design { get; init; }
    public required IReadOnlyList<MigrationDiagnostic> Diagnostics { get; init; }
}

/// <summary>
/// Cautious V1 converter for ActiveReports JS JSON layouts. The detector intentionally requires an
/// explicit ActiveReports marker so ordinary data JSON files are not routed into report migration.
/// </summary>
public sealed class ActiveReportsJsToDesignConverter
{
    private const double A4WidthPt = 595;
    private const double A4HeightPt = 842;

    public static bool LooksLikeActiveReportsJs(string source)
    {
        if (string.IsNullOrWhiteSpace(source) || !source.TrimStart().StartsWith('{')) return false;
        try
        {
            using var doc = JsonDocument.Parse(source);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            var marker = GetString(doc.RootElement, "reportType")
                ?? GetString(doc.RootElement, "reportKind")
                ?? GetString(doc.RootElement, "designer");
            return marker is not null
                && marker.Contains("ActiveReports", StringComparison.OrdinalIgnoreCase)
                && marker.Contains("JS", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public ActiveReportsJsConvertResult ConvertAuto(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source cannot be null or empty.", nameof(source));
        return Convert(source);
    }

    public ActiveReportsJsConvertResult Convert(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Source cannot be null or empty.", nameof(json));

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new ArgumentException($"Invalid ActiveReports JS JSON: {ex.Message}", nameof(json)); }
        using (doc)
        {
            if (!LooksLikeActiveReportsJs(json))
                throw new ArgumentException("Not an ActiveReports JS JSON report — expected reportType/reportKind/designer containing 'ActiveReportsJS'.", nameof(json));

            var root = doc.RootElement;
            var diagnostics = new List<MigrationDiagnostic>();
            var reportName = GetString(root, "name") ?? GetString(root, "reportName") ?? "ActiveReports JS Report";
            var (pageWidth, pageHeight) = PageSize(root);
            var elements = new List<ElementDto>();

            foreach (var item in ReportItems(root))
            {
                if (MapItem(item, diagnostics) is { } mapped)
                    elements.Add(mapped);
            }

            elements.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            diagnostics.Insert(0, Info("CANMIGARJS001",
                $"ActiveReports JS report '{reportName}' detected — {elements.Count} item(s) mapped."));

            var design = new DesignExportDto
            {
                Id = $"activereports-js-{Guid.NewGuid():N}",
                Name = reportName,
                Category = "imported",
                Description = "Imported from an ActiveReports JS JSON report.",
                PageSettings = new PageSettingsDto { Width = pageWidth, Height = pageHeight, Unit = "pt" },
                Pages = [new PageDto { Id = "page-1", Elements = elements }],
                SharedElements = []
            };

            return new ActiveReportsJsConvertResult { Design = design, Diagnostics = diagnostics };
        }
    }

    private static ElementDto? MapItem(JsonElement item, List<MigrationDiagnostic> diagnostics)
    {
        if (item.ValueKind != JsonValueKind.Object) return null;
        var type = (GetString(item, "type") ?? GetString(item, "itemType") ?? GetString(item, "kind") ?? "unknown").Trim();
        var name = GetString(item, "name") ?? type;
        var element = new ElementDto
        {
            Id = $"arjs-{SafeId(name)}",
            Name = name,
            X = Number(item, "left", "x"),
            Y = Number(item, "top", "y"),
            Width = Number(item, "width", "w", fallback: 120),
            Height = Number(item, "height", "h", fallback: 24)
        };

        switch (type.ToLowerInvariant())
        {
            case "textbox":
            case "text":
            case "label":
                element.Type = "text";
                element.Content = ValueText(item);
                element.Style = TextStyle(item);
                ApplyBinding(element, element.Content, diagnostics);
                diagnostics.Add(Info("CANMIGARJS002", $"'{name}' ({type}) → PXA text."));
                return element;

            case "line":
                element.Type = "line";
                element.Style = new Dictionary<string, object> { ["color"] = StyleString(item, "color") ?? "#000000" };
                if (StyleNumber(item, "strokeWidth") is { } sw) element.Style["strokeWidth"] = sw;
                diagnostics.Add(Info("CANMIGARJS002", $"'{name}' ({type}) → PXA line."));
                return element;

            case "image":
            case "picture":
                element.Type = "image";
                element.FitMode = "contain";
                element.Content = GetString(item, "source") ?? GetString(item, "value") ?? GetString(item, "content");
                diagnostics.Add(Info("CANMIGARJS002", $"'{name}' ({type}) → PXA image."));
                return element;

            case "table":
            case "tablix":
                element.Type = "table";
                element.CellData = ParseRows(item);
                element.ColumnWidths = ParseColumnWidths(item, element.Width, element.CellData?.FirstOrDefault()?.Length ?? 1);
                element.HeaderRow = true;
                diagnostics.Add(Warn("CANMIGARJS003", $"'{name}' table mapped best-effort; grouping/repeat semantics need review."));
                return element;

            case "barcode":
                element.Type = "barcode";
                element.BarcodeValue = ValueText(item);
                element.BarcodeType = GetString(item, "barcodeType") ?? GetString(item, "symbology") ?? "code128";
                diagnostics.Add(Info("CANMIGARJS002", $"'{name}' ({type}) → PXA barcode."));
                return element;

            default:
                element.Type = "text";
                element.Content = $"[{type}: migrate manually]";
                element.Style = new Dictionary<string, object>
                {
                    ["backgroundColor"] = "#F0F0F0",
                    ["borderColor"] = "#BBBBBB",
                    ["borderWidth"] = 1.0,
                    ["borderStyle"] = "dashed",
                    ["color"] = "#888888",
                    ["textAlign"] = "center",
                    ["fontStyle"] = "italic",
                    ["activeReportsJsItem"] = type
                };
                diagnostics.Add(Warn("CANMIGARJS011", $"'{name}' is a {type} — inserted a metadata placeholder."));
                return element;
        }
    }

    private static IEnumerable<JsonElement> ReportItems(JsonElement root)
    {
        if (TryGet(root, out var body, "body", "Body")
            && TryGet(body, out var bodyItems, "reportItems", "items", "ReportItems"))
            return ArrayItems(bodyItems);
        if (TryGet(root, out var items, "reportItems", "items", "ReportItems"))
            return ArrayItems(items);
        if (TryGet(root, out var pages, "pages", "Pages") && pages.ValueKind == JsonValueKind.Array)
            return pages.EnumerateArray()
                .SelectMany(page => TryGet(page, out var pageItems, "reportItems", "items", "ReportItems") ? ArrayItems(pageItems) : []);
        return [];
    }

    private static IEnumerable<JsonElement> ArrayItems(JsonElement value) =>
        value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];

    private static string? GetString(JsonElement obj, params string[] names)
    {
        if (!TryGet(obj, out var value, names)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static bool TryGet(JsonElement obj, out JsonElement value, params string[] names)
    {
        if (obj.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in obj.EnumerateObject())
            {
                if (names.Any(name => string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    private static double Number(JsonElement obj, string name, string alt, double fallback = 0)
    {
        if (!TryGet(obj, out var value, name, alt)) return fallback;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var d)) return d;
        if (value.ValueKind == JsonValueKind.String) return LengthToPt(value.GetString(), fallback);
        return fallback;
    }

    private static double LengthToPt(string? value, double fallback = 0)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var text = value.Trim().ToLowerInvariant();
        var unit = text.EndsWith("in") ? "in" : text.EndsWith("cm") ? "cm" : text.EndsWith("mm") ? "mm" : text.EndsWith("px") ? "px" : "pt";
        var numText = unit == "pt" ? text.TrimEnd('p', 't') : text[..^unit.Length];
        if (!double.TryParse(numText, NumberStyles.Any, CultureInfo.InvariantCulture, out var n)) return fallback;
        return unit switch
        {
            "in" => n * 72,
            "cm" => n * 72 / 2.54,
            "mm" => n * 72 / 25.4,
            "px" => n * 0.75,
            _ => n
        };
    }

    private static (double W, double H) PageSize(JsonElement root)
    {
        if (TryGet(root, out var page, "page", "pageSettings", "Page"))
            return (Number(page, "width", "w", A4WidthPt), Number(page, "height", "h", A4HeightPt));
        return (Number(root, "pageWidth", "width", A4WidthPt), Number(root, "pageHeight", "height", A4HeightPt));
    }

    private static string ValueText(JsonElement item) =>
        GetString(item, "value") ?? GetString(item, "text") ?? GetString(item, "content") ?? "";

    private static Dictionary<string, object> TextStyle(JsonElement item)
    {
        var style = new Dictionary<string, object>();
        if (StyleString(item, "color") is { } color) style["color"] = color;
        if (StyleString(item, "backgroundColor") is { } bg) style["backgroundColor"] = bg;
        if (StyleString(item, "fontFamily") is { } fontFamily) style["fontFamily"] = fontFamily;
        if (StyleString(item, "textAlign") is { } textAlign) style["textAlign"] = textAlign.ToLowerInvariant();
        if (StyleNumber(item, "fontSize") is { } fontSize) style["fontSize"] = fontSize;
        if (StyleBool(item, "bold") == true) style["fontWeight"] = "bold";
        if (StyleBool(item, "italic") == true) style["fontStyle"] = "italic";
        return style;
    }

    private static string? StyleString(JsonElement item, string name)
    {
        if (TryGet(item, out var style, "style", "Style") && GetString(style, name) is { } styled) return styled;
        return GetString(item, name);
    }

    private static double? StyleNumber(JsonElement item, string name)
    {
        if (TryGet(item, out var style, "style", "Style") && TryGet(style, out var styled, name))
            return styled.ValueKind == JsonValueKind.Number && styled.TryGetDouble(out var d) ? d : LengthToPt(styled.GetString());
        if (TryGet(item, out var direct, name))
            return direct.ValueKind == JsonValueKind.Number && direct.TryGetDouble(out var d) ? d : LengthToPt(direct.GetString());
        return null;
    }

    private static bool? StyleBool(JsonElement item, string name)
    {
        if (TryGet(item, out var style, "style", "Style") && TryGet(style, out var styled, name))
            return styled.ValueKind == JsonValueKind.True ? true : styled.ValueKind == JsonValueKind.False ? false : null;
        return null;
    }

    private static string[][]? ParseRows(JsonElement item)
    {
        if (!TryGet(item, out var rows, "rows", "cellData", "data") || rows.ValueKind != JsonValueKind.Array)
            return null;
        return rows.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Array)
            .Select(row => row.EnumerateArray().Select(CellText).ToArray())
            .ToArray();
    }

    private static string CellText(JsonElement cell) =>
        cell.ValueKind == JsonValueKind.Object ? ValueText(cell) : GetString(cell) ?? "";

    private static string? GetString(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };

    private static double[]? ParseColumnWidths(JsonElement item, double totalWidth, int columns)
    {
        if (!TryGet(item, out var widths, "columnWidths", "columns") || widths.ValueKind != JsonValueKind.Array)
            return columns > 0 ? Enumerable.Repeat(totalWidth / columns, columns).ToArray() : null;
        var parsed = widths.EnumerateArray()
            .Select(w => w.ValueKind == JsonValueKind.Object ? Number(w, "width", "w", totalWidth / columns) : LengthToPt(GetString(w), totalWidth / columns))
            .ToArray();
        return parsed.Length > 0 ? parsed : null;
    }

    private static void ApplyBinding(ElementDto element, string? value, List<MigrationDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var trimmed = value.Trim();
        if (trimmed.StartsWith("=", StringComparison.Ordinal))
        {
            element.Expression = trimmed;
            diagnostics.Add(Warn("CANMIGARJS010", $"'{element.Name}' expression preserved for review."));
        }
        else if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            var field = trimmed.Trim('{', '}').Split('.').LastOrDefault();
            if (!string.IsNullOrWhiteSpace(field))
            {
                element.Binding = field;
                element.Content = $"{{{{{field}}}}}";
                diagnostics.Add(Info("CANMIGARJS010", $"'{element.Name}' bound to '{field}'."));
            }
        }
    }

    private static string SafeId(string value) =>
        string.Concat(value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')).Trim('-').ToLowerInvariant();

    private static MigrationDiagnostic Info(string id, string message) =>
        new() { Id = id, Message = message, Severity = MigrationDiagnosticSeverity.Info };

    private static MigrationDiagnostic Warn(string id, string message) =>
        new() { Id = id, Message = message, Severity = MigrationDiagnosticSeverity.Warning };
}
