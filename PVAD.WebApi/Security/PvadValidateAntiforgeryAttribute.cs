using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PVAD.WebApi.Infrastructure;

namespace PVAD.WebApi.Security;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class PvadValidateAntiforgeryAttribute : Attribute, IAsyncAuthorizationFilter
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
            context.Result = new ObjectResult(PvadApiProblems.Create(
                context.HttpContext,
                StatusCodes.Status400BadRequest,
                "Invalid CSRF token",
                "Request a fresh CSRF token before performing this administration action.",
                PvadApiProblems.InvalidCsrf))
            {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentTypes = { "application/problem+json" },
            };
        }
    }
}
