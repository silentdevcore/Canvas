using Microsoft.AspNetCore.Mvc;

namespace PVAD.WebApi.Infrastructure;

public static class PvadApiProblems
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
    public const string AccountDisabled = "PXAAPI015";
    public const string OrganizationSuspended = "PXAAPI016";
    public const string PolicyAcceptanceRequired = "PXAAPI017";
    public const string UnexpectedError = "PXAAPI018";

    public static string? CanonicalCode(string code) =>
        PVAD.Core.Contracts.PvadDiagnosticAliases.CanonicalCode(code);

    public static (string Type, string CanonicalType, string LegacyType) TypeIdentity(
        HttpContext context, string code)
    {
        var canonical = "https://docs.pvade.com/problems/" +
            Uri.EscapeDataString((CanonicalCode(code) ?? code).ToLowerInvariant());
        var legacy = "https://docs.powerdoxautomation.com/problems/" +
            Uri.EscapeDataString(code.ToLowerInvariant());
        return (PvadLegacyApiMiddleware.IsLegacyRequest(context) ? legacy : canonical, canonical, legacy);
    }

    public static (string Type, string CanonicalType, string LegacyType) TemplateConflictTypeIdentity(
        HttpContext context)
    {
        const string canonical = "https://docs.pvade.com/problems/designer-template-conflict";
        const string legacy = "https://powerdoxautomation.com/problems/designer-template-conflict";
        return (PvadLegacyApiMiddleware.IsLegacyRequest(context) ? legacy : canonical, canonical, legacy);
    }

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
        var identity = TypeIdentity(context, code);
        problem.Type ??= identity.Type;
        if (problem.Type == identity.CanonicalType || problem.Type == identity.LegacyType)
        {
            problem.Extensions["canonicalType"] = identity.CanonicalType;
            problem.Extensions["legacyType"] = identity.LegacyType;
        }
        else
        {
            problem.Extensions.Remove("canonicalType");
            problem.Extensions.Remove("legacyType");
        }
        problem.Extensions["code"] = code;
        if (CanonicalCode(code) is { } canonicalCode)
            problem.Extensions["canonicalCode"] = canonicalCode;
        else
            problem.Extensions.Remove("canonicalCode");
        problem.Extensions.TryAdd("traceId", context.TraceIdentifier);
        CompleteTitle(problem);
    }

    private static void CompleteTitle(ProblemDetails problem)
    {
        if (problem.Title is { } title && CanonicalCode(title) is { } canonicalTitle)
            problem.Extensions["canonicalTitle"] = canonicalTitle;
        else
            problem.Extensions.Remove("canonicalTitle");
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
