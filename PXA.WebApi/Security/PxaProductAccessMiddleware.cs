using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PXA.WebApi.Services.Entitlements;

namespace PXA.WebApi.Security;

public sealed class PxaProductAccessOptions
{
    public bool Enabled { get; set; }
}

public sealed class PxaProductAccessMiddleware
{
    private static readonly (string Prefix, string Capability)[] ProtectedPrefixes =
    [
        ("/api/migration", "migration"),
        ("/api/pxa/migration", "migration"),
        ("/api/spreadsheet", "spreadsheet"),
        ("/api/pxa/spreadsheet", "spreadsheet"),
        ("/api/export", "generator"),
        ("/api/pxa/export", "generator"),
        ("/api/templates", "generator"),
        ("/api/pxa/templates", "generator"),
        ("/api/document", "generator"),
        ("/api/pxa/document", "generator"),
        ("/api/pdf-viewer", "pdf-viewer"),
        ("/api/pxa/pdf-viewer", "pdf-viewer"),
    ];

    private readonly RequestDelegate next;
    private readonly bool enabled;

    public PxaProductAccessMiddleware(RequestDelegate next, IOptions<PxaProductAccessOptions> options)
    {
        this.next = next;
        enabled = options.Value.Enabled;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IPxaTenantContext tenantContext,
        IPxaEntitlementService entitlementService,
        IPxaUsageService usageService)
    {
        var capability = ResolveCapability(context.Request.Path);
        if (!enabled || capability is null)
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await WriteProblem(context, StatusCodes.Status401Unauthorized, "PXA_AUTHENTICATION_REQUIRED",
                "Authentication is required for this product API.");
            return;
        }
        if (tenantContext.OrganizationId is not { } organizationId)
        {
            await WriteProblem(context, StatusCodes.Status403Forbidden, "PXA_ORGANIZATION_REQUIRED",
                "An active organization is required for this product API.");
            return;
        }

        var apiDecision = await entitlementService.EvaluateAsync(organizationId, "api", 1, context.RequestAborted);
        if (!apiDecision.Allowed)
        {
            await WriteProblem(context, StatusCodes.Status403Forbidden, apiDecision.Code, apiDecision.Reason);
            return;
        }
        var productDecision = await entitlementService.EvaluateAsync(
            organizationId, capability, 1, context.RequestAborted);
        if (!productDecision.Allowed)
        {
            await WriteProblem(context, StatusCodes.Status403Forbidden, productDecision.Code, productDecision.Reason);
            return;
        }

        await next(context);
        if (context.Response.StatusCode < 400)
        {
            var requestId = context.Request.Headers["Idempotency-Key"].FirstOrDefault()
                ?? context.TraceIdentifier;
            await usageService.RecordAsync(
                organizationId,
                "api",
                $"{context.Request.Method} {context.Request.Path}",
                1,
                requestId,
                context.User.HasClaim(claim => claim.Type == PxaClaimTypes.ServiceAccount) ? "api-key" : "browser",
                context.RequestAborted);
        }
    }

    private static string? ResolveCapability(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return ProtectedPrefixes.FirstOrDefault(item =>
                value.Equals(item.Prefix, StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith($"{item.Prefix}/", StringComparison.OrdinalIgnoreCase))
            .Capability;
    }

    private static async Task WriteProblem(HttpContext context, int status, string code, string detail)
    {
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = code,
            Detail = detail,
            Extensions = { ["code"] = code },
        });
    }
}
