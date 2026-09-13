using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Importer.Graphics;
using PXA.Importer.Redaction;
using PXA.Importer.Signing;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Pdf;

namespace PXA.WebApi.Controllers;

[ApiController]
[Route("api/pxa/v1/pdf/redactions")]
[EnableRateLimiting("designer-code")]
public sealed class PdfRedactionsController(
    IPdfRedactionPreflight preflight,
    IPdfRedactionApplier applier,
    IPxaRedactionAuditKeyProvider auditKeyProvider,
    IPxaPdfTimestampAuthorityFactory timestampAuthorityFactory,
    PxaDbContext dbContext,
    IPxaTenantContext tenantContext) : ControllerBase
{
    private const long MaximumUploadBytes = PdfRedactionPreflightOptions.DefaultMaximumFileSizeBytes;
    private const int MaximumReasonCharacters = 2_000;
    private const int MaximumAuditRecordBytes = 256 * 1024;
    private const string AuditTargetType = "pdf-redaction-audit";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Reports what an applied redaction would remove and what PXA cannot account for.
    /// This endpoint never mutates the uploaded document.
    /// </summary>
    [HttpPost("preflight")]
    [Authorize(Policy = PxaDesignerPermissions.PdfRedactionsInspect)]
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
    [Authorize(Policy = PxaSecurityPolicies.AppliedPdfRedaction)]
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
        if (tenantContext.OrganizationId is not { } organizationId || tenantContext.UserId is not { } actorUserId)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Organization context required",
                detail: "An active organization and authenticated user are required to retain redaction evidence.",
                extensions: new Dictionary<string, object?> { ["code"] = "PXAPDFREDACTAPI008" });
        }

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
        if (reason?.Length > MaximumReasonCharacters)
        {
            return ProblemFor(
                "The redaction reason is too long",
                $"The reason may contain at most {MaximumReasonCharacters} characters.",
                "PXAPDFREDACTAPI004");
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
                    Actor = actorUserId.ToString(),
                    AuditSigningKey = auditKeyProvider.Key,
                    AuditTimestampAuthority = timestampAuthorityFactory.Create()
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

            var auditJson = JsonSerializer.Serialize(result.Audit, JsonOptions);
            if (Encoding.UTF8.GetByteCount(auditJson) > MaximumAuditRecordBytes)
            {
                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "The redaction audit record exceeds its safety limit",
                    detail: "The redacted PDF was not returned because its complete audit evidence could not be retained safely.",
                    extensions: new Dictionary<string, object?> { ["code"] = "PXAPDFREDACTAPI011" });
            }

            dbContext.AuditEvents.Add(new AuditEvent
            {
                OrganizationId = organizationId,
                ActorUserId = actorUserId,
                Action = "pdf.redaction.applied",
                TargetType = AuditTargetType,
                TargetId = result.Audit.RecordSha256,
                Outcome = "succeeded",
                DetailsJson = auditJson,
                CreatedAt = result.Audit.AppliedAt
            });
            await dbContext.SaveChangesAsync(cancellationToken);

            Response.Headers["PXA-Redaction-Audit"] = result.Audit.RecordSha256;
            Response.Headers["PXA-Redaction-Result-Sha256"] = result.Audit.ResultSha256;
            // States plainly whether the record carries provenance or only tamper evidence.
            Response.Headers["PXA-Redaction-Audit-Signed"] =
                result.Audit.Signature is null ? "false" : "true";
            Response.Headers["PXA-Redaction-Audit-Timestamped"] =
                result.Audit.Signature?.TimestampTime is null ? "false" : "true";
            if (result.Audit.Signature is { } signature)
            {
                Response.Headers["PXA-Redaction-Audit-Signer"] = signature.SignerThumbprint;
            }
            var auditLocation = $"/api/pxa/v1/pdf/redactions/audits/{result.Audit.RecordSha256}";
            Response.Headers["PXA-Redaction-Audit-Location"] = auditLocation;
            Response.Headers.Append("Link", $"<{auditLocation}>; rel=\"redaction-audit\"");
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
        catch (DbUpdateException)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "The redaction audit record could not be retained",
                detail: "The redacted PDF was not returned because its audit evidence could not be stored.",
                extensions: new Dictionary<string, object?> { ["code"] = "PXAPDFREDACTAPI011" });
        }
        catch (Exception exception) when (exception is PdfTimestampAuthorityException or CryptographicException)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "The redaction audit signature could not be timestamped",
                detail: "The redacted PDF was not returned because the configured timestamp authority did not produce valid audit evidence.",
                extensions: new Dictionary<string, object?> { ["code"] = "PXAPDFREDACTAPI012" });
        }
    }

    /// <summary>
    /// Returns the complete immutable audit evidence for an applied redaction. The lookup is
    /// restricted to the active organization and never falls back to another tenant.
    /// </summary>
    [HttpGet("audits/{recordSha256}")]
    [Authorize(Policy = PxaDesignerPermissions.PdfRedactionsInspect)]
    [ProducesResponseType(typeof(PdfRedactionAuditRecord), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PdfRedactionAuditRecord>> GetAudit(
        string recordSha256,
        CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is not { } organizationId)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Organization context required",
                detail: "Select an active organization before reading redaction evidence.",
                extensions: new Dictionary<string, object?> { ["code"] = "PXAPDFREDACTAPI008" });
        }
        if (!IsSha256(recordSha256))
        {
            return ProblemFor(
                "The redaction audit identifier is invalid",
                "Use the 64-character SHA-256 identifier returned by the apply operation.",
                "PXAPDFREDACTAPI009");
        }

        var normalizedHash = recordSha256.ToLowerInvariant();
        var auditJson = await dbContext.AuditEvents.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId &&
                            value.TargetType == AuditTargetType &&
                            value.TargetId == normalizedHash &&
                            value.Outcome == "succeeded")
            .Select(value => value.DetailsJson)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(auditJson))
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Redaction audit record not found",
                detail: "No redaction audit record with that identifier exists in the active organization.",
                extensions: new Dictionary<string, object?> { ["code"] = "PXAPDFREDACTAPI010" });
        }
        if (Encoding.UTF8.GetByteCount(auditJson) > MaximumAuditRecordBytes)
        {
            return AuditRecordUnavailable();
        }

        PdfRedactionAuditRecord? audit;
        try
        {
            audit = JsonSerializer.Deserialize<PdfRedactionAuditRecord>(auditJson, JsonOptions);
        }
        catch (JsonException)
        {
            return AuditRecordUnavailable();
        }
        if (audit is null ||
            !string.Equals(audit.RecordSha256, normalizedHash, StringComparison.Ordinal) ||
            !PdfRedactionAuditVerifier.Verify(audit))
        {
            return AuditRecordUnavailable();
        }

        Response.Headers.CacheControl = "no-store";
        return Ok(audit);
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

    private ObjectResult AuditRecordUnavailable() => Problem(
        statusCode: StatusCodes.Status500InternalServerError,
        title: "The redaction audit record is unavailable",
        detail: "The stored audit evidence did not pass its integrity and size checks.",
        extensions: new Dictionary<string, object?> { ["code"] = "PXAPDFREDACTAPI011" });

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(static character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

    private sealed record RegionRequest(int PageNumber, double X, double Y, double Width, double Height);
}
