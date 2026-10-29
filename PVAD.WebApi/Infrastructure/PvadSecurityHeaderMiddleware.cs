namespace PVAD.WebApi.Infrastructure;

/// <summary>Normalizes legacy security headers before authentication and CSRF validation.</summary>
public sealed class PvadSecurityHeaderMiddleware(RequestDelegate next)
{
    private static readonly string[] Names = ["Application", "CSRF", "API-Key"];

    public async Task InvokeAsync(HttpContext context)
    {
        foreach (var name in Names)
        {
            var canonical = $"X-PVAD-{name}";
            var legacy = $"X-PXA-{name}";
            var hasCurrent = context.Request.Headers.TryGetValue(canonical, out var current);
            var hasLegacy = context.Request.Headers.TryGetValue(legacy, out var previous);
            if (!hasCurrent && !hasLegacy) continue;
            var comparison = name == "Application" ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if ((hasCurrent && (current.Count != 1 || string.IsNullOrWhiteSpace(current[0])))
                || (hasLegacy && (previous.Count != 1 || string.IsNullOrWhiteSpace(previous[0])))
                || (hasCurrent && hasLegacy && !string.Equals(current[0], previous[0], comparison)))
            {
                await Reject(context, canonical, legacy);
                return;
            }
            var value = hasCurrent ? current[0]! : previous[0]!;
            if (value.Contains(',') || (name == "Application"
                && !new[] { "designer", "account", "admin" }.Contains(value, StringComparer.OrdinalIgnoreCase)))
            {
                await Reject(context, canonical, legacy);
                return;
            }
            context.Request.Headers[canonical] = value;
            context.Request.Headers.Remove(legacy);
        }
        await next(context);
    }

    private static async Task Reject(HttpContext context, string canonical, string legacy)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        var problem = PvadApiProblems.Create(context, StatusCodes.Status400BadRequest,
            "Invalid security headers", $"Provide one consistent value for {canonical} and its legacy alias {legacy}.",
            PvadApiProblems.InvalidRequest);
        await context.Response.WriteAsJsonAsync(problem, options: null,
            contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
