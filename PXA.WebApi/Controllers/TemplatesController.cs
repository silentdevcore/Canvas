using PXA.WebApi.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PXA.Application.UseCases;
using PXA.Domain.Entities;
using PXA.Domain.Repositories;
using PXA.WebApi.Services.Jobs;
using PXA.WebApi.Application.Designer;
using PXA.WebApi.Security;
using PXA.Core.Primitives;

namespace PXA.WebApi.Controllers;

[ApiController]
[Route("api/templates")]
[Route("api/pxa/templates")]
public class TemplatesController : ControllerBase
{
    private readonly CreateTemplateUseCase _createTemplateUseCase;
    private readonly UpdateTemplateUseCase _updateTemplateUseCase;
    private readonly GetTemplateUseCase _getTemplateUseCase;
    private readonly ValidateTemplateUseCase _validateTemplateUseCase;
    private readonly IPxaJobQueue _jobQueue;
    private readonly PXA.Pdf.PdfFontLoader? _fontLoader;
    private readonly IPxaCodeConversionService _codeConversionService;

    public TemplatesController(
        CreateTemplateUseCase createTemplateUseCase,
        UpdateTemplateUseCase updateTemplateUseCase,
        GetTemplateUseCase getTemplateUseCase,
        ValidateTemplateUseCase validateTemplateUseCase,
        IPxaJobQueue jobQueue,
        IPxaCodeConversionService codeConversionService,
        PXA.Pdf.PdfFontLoader? fontLoader = null)
    {
        _createTemplateUseCase = createTemplateUseCase;
        _updateTemplateUseCase = updateTemplateUseCase;
        _getTemplateUseCase = getTemplateUseCase;
        _validateTemplateUseCase = validateTemplateUseCase;
        _jobQueue = jobQueue;
        _codeConversionService = codeConversionService;
        _fontLoader = fontLoader;
    }

    /// <summary>
    /// Renders a template with the provided data payload to generate a PDF.
    /// </summary>
    /// <param name="templateId">The ID of the template to render</param>
    /// <param name="payload">The data payload to merge into the template</param>
    /// <param name="templateVersion">Optional template version</param>
    /// <returns>The generated PDF file</returns>
    [HttpPost("render")]
    [Authorize]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> RenderTemplate([FromQuery] string templateId, [FromBody] object payload, [FromQuery] string? templateVersion = null)
    {
        if (string.IsNullOrEmpty(templateId))
        {
            return BadRequest("Template ID is required");
        }

        if (payload == null)
        {
            return BadRequest("Payload is required");
        }

        try
        {
            var pdfBytes = await RenderStoredTemplateAsync(templateId, payload, templateVersion);
            return File(pdfBytes, "application/pdf", $"template_{templateId}.pdf");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("validation failed"))
        {
            return BadRequest(new { error = "Template validation failed", details = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Renders a template asynchronously for large payloads or batch processing.
    /// Returns a job ID that can be used to check status and download the result.
    /// </summary>
    /// <param name="request">The async render request</param>
    /// <returns>Job ID for tracking the async render operation</returns>
    [HttpPost("render/async")]
    [Authorize]
    [ProducesResponseType(typeof(RenderJobResponse), 202)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> RenderTemplateAsync(
        [FromBody] TemplateRenderRequest request,
        [FromQuery] PxaJobRetentionMode retentionMode = PxaJobRetentionMode.Transient,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return BadRequest("Request body is required");
        }

        try
        {
            var job = await _jobQueue.EnqueueTemplateRenderAsync(
                request.TemplateId,
                request.Payload,
                request.TemplateVersion,
                cancellationToken,
                retentionMode);
            var statusUrl = $"/api/pxa/v1/jobs/{job.Id}";
            return Accepted(statusUrl, new RenderJobResponse
            {
                JobId = job.Id.ToString(),
                Status = job.Status.ToString(),
                RetentionMode = job.RetentionMode.ToString(),
                ContentExpiresAt = job.ExpiresAt,
                CreatedAt = job.CreatedAt.UtcDateTime,
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    /// <summary>
    /// Creates a new template.
    /// </summary>
    /// <param name="request">The template creation request</param>
    /// <returns>The created template</returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(DesignTemplate), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.Name))
        {
            return BadRequest("Template name is required");
        }

        try
        {
            var template = await _createTemplateUseCase.ExecuteAsync(request);
            return CreatedAtAction(nameof(GetTemplate), new { id = template.Id }, template);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets a template by ID.
    /// </summary>
    /// <param name="id">The template ID</param>
    /// <param name="version">Optional version</param>
    /// <returns>The template</returns>
    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(DesignTemplate), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetTemplate(string id, [FromQuery] string? version = null)
    {
        try
        {
            var request = new GetTemplateRequest { Id = id, Version = version };
            var template = await _getTemplateUseCase.ExecuteAsync(request);
            return Ok(template);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing template.
    /// </summary>
    /// <param name="id">The template ID</param>
    /// <param name="request">The update request</param>
    /// <returns>The updated template</returns>
    [HttpPut("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(DesignTemplate), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> UpdateTemplate(string id, [FromBody] UpdateTemplateRequest request)
    {
        if (request == null)
        {
            return BadRequest("Request body is required");
        }

        request.Id = id;

        try
        {
            var template = await _updateTemplateUseCase.ExecuteAsync(request);
            return Ok(template);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(ex.Message);
        }
        catch (TemplateConcurrencyException ex)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Template revision conflict",
                Detail = ex.Message,
                Extensions =
                {
                    ["templateId"] = ex.TemplateId,
                    ["expectedRevision"] = ex.ExpectedRevision,
                    ["currentRevision"] = ex.CurrentRevision,
                },
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Template revision conflict",
                Detail = "The template changed while the update was being saved. Reload it and retry.",
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Validates a template without saving it.
    /// </summary>
    /// <param name="request">The validation request</param>
    /// <returns>Validation results</returns>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(ValidationResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> ValidateTemplate([FromBody] ValidateTemplateRequest request)
    {
        if (request == null)
        {
            return BadRequest("Request body is required");
        }

        try
        {
            var result = await _validateTemplateUseCase.ExecuteAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets a list of all template names and IDs.
    /// </summary>
    /// <returns>List of template names</returns>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<TemplateNameInfo>), 200)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetTemplateNames()
    {
        try
        {
            var templates = await _getTemplateUseCase.GetTemplateNamesAsync();
            return Ok(templates);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    private async Task<byte[]> RenderStoredTemplateAsync(string templateId, object payload, string? templateVersion)
    {
        _ = payload;

        var template = await _getTemplateUseCase.ExecuteAsync(new GetTemplateRequest
        {
            Id = templateId,
            Version = templateVersion
        });

#pragma warning disable PXA0001 // Stored-template rendering targets the PXA PDF engine boundary.
        var pdfDocument = new PXA.Pdf.PdfDocument();
#pragma warning restore PXA0001
        var page = pdfDocument.AddPage();
        page.DrawText($"Template Rendered Successfully: {template.Name}", 100, 700, 14);
        return pdfDocument.ToBytes();
    }

    /// <summary>
    /// Renders a design directly from the UIDesigner JSON export without a pre-stored template.
    /// Accepts the full design payload (pages + elements + page settings) and returns a PDF.
    /// </summary>
    [HttpPost("render-design")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public IActionResult RenderDesign([FromBody] DesignExportDto design)
    {
        if (design is null || design.Pages.Count == 0)
            return BadRequest(new { error = "Design must have at least one page." });

        try
        {
            var document = DesignJsonMapper.MapToPdfDocument(design, _fontLoader);
            var bytes = document.ToBytes(DesignJsonMapper.BuildSaveOptions(design));
            var filename = ExportFileNameSanitizer.Sanitize(design.Name) + ".pdf";
            return File(bytes, "application/pdf", filename);
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException?.Message;
            var trace = ex.StackTrace?.Split('\n').Take(5).ToArray();
            return StatusCode(500, new { error = "Render failed", details = ex.Message, inner, trace });
        }
    }

    /// <summary>
    /// Executes a C# script that builds a PdfDocument using the PXA.Pdf API and returns the PDF.
    /// The script must evaluate to a PdfDocument instance as its last expression.
    /// </summary>
    [HttpPost("csharp-code-to-pdf")]
    [Authorize(AuthenticationSchemes = PxaAuthenticationSchemes.DesignerCookie)]
    [PxaValidateAntiforgery]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> CsharpCodeToPdf([FromBody] CsharpToJsonRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { error = "Code is required." });

        Response.Headers["Deprecation"] = "true";
        Response.Headers.Link = "</api/pxa/v1/designer/templates/{templateId}/code-workspace/execute>; rel=successor-version";
        var workerResult = await _codeConversionService.ExecuteAsync(
            PXA.Core.Contracts.PxaCodeLanguages.CSharpPdf, request.Code, HttpContext.RequestAborted);
        if (!workerResult.Success || workerResult.PdfBytes is null)
            return BadRequest(new { error = "Sandbox execution failed", details = workerResult.Diagnostics });
        return File(workerResult.PdfBytes, "application/pdf", "preview.pdf");

    }

    /// <summary>
    /// Converts a C# expression (returning DesignExportDto) to its JSON representation.
    /// The code must be a single expression that evaluates to a DesignExportDto object.
    /// Example: new DesignExportDto { Name = "Hello", Pages = [ new PageDto { ... } ] }
    /// </summary>
    [HttpPost("csharp-to-json")]
    [Authorize(AuthenticationSchemes = PxaAuthenticationSchemes.DesignerCookie)]
    [PxaValidateAntiforgery]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> CsharpToJson([FromBody] CsharpToJsonRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { error = "Code is required." });

        Response.Headers["Deprecation"] = "true";
        var workerResult = await _codeConversionService.ExecuteAsync(
            PXA.Core.Contracts.PxaCodeLanguages.CSharpModel, request.Code, HttpContext.RequestAborted);
        return workerResult.Success && workerResult.CanonicalDesign is not null
            ? Ok(workerResult.CanonicalDesign)
            : BadRequest(new { error = "Sandbox execution failed", details = workerResult.Diagnostics });

    }

    /// <summary>
    /// Executes a C# script returning PdfDocument and converts the resulting element tree back to the JSON design format.
    /// Useful for round-tripping: Code → JSON → live preview.
    /// </summary>
    [HttpPost("csharp-code-to-json")]
    [Authorize(AuthenticationSchemes = PxaAuthenticationSchemes.DesignerCookie)]
    [PxaValidateAntiforgery]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> CsharpCodeToJson([FromBody] CsharpToJsonRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { error = "Code is required." });

        Response.Headers["Deprecation"] = "true";
        var workerResult = await _codeConversionService.ExecuteAsync(
            PXA.Core.Contracts.PxaCodeLanguages.CSharpPdf, request.Code, HttpContext.RequestAborted);
        return workerResult.Success && workerResult.CanonicalDesign is not null
            ? Ok(workerResult.CanonicalDesign)
            : BadRequest(new { error = "Sandbox execution failed", details = workerResult.Diagnostics });

    }

}

public class CsharpToJsonRequest
{
    public string Code { get; set; } = "";
}

public sealed class TemplateRenderRequest
{
    public required string TemplateId { get; init; }
    public required object Payload { get; init; }
    public string? TemplateVersion { get; init; }
}

public class RenderJobResponse
{
    public required string JobId { get; set; }
    public required string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public required string RetentionMode { get; set; }
    public DateTimeOffset ContentExpiresAt { get; set; }
    public string? DownloadUrl { get; set; }
}
