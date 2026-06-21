using Canvas.Core.Contracts;
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
        if (string.IsNullOrWhiteSpace(request.SourceCode))
            return BadRequest(new { error = "Field 'sourceCode' is required." });

        try
        {
            DesignExportDto design;
            IReadOnlyList<Canvas.Migration.Abstractions.MigrationDiagnostic> diagnostics;
            if (RdlToDesignConverter.LooksLikeRdl(request.SourceCode))
            {
                var result = new RdlToDesignConverter().Convert(request.SourceCode);
                design = result.Design;
                diagnostics = result.Diagnostics;
            }
            else if (RpxToDesignConverter.LooksLikeRpx(request.SourceCode))
            {
                var resources = MergeReportResources(request.ResourceXml, request.Resources);
                var result = new RpxToDesignConverter().Convert(request.SourceCode, resources);
                design = result.Design;
                diagnostics = result.Diagnostics;
            }
            else if (FrxToDesignConverter.LooksLikeFrx(request.SourceCode))
            {
                var result = new FrxToDesignConverter().Convert(request.SourceCode);
                design = result.Design;
                diagnostics = result.Diagnostics;
            }
            else if (TrdxToDesignConverter.LooksLikeTrdx(request.SourceCode))
            {
                var result = new TrdxToDesignConverter().Convert(request.SourceCode);
                design = result.Design;
                diagnostics = result.Diagnostics;
            }
            else if (JrxmlToDesignConverter.LooksLikeJrxml(request.SourceCode))
            {
                var resources = MergeReportResources(request.ResourceXml, request.Resources);
                var result = new JrxmlToDesignConverter().Convert(request.SourceCode, resources);
                design = result.Design;
                diagnostics = result.Diagnostics;
            }
            else if (MrtToDesignConverter.LooksLikeMrt(request.SourceCode))
            {
                var result = new MrtToDesignConverter().Convert(request.SourceCode);
                design = result.Design;
                diagnostics = result.Diagnostics;
            }
            else
            {
                var resources = MergeReportResources(request.ResourceXml, request.Resources);
                var result = new XtraReportToDesignConverter().ConvertAuto(request.SourceCode, resources);
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

    private static IReadOnlyDictionary<string, string>? MergeReportResources(
        string? resourceXml,
        Dictionary<string, string>? resources)
    {
        if (string.IsNullOrWhiteSpace(resourceXml) && resources is null)
            return null;

        var merged = string.IsNullOrWhiteSpace(resourceXml)
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : DevExpressReportResourceParser.ParseResx(resourceXml);

        if (resources is not null)
        {
            foreach (var (key, value) in resources)
                merged[key] = value;
        }

        return merged;
    }
}

public sealed record MigrationRequest(string Framework, string SourceCode);

public sealed record ReportToDesignRequest(
    string SourceCode,
    Dictionary<string, string>? Resources = null,
    string? ResourceXml = null);
