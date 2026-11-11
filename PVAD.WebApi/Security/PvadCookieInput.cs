using Microsoft.Net.Http.Headers;

namespace PVAD.WebApi.Security;

internal static class PvadCookieInput
{
    public static bool HasUnambiguousCookie(HttpRequest request, string name)
    {
        if (request.Headers.Cookie.Count == 0) return true;
        if (!CookieHeaderValue.TryParseStrictList(request.Headers.Cookie.ToArray(), out var cookies)) return false;
        return !cookies.Select(cookie => cookie.Name.Value!)
            .GroupBy(value => value, StringComparer.Ordinal)
            .Any(group => group.Count() > 1 && ManagedName(group.Key, name));
    }

    private static bool ManagedName(string value, string prefix) => value == prefix ||
        value.StartsWith(prefix + "C", StringComparison.Ordinal) &&
        int.TryParse(value.AsSpan(prefix.Length + 1), out var chunk) && chunk > 0;
}
