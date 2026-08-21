using System.Collections;
using System.Globalization;
using System.Text.Json;
using PXA.Core.Contracts;

namespace PXA.Core.Primitives;

public static class ChartDefinitionNormalizer
{
    public const int MaximumSeries = 32;
    public const int MaximumPoints = 5000;

    private static readonly string[] Palette =
    [
        "#2563eb", "#16a34a", "#f59e0b", "#dc2626", "#7c3aed", "#0891b2"
    ];

    public static ChartDefinitionDto Normalize(ElementDto element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.Chart is null
            ? FromLegacy(element.ChartType, element.ChartData)
            : NormalizeDefinition(element.Chart);
    }

    public static void SynchronizeLegacyFields(ElementDto element)
    {
        var chart = Normalize(element);
        element.Chart = chart;
        element.ChartType = chart.Type;
        var legacy = element.ChartData is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(element.ChartData);
        legacy["labels"] = chart.Categories.ToArray();
        legacy["datasets"] = chart.Series.Select(series => new Dictionary<string, object?>
        {
            ["label"] = series.Name,
            ["data"] = series.Values.ToArray(),
            ["backgroundColor"] = series.Color,
            ["color"] = series.Color
        }).ToArray();
        element.ChartData = legacy;
    }

    private static ChartDefinitionDto NormalizeDefinition(ChartDefinitionDto source)
    {
        var type = NormalizeType(source.Type);
        var categories = source.Categories.Take(MaximumPoints).Select(value => value ?? "").ToList();
        var series = source.Series.Take(MaximumSeries).Select((item, index) => new ChartSeriesDto
        {
            Id = string.IsNullOrWhiteSpace(item.Id) ? $"series-{index + 1}" : item.Id,
            Name = string.IsNullOrWhiteSpace(item.Name) ? $"Series {index + 1}" : item.Name,
            Type = string.IsNullOrWhiteSpace(item.Type) ? null : NormalizeType(item.Type),
            Values = item.Values.Take(MaximumPoints).Select(NormalizeNumber).ToList(),
            Color = string.IsNullOrWhiteSpace(item.Color) ? Palette[index % Palette.Length] : item.Color,
            StackGroup = type == PxaChartTypes.StackedBar && string.IsNullOrWhiteSpace(item.StackGroup)
                ? "default"
                : item.StackGroup,
            ValueAxisId = string.IsNullOrWhiteSpace(item.ValueAxisId) ? "primary" : item.ValueAxisId,
            Fill = item.Fill || type == PxaChartTypes.Area,
            ShowMarkers = item.ShowMarkers
        }).ToList();

        return new ChartDefinitionDto
        {
            SchemaVersion = 2,
            Type = type,
            Title = source.Title,
            Subtitle = source.Subtitle,
            Locale = NormalizeLocale(source.Locale),
            Categories = categories,
            Series = series,
            CategoryAxis = NormalizeAxis(source.CategoryAxis, "category"),
            ValueAxes = source.ValueAxes.Take(4).Select((axis, index) =>
                NormalizeAxis(axis, index == 0 ? "primary" : $"axis-{index + 1}")!).ToList(),
            Legend = source.Legend is null
                ? new ChartLegendDto()
                : new ChartLegendDto
                {
                    Visible = source.Legend.Visible,
                    Position = NormalizeLegendPosition(source.Legend.Position)
                },
            DataLabels = source.DataLabels is null
                ? new ChartDataLabelsDto()
                : new ChartDataLabelsDto
                {
                    Visible = source.DataLabels.Visible,
                    Position = source.DataLabels.Position,
                    NumberFormat = source.DataLabels.NumberFormat
                },
            Palette = source.Palette.Where(IsColor).Take(32).DefaultIfEmpty(Palette[0]).ToList(),
            Binding = source.Binding,
            Recognition = source.Recognition is null
                ? null
                : new ChartRecognitionDto
                {
                    Status = NormalizeRecognitionStatus(source.Recognition.Status),
                    Confidence = Math.Clamp(source.Recognition.Confidence, 0, 1),
                    SourceKind = source.Recognition.SourceKind,
                    SourceAssetId = source.Recognition.SourceAssetId,
                    DiagnosticCode = source.Recognition.DiagnosticCode
                }
        };
    }

    private static ChartDefinitionDto FromLegacy(string? chartType, Dictionary<string, object>? data)
    {
        var type = NormalizeType(chartType);
        var labels = ReadArray(data?.GetValueOrDefault("labels"))
            .Take(MaximumPoints)
            .Select(value => ReadString(value) ?? "")
            .ToList();
        var datasets = ReadArray(data?.GetValueOrDefault("datasets")).Take(MaximumSeries).ToList();
        var series = datasets.Select((value, index) =>
        {
            var values = ReadArray(GetProperty(value, "data"))
                .Take(MaximumPoints)
                .Select(ReadNullableDouble)
                .ToList();
            return new ChartSeriesDto
            {
                Id = $"series-{index + 1}",
                Name = ReadString(GetProperty(value, "label")) ?? $"Series {index + 1}",
                Values = values,
                Color = ReadString(GetProperty(value, "backgroundColor"))
                    ?? ReadString(GetProperty(value, "color"))
                    ?? Palette[index % Palette.Length],
                StackGroup = type == PxaChartTypes.StackedBar ? "default" : null,
                ValueAxisId = "primary",
                Fill = type == PxaChartTypes.Area,
                ShowMarkers = true
            };
        }).ToList();

        return NormalizeDefinition(new ChartDefinitionDto
        {
            Type = type,
            Title = ReadString(data?.GetValueOrDefault("title")) ?? ReadString(data?.GetValueOrDefault("rdlTitle")),
            Categories = labels,
            Series = series,
            ValueAxes = [new ChartAxisDto()],
            Palette = [.. Palette]
        });
    }

    private static ChartAxisDto? NormalizeAxis(ChartAxisDto? axis, string fallbackId)
    {
        if (axis is null) return null;
        var minimum = NormalizeNumber(axis.Minimum);
        var maximum = NormalizeNumber(axis.Maximum);
        if (minimum.HasValue && maximum.HasValue && minimum > maximum)
            (minimum, maximum) = (maximum, minimum);
        return new ChartAxisDto
        {
            Id = string.IsNullOrWhiteSpace(axis.Id) ? fallbackId : axis.Id,
            Title = axis.Title,
            Minimum = minimum,
            Maximum = maximum,
            Scale = string.Equals(axis.Scale, "logarithmic", StringComparison.OrdinalIgnoreCase)
                ? "logarithmic"
                : "linear",
            NumberFormat = axis.NumberFormat,
            Visible = axis.Visible,
            GridLines = axis.GridLines
        };
    }

    private static string NormalizeType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return PxaChartTypes.Bar;
        return PxaChartTypes.Supported.FirstOrDefault(type =>
                   string.Equals(type, value, StringComparison.OrdinalIgnoreCase))
               ?? PxaChartTypes.Bar;
    }

    private static string NormalizeLegendPosition(string? value) => value?.ToLowerInvariant() switch
    {
        "top" => "top",
        "right" => "right",
        "left" => "left",
        _ => "bottom"
    };

    private static string? NormalizeLocale(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 35) return null;
        try { return CultureInfo.GetCultureInfo(value).Name; }
        catch (CultureNotFoundException) { return null; }
    }

    private static string NormalizeRecognitionStatus(string? value) => value switch
    {
        "automatic" => "automatic",
        "reviewRequired" => "reviewRequired",
        "visualFallback" => "visualFallback",
        _ => "native"
    };

    private static double? NormalizeNumber(double? value) =>
        value.HasValue && double.IsFinite(value.Value) ? value.Value : null;

    private static bool IsColor(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 64;

    private static IEnumerable<object?> ReadArray(object? value)
    {
        if (value is JsonElement { ValueKind: JsonValueKind.Array } json)
            return json.EnumerateArray().Cast<object?>().ToArray();
        if (value is IEnumerable enumerable && value is not string)
            return enumerable.Cast<object?>();
        return [];
    }

    private static object? GetProperty(object? source, string name)
    {
        if (source is JsonElement { ValueKind: JsonValueKind.Object } json &&
            json.TryGetProperty(name, out var property))
            return property;
        if (source is IDictionary<string, object> dictionary && dictionary.TryGetValue(name, out var value))
            return value;
        return null;
    }

    private static string? ReadString(object? value) => value switch
    {
        null => null,
        JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
        JsonElement json => json.ToString(),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)
    };

    private static double? ReadNullableDouble(object? value)
    {
        if (value is null) return null;
        if (value is JsonElement json)
            return json.ValueKind == JsonValueKind.Null
                ? null
                : json.TryGetDouble(out var parsed) ? parsed : null;
        try
        {
            var parsed = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return double.IsFinite(parsed) ? parsed : null;
        }
        catch
        {
            return null;
        }
    }
}
