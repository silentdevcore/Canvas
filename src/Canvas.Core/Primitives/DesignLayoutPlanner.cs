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
                yield return element;
                continue;
            }

            var items = ToEnumerable(data).ToList();
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
                yield return clone;
            }
        }
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
        return payload;
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
