using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PXA.WebApi.Infrastructure;

public sealed class PxaProblemDetailsResultFilter : IAsyncResultFilter
{
    public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        switch (context.Result)
        {
            case ObjectResult { Value: ProblemDetails problem } objectResult:
                PxaApiProblems.Complete(
                    context.HttpContext,
                    problem,
                    objectResult.StatusCode ?? problem.Status ?? StatusCodes.Status400BadRequest);
                break;
            case StatusCodeResult statusResult when statusResult.StatusCode >= 400:
                context.Result = ToObjectResult(context.HttpContext, statusResult.StatusCode);
                break;
            case ForbidResult:
                context.Result = ToObjectResult(context.HttpContext, StatusCodes.Status403Forbidden);
                break;
            case ChallengeResult:
                context.Result = ToObjectResult(context.HttpContext, StatusCodes.Status401Unauthorized);
                break;
        }
        return next();
    }

    private static ObjectResult ToObjectResult(HttpContext context, int status) =>
        new(PxaApiProblems.Create(context, status))
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
}
