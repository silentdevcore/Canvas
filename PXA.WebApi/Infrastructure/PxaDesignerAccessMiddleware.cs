using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Entitlements;

namespace PXA.WebApi.Infrastructure;

public sealed class PxaDesignerAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IPxaEntitlementService entitlementService,
        PxaDbContext dbContext,
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
                "Sign in through PXA Account before using Designer operations.",
                PxaApiProblems.AuthenticationRequired);
            return;
        }

        var isOrganizationSwitch = IsDesignerOrganizationSwitch(context.Request.Path);
        var organizationId = Guid.Empty;
        if (!isOrganizationSwitch &&
            !Guid.TryParse(
                context.User.FindFirstValue(PxaClaimTypes.ActiveOrganization),
                out organizationId))
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status403Forbidden,
                "Organization context required",
                "Select an active organization before using PXA Designer.",
                PxaApiProblems.OrganizationRequired);
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
                    PxaApiProblems.InvalidCsrf);
                return;
            }
        }

        await next(context);
    }

    private static bool IsDesignerRequest(HttpContext context) =>
        string.Equals(
            context.Request.Headers["X-PXA-Application"].ToString(),
            "designer",
            StringComparison.OrdinalIgnoreCase) &&
        context.Request.Path.StartsWithSegments("/api");

    private static bool IsPublicDesignerRequest(PathString path) =>
        path.Equals("/api/pxa/v1/auth/csrf") ||
        path.Equals("/api/pxa/v1/auth/designer-handoff/exchange") ||
        path.Equals("/api/pxa/v1/auth/logout") ||
        path.Equals("/api/pxa/v1/telemetry/browser");

    private static bool IsDesignerOrganizationSwitch(PathString path) =>
        path.Equals("/api/pxa/v1/auth/switch-organization");

    private static async Task WriteProblemAsync(
        HttpContext context,
        int status,
        string title,
        string detail,
        string code)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(
            PxaApiProblems.Create(context, status, title, detail, code),
            cancellationToken: context.RequestAborted);
    }
}
