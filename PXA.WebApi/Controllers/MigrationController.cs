using PXA.WebApi.Services;
using Microsoft.AspNetCore.Mvc;
using PXA.Migration.Report;
using PXA.WebApi.Security;
using PxaDesignExportDto = PXA.Core.Contracts.DesignExportDto;
using PxaMigrationDiagnostic = PXA.Migration.Abstractions.MigrationDiagnostic;

namespace PXA.WebApi.Controllers;

[ApiController]
[Route("api/migration")]
[Route("api/pxa/migration")]
public class MigrationController : ControllerBase
{
    private readonly MigrationService _service;

    public MigrationController(MigrationService service)
    {
        _service = service;
    }

    /// <summary>Returns metadata for all supported migration frameworks.</summary>
    [HttpGet("frameworks")]
    [ProducesResponseType(typeof(IEnumerable<object>), 200)]
    public IActionResult GetFrameworks()
    {
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
            requiredPermission = PxaDesignerMigrationPermissions.ResolveCodeProvider(f.Id, f.Domain)
        });
        return Ok(frameworks);
    }

    /// <summary>Converts source code from a third-party PDF framework to PXA.Pdf C# code.</summary>
    [HttpPost("convert")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public IActionResult Convert([FromBody] MigrationRequest request)
    {
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
                })
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
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
        // A binary/zip upload (sourceBase64) — e.g. Telerik .trdp or a packaged .rdlx — is unpacked to its
        // inner report document; the remaining package entries become sub-report resources.
        string? sourceCode = request.SourceCode;
        Dictionary<string, string>? packageResources = null;
        if (!string.IsNullOrWhiteSpace(request.SourceBase64))
        {
            byte[] bytes;
            try { bytes = System.Convert.FromBase64String(request.SourceBase64); }
            catch (FormatException) { return BadRequest(new { error = "Field 'sourceBase64' is not valid base64." }); }

            if (ReportPackageExtractor.IsZip(bytes))
            {
                try
                {
                    var (report, resources) = ReportPackageExtractor.Extract(bytes, LooksLikeAnyReport);
                    if (report is null)
                        return BadRequest(new { error = "Zip package contains no recognizable report document." });
                    sourceCode = report;
                    packageResources = resources;
                }
                catch (InvalidDataException)
                {
                    return BadRequest(new { error = "Field 'sourceBase64' is not a valid zip package." });
                }
            }
            else
            {
                sourceCode = System.Text.Encoding.UTF8.GetString(bytes);
            }
        }

        if (string.IsNullOrWhiteSpace(sourceCode))
            return BadRequest(new { error = "Field 'sourceCode' (or 'sourceBase64') is required." });
        var reportProvider = DetectReportProvider(sourceCode);
        if (AuthorizeReportMigration(reportProvider) is { } permissionError)
            return permissionError;

        try
        {
            PxaDesignExportDto design;
            IReadOnlyList<PxaMigrationDiagnostic> diagnostics;
            if (RdlReportMigration.LooksLike(sourceCode))
            {
                var result = new RdlReportMigration().Convert(sourceCode);
                design = result.Design;
                diagnostics = result.Diagnostics;
            }
            else if (RpxReportMigration.LooksLike(sourceCode))
            {
                var resources = MergeReportResources(request.ResourceXml, request.Resources, packageResources);
                var result = new RpxReportMigration().Convert(sourceCode, resources);
                design = result.Design;
                diagnostics = result.Diagnostics;
            }
            else if (FastReportMigration.LooksLike(sourceCode))
            {
                var result = new FastReportMigration().Convert(sourceCode);
                design = result.Design;
                diagnostics = result.Diagnostics;
            }
            else if (TelerikReportMigration.LooksLike(sourceCode))
            {
                var result = new TelerikReportMigration().Convert(sourceCode);
                design = result.Design;
                diagnostics = result.Diagnostics;
            }
            else if (JasperReportsMigration.LooksLike(sourceCode))
            {
                var resources = MergeReportResources(request.ResourceXml, request.Resources, packageResources);
                var result = new JasperReportsMigration().Convert(sourceCode, resources);
                design = result.Design;
                diagnostics = result.Diagnostics;
            }
            else if (ActiveReportsJsMigration.LooksLike(sourceCode))
            {
                var result = new ActiveReportsJsMigration().Convert(sourceCode);
                design = result.Design;
                diagnostics = result.Diagnostics;
            }
            else if (StimulsoftReportMigration.LooksLike(sourceCode))
            {
                var result = new StimulsoftReportMigration().Convert(sourceCode);
                design = result.Design;
                diagnostics = result.Diagnostics;
            }
            else
            {
                var resources = MergeReportResources(request.ResourceXml, request.Resources, packageResources);
                var result = new DevExpressReportMigration().Convert(sourceCode, resources);
                design = result.Design;
                diagnostics = result.Diagnostics;
            }

            return Ok(new
            {
                design,
                diagnostics = diagnostics.Select(d => new
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

    // Recognized by one of the explicit detectors — used to pick the main report inside a zip package.
    private static bool LooksLikeAnyReport(string content) =>
        RdlReportMigration.LooksLike(content)
        || RpxReportMigration.LooksLike(content)
        || FastReportMigration.LooksLike(content)
        || TelerikReportMigration.LooksLike(content)
        || JasperReportsMigration.LooksLike(content)
        || ActiveReportsJsMigration.LooksLike(content)
        || StimulsoftReportMigration.LooksLike(content);

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

    private static string DetectReportProvider(string sourceCode)
    {
        if (RdlReportMigration.LooksLike(sourceCode)) return ReportMigrationProviderKeys.Rdl;
        if (RpxReportMigration.LooksLike(sourceCode)) return ReportMigrationProviderKeys.Rpx;
        if (FastReportMigration.LooksLike(sourceCode)) return ReportMigrationProviderKeys.FastReport;
        if (TelerikReportMigration.LooksLike(sourceCode)) return ReportMigrationProviderKeys.Telerik;
        if (JasperReportsMigration.LooksLike(sourceCode)) return ReportMigrationProviderKeys.JasperReports;
        if (ActiveReportsJsMigration.LooksLike(sourceCode)) return ReportMigrationProviderKeys.ActiveReportsJs;
        if (StimulsoftReportMigration.LooksLike(sourceCode)) return ReportMigrationProviderKeys.Stimulsoft;
        return ReportMigrationProviderKeys.DevExpressReport;
    }

    private static IReadOnlyDictionary<string, string>? MergeReportResources(
        string? resourceXml,
        Dictionary<string, string>? resources,
        IReadOnlyDictionary<string, string>? packageResources = null)
    {
        if (string.IsNullOrWhiteSpace(resourceXml) && resources is null && packageResources is null)
            return null;

        var merged = string.IsNullOrWhiteSpace(resourceXml)
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : DevExpressReportResourceParser.ParseResx(resourceXml);

        if (packageResources is not null)
            foreach (var (key, value) in packageResources)
                merged[key] = value;

        if (resources is not null)
            foreach (var (key, value) in resources)
                merged[key] = value;   // explicit request resources win over package entries

        return merged;
    }
}

public sealed record MigrationRequest(string Framework, string SourceCode);

public sealed record ReportToDesignRequest(
    string SourceCode,
    Dictionary<string, string>? Resources = null,
    string? ResourceXml = null,
    string? SourceBase64 = null);
