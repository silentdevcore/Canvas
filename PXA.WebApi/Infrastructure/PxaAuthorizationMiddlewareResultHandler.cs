using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace PXA.WebApi.Infrastructure;

public sealed class PxaAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler fallback = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await fallback.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var status = authorizeResult.Challenged
            ? StatusCodes.Status401Unauthorized
            : StatusCodes.Status403Forbidden;
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var problem = PxaApiProblems.Create(context, status);
        await context.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    }
}
