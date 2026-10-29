using System.Collections;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PVAD.WebApi.Security;

namespace PVAD.WebApi.Infrastructure;

public sealed class PvadAntiforgeryCookieMiddleware(RequestDelegate next, IOptions<AntiforgeryOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var name = options.Value.Cookie.Name!;
        var legacy = name switch
        {
            "__Host-PVAD.Antiforgery" => PvadCookieNames.LegacyAntiforgery(true),
            "PVAD.Antiforgery.Development" => PvadCookieNames.LegacyAntiforgery(false),
            _ => null,
        };
        if (legacy is null)
        {
            await next(context);
            return;
        }
        var cookieOptions = options.Value.Cookie.Build(context);
        var validInput = PvadCookieInput.TryGetPresence(context.Request, name, legacy, out var hasCanonical, out var hasLegacy);
        if (validInput && !hasLegacy)
        {
            await next(context);
            return;
        }
        var legacyValue = context.Request.Cookies[legacy] ?? "";
        if (!validInput || hasCanonical)
        {
            if (validInput && !string.IsNullOrEmpty(legacyValue) &&
                string.Equals(context.Request.Cookies[name], legacyValue, StringComparison.Ordinal))
            {
                context.Response.Cookies.Delete(legacy, cookieOptions);
                await next(context);
                return;
            }
            context.Response.Cookies.Delete(name, cookieOptions);
            context.Response.Cookies.Delete(legacy, cookieOptions);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 400, Title = "Conflicting CSRF cookies", Detail = "Request a fresh CSRF token and retry.",
                Extensions = { ["code"] = PvadApiProblems.InvalidCsrf },
            }, options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
            return;
        }

        var original = context.Request.Cookies;
        context.Request.Cookies = new AliasedCookies(original, name, legacyValue);
        context.Response.Cookies.Append(name, legacyValue, cookieOptions);
        context.Response.Cookies.Delete(legacy, cookieOptions);
        try { await next(context); }
        finally { context.Request.Cookies = original; }
    }

    private sealed class AliasedCookies(IRequestCookieCollection original, string name, string value) : IRequestCookieCollection
    {
        public int Count => original.Count + 1;
        public ICollection<string> Keys => original.Keys.Append(name).ToArray();
        public string? this[string key] => key == name ? value : original[key];
        public bool ContainsKey(string key) => key == name || original.ContainsKey(key);
        public bool TryGetValue(string key, out string valueResult)
        {
            if (key == name) { valueResult = value; return true; }
            return original.TryGetValue(key, out valueResult!);
        }
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() =>
            original.Append(new KeyValuePair<string, string>(name, value)).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
