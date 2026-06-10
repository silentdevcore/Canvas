using Canvas.Migration.DevExpressReport;
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
    /// Converts a C# DevExpress XtraReport class into a Canvas design (DesignExportDto) that the
    /// visual designer can open. Returns the design plus migration diagnostics.
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
            var result = new XtraReportToDesignConverter().Convert(request.SourceCode);
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
}

public sealed record MigrationRequest(string Framework, string SourceCode);

public sealed record ReportToDesignRequest(string SourceCode);
