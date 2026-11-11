using Microsoft.AspNetCore.Authentication.Cookies;

namespace PVAD.WebApi.Security;

/// <summary>Rejects ambiguous requests that carry the managed session cookie more than once.</summary>
public sealed class PvadStrictCookieManager : ICookieManager
{
    private readonly ChunkingCookieManager inner = new();

    public string? GetRequestCookie(HttpContext context, string key) =>
        PvadCookieInput.HasUnambiguousCookie(context.Request, key) ? inner.GetRequestCookie(context, key) : null;

    public void AppendResponseCookie(HttpContext context, string key, string? value, CookieOptions options) =>
        inner.AppendResponseCookie(context, key, value, options);

    public void DeleteCookie(HttpContext context, string key, CookieOptions options) =>
        inner.DeleteCookie(context, key, options);
}
