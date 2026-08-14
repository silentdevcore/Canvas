using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PXA.Core.Contracts;
using PXA.Domain.Entities;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Jobs;
using PXA.WebApi.Services.Storage;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/pxa/v1/document-jobs")]
public sealed class DocumentJobsController(
    IPxaTenantContext tenantContext,
    PxaStoredObjectService storedObjects,
    IPxaJobQueue jobQueue) : ControllerBase
{
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<PxaJobAcceptedResponse>> Import(
        IFormFile? file,
        [FromQuery] PxaJobRetentionMode retentionMode = PxaJobRetentionMode.Transient,
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
            PxaJobQueue.DocumentImportType,
            new DocumentImportJobPayload(extension, Path.GetFileNameWithoutExtension(file.FileName)),
            retentionMode,
            cancellationToken);
    }

    [HttpPost("export")]
    public async Task<ActionResult<PxaJobAcceptedResponse>> Export(
        [FromQuery] string format,
        [FromQuery] float? dpi,
        [FromQuery] int? quality,
        [FromBody] DesignExportDto design,
        [FromQuery] PxaJobRetentionMode retentionMode = PxaJobRetentionMode.Transient,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(format))
            return BadRequest(new ProblemDetails { Detail = "The export format is required." });
        await using var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(design), writable: false);
        return await StoreAndQueueAsync(
            content,
            "application/json",
            $"{design.Name}.pxa.json",
            PxaJobQueue.DocumentExportType,
            new DocumentExportJobPayload(format.Trim(), dpi, quality),
            retentionMode,
            cancellationToken);
    }

    [HttpPost("code-migration")]
    public async Task<ActionResult<PxaJobAcceptedResponse>> CodeMigration(
        CodeMigrationJobRequest request,
        [FromQuery] PxaJobRetentionMode retentionMode = PxaJobRetentionMode.Transient,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Framework) || string.IsNullOrWhiteSpace(request.SourceCode))
            return BadRequest(new ProblemDetails { Detail = "Framework and sourceCode are required." });
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes(request.SourceCode), writable: false);
        return await StoreAndQueueAsync(
            content,
            "text/plain; charset=utf-8",
            $"{request.Framework}.source.cs",
            PxaJobQueue.CodeMigrationType,
            new CodeMigrationJobPayload(request.Framework.Trim()),
            retentionMode,
            cancellationToken);
    }

    private async Task<ActionResult<PxaJobAcceptedResponse>> StoreAndQueueAsync(
        Stream content,
        string contentType,
        string fileName,
        string jobType,
        object payload,
        PxaJobRetentionMode retentionMode,
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
            var statusUrl = $"/api/pxa/v1/jobs/{job.Id}";
            return Accepted(statusUrl, new PxaJobAcceptedResponse(
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
public sealed record PxaJobAcceptedResponse(
    Guid JobId,
    string Status,
    string RetentionMode,
    DateTimeOffset ContentExpiresAt,
    string StatusUrl);
