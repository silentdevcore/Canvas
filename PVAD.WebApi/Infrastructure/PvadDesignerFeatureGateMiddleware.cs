using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PVAD.WebApi.Application.Designer;
using PVAD.WebApi.Security;

namespace PVAD.WebApi.Infrastructure;

public sealed class PvadDesignerFeatureGateMiddleware(RequestDelegate next)
{
    private static readonly (string Prefix, string FeatureId)[] ProtectedPrefixes =
    [
        ("/api/templates/csharp-code-to-pdf", "designer.code-workspace"),
        ("/api/templates/csharp-code-to-json", "designer.code-workspace"),
        ("/api/templates/csharp-to-json", "designer.code-workspace"),
        ("/api/pxa/templates/csharp-code-to-pdf", "designer.code-workspace"),
        ("/api/pxa/templates/csharp-code-to-json", "designer.code-workspace"),
        ("/api/pxa/templates/csharp-to-json", "designer.code-workspace"),
        ("/api/pvad/v1/designer/templates", "designer.code-workspace"),
        ("/api/document/import-pdf-engine", "designer.pdf-chart-recognition"),
        ("/api/pxa/document/import-pdf-engine", "designer.pdf-chart-recognition"),
        ("/api/pvad/v1/migrations/pdf/viewer", "designer.nutrient-viewer-migration"),
        ("/api/pdf-viewer", "designer.pdf-viewer"),
        ("/api/pxa/pdf-viewer", "designer.pdf-viewer"),
        ("/api/spreadsheet", "designer.spreadsheet"),
        ("/api/pxa/spreadsheet", "designer.spreadsheet"),
    ];

    public async Task InvokeAsync(
        HttpContext context,
        IPvadTenantContext tenantContext,
        IPvadDesignerFeatureGate featureGate)
    {
        var featureId = ResolveFeature(context);
        if (featureId is null)
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var userId = tenantContext.UserId ??
            (Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
                ? parsedUserId
                : Guid.Empty);
        if (tenantContext.OrganizationId is not { } organizationId || userId == Guid.Empty)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status403Forbidden,
                "PXA_DESIGNER_FEATURE_CONTEXT_REQUIRED",
                "An active organization and user are required to evaluate Designer features.");
            return;
        }

        var decision = await featureGate.EvaluateAsync(
            organizationId,
            userId,
            featureId,
            context.RequestAborted);
        if (!decision.Enabled)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status403Forbidden,
                decision.Code,
                decision.Reason);
            return;
        }

        await next(context);
    }

    private static string? ResolveFeature(HttpContext context)
    {
        var value = PvadAuthorizationPaths.Normalize(context.Request.Path).Value ?? string.Empty;
        var match = ProtectedPrefixes.FirstOrDefault(item =>
            value.Equals(item.Prefix, StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith($"{item.Prefix}/", StringComparison.OrdinalIgnoreCase));
        if (match.FeatureId is "designer.pdf-chart-recognition" or "designer.code-workspace" or "designer.nutrient-viewer-migration")
        {
            if (match.FeatureId == "designer.code-workspace" &&
                value.StartsWith("/api/pvad/v1/designer/templates", StringComparison.OrdinalIgnoreCase) &&
                !value.Contains("/code-workspace", StringComparison.OrdinalIgnoreCase))
                return null;
            return match.FeatureId;
        }

        if (!string.Equals(
                context.Request.Headers["X-PVAD-Application"].ToString(),
                "designer",
                StringComparison.OrdinalIgnoreCase))
            return null;

        return match.FeatureId;
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int status,
        string code,
        string detail)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var problem = PvadApiProblems.Create(context, status, "Designer feature unavailable", detail, code);
        await context.Response.WriteAsJsonAsync(problem, options: null,
            contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
