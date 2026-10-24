namespace PVAD.WebApi.Infrastructure;

/// <summary>Routes the former branded API prefix through the canonical pipeline.</summary>
public sealed class PvadLegacyApiMiddleware(RequestDelegate next)
{
    public const string LegacyPrefix = "/api/pxa/v1";
    public const string CanonicalPrefix = "/api/pvad/v1";
    private static readonly object LegacyRequestMarker = new();

    public static bool IsLegacyRequest(HttpContext context) =>
        context.Items.ContainsKey(LegacyRequestMarker) ||
        (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) &&
         !context.Request.Path.StartsWithSegments("/api/pvad", StringComparison.OrdinalIgnoreCase));

    public async Task InvokeAsync(HttpContext context)
    {
        var matchesVersioned = context.Request.Path.StartsWithSegments(
            LegacyPrefix, StringComparison.OrdinalIgnoreCase, out var remainder);
        var canonicalBase = CanonicalPrefix;
        if (!matchesVersioned)
        {
            var matchesUnversioned = context.Request.Path.StartsWithSegments(
                "/api/pxa", StringComparison.OrdinalIgnoreCase, out remainder)
                && new[] { "/document", "/export", "/migration", "/pdf-viewer", "/spreadsheet", "/system", "/templates" }
                    .Any(prefix => remainder.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
            if (matchesUnversioned) canonicalBase = "/api/pvad";
            else
            {
                await next(context);
                return;
            }
        }
        var originalPath = context.Request.Path;
        var alreadyMarked = context.Items.ContainsKey(LegacyRequestMarker);
        var canonicalPath = new PathString(canonicalBase).Add(remainder);
        context.Items[LegacyRequestMarker] = true;
        context.Request.Path = canonicalPath;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["Deprecation"] = "true";
            context.Response.Headers.Append("Link",
                $"<{context.Request.PathBase.Add(canonicalPath).ToUriComponent()}>; rel=\"successor-version\"");
            return Task.CompletedTask;
        });

        try
        {
            await next(context);
        }
        finally
        {
            context.Request.Path = originalPath;
            if (!alreadyMarked)
                context.Items.Remove(LegacyRequestMarker);
        }
    }
}
