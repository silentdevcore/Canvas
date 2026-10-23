using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PVAD.WebApi.Services.Entitlements;

namespace PVAD.WebApi.Security;

public sealed class PvadProductAccessOptions
{
    public bool Enabled { get; set; }
}

public sealed class PvadProductAccessMiddleware
{
    private static readonly (string Prefix, string Capability)[] ProtectedPrefixes =
    [
        ("/api/pvad/v1/pdf/signatures", PvadPdfTrustCapabilities.Signing),
        ("/api/pvad/v1/pdf/redactions", PvadPdfTrustCapabilities.Redaction),
        ("/api/pvad/v1/pdf/compliance", PvadPdfTrustCapabilities.Compliance),
        ("/api/pvad/v1/pdf/trust", PvadPdfTrustCapabilities.Inspection),
        ("/api/pvad/v1/pdf/e-invoices", "generator"),
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

    public PvadProductAccessMiddleware(RequestDelegate next, IOptions<PvadProductAccessOptions> options)
    {
        this.next = next;
        enabled = options.Value.Enabled;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IPvadTenantContext tenantContext,
        IPvadEntitlementService entitlementService,
        IPvadUsageService usageService)
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
                context.User.HasClaim(claim => claim.Type == PvadClaimTypes.ServiceAccount) ? "api-key" : "browser",
                context.RequestAborted);
        }
    }

    internal static string? ResolveCapability(PathString path)
    {
        var value = PvadAuthorizationPaths.Normalize(path).Value ?? string.Empty;
        return ProtectedPrefixes.FirstOrDefault(item =>
                value.Equals(item.Prefix, StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith($"{item.Prefix}/", StringComparison.OrdinalIgnoreCase))
            .Capability;
    }

    private static async Task WriteProblem(HttpContext context, int status, string code, string detail)
    {
        context.Response.StatusCode = status;
        var problem = PVAD.WebApi.Infrastructure.PvadApiProblems.Create(context, status, code, detail, code);
        await context.Response.WriteAsJsonAsync(problem, options: null,
            contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}

public static class PvadPdfTrustCapabilities
{
    public const string Inspection = "pdf-trust";
    public const string Signing = "pdf-signing";
    public const string Redaction = "pdf-redaction";
    public const string Compliance = "pdf-compliance";

    public static IReadOnlyList<string> All { get; } =
        [Inspection, Signing, Redaction, Compliance];
}
