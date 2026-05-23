namespace Canvas.Infrastructure.Word;

internal static class StyleExtensions
{
    internal static string GetStr(this Dictionary<string, object>? d, string key, string fallback)
    {
        if (d is null || !d.TryGetValue(key, out var v) || v is null) return fallback;
        return v.ToString()!;
    }

    internal static double GetNum(this Dictionary<string, object>? d, string key, double fallback)
    {
        if (d is null || !d.TryGetValue(key, out var v) || v is null) return fallback;
        return v switch
        {
            double dbl => dbl,
            long lng   => lng,
            int i      => i,
            float f    => f,
            string str => double.TryParse(str, System.Globalization.NumberStyles.Any,
                              System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : fallback,
            System.Text.Json.JsonElement je => je.ValueKind == System.Text.Json.JsonValueKind.Number
                ? je.GetDouble() : fallback,
            _ => fallback
        };
    }
}
