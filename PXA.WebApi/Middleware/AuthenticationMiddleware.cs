using PXA.Application.UseCases;

namespace PXA.WebApi.Middleware;

public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public AuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower();

        // Skip authentication for login, logout, and some public endpoints
        if (path?.StartsWith("/api/auth") == true ||
            path?.StartsWith("/swagger") == true ||
            path?.StartsWith("/health") == true ||
            path?.StartsWith("/api/templates/render-design") == true ||
            path?.StartsWith("/api/templates/csharp-to-json") == true ||
            path?.StartsWith("/api/templates/csharp-code-to-pdf") == true)
        {
            await _next(context);
            return;
        }

        // Get the authentication use case from the request services
        var authUseCase = context.RequestServices.GetRequiredService<AuthenticateUserUseCase>();

        // Check for Authorization header
        if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Authorization header is required" });
            return;
        }

        var token = authHeader.ToString().Replace("Bearer ", "");
        if (string.IsNullOrEmpty(token))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Token is required" });
            return;
        }

        // Validate token
        if (!await authUseCase.ValidateTokenAsync(token))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or expired token" });
            return;
        }

        // Get user ID and add to context for use in controllers
        var userId = await authUseCase.GetUserIdFromTokenAsync(token);
        if (userId != null)
        {
            context.Items["UserId"] = userId;
        }

        await _next(context);
    }
}

public static class AuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseAuthenticationMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuthenticationMiddleware>();
    }
}
