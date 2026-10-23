using Microsoft.AspNetCore.Authentication.Cookies;

namespace PVAD.WebApi.Security;

public sealed class PvadMigratingCookieManager(string legacyName) : ICookieManager
{
    private readonly ChunkingCookieManager inner = new();
    private const string RenewalMarker = "PVAD.CookieMigration:";

    public static bool WasLegacyRead(HttpContext context, string cookieName) =>
        context.Items.ContainsKey(RenewalMarker + cookieName);

    public string? GetRequestCookie(HttpContext context, string key)
    {
        if (!PvadCookieInput.TryGetPresence(context.Request, key, legacyName, out var canonicalPresent, out _)) return null;
        var canonical = inner.GetRequestCookie(context, key);
        var legacy = inner.GetRequestCookie(context, legacyName);
        if (canonicalPresent)
            return legacy is not null && !string.Equals(canonical, legacy, StringComparison.Ordinal) ? null : canonical;
        if (legacy is not null) context.Items[RenewalMarker + key] = true;
        return legacy;
    }

    public void AppendResponseCookie(HttpContext context, string key, string? value, CookieOptions options)
    {
        inner.DeleteCookie(context, legacyName, options);
        inner.AppendResponseCookie(context, key, value, options);
    }

    public void DeleteCookie(HttpContext context, string key, CookieOptions options)
    {
        inner.DeleteCookie(context, key, options);
        inner.DeleteCookie(context, legacyName, options);
    }
}
