namespace PVAD.WebApi.Infrastructure;

/// <summary>Rejects ambiguous security headers before authentication and CSRF validation.</summary>
public sealed class PvadSecurityHeaderMiddleware(RequestDelegate next)
{
    private static readonly string[] Names = ["Application", "CSRF", "API-Key"];

    public async Task InvokeAsync(HttpContext context)
    {
        foreach (var name in Names)
        {
            var header = $"X-PVAD-{name}";
            if (!context.Request.Headers.TryGetValue(header, out var values)) continue;
            if (values.Count != 1 || string.IsNullOrWhiteSpace(values[0]) || values[0]!.Contains(',') ||
                (name == "Application" &&
                 !new[] { "designer", "account", "admin" }.Contains(values[0], StringComparer.OrdinalIgnoreCase)))
            {
                await Reject(context, header);
                return;
            }
        }
        await next(context);
    }

    private static async Task Reject(HttpContext context, string header)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        var problem = PvadApiProblems.Create(context, StatusCodes.Status400BadRequest,
            "Invalid security headers", $"Provide exactly one valid value for {header}.",
            PvadApiProblems.InvalidRequest);
        await context.Response.WriteAsJsonAsync(problem, options: null,
            contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
