using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PXA.Importer.Graphics;
using PXA.Importer.Redaction;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Route("api/pxa/v1/pdf/redactions")]
[Authorize(Policy = PxaDesignerPermissions.PdfRedactionsInspect)]
[EnableRateLimiting("designer-code")]
public sealed class PdfRedactionsController(
    IPdfRedactionPreflight preflight,
    IPdfRedactionApplier applier) : ControllerBase
{
    private const long MaximumUploadBytes = PdfRedactionPreflightOptions.DefaultMaximumFileSizeBytes;

    /// <summary>
    /// Reports what an applied redaction would remove and what PXA cannot account for.
    /// This endpoint never mutates the uploaded document.
    /// </summary>
    [HttpPost("preflight")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumUploadBytes + 64 * 1024)]
    [ProducesResponseType(typeof(PdfRedactionPreflightReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<PdfRedactionPreflightReport>> Preflight(
        IFormFile? file,
        [FromForm] string? regions,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return ProblemFor("A PDF file is required", "Upload a non-empty PDF in the 'file' multipart field.", "PXAPDFREDACTAPI001");
        }

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) &&
            !file.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            return ProblemFor("The uploaded file is not a PDF", "Use a .pdf file or the application/pdf content type.", "PXAPDFREDACTAPI002");
        }

        if (file.Length > MaximumUploadBytes)
        {
            return Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "The PDF is too large to preflight",
                detail: $"Redaction preflight accepts files up to {MaximumUploadBytes} bytes.",
                extensions: new Dictionary<string, object?> { ["code"] = "PXAPDFREDACTAPI003" });
        }

        if (!TryReadRegions(regions, out var parsedRegions, out var regionError))
        {
            return ProblemFor("The redaction regions are invalid", regionError, "PXAPDFREDACTAPI004");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            return Ok(await preflight.PreflightAsync(stream, parsedRegions, null, cancellationToken));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return ProblemFor("The redaction request exceeds a limit", exception.Message, "PXAPDFREDACTAPI005");
        }
        catch (ArgumentException exception)
        {
            return ProblemFor("The redaction request is invalid", exception.Message, "PXAPDFREDACTAPI004");
        }
        catch (InvalidDataException)
        {
            return Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "The PDF exceeds the preflight limit",
                detail: "The uploaded stream exceeded the configured redaction preflight limit.",
                extensions: new Dictionary<string, object?> { ["code"] = "PXAPDFREDACTAPI003" });
        }
    }

    /// <summary>
    /// Applies redaction and streams the result. Refuses when preflight reports content that
    /// cannot be confidently removed, rather than returning a file that only looks redacted.
    /// </summary>
    [HttpPost("apply")]
    [Consumes("multipart/form-data")]
    [Authorize(Policy = PxaDesignerPermissions.PdfRedactionsApply)]
    [RequestSizeLimit(MaximumUploadBytes + 64 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> Apply(
        IFormFile? file,
        [FromForm] string? regions,
        [FromForm] string? reason,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return ProblemFor("A PDF file is required", "Upload a non-empty PDF in the 'file' multipart field.", "PXAPDFREDACTAPI001");
        }

        if (file.Length > MaximumUploadBytes)
        {
            return Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "The PDF is too large to redact",
                detail: $"Redaction accepts files up to {MaximumUploadBytes} bytes.",
                extensions: new Dictionary<string, object?> { ["code"] = "PXAPDFREDACTAPI003" });
        }

        if (!TryReadRegions(regions, out var parsedRegions, out var regionError))
        {
            return ProblemFor("The redaction regions are invalid", regionError, "PXAPDFREDACTAPI004");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await applier.ApplyAsync(
                stream,
                parsedRegions,
                new PdfRedactionApplyOptions
                {
                    Reason = reason,
                    Actor = User.Identity?.Name
                },
                cancellationToken);

            if (!result.Verification.Passed)
            {
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "The redaction could not be verified",
                    detail: "Verification found residual covered content, so the result was not returned.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "PXAPDFREDACTAPI007",
                        ["verification"] = result.Verification
                    });
            }

            Response.Headers["PXA-Redaction-Audit"] = result.Audit.RecordSha256;
            Response.Headers["PXA-Redaction-Result-Sha256"] = result.Audit.ResultSha256;
            return File(result.Bytes, "application/pdf", "redacted.pdf");
        }
        catch (PdfRedactionBlockedException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The redaction was blocked by preflight",
                detail: exception.Message,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "PXAPDFREDACTAPI006",
                    ["preflight"] = exception.Preflight
                });
        }
        catch (ArgumentException exception)
        {
            return ProblemFor("The redaction request is invalid", exception.Message, "PXAPDFREDACTAPI004");
        }
    }

    private static bool TryReadRegions(
        string? regions,
        out IReadOnlyList<PdfRedactionRegion> parsed,
        out string error)
    {
        parsed = [];
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(regions))
        {
            error = "Provide a 'regions' JSON array of { pageNumber, x, y, width, height } objects.";
            return false;
        }

        List<RegionRequest>? requests;
        try
        {
            requests = JsonSerializer.Deserialize<List<RegionRequest>>(
                regions,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException exception)
        {
            error = $"The 'regions' field is not valid JSON: {exception.Message}";
            return false;
        }

        if (requests is null || requests.Count == 0)
        {
            error = "At least one redaction region is required.";
            return false;
        }

        var result = new List<PdfRedactionRegion>(requests.Count);
        foreach (var request in requests)
        {
            if (request.PageNumber < 1)
            {
                error = "Each region requires a pageNumber of 1 or greater.";
                return false;
            }

            if (!double.IsFinite(request.X) || !double.IsFinite(request.Y) ||
                !double.IsFinite(request.Width) || !double.IsFinite(request.Height))
            {
                error = "Each region requires finite x, y, width, and height values.";
                return false;
            }

            result.Add(new PdfRedactionRegion(
                request.PageNumber,
                new PdfRectangle(request.X, request.Y, request.Width, request.Height)));
        }

        parsed = result;
        return true;
    }

    private ObjectResult ProblemFor(string title, string detail, string code) =>
        Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: title,
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    private sealed record RegionRequest(int PageNumber, double X, double Y, double Width, double Height);
}
