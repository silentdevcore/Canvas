using Canvas.Core.Contracts;
using Canvas.Migration.Abstractions;
using Canvas.Migration.ActiveReportsJs;
using Canvas.Migration.DevExpressReport;
using Canvas.Migration.FastReport;
using Canvas.Migration.JasperReports;
using Canvas.Migration.Rdl;
using Canvas.Migration.Rpx;
using Canvas.Migration.Stimulsoft;
using Canvas.Migration.Telerik;
using Canvas.WebApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace Canvas.WebApi.Controllers;

[ApiController]
[Route("api/migration")]
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
        var frameworks = _service.GetFrameworks().Select(f => new
        {
            id = f.Id,
            name = f.Name,
            status = f.Status,
            description = f.Description
        });
        return Ok(frameworks);
    }

    /// <summary>Converts source code from a third-party PDF framework to Canvas.Pdf C# code.</summary>
    [HttpPost("convert")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public IActionResult Convert([FromBody] MigrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Framework))
            return BadRequest(new { error = "Field 'framework' is required." });
        if (string.IsNullOrWhiteSpace(request.SourceCode))
            return BadRequest(new { error = "Field 'sourceCode' is required." });

        try
        {
            var result = _service.Convert(request.Framework, request.SourceCode);
            return Ok(new
            {
                canvasCode = result.CanvasCode,
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
    /// Converts a report-designer layout into a Canvas design (DesignExportDto) the visual designer can
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

        try
        {
            DesignExportDto design;
            IReadOnlyList<Canvas.Migration.Abstractions.MigrationDiagnostic> diagnostics;
            if (RdlToDesignConverter.LooksLikeRdl(sourceCode))
            {
                var result = new RdlToDesignConverter().Convert(sourceCode);
                design = result.Design;
                diagnostics = result.Diagnostics;
            }
            else if (RpxToDesignConverter.LooksLikeRpx(sourceCode))
            {
                var resources = MergeReportResources(request.ResourceXml, request.Resources, packageResources);
                var result = new RpxToDesignConverter().Convert(sourceCode, resources);
                design = result.Design;
                diagnostics = result.Diagnostics;
            }
            else if (FrxToDesignConverter.LooksLikeFrx(sourceCode))
            {
                var result = new FrxToDesignConverter().Convert(sourceCode);
                design = result.Design;
                diagnostics = result.Diagnostics;
            }
            else if (TrdxToDesignConverter.LooksLikeTrdx(sourceCode))
            {
                var result = new TrdxToDesignConverter().Convert(sourceCode);
                design = result.Design;
                diagnostics = result.Diagnostics;
            }
            else if (JrxmlToDesignConverter.LooksLikeJrxml(sourceCode))
            {
                var resources = MergeReportResources(request.ResourceXml, request.Resources, packageResources);
                var result = new JrxmlToDesignConverter().Convert(sourceCode, resources);
                design = result.Design;
                diagnostics = result.Diagnostics;
            }
            else if (ActiveReportsJsToDesignConverter.LooksLikeActiveReportsJs(sourceCode))
            {
                var result = new ActiveReportsJsToDesignConverter().Convert(sourceCode);
                design = result.Design;
                diagnostics = result.Diagnostics;
            }
            else if (MrtToDesignConverter.LooksLikeMrt(sourceCode))
            {
                var result = new MrtToDesignConverter().Convert(sourceCode);
                design = result.Design;
                diagnostics = result.Diagnostics;
            }
            else
            {
                var resources = MergeReportResources(request.ResourceXml, request.Resources, packageResources);
                var result = new XtraReportToDesignConverter().ConvertAuto(sourceCode, resources);
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

    /// <summary>Converts source code and returns a rendered PDF preview as binary.</summary>
    [HttpPost("preview")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public IActionResult Preview([FromBody] MigrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Framework))
            return BadRequest(new { error = "Field 'framework' is required." });
        if (string.IsNullOrWhiteSpace(request.SourceCode))
            return BadRequest(new { error = "Field 'sourceCode' is required." });

        try
        {
            var pdfBytes = _service.GeneratePreview(request.Framework, request.SourceCode);
            return File(pdfBytes, "application/pdf", "migration-preview.pdf");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // Recognized by one of the explicit detectors — used to pick the main report inside a zip package.
    private static bool LooksLikeAnyReport(string content) =>
        RdlToDesignConverter.LooksLikeRdl(content)
        || RpxToDesignConverter.LooksLikeRpx(content)
        || FrxToDesignConverter.LooksLikeFrx(content)
        || TrdxToDesignConverter.LooksLikeTrdx(content)
        || JrxmlToDesignConverter.LooksLikeJrxml(content)
        || ActiveReportsJsToDesignConverter.LooksLikeActiveReportsJs(content)
        || MrtToDesignConverter.LooksLikeMrt(content);

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
