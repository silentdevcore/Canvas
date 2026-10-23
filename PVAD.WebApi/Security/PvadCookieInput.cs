using Microsoft.Net.Http.Headers;

namespace PVAD.WebApi.Security;

internal static class PvadCookieInput
{
    public static bool TryGetPresence(HttpRequest request, string canonical, string legacy,
        out bool hasCanonical, out bool hasLegacy)
    {
        hasCanonical = hasLegacy = false;
        if (request.Headers.Cookie.Count == 0) return true;
        if (!CookieHeaderValue.TryParseStrictList(request.Headers.Cookie.ToArray(), out var cookies)) return false;
        var names = cookies.Select(cookie => cookie.Name.Value!).ToArray();
        hasCanonical = names.Contains(canonical, StringComparer.Ordinal);
        hasLegacy = names.Contains(legacy, StringComparer.Ordinal);
        return !names.GroupBy(name => name, StringComparer.Ordinal).Any(group => group.Count() > 1 &&
            (ManagedName(group.Key, canonical) || ManagedName(group.Key, legacy)));
    }

    private static bool ManagedName(string name, string prefix) => name == prefix ||
        name.StartsWith(prefix + "C", StringComparison.Ordinal) &&
        int.TryParse(name.AsSpan(prefix.Length + 1), out var chunk) && chunk > 0;
}
