using Canvas.WebApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace Canvas.WebApi.Controllers;

[ApiController]
[Route("api/pdf-viewer/forms")]
public sealed class PdfViewerFormsController : ControllerBase
{
    private readonly PdfViewerFormExtractionService _extractionService;

    public PdfViewerFormsController(PdfViewerFormExtractionService extractionService)
    {
        _extractionService = extractionService;
    }

    [HttpPost("extract")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(PdfViewerFormFieldsResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Extract(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "A PDF file is required." });
        if (!file.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) &&
            !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only PDF files are accepted." });

        try
        {
            await using var pdfStream = file.OpenReadStream();
            var fields = await _extractionService.ExtractAsync(pdfStream, file.FileName, cancellationToken);
            return Ok(fields);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not extract form fields: {ex.Message}" });
        }
    }
}
