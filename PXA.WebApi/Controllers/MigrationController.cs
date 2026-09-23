using PXA.WebApi.Services;
using Microsoft.AspNetCore.Mvc;
using PXA.WebApi.Security;
using PdfTrustMigrationReport = PXA.Migration.Abstractions.PdfTrustMigrationReport;

namespace PXA.WebApi.Controllers;

[ApiController]
[Route("api/migration")]
[Route("api/pxa/migration")]
public class MigrationController : ControllerBase
{
    private readonly MigrationService _service;
    private readonly ReportMigrationService _reportService;

    public MigrationController(MigrationService service, ReportMigrationService? reportService = null)
    {
        _service = service;
        _reportService = reportService ?? new ReportMigrationService();
    }

    /// <summary>Returns metadata for all supported migration frameworks.</summary>
    [HttpGet("frameworks")]
    [ProducesResponseType(typeof(IEnumerable<object>), 200)]
    public IActionResult GetFrameworks()
    {
        AddCompatibilityHeaders("/api/pxa/v1/migrations/catalog");
        var frameworks = _service.GetFrameworks()
            .Where(CanReadFramework)
            .Select(f => new
        {
            id = f.Id,
            name = f.Name,
            status = f.Status,
            description = f.Description,
            kind = f.Kind,
            domain = f.Domain,
            migrationKind = f.MigrationKind,
            provider = f.Provider,
            requiredPermission = string.Equals(f.MigrationKind, "viewer", StringComparison.OrdinalIgnoreCase)
                ? PxaDesignerMigrationPermissions.ResolveViewerProvider(f.Id)
                : PxaDesignerMigrationPermissions.ResolveCodeProvider(f.Id, f.Domain)
        });
        return Ok(frameworks);
    }

    /// <summary>Converts source code from a third-party PDF framework to PXA.Pdf C# code.</summary>
    [HttpPost("convert")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public IActionResult Convert([FromBody] MigrationRequest request)
    {
        AddCompatibilityHeaders("/api/pxa/v1/migrations/{domain}/code/{provider}/convert");
        if (string.IsNullOrWhiteSpace(request.Framework))
            return BadRequest(new { error = "Field 'framework' is required." });
        if (string.IsNullOrWhiteSpace(request.SourceCode))
            return BadRequest(new { error = "Field 'sourceCode' is required." });
        if (AuthorizeCodeMigration(request.Framework) is { } permissionError)
            return permissionError;

        try
        {
            var result = _service.Convert(request.Framework, request.SourceCode);
            return Ok(new
            {
                pxaCode = result.PxaCode,
                summary = new
                {
                    convertedCount = result.Summary.ConvertedCount,
                    warningCount = result.Summary.WarningCount,
                    errorCount = result.Summary.ErrorCount,
                    totalDiagnostics = result.Summary.TotalDiagnostics
                },
                diagnostics = result.Diagnostics.Select(d => new
                {
                    code = d.Code,
                    severity = d.Severity,
                    message = d.Message
                }),
                pdfTrust = CreatePdfTrustResponse(result.PdfTrust)
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static object? CreatePdfTrustResponse(PdfTrustMigrationReport? report)
    {
        if (report is null)
            return null;

        return new
        {
            providerKey = report.ProviderKey,
            migrationKind = report.MigrationKind,
            overallDisposition = report.OverallDisposition.ToString(),
            evidence = report.Evidence.Select(item => new
            {
                capability = item.Capability.ToString(),
                disposition = item.Disposition.ToString(),
                sourceOperation = item.SourceOperation,
                targetOperation = item.TargetOperation,
                diagnosticId = item.DiagnosticId,
                documentationPath = item.DocumentationPath,
                providerOptions = item.ProviderOptions
            }),
            manualActions = report.ManualActions.Select(action => new
            {
                actionId = action.ActionId,
                capability = action.Capability.ToString(),
                disposition = action.Disposition.ToString(),
                sourceOperation = action.SourceOperation,
                targetOperation = action.TargetOperation,
                documentationPath = action.DocumentationPath
            })
        };
    }

    /// <summary>
    /// Converts a report-designer layout into a PXA design (DesignExportDto) the visual designer can
    /// open. Auto-detects the format: an RDL/RDLC report (SSRS, Syncfusion — root <c>&lt;Report&gt;</c> in
    /// an RDL namespace), an ActiveReports <c>.rpx</c> section report (root <c>&lt;Report&gt;</c> with
    /// <c>&lt;Sections&gt;</c>), otherwise a DevExpress XtraReport (a C# Report Designer class or a
    /// serialized <c>.repx</c> XML layout). Returns the design plus migration diagnostics.
    /// </summary>
    [HttpPost("report-to-design")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public IActionResult ReportToDesign([FromBody] ReportToDesignRequest request)
    {
        AddCompatibilityHeaders("/api/pxa/v1/migrations/report/designer/auto/convert");
        try
        {
            var detectedProvider = string.IsNullOrWhiteSpace(request.SourceCode)
                ? "auto"
                : ReportMigrationService.DetectProvider(request.SourceCode);
            if (AuthorizeReportMigration(detectedProvider) is { } permissionError)
                return permissionError;

            var result = _reportService.Convert(new ReportMigrationInput(
                "auto",
                request.SourceCode,
                request.Resources,
                request.ResourceXml,
                request.SourceBase64));

            return Ok(new
            {
                design = result.Design,
                diagnostics = result.Diagnostics.Select(d => new
                {
                    code = d.Id,
                    severity = d.Severity.ToString(),
                    message = d.Message
                })
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Converts source code and returns a rendered preview as binary — a PDF for PDF-code
    /// migrations, or an HTML grid for spreadsheet-code migrations.</summary>
    [HttpPost("preview")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public IActionResult Preview([FromBody] MigrationRequest request)
    {
        AddCompatibilityHeaders("/api/pxa/v1/migrations/{domain}/code/{provider}/preview");
        if (string.IsNullOrWhiteSpace(request.Framework))
            return BadRequest(new { error = "Field 'framework' is required." });
        if (string.IsNullOrWhiteSpace(request.SourceCode))
            return BadRequest(new { error = "Field 'sourceCode' is required." });
        if (AuthorizeCodeMigration(request.Framework) is { } permissionError)
            return permissionError;

        try
        {
            var bytes = _service.GeneratePreview(request.Framework, request.SourceCode);
            return _service.GetKind(request.Framework) == "spreadsheet"
                ? File(bytes, "text/html; charset=utf-8")
                : File(bytes, "application/pdf", "migration-preview.pdf");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private bool CanReadFramework(FrameworkInfo framework)
    {
        if (!IsDesignerRequest()) return true;
        var domainPermission = string.Equals(framework.Domain, "spreadsheet", StringComparison.OrdinalIgnoreCase)
            ? PxaDesignerPermissions.MigrationSpreadsheetRead
            : PxaDesignerPermissions.MigrationPdfRead;
        return HasPermission(domainPermission);
    }

    private IActionResult? AuthorizeCodeMigration(string framework)
    {
        if (!IsDesignerRequest()) return null;
        var domain = _service.GetKind(framework);
        var domainPermission = string.Equals(domain, "spreadsheet", StringComparison.OrdinalIgnoreCase)
            ? PxaDesignerPermissions.MigrationSpreadsheetExecute
            : PxaDesignerPermissions.MigrationPdfExecute;
        var providerPermission = PxaDesignerMigrationPermissions.ResolveCodeProvider(framework, domain);
        if (!HasPermission(domainPermission)) return PermissionDenied(domainPermission);
        return providerPermission is null || HasPermission(providerPermission)
            ? null
            : PermissionDenied(providerPermission);
    }

    private IActionResult? AuthorizeReportMigration(string provider)
    {
        if (!IsDesignerRequest()) return null;
        if (!HasPermission(PxaDesignerPermissions.MigrationReportExecute))
            return PermissionDenied(PxaDesignerPermissions.MigrationReportExecute);
        var providerPermission = PxaDesignerMigrationPermissions.ResolveReportProvider(provider);
        return providerPermission is null || HasPermission(providerPermission)
            ? null
            : PermissionDenied(providerPermission);
    }

    private IActionResult PermissionDenied(string permission) => StatusCode(
        StatusCodes.Status403Forbidden,
        new
        {
            title = "Designer migration permission required",
            detail = "Your organization role does not allow this migration provider.",
            code = "PXA_DESIGNER_PERMISSION_REQUIRED",
            requiredPermission = permission,
        });

    private bool IsDesignerRequest() => string.Equals(
        Request.Headers["X-PXA-Application"].ToString(),
        "designer",
        StringComparison.OrdinalIgnoreCase);

    private bool HasPermission(string permission) =>
        User.HasClaim(PxaClaimTypes.Permission, permission);

    private void AddCompatibilityHeaders(string successor)
    {
        if (ControllerContext.HttpContext is null) return;
        Response.Headers["Deprecation"] = "true";
        Response.Headers["Sunset"] = "Thu, 30 Sep 2027 23:59:59 GMT";
        Response.Headers.Link = $"<{successor}>; rel=\"successor-version\"";
    }

}

public sealed record MigrationRequest(string Framework, string SourceCode);

public sealed record ReportToDesignRequest(
    string SourceCode,
    Dictionary<string, string>? Resources = null,
    string? ResourceXml = null,
    string? SourceBase64 = null);
