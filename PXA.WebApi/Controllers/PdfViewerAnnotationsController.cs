using System.Text.Json;
using PXA.Importer.Redaction;
using PXA.WebApi.Services;
using PXA.WebApi.Services.Pdf;
using Microsoft.AspNetCore.Mvc;

namespace PXA.WebApi.Controllers;

[ApiController]
[Route("api/pdf-viewer/annotations")]
[Route("api/pxa/pdf-viewer/annotations")]
public sealed class PdfViewerAnnotationsController : ControllerBase
{
    private readonly PdfViewerAnnotationStore _store;
    private readonly PdfViewerAnnotationFlatteningService _flatteningService;
    private readonly PdfViewerNativeAnnotationExtractionService _extractionService;
    private readonly IPdfRedactionPreflight _redactionPreflight;
    private readonly IPdfRedactionApplier _redactionApplier;
    private readonly PdfViewerRedactionRegionMapper _redactionRegions;
    private readonly IPxaRedactionAuditKeyProvider _auditKeyProvider;

    public PdfViewerAnnotationsController(
        PdfViewerAnnotationStore store,
        PdfViewerAnnotationFlatteningService flatteningService,
        PdfViewerNativeAnnotationExtractionService extractionService,
        IPdfRedactionPreflight redactionPreflight,
        IPdfRedactionApplier redactionApplier,
        PdfViewerRedactionRegionMapper redactionRegions,
        IPxaRedactionAuditKeyProvider auditKeyProvider)
    {
        _store = store;
        _flatteningService = flatteningService;
        _extractionService = extractionService;
        _redactionPreflight = redactionPreflight;
        _redactionApplier = redactionApplier;
        _redactionRegions = redactionRegions;
        _auditKeyProvider = auditKeyProvider;
    }

    [HttpPost]
    [ProducesResponseType(typeof(PdfViewerAnnotationsResponse), 200)]
    [ProducesResponseType(400)]
    public IActionResult Save([FromBody] SavePdfViewerAnnotationsRequest? request)
    {
        if (request is null)
            return BadRequest(new { error = "Request body is required." });
        if (string.IsNullOrWhiteSpace(request.DocumentId))
            return BadRequest(new { error = "documentId is required." });
        if (request.Version != 1)
            return BadRequest(new { error = "Only annotation sidecar version 1 is supported." });
        if (request.Annotations.ValueKind != JsonValueKind.Array)
            return BadRequest(new { error = "annotations must be an array." });

        var exportedAt = request.ExportedAt ?? DateTimeOffset.UtcNow;
        var stored = _store.Save(
            request.DocumentId.Trim(),
            request.Version,
            request.SourceName,
            exportedAt,
            request.Annotations);

        return Ok(ToResponse(stored));
    }

    [HttpGet("{documentId}")]
    [ProducesResponseType(typeof(PdfViewerAnnotationsResponse), 200)]
    [ProducesResponseType(404)]
    public IActionResult Get(string documentId)
    {
        if (!_store.TryGet(documentId, out var stored))
            return NotFound(new { error = $"No annotations found for document '{documentId}'." });

        return Ok(ToResponse(stored));
    }

    [HttpDelete("{documentId}")]
    [ProducesResponseType(204)]
    public IActionResult Delete(string documentId)
    {
        _store.Delete(documentId);
        return NoContent();
    }

    [HttpPost("flatten")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Flatten(
        IFormFile? file,
        [FromForm] string? sidecar,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "A PDF file is required." });
        if (!file.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) &&
            !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only PDF files are accepted." });
        if (string.IsNullOrWhiteSpace(sidecar))
            return BadRequest(new { error = "sidecar is required." });

        try
        {
            using var sidecarDocument = JsonDocument.Parse(sidecar);
            var annotations = GetAnnotationsArray(sidecarDocument.RootElement);

            if (annotations.ValueKind != JsonValueKind.Array)
                return BadRequest(new { error = "sidecar must contain an annotations array." });

            await using var pdfStream = file.OpenReadStream();
            var pdfBytes = await _flatteningService.FlattenAsync(pdfStream, annotations, cancellationToken);
            var safeName = Path.GetFileNameWithoutExtension(file.FileName);
            return File(pdfBytes, "application/pdf", $"{safeName}-reviewed.pdf");
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "sidecar must be valid JSON." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not flatten annotations: {ex.Message}" });
        }
    }

    /// <summary>
    /// Reports what the Viewer's redaction marks would remove and what PXA cannot account
    /// for, without changing the document.
    /// </summary>
    [HttpPost("redact/preflight")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(PdfRedactionPreflightReport), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> RedactPreflight(
        IFormFile? file,
        [FromForm] string? sidecar,
        CancellationToken cancellationToken)
    {
        if (!TryValidateRedactionRequest(file, sidecar, out var annotations, out var error))
            return BadRequest(new { error });

        try
        {
            var pdfBytes = await ReadAsync(file!, cancellationToken);
            var regions = await _redactionRegions.MapAsync(pdfBytes, annotations, cancellationToken);
            await using var stream = new MemoryStream(pdfBytes, writable: false);
            return Ok(await _redactionPreflight.PreflightAsync(stream, regions, null, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Applies the Viewer's redaction marks. Fails closed: when preflight reports content
    /// that cannot be confidently removed, no file is produced.
    /// </summary>
    [HttpPost("redact")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Redact(
        IFormFile? file,
        [FromForm] string? sidecar,
        [FromForm] string? reason,
        CancellationToken cancellationToken)
    {
        if (!TryValidateRedactionRequest(file, sidecar, out var annotations, out var error))
            return BadRequest(new { error });

        byte[] pdfBytes;
        IReadOnlyList<PdfRedactionRegion> regions;
        try
        {
            pdfBytes = await ReadAsync(file!, cancellationToken);
            regions = await _redactionRegions.MapAsync(pdfBytes, annotations, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        try
        {
            await using var pdfStream = new MemoryStream(pdfBytes, writable: false);
            var result = await _redactionApplier.ApplyAsync(
                pdfStream,
                regions,
                new PdfRedactionApplyOptions
                {
                    Reason = reason,
                    Actor = User.Identity?.Name,
                    AuditSigningKey = _auditKeyProvider.Key
                },
                cancellationToken);

            if (!result.Verification.Passed)
            {
                return Conflict(new
                {
                    error = "The redaction could not be verified, so no file was produced.",
                    code = "PXAPDFREDACTVIEWER002",
                    verification = result.Verification
                });
            }

            Response.Headers["PXA-Redaction-Audit"] = result.Audit.RecordSha256;
            Response.Headers["PXA-Redaction-Result-Sha256"] = result.Audit.ResultSha256;
            Response.Headers["PXA-Redaction-Audit-Signed"] =
                result.Audit.Signature is null ? "false" : "true";
            var safeName = Path.GetFileNameWithoutExtension(file!.FileName);
            return File(result.Bytes, "application/pdf", $"{safeName}-redacted.pdf");
        }
        catch (PdfRedactionBlockedException ex)
        {
            return Conflict(new
            {
                error = ex.Message,
                code = "PXAPDFREDACTVIEWER001",
                preflight = ex.Preflight
            });
        }
    }

    private bool TryValidateRedactionRequest(
        IFormFile? file,
        string? sidecar,
        out JsonElement annotations,
        out string error)
    {
        annotations = default;
        error = string.Empty;
        if (file is null || file.Length == 0)
        {
            error = "A PDF file is required.";
            return false;
        }

        if (!file.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) &&
            !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            error = "Only PDF files are accepted.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(sidecar))
        {
            error = "sidecar is required.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(sidecar);
            var parsed = GetAnnotationsArray(document.RootElement);
            if (parsed.ValueKind != JsonValueKind.Array)
            {
                error = "sidecar must contain an annotations array.";
                return false;
            }

            annotations = parsed.Clone();
            return true;
        }
        catch (JsonException)
        {
            error = "sidecar must be valid JSON.";
            return false;
        }
    }

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var source = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    [HttpPost("embed")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Embed(
        IFormFile? file,
        [FromForm] string? sidecar,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "A PDF file is required." });
        if (!file.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) &&
            !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only PDF files are accepted." });
        if (string.IsNullOrWhiteSpace(sidecar))
            return BadRequest(new { error = "sidecar is required." });

        try
        {
            using var sidecarDocument = JsonDocument.Parse(sidecar);
            var annotations = GetAnnotationsArray(sidecarDocument.RootElement);

            if (annotations.ValueKind != JsonValueKind.Array)
                return BadRequest(new { error = "sidecar must contain an annotations array." });

            await using var pdfStream = file.OpenReadStream();
            var pdfBytes = await _flatteningService.EmbedNativeAnnotationsAsync(pdfStream, annotations, cancellationToken);
            var safeName = Path.GetFileNameWithoutExtension(file.FileName);
            return File(pdfBytes, "application/pdf", $"{safeName}-annotated.pdf");
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "sidecar must be valid JSON." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not embed annotations: {ex.Message}" });
        }
    }

    [HttpPost("extract")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(PdfViewerAnnotationSidecarResponse), 200)]
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
            var sidecar = await _extractionService.ExtractAsync(pdfStream, file.FileName, cancellationToken);
            return Ok(sidecar);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not extract annotations: {ex.Message}" });
        }
    }

    private static PdfViewerAnnotationsResponse ToResponse(StoredPdfViewerAnnotations stored) => new()
    {
        DocumentId = stored.DocumentId,
        Version = stored.Version,
        SourceName = stored.SourceName,
        ExportedAt = stored.ExportedAt,
        SavedAt = stored.SavedAt,
        Annotations = stored.Annotations,
        AnnotationCount = stored.Annotations.GetArrayLength(),
    };

    private static JsonElement GetAnnotationsArray(JsonElement root)
    {
        return root.ValueKind == JsonValueKind.Array
            ? root
            : root.TryGetProperty("annotations", out var sidecarAnnotations)
                ? sidecarAnnotations
                : default;
    }
}

public sealed class SavePdfViewerAnnotationsRequest
{
    public string? DocumentId { get; set; }
    public int Version { get; set; } = 1;
    public string? SourceName { get; set; }
    public DateTimeOffset? ExportedAt { get; set; }
    public JsonElement Annotations { get; set; }
}

public sealed class PdfViewerAnnotationsResponse
{
    public string DocumentId { get; set; } = "";
    public int Version { get; set; }
    public string? SourceName { get; set; }
    public DateTimeOffset ExportedAt { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public JsonElement Annotations { get; set; }
    public int AnnotationCount { get; set; }
}
