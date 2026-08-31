using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PXA.Importer.Trust;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Pdf;

namespace PXA.WebApi.Controllers;

[ApiController]
[Route("api/pxa/v1/pdf/trust")]
[Authorize(Policy = PxaDesignerPermissions.PdfTrustInspect)]
[EnableRateLimiting("designer-code")]
public sealed class PdfTrustController(
    IPdfTrustInspector inspector,
    IPdfA2bConverter pdfA2bConverter,
    IPxaPdfTrustPolicyProvider trustPolicyProvider) : ControllerBase
{
    private const long MaximumUploadBytes = PdfTrustInspectionOptions.DefaultMaximumFileSizeBytes;

    [HttpPost("inspect")]
    [HttpPost("/api/pxa/v1/pdf/compliance/validate")]
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
                new PdfTrustInspectionOptions
                {
                    MaximumFileSizeBytes = MaximumUploadBytes,
                    SignatureValidationPolicy = trustPolicyProvider.Policy
                },
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

    [HttpPost("/api/pxa/v1/pdf/compliance/preflight")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumUploadBytes + 64 * 1024)]
    [ProducesResponseType(typeof(PdfA2bConversionPreflightReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<PdfA2bConversionPreflightReport>> PreflightPdfA2b(
        IFormFile? file,
        [FromForm] string? profile,
        CancellationToken cancellationToken)
    {
        if (ValidateFile(file) is { } problem)
        {
            return problem;
        }

        if (!TryReadProfile(profile, out var archiveProfile))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The requested archival profile is not supported",
                detail: "Use 'PDF/A-2b' or 'PDF/A-3b'.",
                extensions: new Dictionary<string, object?> { ["code"] = "PXAPDFACONVERTAPI006" });
        }

        try
        {
            await using var stream = file!.OpenReadStream();
            return Ok(await pdfA2bConverter.PreflightAsync(
                stream,
                new PdfA2bConversionOptions { Profile = archiveProfile },
                cancellationToken));
        }
        catch (InvalidDataException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The PDF could not be preflighted",
                detail: exception.Message,
                extensions: new Dictionary<string, object?> { ["code"] = "PXAPDFACONVERTAPI004" });
        }
    }

    [HttpPost("/api/pxa/v1/pdf/compliance/convert")]
    [Authorize(Policy = PxaDesignerPermissions.PdfConvert)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumUploadBytes + 64 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> ConvertPdfA2b(
        IFormFile? file,
        [FromForm] bool allowSignatureInvalidation,
        [FromForm] string? profile,
        CancellationToken cancellationToken)
    {
        if (ValidateFile(file) is { } problem)
        {
            return problem;
        }

        if (!TryReadProfile(profile, out var archiveProfile))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The requested archival profile is not supported",
                detail: "Use 'PDF/A-2b' or 'PDF/A-3b'.",
                extensions: new Dictionary<string, object?> { ["code"] = "PXAPDFACONVERTAPI006" });
        }

        try
        {
            await using var stream = file!.OpenReadStream();
            var result = await pdfA2bConverter.ConvertAsync(
                stream,
                new PdfA2bConversionOptions
                {
                    AllowSignatureInvalidation = allowSignatureInvalidation,
                    Profile = archiveProfile
                },
                cancellationToken);
            Response.Headers["PXA-Source-SHA256"] = result.SourceSha256;
            Response.Headers["PXA-Result-SHA256"] = result.ResultSha256;
            Response.Headers["PXA-Compliance-Profile"] = result.Validation.Profile ?? "PDF/A-2b";
            return File(result.Bytes, "application/pdf", PdfAFileName(file.FileName, result.Preflight.TargetProfile));
        }
        catch (PdfA2bConversionBlockedException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: $"{exception.Preflight.TargetProfile} conversion was blocked",
                detail: exception.Message,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "PXAPDFACONVERTAPI005",
                    ["preflight"] = exception.Preflight
                });
        }
        catch (InvalidDataException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The PDF could not be converted",
                detail: exception.Message,
                extensions: new Dictionary<string, object?> { ["code"] = "PXAPDFACONVERTAPI004" });
        }
    }

    /// <summary>Defaults to PDF/A-2b so an existing caller keeps its current behaviour.</summary>
    private static bool TryReadProfile(string? value, out PdfArchiveProfile profile)
    {
        profile = PdfArchiveProfile.PdfA2b;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value.Trim().Replace("/", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        switch (normalized.ToUpperInvariant())
        {
            case "PDFA2B":
                profile = PdfArchiveProfile.PdfA2b;
                return true;
            case "PDFA3B":
                profile = PdfArchiveProfile.PdfA3b;
                return true;
            default:
                return false;
        }
    }

    private ActionResult? ValidateFile(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "A PDF file is required",
                detail: "Upload a non-empty PDF in the 'file' multipart field.",
                extensions: new Dictionary<string, object?> { ["code"] = "PXAPDFACONVERTAPI001" });
        }

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) &&
            !file.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The uploaded file is not a PDF",
                detail: "Use a .pdf file or the application/pdf content type.",
                extensions: new Dictionary<string, object?> { ["code"] = "PXAPDFACONVERTAPI002" });
        }

        if (file.Length > MaximumUploadBytes)
        {
            return Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "The PDF is too large to convert",
                detail: $"PDF/A conversion accepts files up to {MaximumUploadBytes} bytes.",
                extensions: new Dictionary<string, object?> { ["code"] = "PXAPDFACONVERTAPI003" });
        }

        return null;
    }

    private static string PdfAFileName(string sourceName, string targetProfile)
    {
        var stem = Path.GetFileNameWithoutExtension(sourceName);
        var suffix = targetProfile.Replace("/", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
        return $"{(string.IsNullOrWhiteSpace(stem) ? "document" : stem)}-{suffix}.pdf";
    }
}
