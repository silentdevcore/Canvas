using System.Diagnostics;

namespace PXA.WebApi.Observability;

public sealed class PxaOperationMetricsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var operation = ClassifyDocumentOperation(context.Request);
        var stopwatch = operation is null ? null : Stopwatch.StartNew();
        using var activity = operation is null
            ? null
            : PxaTelemetry.StartDocumentOperation(operation);
        try
        {
            await next(context);
        }
        catch
        {
            if (operation is not null)
            {
                PxaTelemetry.CompleteDocumentOperation(activity, "failed");
                PxaTelemetry.RecordDocumentOperation(operation, "failed", stopwatch!.Elapsed);
            }
            if (context.Request.Path.StartsWithSegments("/api"))
                PxaTelemetry.RecordApiFailure("server_error", ClassifySurface(context.Request.Path));
            throw;
        }

        if (operation is not null)
        {
            var outcome = context.Response.StatusCode switch
            {
                < 400 => "completed",
                < 500 => "rejected",
                _ => "failed",
            };
            PxaTelemetry.CompleteDocumentOperation(activity, outcome);
            PxaTelemetry.RecordDocumentOperation(operation, outcome, stopwatch!.Elapsed);
        }

        var failureType = context.Response.StatusCode switch
        {
            StatusCodes.Status401Unauthorized => "authentication",
            StatusCodes.Status403Forbidden => "authorization",
            StatusCodes.Status429TooManyRequests => "rate_limit",
            >= 400 and < 500 => "client_error",
            >= 500 => "server_error",
            _ => null,
        };
        if (failureType is not null && context.Request.Path.StartsWithSegments("/api"))
            PxaTelemetry.RecordApiFailure(failureType, ClassifySurface(context.Request.Path));
    }

    public static string? ClassifyDocumentOperation(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method))
            return null;

        var path = request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        if (path.Contains("migration", StringComparison.Ordinal))
            return "migration";
        if (path.Contains("import", StringComparison.Ordinal))
            return "import";
        if (path.Contains("export", StringComparison.Ordinal) ||
            path.Contains("convert-image-to-pdf", StringComparison.Ordinal))
            return "export";
        if (path.Contains("render", StringComparison.Ordinal) ||
            path.Contains("csharp-code-to-pdf", StringComparison.Ordinal))
            return "rendering";
        return null;
    }

    public static string ClassifySurface(PathString path)
    {
        if (path.StartsWithSegments("/api/pxa/v1/admin"))
            return "admin";
        if (path.StartsWithSegments("/api/pxa/v1/account") ||
            path.StartsWithSegments("/api/pxa/v1/auth"))
            return "account";
        if (path.StartsWithSegments("/api/pxa/v1/designer"))
            return "designer";
        if (path.StartsWithSegments("/api/document") ||
            path.StartsWithSegments("/api/export") ||
            path.StartsWithSegments("/api/migration") ||
            path.StartsWithSegments("/api/spreadsheet") ||
            path.StartsWithSegments("/api/templates") ||
            path.StartsWithSegments("/api/pxa/v1/document-jobs"))
            return "document";
        return "api";
    }
}
