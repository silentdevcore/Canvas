using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PXA.WebApi.Application.Designer;
using PXA.WebApi.Security;

namespace PXA.WebApi.Infrastructure;

public sealed class PxaDesignerFeatureGateMiddleware(RequestDelegate next)
{
    private static readonly (string Prefix, string FeatureId)[] ProtectedPrefixes =
    [
        ("/api/templates/csharp-code-to-pdf", "designer.code-workspace"),
        ("/api/templates/csharp-code-to-json", "designer.code-workspace"),
        ("/api/templates/csharp-to-json", "designer.code-workspace"),
        ("/api/pxa/templates/csharp-code-to-pdf", "designer.code-workspace"),
        ("/api/pxa/templates/csharp-code-to-json", "designer.code-workspace"),
        ("/api/pxa/templates/csharp-to-json", "designer.code-workspace"),
        ("/api/pxa/v1/designer/templates", "designer.code-workspace"),
        ("/api/document/import-pdf-engine", "designer.pdf-chart-recognition"),
        ("/api/pxa/document/import-pdf-engine", "designer.pdf-chart-recognition"),
        ("/api/pdf-viewer", "designer.pdf-viewer"),
        ("/api/pxa/pdf-viewer", "designer.pdf-viewer"),
        ("/api/spreadsheet", "designer.spreadsheet"),
        ("/api/pxa/spreadsheet", "designer.spreadsheet"),
    ];

    public async Task InvokeAsync(
        HttpContext context,
        IPxaTenantContext tenantContext,
        IPxaDesignerFeatureGate featureGate)
    {
        var featureId = ResolveFeature(context);
        if (featureId is null)
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
        var value = context.Request.Path.Value ?? string.Empty;
        var match = ProtectedPrefixes.FirstOrDefault(item =>
            value.Equals(item.Prefix, StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith($"{item.Prefix}/", StringComparison.OrdinalIgnoreCase));
        if (match.FeatureId is "designer.pdf-chart-recognition" or "designer.code-workspace")
        {
            if (match.FeatureId == "designer.code-workspace" &&
                value.StartsWith("/api/pxa/v1/designer/templates", StringComparison.OrdinalIgnoreCase) &&
                !value.Contains("/code-workspace", StringComparison.OrdinalIgnoreCase))
                return null;
            return match.FeatureId;
        }

        if (!string.Equals(
                context.Request.Headers["X-PXA-Application"].ToString(),
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
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = "Designer feature unavailable",
            Detail = detail,
            Extensions = { ["code"] = code },
        }, cancellationToken: context.RequestAborted);
    }
}
