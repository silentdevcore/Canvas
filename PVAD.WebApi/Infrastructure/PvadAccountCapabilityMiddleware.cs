using Microsoft.AspNetCore.Mvc;
using PVAD.Infrastructure.Persistence;
using PVAD.Licensing;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Licensing;

namespace PVAD.WebApi.Infrastructure;

/// <summary>Account policy applies to product APIs regardless of the caller's UI/header or role.</summary>
public sealed class PvadAccountCapabilityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, PvadDbContext db, IPvadTenantContext tenant)
    {
        if (!IsProductPath(context.Request.Path))
        {
            await next(context);
            return;
        }
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await Denied(context, StatusCodes.Status401Unauthorized);
            return;
        }
        var policy = tenant.OrganizationId is { } organizationId
            ? await PvadAccountPolicyService.ReadAsync(db, organizationId, context.RequestAborted, requireActive: true) : null;
        if (policy is null)
        {
            await Denied(context);
            return;
        }
        using var scope = PvadAccountCapabilityContext.Enter(policy.Kind, policy.ApprovedMigrations);
        var capability = ResolveCapability(context.Request.Path, context.Request.Method);
        if (capability is not null && !PvadAccountCapabilityContext.IsAllowed(capability))
        {
            await Denied(context);
            return;
        }
        // All format-specific native admissions additionally intersect this scope.
        await next(context);
    }

    internal static bool IsProductPath(PathString path) => new[]
    {
        "/api/document", "/api/pvad/document", "/api/export", "/api/pvad/export",
        "/api/spreadsheet", "/api/pvad/spreadsheet", "/api/templates", "/api/pvad/templates",
        "/api/pdf-viewer", "/api/pvad/pdf-viewer", "/api/pvad/v1/pdf",
        "/api/migration", "/api/pvad/migration", "/api/pvad/v1/migrations",
        "/api/pvad/v1/designer", "/api/pvad/v1/jobs", "/api/pvad/v1/document-jobs", "/api/pvad/v1/mcp",
    }.Any(prefix => path.StartsWithSegments(prefix));

    internal static string? ResolveCapability(PathString path, string method)
    {
        var value = path.Value?.ToLowerInvariant() ?? string.Empty;
        if (HttpMethods.IsPost(method))
        {
            if (path.StartsWithSegments("/api/pvad/v1/migrations"))
            {
                var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 6) return $"migration.{segments[4]}.{segments[5]}";
            }
            if (path.StartsWithSegments("/api/migration") || path.StartsWithSegments("/api/pvad/migration"))
                return "migration.pdf.code";
        }
        if (value.EndsWith("/convert-image-to-pdf")) return "image.ocr";
        if (value.EndsWith("/import-image-analysis")) return "image.analysis";
        if (value.EndsWith("/debug-pdf-engine")) return "import.pdf";
        var marker = value.LastIndexOf("/import-", StringComparison.Ordinal);
        if (marker >= 0)
        {
            var format = value[(marker + 8)..];
            return "import." + (format switch { "pdf-engine" => "pdf", "markdown" => "md", _ => format });
        }
        var permission = PvadDesignerPermissionResolver.Resolve(path, method);
        return permission switch
        {
            PvadDesignerPermissions.PdfSignaturesSign or PvadDesignerPermissions.PdfSignatureKeyProvidersUse or
                PvadDesignerPermissions.PdfSignatureKeyProvidersManage => "pdf.signature.sign",
            PvadDesignerPermissions.PdfSignatureTrustPoliciesManage => "pdf.trust.inspect",
            PvadDesignerPermissions.PdfSignaturesValidate => "pdf.signature.validate",
            PvadDesignerPermissions.PdfRedactionsApply or PvadDesignerPermissions.PdfViewerRedact => "pdf.redaction.apply",
            PvadDesignerPermissions.PdfRedactionsInspect => "pdf.redaction.inspect",
            PvadDesignerPermissions.PdfComplianceConvert => "pdf.compliance.convert",
            PvadDesignerPermissions.PdfComplianceValidate => "pdf.compliance.validate",
            PvadDesignerPermissions.PdfTrustInspect => "pdf.trust.inspect",
            PvadDesignerPermissions.PdfEInvoicesCreate or PvadDesignerPermissions.PdfEInvoicesGenerate => "pdf.e-invoice.create",
            PvadDesignerPermissions.PdfEInvoicesValidate => "pdf.e-invoice.validate",
            PvadDesignerPermissions.PdfEInvoicesAttach => "pdf.e-invoice.attach",
            PvadDesignerPermissions.PdfEInvoicesRead => "pdf.e-invoice.read",
            _ => null,
        };
    }

    private static Task Denied(HttpContext context, int status = StatusCodes.Status403Forbidden)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(PvadApiProblems.Create(context, status,
            status == 401 ? "Authentication required" : "Account capability denied",
            status == 401 ? "Sign in before using product processing APIs." : "This operation is not included in this account's rights.",
            status == 401 ? "PVAD_AUTHENTICATION_REQUIRED" : "PVAD_ACCOUNT_CAPABILITY_DENIED"), options: null, contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    }
}
