using PXA.WebApi.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace PXA.WebApi.Controllers;

[ApiController]
[Route("api/pdf-viewer/forms")]
[Route("api/pxa/pdf-viewer/forms")]
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

    [HttpPost("fill")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Fill(
        IFormFile? file,
        [FromForm] string? fields,
        [FromForm] bool flatten,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "A PDF file is required." });
        if (!file.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) &&
            !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only PDF files are accepted." });
        if (string.IsNullOrWhiteSpace(fields))
            return BadRequest(new { error = "fields is required." });

        try
        {
            using var fieldsDocument = JsonDocument.Parse(fields);
            var fieldsArray = fieldsDocument.RootElement.ValueKind == JsonValueKind.Array
                ? fieldsDocument.RootElement
                : fieldsDocument.RootElement.TryGetProperty("fields", out var nestedFields)
                    ? nestedFields
                    : default;

            if (fieldsArray.ValueKind != JsonValueKind.Array)
                return BadRequest(new { error = "fields must contain an array." });

            await using var pdfStream = file.OpenReadStream();
            var pdfBytes = await _extractionService.FillAsync(pdfStream, fieldsArray, flatten, cancellationToken);
            var safeName = Path.GetFileNameWithoutExtension(file.FileName);
            return File(pdfBytes, "application/pdf", $"{safeName}-filled.pdf");
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "fields must be valid JSON." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not fill form fields: {ex.Message}" });
        }
    }
}
