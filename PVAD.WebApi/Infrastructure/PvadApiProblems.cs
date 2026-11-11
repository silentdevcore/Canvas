using Microsoft.AspNetCore.Mvc;

namespace PVAD.WebApi.Infrastructure;

public static class PvadApiProblems
{
    public const string AuthenticationRequired = "PVADAPI001";
    public const string PermissionDenied = "PVADAPI002";
    public const string OrganizationRequired = "PVADAPI003";
    public const string ResourceNotFound = "PVADAPI004";
    public const string InvalidRequest = "PVADAPI005";
    public const string Conflict = "PVADAPI006";
    public const string RateLimited = "PVADAPI007";
    public const string InvalidCsrf = "PVADAPI008";
    public const string AccountLocked = "PVADAPI009";
    public const string VerificationRequired = "PVADAPI010";
    public const string TrialAlreadyClaimed = "PVADAPI011";
    public const string OrganizationSlugUnavailable = "PVADAPI012";
    public const string LastOwnerProtected = "PVADAPI013";
    public const string ClosureConflict = "PVADAPI014";
    public const string AccountDisabled = "PVADAPI015";
    public const string OrganizationSuspended = "PVADAPI016";
    public const string PolicyAcceptanceRequired = "PVADAPI017";
    public const string UnexpectedError = "PVADAPI018";

    public static string TypeFor(string code) =>
        "https://docs.pvade.com/problems/" + Uri.EscapeDataString(code.ToLowerInvariant());

    public const string TemplateConflictType = "https://docs.pvade.com/problems/designer-template-conflict";

    public static ProblemDetails Create(
        HttpContext context,
        int status,
        string? title = null,
        string? detail = null,
        string? code = null)
    {
        code ??= ResolveCode(status, title);
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title ?? ResolveTitle(status),
            Detail = detail,
            Instance = context.Request.Path,
        };
        problem.Extensions["code"] = code;
        Complete(context, problem, status);
        return problem;
    }

    public static void Complete(HttpContext context, ProblemDetails problem, int fallbackStatus)
    {
        var status = problem.Status ?? fallbackStatus;
        var code = problem.Extensions.TryGetValue("code", out var existing) && existing is string value
            ? value
            : ResolveCode(status, problem.Title);
        problem.Status = status;
        problem.Title ??= ResolveTitle(status);
        problem.Instance ??= context.Request.Path;
        problem.Type ??= TypeFor(code);
        problem.Extensions["code"] = code;
        problem.Extensions.TryAdd("traceId", context.TraceIdentifier);
    }

    public static string ResolveCode(int status, string? title = null)
    {
        if (title?.Contains("CSRF", StringComparison.OrdinalIgnoreCase) == true)
            return InvalidCsrf;
        if (title?.Contains("Organization context", StringComparison.OrdinalIgnoreCase) == true)
            return OrganizationRequired;
        if (title?.Contains("Email verification", StringComparison.OrdinalIgnoreCase) == true)
            return VerificationRequired;
        return status switch
        {
            StatusCodes.Status401Unauthorized => AuthenticationRequired,
            StatusCodes.Status403Forbidden => PermissionDenied,
            StatusCodes.Status404NotFound => ResourceNotFound,
            StatusCodes.Status409Conflict => Conflict,
            StatusCodes.Status423Locked => AccountLocked,
            StatusCodes.Status429TooManyRequests => RateLimited,
            StatusCodes.Status500InternalServerError => UnexpectedError,
            _ => InvalidRequest,
        };
    }

    private static string ResolveTitle(int status) => status switch
    {
        StatusCodes.Status401Unauthorized => "Authentication required",
        StatusCodes.Status403Forbidden => "Permission denied",
        StatusCodes.Status404NotFound => "Resource not found",
        StatusCodes.Status409Conflict => "Request conflict",
        StatusCodes.Status429TooManyRequests => "Too many requests",
        StatusCodes.Status500InternalServerError => "Unexpected server error",
        _ => "Invalid request",
    };
}
