using Microsoft.AspNetCore.Mvc;

namespace PXA.WebApi.Infrastructure;

public static class PxaApiProblems
{
    public const string AuthenticationRequired = "PXAAPI001";
    public const string PermissionDenied = "PXAAPI002";
    public const string OrganizationRequired = "PXAAPI003";
    public const string ResourceNotFound = "PXAAPI004";
    public const string InvalidRequest = "PXAAPI005";
    public const string Conflict = "PXAAPI006";
    public const string RateLimited = "PXAAPI007";
    public const string InvalidCsrf = "PXAAPI008";
    public const string AccountLocked = "PXAAPI009";
    public const string VerificationRequired = "PXAAPI010";
    public const string TrialAlreadyClaimed = "PXAAPI011";
    public const string OrganizationSlugUnavailable = "PXAAPI012";
    public const string LastOwnerProtected = "PXAAPI013";
    public const string ClosureConflict = "PXAAPI014";

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
            Type = $"https://docs.powerdoxautomation.com/problems/{code.ToLowerInvariant()}",
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;
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
        problem.Type ??= $"https://docs.powerdoxautomation.com/problems/{code.ToLowerInvariant()}";
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
        _ => "Invalid request",
    };
}
