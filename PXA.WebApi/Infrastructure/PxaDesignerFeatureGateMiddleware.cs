using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PXA.WebApi.Application.Designer;
using PXA.WebApi.Security;

namespace PXA.WebApi.Infrastructure;

public sealed class PxaDesignerFeatureGateMiddleware(RequestDelegate next)
{
    private static readonly (string Prefix, string FeatureId)[] ProtectedPrefixes =
    [
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
        if (!string.Equals(
                context.Request.Headers["X-PXA-Application"].ToString(),
                "designer",
                StringComparison.OrdinalIgnoreCase))
            return null;

        var value = context.Request.Path.Value ?? string.Empty;
        return ProtectedPrefixes.FirstOrDefault(item =>
                value.Equals(item.Prefix, StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith($"{item.Prefix}/", StringComparison.OrdinalIgnoreCase))
            .FeatureId;
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
