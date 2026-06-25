using System.Text.Json;
using Canvas.WebApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace Canvas.WebApi.Controllers;

[ApiController]
[Route("api/pdf-viewer/annotations")]
public sealed class PdfViewerAnnotationsController : ControllerBase
{
    private readonly PdfViewerAnnotationStore _store;

    public PdfViewerAnnotationsController(PdfViewerAnnotationStore store)
    {
        _store = store;
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
