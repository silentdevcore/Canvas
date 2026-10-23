using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PVAD.Core.Contracts;
using PVAD.Domain.Entities;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Jobs;
using PVAD.WebApi.Services.Storage;

namespace PVAD.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/pvad/v1/document-jobs")]
public sealed class DocumentJobsController(
    IPvadTenantContext tenantContext,
    PvadStoredObjectService storedObjects,
    IPvadJobQueue jobQueue) : ControllerBase
{
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<PvadJobAcceptedResponse>> Import(
        IFormFile? file,
        [FromQuery] PvadJobRetentionMode retentionMode = PvadJobRetentionMode.Transient,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ProblemDetails { Detail = "A non-empty source file is required." });
        var extension = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension))
            return BadRequest(new ProblemDetails { Detail = "The source file must have an extension." });

        await using var content = file.OpenReadStream();
        return await StoreAndQueueAsync(
            content,
            file.ContentType ?? "application/octet-stream",
            file.FileName,
            PvadJobQueue.DocumentImportType,
            new DocumentImportJobPayload(extension, Path.GetFileNameWithoutExtension(file.FileName)),
            retentionMode,
            cancellationToken);
    }

    [HttpPost("export")]
    public async Task<ActionResult<PvadJobAcceptedResponse>> Export(
        [FromQuery] string format,
        [FromQuery] float? dpi,
        [FromQuery] int? quality,
        [FromBody] DesignExportDto design,
        [FromQuery] PvadJobRetentionMode retentionMode = PvadJobRetentionMode.Transient,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(format))
            return BadRequest(new ProblemDetails { Detail = "The export format is required." });
        await using var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(design), writable: false);
        return await StoreAndQueueAsync(
            content,
            "application/json",
            $"{design.Name}.pvad.json",
            PvadJobQueue.DocumentExportType,
            new DocumentExportJobPayload(format.Trim(), dpi, quality),
            retentionMode,
            cancellationToken);
    }

    [HttpPost("code-migration")]
    public async Task<ActionResult<PvadJobAcceptedResponse>> CodeMigration(
        CodeMigrationJobRequest request,
        [FromQuery] PvadJobRetentionMode retentionMode = PvadJobRetentionMode.Transient,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Framework) || string.IsNullOrWhiteSpace(request.SourceCode))
            return BadRequest(new ProblemDetails { Detail = "Framework and sourceCode are required." });
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes(request.SourceCode), writable: false);
        return await StoreAndQueueAsync(
            content,
            "text/plain; charset=utf-8",
            $"{request.Framework}.source.cs",
            PvadJobQueue.CodeMigrationType,
            new CodeMigrationJobPayload(request.Framework.Trim()),
            retentionMode,
            cancellationToken);
    }

    private async Task<ActionResult<PvadJobAcceptedResponse>> StoreAndQueueAsync(
        Stream content,
        string contentType,
        string fileName,
        string jobType,
        object payload,
        PvadJobRetentionMode retentionMode,
        CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is not { } organizationId ||
            tenantContext.UserId is not { } userId)
            return Unauthorized();

        var input = await storedObjects.StoreAsync(
            organizationId,
            userId,
            "job-input",
            contentType,
            fileName,
            content,
            cancellationToken);
        try
        {
            var job = await jobQueue.EnqueueDocumentJobAsync(
                jobType,
                input.Id,
                payload,
                cancellationToken,
                retentionMode);
            var statusUrl = $"/api/pvad/v1/jobs/{job.Id}";
            return Accepted(statusUrl, new PvadJobAcceptedResponse(
                job.Id, job.Status.ToString(), job.RetentionMode.ToString(), job.ExpiresAt, statusUrl));
        }
        catch
        {
            await storedObjects.DeleteAsync(input.Id, organizationId, CancellationToken.None);
            throw;
        }
    }
}

public sealed record CodeMigrationJobRequest(string Framework, string SourceCode);
public sealed record PvadJobAcceptedResponse(
    Guid JobId,
    string Status,
    string RetentionMode,
    DateTimeOffset ContentExpiresAt,
    string StatusUrl);
