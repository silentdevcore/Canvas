using Canvas.Core.Contracts;

namespace Canvas.WebApi.Infrastructure;

/// <summary>
/// Resolves the effective value for each localized property given a target language.
/// - Global properties: included for every language; value = localizedValues[target] → localizedValues[system] → "".
/// - Own properties: included ONLY when target matches ownerLanguage; value = localizedValues[ownerLanguage].
/// </summary>
public static class LocalizedPropertyResolver
{
    /// <summary>
    /// Builds a flat dictionary of { key → value } for the given target language,
    /// ready to be injected into the expression evaluator context.
    /// Own properties that do not belong to targetLanguage are excluded entirely.
    /// </summary>
    public static Dictionary<string, string> Resolve(
        IEnumerable<LocalizedPropertyDto>? properties,
        string? targetLanguage,
        string? systemLanguage)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (properties is null) return result;

        var target = NormalizeTag(targetLanguage);
        var system = NormalizeTag(systemLanguage);

        foreach (var prop in properties)
        {
            var (include, value) = ResolveOne(prop, target, system);
            if (include)
                result[prop.Key] = value;
        }

        return result;
    }

    private static (bool include, string value) ResolveOne(LocalizedPropertyDto prop, string? target, string? system)
    {
        if (string.Equals(prop.Scope, "own", StringComparison.OrdinalIgnoreCase))
        {
            var owner = NormalizeTag(prop.OwnerLanguage);
            if (owner != target)
                return (false, ""); // Own property for a different language — skip
            prop.LocalizedValues.TryGetValue(owner ?? "", out var ownVal);
            return (true, ownVal ?? "");
        }

        // Global scope: each language fills its own value
        if (target is not null && prop.LocalizedValues.TryGetValue(target, out var v) && v.Length > 0)
            return (true, v);

        if (system is not null && system != target && prop.LocalizedValues.TryGetValue(system, out var sv) && sv.Length > 0)
            return (true, sv);

        return (true, "");
    }

    /// <summary>Extracts the base language tag (e.g. "de" from "de-DE").</summary>
    public static string? NormalizeTag(string? tag) =>
        string.IsNullOrWhiteSpace(tag) ? null : tag.Split('-')[0].ToLowerInvariant();

    /// <summary>
    /// Scans all element content strings for {{KEY}} patterns and returns distinct keys found.
    /// </summary>
    public static IReadOnlyList<string> ScanPropertyKeys(DesignExportDto design)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in design.Pages)
            foreach (var el in page.Elements)
            {
                ScanContent(el.Content,     keys);
                ScanContent(el.HtmlContent, keys);
            }
        foreach (var el in design.SharedElements)
        {
            ScanContent(el.Content,     keys);
            ScanContent(el.HtmlContent, keys);
        }
        return [.. keys.OrderBy(k => k)];
    }

    private static void ScanContent(string? content, HashSet<string> keys)
    {
        if (string.IsNullOrEmpty(content)) return;
        var span = content.AsSpan();
        int i = 0;
        while (i < span.Length - 3)
        {
            int start = span[i..].IndexOf("{{");
            if (start < 0) break;
            i += start + 2;
            int end = span[i..].IndexOf("}}");
            if (end < 0) break;
            var key = span.Slice(i, end).Trim().ToString();
            if (key.Length > 0 && !key.Contains(' '))
                keys.Add(key);
            i += end + 2;
        }
    }
}
