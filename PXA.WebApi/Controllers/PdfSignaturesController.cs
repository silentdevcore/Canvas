using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PXA.Importer.Signing;
using PXA.Importer.Trust;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Route("api/pxa/v1/pdf/signatures")]
[EnableRateLimiting("designer-code")]
public sealed class PdfSignaturesController(
    IPdfPadesSigningService signingService,
    IPdfTrustInspector trustInspector) : ControllerBase
{
    private const long MaximumPdfBytes = 100L * 1024 * 1024;
    private const long MaximumCertificateBytes = 10L * 1024 * 1024;

    [HttpPost("sign")]
    [Authorize(Policy = PxaDesignerPermissions.PdfSignaturesSign)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumPdfBytes + MaximumCertificateBytes + 128 * 1024)]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> Sign([FromForm] PdfSignatureSignRequest request, CancellationToken cancellationToken)
    {
        if (request.Pdf is null || request.Pdf.Length == 0 ||
            request.Certificate is null || request.Certificate.Length == 0)
        {
            return SigningProblem(
                StatusCodes.Status400BadRequest,
                "PXAPDFTRUSTAPI101",
                "A PDF and signing certificate are required",
                "Upload non-empty 'pdf' and 'certificate' multipart fields.");
        }

        if (request.Pdf.Length > MaximumPdfBytes || request.Certificate.Length > MaximumCertificateBytes)
        {
            return SigningProblem(
                StatusCodes.Status413PayloadTooLarge,
                "PXAPDFTRUSTAPI102",
                "A signing upload exceeds its limit",
                "PDF files may contain up to 100 MiB and PKCS#12 certificates up to 10 MiB.");
        }

        if (!request.Pdf.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) &&
            !request.Pdf.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            return SigningProblem(
                StatusCodes.Status400BadRequest,
                "PXAPDFTRUSTAPI103",
                "The document is not a PDF",
                "Use a .pdf file or the application/pdf content type.");
        }

        byte[]? certificateBytes = null;
        try
        {
            var pdfBytes = await ReadFileAsync(request.Pdf, MaximumPdfBytes, cancellationToken);
            certificateBytes = await ReadFileAsync(request.Certificate, MaximumCertificateBytes, cancellationToken);
            using var signingKey = new PdfPkcs12SigningKey(certificateBytes, request.CertificatePassword);
            var result = signingService.Sign(
                pdfBytes,
                signingKey,
                new PdfSigningOptions
                {
                    FieldName = request.FieldName ?? "Signature1",
                    Reason = request.Reason,
                    Location = request.Location,
                    ContactInfo = request.ContactInfo
                });
            var baseName = Path.GetFileNameWithoutExtension(request.Pdf.FileName);
            var downloadName = $"{(string.IsNullOrWhiteSpace(baseName) ? "document" : baseName)}.signed.pdf";
            Response.Headers["X-PXA-PAdES-Profile"] = "B-B";
            return File(result.Bytes, "application/pdf", downloadName);
        }
        catch (Exception exception) when (exception is
            CryptographicException or InvalidDataException or ArgumentException or
            InvalidOperationException or NotSupportedException)
        {
            return SigningProblem(
                StatusCodes.Status400BadRequest,
                "PXAPDFTRUSTAPI104",
                "The PDF could not be signed",
                "Verify the PDF, PKCS#12 certificate, password, certificate validity, and supported RSA key type.");
        }
        finally
        {
            if (certificateBytes is not null)
            {
                CryptographicOperations.ZeroMemory(certificateBytes);
            }
        }
    }

    [HttpPost("validate")]
    [Authorize(Policy = PxaDesignerPermissions.PdfSignaturesValidate)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumPdfBytes + 64 * 1024)]
    [ProducesResponseType(typeof(PdfTrustReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<PdfTrustReport>> Validate(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return SignatureValidationProblem(
                StatusCodes.Status400BadRequest,
                "PXAPDFTRUSTAPI111",
                "A PDF file is required",
                "Upload a non-empty PDF in the 'file' multipart field.");
        }

        if (file.Length > MaximumPdfBytes)
        {
            return SignatureValidationProblem(
                StatusCodes.Status413PayloadTooLarge,
                "PXAPDFTRUSTAPI112",
                "The PDF is too large to validate",
                "PDF signature validation accepts files up to 100 MiB.");
        }

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) &&
            !file.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            return SignatureValidationProblem(
                StatusCodes.Status400BadRequest,
                "PXAPDFTRUSTAPI113",
                "The uploaded file is not a PDF",
                "Use a .pdf file or the application/pdf content type.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            return Ok(await trustInspector.InspectAsync(
                stream,
                new PdfTrustInspectionOptions { MaximumFileSizeBytes = MaximumPdfBytes },
                cancellationToken));
        }
        catch (InvalidDataException)
        {
            return SignatureValidationProblem(
                StatusCodes.Status400BadRequest,
                "PXAPDFTRUSTAPI114",
                "The PDF could not be validated",
                "The uploaded PDF is malformed or exceeds the configured validation limits.");
        }
    }

    private ObjectResult SigningProblem(int status, string code, string title, string detail) =>
        Problem(
            statusCode: status,
            title: title,
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    private ActionResult<PdfTrustReport> SignatureValidationProblem(
        int status,
        string code,
        string title,
        string detail) =>
        new ObjectResult(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Extensions = { ["code"] = code }
        })
        {
            StatusCode = status
        };

    private static async Task<byte[]> ReadFileAsync(IFormFile file, long limit, CancellationToken cancellationToken)
    {
        await using var input = file.OpenReadStream();
        using var output = new MemoryStream(checked((int)Math.Min(file.Length, limit)));
        await input.CopyToAsync(output, cancellationToken);
        if (output.Length > limit)
        {
            throw new InvalidDataException("The uploaded stream exceeded its configured limit.");
        }

        return output.ToArray();
    }
}

public sealed class PdfSignatureSignRequest
{
    public IFormFile? Pdf { get; init; }
    public IFormFile? Certificate { get; init; }
    public string? CertificatePassword { get; init; }
    public string? FieldName { get; init; }
    public string? Reason { get; init; }
    public string? Location { get; init; }
    public string? ContactInfo { get; init; }
}
