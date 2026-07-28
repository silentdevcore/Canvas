using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Services.Storage;
using PXA.Application.UseCases;
using PXA.Core.Contracts;
using PXA.FileImporter;
using PXA.WebApi.Services;
using PXA.WebApi.Observability;

namespace PXA.WebApi.Services.Jobs;

public sealed class PxaJobProcessor(
    PxaDbContext dbContext,
    PxaStoredObjectService storedObjects,
    IEnumerable<IFileImporter> importers,
    ExportDocumentUseCase exportUseCase,
    MigrationService migrationService,
    IOptions<PxaJobOptions> options,
    ILogger<PxaJobProcessor> logger)
{
    private readonly PxaJobOptions settings = options.Value;

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        await RecoverExpiredLeasesAsync(cancellationToken);
        var job = await ClaimNextAsync(cancellationToken);
        if (job is null)
            return false;

        using var activity = PxaTelemetry.StartJobProcessing(
            job.Type,
            job.Attempts,
            job.TraceParent,
            job.TraceState);
        using var operationActivity = PxaTelemetry.StartDocumentOperation(
            ClassifyDocumentOperation(job.Type));
        var stopwatch = Stopwatch.StartNew();
        PxaTelemetry.RecordJobQueueDuration(job.Type, DateTimeOffset.UtcNow - job.CreatedAt);
        var outcome = "completed";
        try
        {
            var execution = job.Type switch
            {
                PxaJobQueue.TemplateRenderType => await RenderTemplateAsync(job, cancellationToken),
                PxaJobQueue.DocumentImportType => await ImportDocumentAsync(job, cancellationToken),
                PxaJobQueue.DocumentExportType => await ExportDocumentAsync(job, cancellationToken),
                PxaJobQueue.CodeMigrationType => await MigrateCodeAsync(job, cancellationToken),
                _ => throw new PxaPermanentJobException($"Unsupported job type '{job.Type}'."),
            };
            job.ResultObjectId = execution.Result.Id;
            job.DiagnosticsJson = execution.DiagnosticsJson;
            job.ProgressPercent = 100;
            job.Status = PxaBackgroundJobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.LeaseId = null;
            job.LeaseExpiresAt = null;
            job.FailureReason = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            PxaTelemetry.CompleteDocumentOperation(operationActivity, "cancelled");
            throw;
        }
        catch (Exception exception)
        {
            job.FailureReason = Truncate(exception.Message, 2000);
            job.LeaseId = null;
            job.LeaseExpiresAt = null;
            if (exception is PxaPermanentJobException || job.Attempts >= job.MaximumAttempts)
            {
                job.Status = PxaBackgroundJobStatus.DeadLetter;
                job.CompletedAt = DateTimeOffset.UtcNow;
                outcome = "dead_letter";
            }
            else
            {
                job.Status = PxaBackgroundJobStatus.Pending;
                job.ScheduledAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(60, 5 * job.Attempts));
                outcome = "retry";
            }
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            logger.LogWarning(
                PxaLogEvents.JobProcessingFailed,
                exception,
                "PXA background job {JobId} failed on attempt {Attempt}.",
                job.Id,
                job.Attempts);
        }

        job.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        PxaTelemetry.CompleteDocumentOperation(operationActivity, outcome);
        PxaTelemetry.RecordJobProcessed(job.Type, outcome, stopwatch.Elapsed);
        return true;
    }

    private async Task<PxaBackgroundJob?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var job = await dbContext.BackgroundJobs
            .FromSqlInterpolated($"""
                SELECT * FROM administration.background_jobs
                WHERE "Status" = 'Pending' AND "ScheduledAt" <= {now}
                ORDER BY "ScheduledAt", "CreatedAt"
                FOR UPDATE SKIP LOCKED
                LIMIT 1
                """)
            .SingleOrDefaultAsync(cancellationToken);
        if (job is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        job.Status = PxaBackgroundJobStatus.Processing;
        job.Attempts++;
        job.ProgressPercent = Math.Max(job.ProgressPercent, 5);
        job.StartedAt ??= now;
        job.LeaseId = Guid.NewGuid();
        job.LeaseExpiresAt = now.AddMinutes(settings.LeaseMinutes);
        job.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return job;
    }

    private async Task RecoverExpiredLeasesAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var recovered = await dbContext.BackgroundJobs
            .Where(value =>
                value.Status == PxaBackgroundJobStatus.Processing &&
                value.LeaseExpiresAt < now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.Status, PxaBackgroundJobStatus.Pending)
                .SetProperty(value => value.LeaseId, (Guid?)null)
                .SetProperty(value => value.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(value => value.ScheduledAt, now)
                .SetProperty(value => value.UpdatedAt, now), cancellationToken);
        PxaTelemetry.RecordJobLeaseRecoveries(recovered);
    }

    private async Task<JobExecutionResult> RenderTemplateAsync(
        PxaBackgroundJob job,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<TemplateRenderJobPayload>(job.PayloadJson)
            ?? throw new PxaPermanentJobException("The template render payload is invalid.");
        if (string.IsNullOrWhiteSpace(payload.TemplateId))
            throw new PxaPermanentJobException("A template ID is required.");

        var templateQuery = dbContext.DesignerTemplates.AsNoTracking()
            .Where(value => value.OrganizationId == job.OrganizationId);
        var template = Guid.TryParse(payload.TemplateId, out var templateId)
            ? await templateQuery.SingleOrDefaultAsync(
                value => value.Id == templateId || value.ExternalId == payload.TemplateId,
                cancellationToken)
            : await templateQuery.SingleOrDefaultAsync(
                value => value.ExternalId == payload.TemplateId,
                cancellationToken);
        if (template is null)
            throw new PxaPermanentJobException("The requested template was not found.");
        if (!string.IsNullOrWhiteSpace(payload.TemplateVersion))
        {
            var versionExists = long.TryParse(payload.TemplateVersion, out var versionNumber)
                ? await dbContext.DesignerTemplateVersions.AsNoTracking().AnyAsync(
                    value => value.OrganizationId == job.OrganizationId &&
                             value.TemplateId == template.Id &&
                             value.VersionNumber == versionNumber,
                    cancellationToken)
                : await dbContext.DesignerTemplateVersions.AsNoTracking().AnyAsync(
                    value => value.OrganizationId == job.OrganizationId &&
                             value.TemplateId == template.Id &&
                             value.Label == payload.TemplateVersion,
                    cancellationToken);
            if (!versionExists)
                throw new PxaPermanentJobException("The requested template version was not found.");
        }

#pragma warning disable PXA0001
        var document = new PXA.Pdf.PdfDocument();
#pragma warning restore PXA0001
        var page = document.AddPage();
        page.DrawText($"Template Rendered Successfully: {template.Name}", 100, 700, 14);
        await using var content = new MemoryStream(document.ToBytes(), writable: false);
        var result = await storedObjects.StoreAsync(
            job.OrganizationId,
            job.CreatedByUserId,
            "job-result",
            "application/pdf",
            $"template-{payload.TemplateId}.pdf",
            content,
            cancellationToken);
        return new JobExecutionResult(result, null);
    }

    private async Task<JobExecutionResult> ImportDocumentAsync(
        PxaBackgroundJob job,
        CancellationToken cancellationToken)
    {
        var payload = Deserialize<DocumentImportJobPayload>(job);
        var extension = payload.Extension.Trim().TrimStart('.').ToLowerInvariant();
        var importer = importers.FirstOrDefault(value => value.SupportedExtensions.Contains(extension))
            ?? throw new PxaPermanentJobException($"No importer is registered for '.{extension}'.");
        var input = await RequireInputAsync(job, cancellationToken);
        await using var inputContent = input.Content;
        var design = await importer.ImportAsync(inputContent, payload.Name, cancellationToken);
        await using var output = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(design), writable: false);
        var result = await storedObjects.StoreAsync(
            job.OrganizationId,
            job.CreatedByUserId,
            "job-result",
            "application/json",
            $"{payload.Name ?? "imported-document"}.pxa.json",
            output,
            cancellationToken);
        return new JobExecutionResult(result, null);
    }

    private async Task<JobExecutionResult> ExportDocumentAsync(
        PxaBackgroundJob job,
        CancellationToken cancellationToken)
    {
        var payload = Deserialize<DocumentExportJobPayload>(job);
        var input = await RequireInputAsync(job, cancellationToken);
        await using var inputContent = input.Content;
        DesignExportDto design;
        try
        {
            design = await JsonSerializer.DeserializeAsync<DesignExportDto>(
                inputContent,
                cancellationToken: cancellationToken)
                ?? throw new JsonException("The design document is empty.");
        }
        catch (JsonException exception)
        {
            throw new PxaPermanentJobException($"The design document is invalid: {exception.Message}");
        }

        PXA.Core.Contracts.ExportOptions? exportOptions =
            payload.Dpi.HasValue || payload.Quality.HasValue
                ? new PXA.Core.Contracts.ExportOptions(payload.Dpi, payload.Quality)
                : null;
        ExportResult exported;
        try
        {
            exported = exportUseCase.Execute(new ExportDocumentRequest(design, payload.Format, exportOptions));
        }
        catch (NotSupportedException exception)
        {
            throw new PxaPermanentJobException(exception.Message);
        }
        await using var output = new MemoryStream(exported.Data, writable: false);
        var result = await storedObjects.StoreAsync(
            job.OrganizationId,
            job.CreatedByUserId,
            "job-result",
            exported.MimeType,
            exported.FileName,
            output,
            cancellationToken);
        return new JobExecutionResult(result, null);
    }

    private async Task<JobExecutionResult> MigrateCodeAsync(
        PxaBackgroundJob job,
        CancellationToken cancellationToken)
    {
        var payload = Deserialize<CodeMigrationJobPayload>(job);
        var input = await RequireInputAsync(job, cancellationToken);
        await using var inputContent = input.Content;
        using var reader = new StreamReader(inputContent);
        var sourceCode = await reader.ReadToEndAsync(cancellationToken);
        try
        {
            var migration = migrationService.Convert(payload.Framework, sourceCode);
            var resultJson = JsonSerializer.SerializeToUtf8Bytes(new
            {
                migration.PxaCode,
                migration.Summary,
                migration.Diagnostics,
            });
            var diagnosticsJson = JsonSerializer.Serialize(migration.Diagnostics);
            await using var output = new MemoryStream(resultJson, writable: false);
            var result = await storedObjects.StoreAsync(
                job.OrganizationId,
                job.CreatedByUserId,
                "job-result",
                "application/json",
                $"{payload.Framework}-migration.json",
                output,
                cancellationToken);
            return new JobExecutionResult(result, diagnosticsJson);
        }
        catch (ArgumentException exception)
        {
            throw new PxaPermanentJobException(exception.Message);
        }
    }

    private async Task<(PxaStoredObject Metadata, Stream Content)> RequireInputAsync(
        PxaBackgroundJob job,
        CancellationToken cancellationToken)
    {
        if (job.InputObjectId is not { } inputObjectId)
            throw new PxaPermanentJobException("The job has no input object.");
        return await storedObjects.OpenAsync(inputObjectId, job.OrganizationId, cancellationToken)
            ?? throw new PxaPermanentJobException("The job input object is unavailable.");
    }

    private static T Deserialize<T>(PxaBackgroundJob job)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(job.PayloadJson)
                ?? throw new JsonException("The payload is empty.");
        }
        catch (JsonException exception)
        {
            throw new PxaPermanentJobException($"The job payload is invalid: {exception.Message}");
        }
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];

    private static string ClassifyDocumentOperation(string jobType) =>
        jobType switch
        {
            PxaJobQueue.TemplateRenderType => "rendering",
            PxaJobQueue.DocumentImportType => "import",
            PxaJobQueue.DocumentExportType => "export",
            PxaJobQueue.CodeMigrationType => "migration",
            _ => "other",
        };
}

internal sealed record JobExecutionResult(PxaStoredObject Result, string? DiagnosticsJson);

public sealed class PxaPermanentJobException(string message) : Exception(message);
