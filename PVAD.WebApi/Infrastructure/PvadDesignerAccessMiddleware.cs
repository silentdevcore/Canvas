using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Entitlements;

namespace PVAD.WebApi.Infrastructure;

public sealed class PvadDesignerAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IPvadEntitlementService entitlementService,
        PvadDbContext dbContext,
        IAntiforgery antiforgery)
    {
        if (!IsDesignerRequest(context) || IsPublicDesignerRequest(context.Request.Path))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "Authentication required",
                "Sign in through PVADE Account before using Designer operations.",
                PvadApiProblems.AuthenticationRequired);
            return;
        }

        var isOrganizationSwitch = IsDesignerOrganizationSwitch(context.Request.Path);
        if (!isOrganizationSwitch && !context.User.Claims.Any(claim =>
                claim.Type == PvadClaimTypes.Permission &&
                claim.Value.StartsWith("designer.", StringComparison.Ordinal)))
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "Designer session refresh required",
                "Sign in through PVADE Account again to refresh Designer permissions.",
                "PXA_DESIGNER_SESSION_EXPIRED");
            return;
        }

        var organizationId = Guid.Empty;
        if (!isOrganizationSwitch &&
            !Guid.TryParse(
                context.User.FindFirstValue(PvadClaimTypes.ActiveOrganization),
                out organizationId))
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status403Forbidden,
                "Organization context required",
                "Select an active organization before using PVADE Designer.",
                PvadApiProblems.OrganizationRequired);
            return;
        }

        if (!isOrganizationSwitch)
        {
            var entitlement = await entitlementService.EvaluateAsync(
                organizationId,
                "designer",
                cancellationToken: context.RequestAborted);
            if (!entitlement.Allowed)
            {
                dbContext.AuditEvents.Add(new AuditEvent
                {
                    OrganizationId = organizationId,
                    ActorUserId = Guid.TryParse(
                        context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                        ? userId
                        : null,
                    Action = "security.designer-entitlement.denied",
                    TargetType = "entitlement",
                    TargetId = $"{organizationId}:designer",
                    Outcome = "rejected",
                    DetailsJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Capability = "designer",
                        entitlement.Code,
                    }),
                });
                await dbContext.SaveChangesAsync(context.RequestAborted);
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "Designer access denied",
                    entitlement.Reason,
                    entitlement.Code);
                return;
            }

            var requiredPermission = PvadDesignerPermissionResolver.Resolve(
                context.Request.Path,
                context.Request.Method);
            if (!HasPermission(context.User, requiredPermission) &&
                !HasCanonicalMigrationPermission(context))
            {
                dbContext.AuditEvents.Add(new AuditEvent
                {
                    OrganizationId = organizationId,
                    ActorUserId = Guid.TryParse(
                        context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                        ? userId
                        : null,
                    Action = "security.designer-permission.denied",
                    TargetType = "permission",
                    TargetId = requiredPermission,
                    Outcome = "rejected",
                    DetailsJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Permission = requiredPermission,
                        Method = context.Request.Method,
                        Path = context.Request.Path.Value,
                    }),
                });
                await dbContext.SaveChangesAsync(context.RequestAborted);
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "Designer permission required",
                    "Your organization role does not allow this Designer operation.",
                    "PXA_DESIGNER_PERMISSION_REQUIRED",
                    requiredPermission);
                return;
            }
        }

        if (!HttpMethods.IsGet(context.Request.Method) &&
            !HttpMethods.IsHead(context.Request.Method) &&
            !HttpMethods.IsOptions(context.Request.Method))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status400BadRequest,
                    "Invalid CSRF token",
                    "Request a fresh CSRF token before changing Designer data.",
                    PvadApiProblems.InvalidCsrf);
                return;
            }
        }

        await next(context);
    }

    private static bool IsDesignerRequest(HttpContext context) =>
        string.Equals(
            context.Request.Headers["X-PVAD-Application"].ToString(),
            "designer",
            StringComparison.OrdinalIgnoreCase) &&
        context.Request.Path.StartsWithSegments("/api");

    private static bool IsPublicDesignerRequest(PathString path) =>
        path.Equals("/api/pvad/v1/auth/csrf") ||
        path.Equals("/api/pvad/v1/auth/designer-handoff/exchange") ||
        path.Equals("/api/pvad/v1/auth/logout") ||
        path.Equals("/api/pvad/v1/telemetry/browser");

    private static bool IsDesignerOrganizationSwitch(PathString path) =>
        path.Equals("/api/pvad/v1/auth/switch-organization");

    private static bool HasCanonicalMigrationPermission(HttpContext context)
    {
        var segments = (context.Request.Path.Value ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 5 ||
            !string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(segments[1], "pxa", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(segments[2], "v1", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(segments[3], "migrations", StringComparison.OrdinalIgnoreCase))
            return false;

        if (segments.Length == 5 &&
            string.Equals(segments[4], "catalog", StringComparison.OrdinalIgnoreCase))
        {
            var domain = context.Request.Query["domain"].ToString();
            var kind = context.Request.Query["kind"].ToString();
            return !string.IsNullOrWhiteSpace(domain) &&
                   !string.IsNullOrWhiteSpace(kind) &&
                   HasPermission(context.User, PvadDesignerMigrationPermissions.KindPermission(domain, kind, "read"));
        }

        if (segments.Length < 6) return false;

        var action = HttpMethods.IsGet(context.Request.Method) ||
                     HttpMethods.IsHead(context.Request.Method) ||
                     HttpMethods.IsOptions(context.Request.Method)
            ? "read"
            : "execute";
        return HasPermission(
            context.User,
            PvadDesignerMigrationPermissions.KindPermission(segments[4], segments[5], action));
    }

    private static bool HasPermission(ClaimsPrincipal user, string permission) =>
        user.HasClaim(PvadClaimTypes.Permission, permission);

    private static async Task WriteProblemAsync(
        HttpContext context,
        int status,
        string title,
        string detail,
        string code,
        string? requiredPermission = null)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var problem = PvadApiProblems.Create(context, status, title, detail, code);
        if (requiredPermission is not null)
            problem.Extensions["requiredPermission"] = requiredPermission;
        await context.Response.WriteAsJsonAsync(problem, cancellationToken: context.RequestAborted);
    }
}
