using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PXA.Importer.Trust;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Route("api/pxa/v1/pdf/trust")]
[Authorize(Policy = PxaDesignerPermissions.PdfTrustInspect)]
[EnableRateLimiting("designer-code")]
public sealed class PdfTrustController(IPdfTrustInspector inspector) : ControllerBase
{
    private const long MaximumUploadBytes = PdfTrustInspectionOptions.DefaultMaximumFileSizeBytes;

    [HttpPost("inspect")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumUploadBytes + 64 * 1024)]
    [ProducesResponseType(typeof(PdfTrustReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<PdfTrustReport>> Inspect(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "A PDF file is required",
                detail: "Upload a non-empty PDF in the 'file' multipart field.",
                extensions: new Dictionary<string, object?> { ["code"] = "PXAPDFTRUSTAPI001" });
        }

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) &&
            !file.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The uploaded file is not a PDF",
                detail: "Use a .pdf file or the application/pdf content type.",
                extensions: new Dictionary<string, object?> { ["code"] = "PXAPDFTRUSTAPI002" });
        }

        if (file.Length > MaximumUploadBytes)
        {
            return Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "The PDF is too large to inspect",
                detail: $"PDF Trust inspection accepts files up to {MaximumUploadBytes} bytes.",
                extensions: new Dictionary<string, object?> { ["code"] = "PXAPDFTRUSTAPI003" });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            return Ok(await inspector.InspectAsync(
                stream,
                new PdfTrustInspectionOptions { MaximumFileSizeBytes = MaximumUploadBytes },
                cancellationToken));
        }
        catch (InvalidDataException)
        {
            return Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "The PDF exceeds the inspection limit",
                detail: "The uploaded stream exceeded the configured PDF Trust inspection limit.",
                extensions: new Dictionary<string, object?> { ["code"] = "PXAPDFTRUSTAPI003" });
        }
    }
}
