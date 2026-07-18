using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

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
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid CSRF token",
                Detail = "Request a fresh CSRF token before performing this administration action.",
            })
            {
                StatusCode = StatusCodes.Status400BadRequest,
            };
        }
    }
}
