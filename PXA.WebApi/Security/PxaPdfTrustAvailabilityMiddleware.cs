using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PXA.WebApi.Services.Pdf;

namespace PXA.WebApi.Security;

public sealed class PxaPdfTrustAvailabilityMiddleware(
    RequestDelegate next,
    IOptions<PxaPdfTrustRuntimeOptions> options)
{
    private readonly PxaPdfTrustRuntimeOptions runtime = options.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        var unavailableFeature = ResolveUnavailableFeature(context.Request.Path);
        if (unavailableFeature is null)
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "PDF Trust feature unavailable",
            Detail = $"The {unavailableFeature} capability is disabled by the server trust configuration.",
            Extensions = { ["code"] = "PXAPDFTRUSTAPI901", ["feature"] = unavailableFeature },
        }, context.RequestAborted);
    }

    private string? ResolveUnavailableFeature(PathString path)
    {
        var value = path.Value ?? string.Empty;
        if (StartsWith(value, "/api/pxa/v1/pdf/signatures") && !runtime.SigningEnabled)
            return "signing";
        if (StartsWith(value, "/api/pxa/v1/pdf/redactions") && !runtime.RedactionEnabled)
            return "redaction";
        if (StartsWith(value, "/api/pxa/v1/pdf/compliance") && !runtime.ComplianceEnabled)
            return "compliance";
        return null;
    }

    private static bool StartsWith(string path, string prefix) =>
        path.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith($"{prefix}/", StringComparison.OrdinalIgnoreCase);
}
