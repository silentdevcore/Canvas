using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PXA.WebApi.Infrastructure;

namespace PXA.WebApi.Security;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class PxaValidateAntiforgeryAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new ObjectResult(PxaApiProblems.Create(
                context.HttpContext,
                StatusCodes.Status400BadRequest,
                "Invalid CSRF token",
                "Request a fresh CSRF token before performing this administration action.",
                PxaApiProblems.InvalidCsrf))
            {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentTypes = { "application/problem+json" },
            };
        }
    }
}
