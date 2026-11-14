using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PVAD.Core.Primitives;

public sealed record DesignerDateResult(string Text, IReadOnlyList<string> Warnings);

/// <summary>Portable Designer date contract, backed by .NET calendar and timezone APIs.</summary>
public static class DesignerDateFormatter
{
    private static readonly JsonElement Contract = JsonDocument.Parse(typeof(DesignerDateFormatter).Assembly
        .GetManifestResourceStream("PVAD.DesignerDateFormatting.json")!).RootElement;

    public static DesignerDateResult Format(string? mode, string? content, string? fallback, string? binding,
        string? format, string? locale, string? timezone, DateTimeOffset instant)
    {
        if (mode is not ("render" or "binding")) return new(content ?? fallback ?? "", []);
        var warnings = new List<string>();
        if (mode == "binding" && !TryParseBoundDate(content, out instant))
            return new(fallback ?? (string.IsNullOrWhiteSpace(binding) ? "" : "{{ " + binding.Trim() + " }}"), ["dateBindingUnresolved"]);

        var defaultLocale = Contract.GetProperty("defaultLocale").GetString()!;
        var defaultFormat = Contract.GetProperty("defaultFormat").GetString()!;
        var localeTag = string.IsNullOrEmpty(locale) ? defaultLocale : locale;
        var language = localeTag.Split('-')[0].ToLowerInvariant();
        if (!Regex.IsMatch(localeTag, @"^[a-zA-Z]{2,3}(?:-[a-zA-Z0-9]{2,8})*$", RegexOptions.None, TimeSpan.FromSeconds(1))
            || !Contract.GetProperty("locales").TryGetProperty(language, out _))
        {
            warnings.Add("dateLocaleFallback");
            language = "en";
        }
        var settings = Contract.GetProperty("locales").GetProperty(language);
        var culture = (CultureInfo)CultureInfo.GetCultureInfo(settings.GetProperty("culture").GetString()!).Clone();
        culture.DateTimeFormat.Calendar = new GregorianCalendar();
        culture.DateTimeFormat.DateSeparator = settings.GetProperty("dateSeparator").GetString()!;
        culture.DateTimeFormat.TimeSeparator = ":";
        culture.DateTimeFormat.AMDesignator = settings.GetProperty("am").GetString()!;
        culture.DateTimeFormat.PMDesignator = settings.GetProperty("pm").GetString()!;

        var zone = string.IsNullOrWhiteSpace(timezone) ? Contract.GetProperty("defaultTimeZone").GetString()! : timezone.Trim();
        if (Contract.GetProperty("timeZoneAliases").TryGetProperty(zone, out var alias)) zone = alias.GetString()!;
        TimeZoneInfo timeZone;
        try
        {
            // Unmapped Windows identifiers must not work on .NET but silently fail in browsers.
            if (zone != "UTC" && !zone.Contains('/')) throw new TimeZoneNotFoundException();
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(zone);
        }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            warnings.Add("dateTimezoneFallback");
            timeZone = TimeZoneInfo.Utc;
        }

        var pattern = string.IsNullOrWhiteSpace(format) ? defaultFormat : format;
        var utc = pattern is "u" or "U" or "r" or "R";
        if (pattern is "r" or "R") culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        if (pattern.Length == 1)
        {
            string S(string key) => settings.GetProperty(key).GetString()!;
            var shortDate = language == "en" && string.Equals(locale, "en-GB", StringComparison.OrdinalIgnoreCase) ? "dd/MM/yyyy" : S("shortDate");
            pattern = Contract.GetProperty("standardFormats").TryGetProperty(pattern, out var standard) ? standard.GetString()! : pattern switch
            {
                "d" => shortDate, "D" => S("longDate"), "t" => S("shortTime"), "T" => S("longTime"),
                "g" => shortDate + " " + S("shortTime"), "G" => shortDate + " " + S("longTime"),
                "f" => S("longDate") + " " + S("shortTime"), "F" or "U" => S("longDate") + " " + S("longTime"),
                "m" or "M" => S("monthDay"), "y" or "Y" => S("yearMonth"), _ => ""
            };
        }
        if (!ValidPattern(pattern))
        {
            warnings.Add("dateFormatFallback");
            pattern = defaultFormat;
            utc = false;
        }
        var dateOnly = mode == "binding" && content?.Length == 10;
        var value = TimeZoneInfo.ConvertTime(instant, utc || dateOnly ? TimeZoneInfo.Utc : timeZone);
        return new(value.ToString(pattern, culture), warnings);
    }

    private static bool TryParseBoundDate(string? content, out DateTimeOffset result)
    {
        result = default;
        if (content is null || !Regex.IsMatch(content,
            @"^\d{4}-\d{2}-\d{2}(?:T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2}))?$",
            RegexOptions.None, TimeSpan.FromSeconds(1))) return false;
        return DateTimeOffset.TryParse(content, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result);
    }

    private static bool ValidPattern(string pattern)
    {
        if (pattern.Length == 0 || pattern.Length > Contract.GetProperty("maximumFormatLength").GetInt32()) return false;
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c is '\'' or '"')
            {
                var quote = c;
                while (++i < pattern.Length && pattern[i] != quote)
                    if (pattern[i] == '\\' && ++i >= pattern.Length) return false;
                if (i >= pattern.Length) return false;
            }
            else if (c == '\\') { if (++i >= pattern.Length) return false; }
            else if (c == '%')
            {
                if (++i >= pattern.Length || !(Contract.GetProperty("tokens").TryGetProperty(pattern[i].ToString(), out _) || pattern[i] is '/' or ':')) return false;
            }
            else if (Contract.GetProperty("tokens").TryGetProperty(c.ToString(), out var maximum))
            {
                var start = i;
                while (i + 1 < pattern.Length && pattern[i + 1] == c) i++;
                if (i - start + 1 > maximum.GetInt32()) return false;
            }
            else if (char.IsAsciiLetter(c) && !Contract.GetProperty("literalLetters").EnumerateArray().Any(value => value.GetString() == c.ToString())) return false;
        }
        return true;
    }
}
