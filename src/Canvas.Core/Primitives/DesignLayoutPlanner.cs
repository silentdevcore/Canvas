using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Canvas.Core.Contracts;

namespace Canvas.Core.Primitives;

public sealed record PlannedPage(string PageId, IReadOnlyList<ElementDto> Elements);

public static class DesignLayoutPlanner
{
    public static IReadOnlyList<PlannedPage> BuildPages(
        DesignExportDto design,
        Func<ElementDto, int>? zIndexSelector = null)
    {
        ArgumentNullException.ThrowIfNull(design);

        var sharedElements = design.SharedElements ?? [];
        var pages = design.Pages ?? [];
        var allPages = pages.Count > 0
            ? pages
            : [new PageDto { Id = "p1", Elements = sharedElements }];

        var planned = new List<PlannedPage>(allPages.Count);
        foreach (var page in allPages)
        {
            var pageElements = page.Elements ?? [];
            var visible = ExpandRepeats(pageElements, design)
                .Concat(sharedElements.Where(s => !pageElements.Any(e => e.Id == s.Id)))
                .Where(e => e.Hidden != true)
                .OrderBy(e => e.Y)
                .ThenBy(e => e.X)
                .ThenBy(zIndexSelector ?? (_ => 0))
                .ThenBy(e => e.Id, StringComparer.Ordinal)
                .ToList();

            planned.Add(new PlannedPage(page.Id, visible));
        }

        return planned;
    }

    private static IEnumerable<ElementDto> ExpandRepeats(IEnumerable<ElementDto> elements, DesignExportDto design)
    {
        var payload = BuildPayload(design);
        foreach (var element in elements)
        {
            var dataPath = element.Repeat?.DataPath;
            if (string.IsNullOrWhiteSpace(dataPath) || ResolveDataPath(payload, dataPath) is not { } data)
            {
                yield return ApplyDynamicData(element, payload);
                continue;
            }

            var items = ApplyRdlFilters(ToEnumerable(data), element.Style, payload).ToList();
            if (items.Count == 0)
                continue;

            for (var index = 0; index < items.Count; index++)
            {
                var clone = CloneElement(element);
                clone.Id = $"{element.Id}__repeat_{index}";
                clone.Name = string.IsNullOrWhiteSpace(element.Name) ? element.Name : $"{element.Name} {index + 1}";
                clone.Y = element.Y + element.Height * index;
                clone.Repeat = null;
                ApplyRepeatItem(clone, items[index], index);
                yield return ApplyDynamicData(clone, payload);
            }
        }
    }

    private static ElementDto ApplyDynamicData(ElementDto element, Dictionary<string, object?> payload)
    {
        if (element.Type != "chart" || element.ChartData is null)
            return element;

        if (ReadChartString(element.ChartData, "rdlDataSetName") is not { Length: > 0 } dataSetName
            || ResolveDataPath(payload, dataSetName) is not { } data
            || ToEnumerable(data).ToList() is not { Count: > 0 } rows)
            return element;

        var categoryExpression = ReadChartString(element.ChartData, "rdlCategoryExpression");
        var categoryField = RdlFieldName(categoryExpression);
        var labels = categoryField is null
            ? Enumerable.Range(1, rows.Count).Select(i => i.ToString()).ToArray()
            : rows.Select(row => ResolveToken(ItemValues(row), categoryField)?.ToString() ?? "").ToArray();

        var series = ReadChartSeries(element.ChartData).ToList();
        if (series.Count == 0 && ReadChartString(element.ChartData, "rdlValueExpression") is { Length: > 0 } valueExpression)
            series.Add(new Dictionary<string, object?> { ["name"] = "Data", ["y"] = valueExpression });

        if (series.Count == 0)
            return element;

        var clone = CloneElement(element);
        clone.ChartData ??= [];
        clone.ChartData["labels"] = labels;
        clone.ChartData["datasets"] = series.Select((seriesItem, index) =>
        {
            var field = RdlFieldName(DictString(seriesItem, "y"));
            var values = field is null
                ? Enumerable.Repeat(0d, rows.Count).ToArray()
                : rows.Select(row => NumericValue(ResolveToken(ItemValues(row), field))).ToArray();
            return new Dictionary<string, object>
            {
                ["label"] = DictString(seriesItem, "name") ?? $"Series {index + 1}",
                ["data"] = values,
                ["backgroundColor"] = ChartColor(index)
            };
        }).ToArray();
        clone.ChartData["rdlSampleDataSource"] = dataSetName;
        return clone;
    }

    private static Dictionary<string, object?> BuildPayload(DesignExportDto design)
    {
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in design.PageSettings?.CustomProperties ?? [])
        {
            if (string.IsNullOrWhiteSpace(property.Name))
                continue;

            var value = ParsePayloadValue(property.Value);
            payload[property.Name] = value;
            if (value is Dictionary<string, object?> nested)
            {
                foreach (var pair in nested)
                    payload.TryAdd(pair.Key, pair.Value);
            }
        }
        AddRdlReportParameterDefaults(payload);
        return payload;
    }

    private static void AddRdlReportParameterDefaults(Dictionary<string, object?> payload)
    {
        if (!payload.TryGetValue("rdlReportParameters", out var value) || value is not List<object?> parameters)
            return;

        foreach (var item in parameters)
        {
            if (item is not IReadOnlyDictionary<string, object?> parameter
                || !parameter.TryGetValue("Name", out var nameValue)
                || nameValue?.ToString() is not { Length: > 0 } name)
                continue;

            if (!parameter.TryGetValue("DefaultValue", out var defaultValue) || defaultValue is null)
                continue;

            var text = defaultValue.ToString();
            payload.TryAdd(name, text);
            payload.TryAdd($"Parameters.{name}", text);
            payload.TryAdd($"Parameters!{name}.Value", text);
        }
    }

    private static object? ParsePayloadValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        try
        {
            using var doc = JsonDocument.Parse(value);
            return FromJson(doc.RootElement);
        }
        catch (JsonException)
        {
            return value;
        }
    }

    private static object? FromJson(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject()
            .ToDictionary(p => p.Name, p => FromJson(p.Value), StringComparer.OrdinalIgnoreCase),
        JsonValueKind.Array => element.EnumerateArray().Select(FromJson).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };

    private static object? ResolveDataPath(Dictionary<string, object?> payload, string dataPath)
    {
        object? current = payload;
        foreach (var part in dataPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            current = current switch
            {
                Dictionary<string, object?> dict when dict.TryGetValue(part, out var value) => value,
                IReadOnlyDictionary<string, object?> dict when dict.TryGetValue(part, out var value) => value,
                IReadOnlyDictionary<string, object> dict when dict.TryGetValue(part, out var value) => value,
                _ => null
            };
            if (current is null)
                return null;
        }
        return current;
    }

    private static IEnumerable<object?> ToEnumerable(object data)
    {
        if (data is string)
            yield break;
        if (data is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable)
                yield return item;
            yield break;
        }
        yield return data;
    }

    private static IEnumerable<object?> ApplyRdlFilters(
        IEnumerable<object?> items,
        Dictionary<string, object>? style,
        IReadOnlyDictionary<string, object?> payload)
    {
        var filters = ReadDictionaryArray(style, "rdlFilters").ToList();
        if (filters.Count == 0)
            return items;

        return items.Where(item => filters.All(filter => MatchesRdlFilter(item, filter, payload)));
    }

    private static IEnumerable<IReadOnlyDictionary<string, object?>> ReadDictionaryArray(
        Dictionary<string, object>? style,
        string key)
    {
        if (style is null || !style.TryGetValue(key, out var value) || value is null)
            yield break;

        if (value is JsonElement { ValueKind: JsonValueKind.Array } jsonArray)
        {
            foreach (var item in jsonArray.EnumerateArray())
            {
                if (FromJson(item) is IReadOnlyDictionary<string, object?> dict)
                    yield return dict;
            }
            yield break;
        }

        if (value is System.Collections.IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                if (item is IReadOnlyDictionary<string, object?> nullableDict)
                    yield return nullableDict;
                else if (item is IReadOnlyDictionary<string, object> dict)
                    yield return dict.ToDictionary(k => k.Key, v => (object?)v.Value, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private static string? ReadChartString(Dictionary<string, object> chartData, string key) =>
        chartData.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static IEnumerable<IReadOnlyDictionary<string, object?>> ReadChartSeries(Dictionary<string, object> chartData)
    {
        if (!chartData.TryGetValue("rdlSeries", out var value) || value is null)
            yield break;

        if (value is JsonElement { ValueKind: JsonValueKind.Array } jsonArray)
        {
            foreach (var item in jsonArray.EnumerateArray())
            {
                if (FromJson(item) is IReadOnlyDictionary<string, object?> dict)
                    yield return dict;
            }
            yield break;
        }

        if (value is System.Collections.IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                if (item is IReadOnlyDictionary<string, object?> nullableDict)
                    yield return nullableDict;
                else if (item is IReadOnlyDictionary<string, object> dict)
                    yield return dict.ToDictionary(k => k.Key, v => (object?)v.Value, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private static bool MatchesRdlFilter(
        object? item,
        IReadOnlyDictionary<string, object?> filter,
        IReadOnlyDictionary<string, object?> payload)
    {
        var expression = DictString(filter, "FilterExpression");
        var op = DictString(filter, "Operator") ?? "Equal";
        var values = DictValues(filter, "FilterValues")
            .Select(value => ResolveRdlValue(value, payload)?.ToString() ?? value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        if (string.IsNullOrWhiteSpace(expression) || values.Count == 0)
            return true;

        var actual = ResolveRdlValue(expression, item)?.ToString();
        return op switch
        {
            "Equal" => values.Any(value => string.Equals(actual ?? "", value, StringComparison.OrdinalIgnoreCase)),
            "Like" => values.Any(value => Like(actual ?? "", value)),
            "NotEqual" => values.All(value => !string.Equals(actual ?? "", value, StringComparison.OrdinalIgnoreCase)),
            _ => true
        };
    }

    private static string? DictString(IReadOnlyDictionary<string, object?> dict, string key) =>
        dict.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static string? RdlFieldName(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return null;
        var match = Regex.Match(expression, @"Fields!(?<name>\w+)\.Value", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["name"].Value : null;
    }

    private static double NumericValue(object? value)
    {
        if (value is null)
            return 0;
        if (value is double d)
            return d;
        if (value is float f)
            return f;
        if (value is int i)
            return i;
        if (value is long l)
            return l;
        return double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static string ChartColor(int index) =>
        new[] { "#2563eb", "#16a34a", "#dc2626", "#9333ea", "#ea580c", "#0891b2" }[index % 6];

    private static IEnumerable<string> DictValues(IReadOnlyDictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value) || value is null)
            yield break;

        if (value is string text)
        {
            yield return text;
            yield break;
        }

        if (value is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable)
                if (item?.ToString() is { Length: > 0 } itemText)
                    yield return itemText;
        }
    }

    private static object? ResolveRdlValue(string expression, object? source)
    {
        var field = Regex.Match(expression, @"^\s*=?\s*Fields!(?<name>\w+)\.Value\s*$", RegexOptions.IgnoreCase);
        if (field.Success)
            return ResolveToken(ItemValues(source), field.Groups["name"].Value);

        var parameter = Regex.Match(expression, @"^\s*=?\s*Parameters!(?<name>\w+)\.Value\s*$", RegexOptions.IgnoreCase);
        if (parameter.Success && source is IReadOnlyDictionary<string, object?> payload)
            return ResolveToken(payload, parameter.Groups["name"].Value)
                ?? ResolveToken(payload, $"Parameters.{parameter.Groups["name"].Value}");

        return expression.TrimStart('=');
    }

    private static bool Like(string value, string pattern)
    {
        var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(value, regex, RegexOptions.IgnoreCase);
    }

    private static ElementDto CloneElement(ElementDto element) =>
        JsonSerializer.Deserialize<ElementDto>(JsonSerializer.Serialize(element))!;

    private static void ApplyRepeatItem(ElementDto element, object? item, int index)
    {
        var values = ItemValues(item);
        values["index"] = index;
        element.Content = Substitute(element.Content, values);
        element.HtmlContent = Substitute(element.HtmlContent, values);
        element.Binding = Substitute(element.Binding, values);
        element.Expression = Substitute(element.Expression, values);
        if (element.CellData is not null)
        {
            element.CellData = element.CellData
                .Select(row => row.Select(cell => Substitute(cell, values) ?? "").ToArray())
                .ToArray();
        }
    }

    private static Dictionary<string, object?> ItemValues(object? item)
    {
        if (item is Dictionary<string, object?> nullableDict)
            return new Dictionary<string, object?>(nullableDict, StringComparer.OrdinalIgnoreCase);
        if (item is IReadOnlyDictionary<string, object?> readNullable)
            return new Dictionary<string, object?>(readNullable, StringComparer.OrdinalIgnoreCase);
        if (item is IReadOnlyDictionary<string, object> readDict)
            return readDict.ToDictionary(k => k.Key, v => (object?)v.Value, StringComparer.OrdinalIgnoreCase);
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["value"] = item };
    }

    private static readonly Regex BindingTokenRegex = new(@"\{\{\s*([A-Za-z_][A-Za-z0-9_.]*)\s*\}\}", RegexOptions.Compiled);

    private static string? Substitute(string? value, IReadOnlyDictionary<string, object?> item)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return BindingTokenRegex.Replace(value, match =>
        {
            var key = match.Groups[1].Value;
            return ResolveToken(item, key)?.ToString() ?? match.Value;
        });
    }

    private static object? ResolveToken(IReadOnlyDictionary<string, object?> item, string key)
    {
        object? current = item;
        foreach (var part in key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            current = current switch
            {
                IReadOnlyDictionary<string, object?> dict when dict.TryGetValue(part, out var value) => value,
                IReadOnlyDictionary<string, object> dict when dict.TryGetValue(part, out var value) => value,
                _ => null
            };
            if (current is null)
                return null;
        }
        return current;
    }
}
