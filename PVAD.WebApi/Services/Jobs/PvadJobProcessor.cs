using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Services.Storage;
using PVAD.Application.UseCases;
using PVAD.Core.Contracts;
using PVAD.FileImporter;
using PVAD.WebApi.Services;
using PVAD.WebApi.Observability;

namespace PVAD.WebApi.Services.Jobs;

public sealed class PvadJobProcessor(
    PvadDbContext dbContext,
    PvadStoredObjectService storedObjects,
    IEnumerable<IFileImporter> importers,
    ExportDocumentUseCase exportUseCase,
    MigrationService migrationService,
    PdfTrustInspectionJobExecutor pdfTrustInspection,
    IOptions<PvadJobOptions> options,
    ILogger<PvadJobProcessor> logger)
{
    private readonly PvadJobOptions settings = options.Value;

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        await RecoverExpiredLeasesAsync(cancellationToken);
        var job = await ClaimNextAsync(cancellationToken);
        if (job is null)
            return false;
        var claimedLeaseId = job.LeaseId;
        PvadStoredObject? generatedResult = null;

        using var activity = PvadTelemetry.StartJobProcessing(
            job.Type,
            job.Attempts,
            job.TraceParent,
            job.TraceState);
        using var operationActivity = PvadTelemetry.StartDocumentOperation(
            ClassifyDocumentOperation(job.Type));
        var stopwatch = Stopwatch.StartNew();
        PvadTelemetry.RecordJobQueueDuration(job.Type, DateTimeOffset.UtcNow - job.CreatedAt);
        var outcome = "completed";
        try
        {
            var execution = job.Type switch
            {
                PvadJobQueue.TemplateRenderType => await RenderTemplateAsync(job, cancellationToken),
                PvadJobQueue.DocumentImportType => await ImportDocumentAsync(job, cancellationToken),
                PvadJobQueue.DocumentExportType => await ExportDocumentAsync(job, cancellationToken),
                PvadJobQueue.CodeMigrationType => await MigrateCodeAsync(job, cancellationToken),
                PvadJobQueue.PdfTrustInspectionType => await InspectPdfTrustAsync(job, cancellationToken),
                _ => throw new PvadPermanentJobException($"Unsupported job type '{job.Type}'."),
            };
            generatedResult = execution.Result;
            job.ResultObjectId = execution.Result.Id;
            job.DiagnosticsJson = execution.DiagnosticsJson;
            job.ProgressPercent = 100;
            job.Status = PvadBackgroundJobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            ApplyTerminalRetention(job, job.CompletedAt.Value);
            job.LeaseId = null;
            job.LeaseExpiresAt = null;
            job.FailureReason = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            PvadTelemetry.CompleteDocumentOperation(operationActivity, "cancelled");
            throw;
        }
        catch (Exception exception)
        {
            job.FailureReason = Truncate(exception.Message, 2000);
            job.LeaseId = null;
            job.LeaseExpiresAt = null;
            if (exception is PvadPermanentJobException || job.Attempts >= job.MaximumAttempts)
            {
                job.Status = PvadBackgroundJobStatus.DeadLetter;
                job.CompletedAt = DateTimeOffset.UtcNow;
                ApplyTerminalRetention(job, job.CompletedAt.Value);
                outcome = "dead_letter";
            }
            else
            {
                job.Status = PvadBackgroundJobStatus.Pending;
                job.ScheduledAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(60, 5 * job.Attempts));
                outcome = "retry";
            }
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            logger.LogWarning(
                PvadLogEvents.JobProcessingFailed,
                exception,
                "PVAD background job {JobId} failed on attempt {Attempt}.",
                job.Id,
                job.Attempts);
        }

        job.UpdatedAt = DateTimeOffset.UtcNow;
        var finalizedAt = DateTimeOffset.UtcNow;
        var updated = await dbContext.BackgroundJobs.Where(value => value.Id == job.Id &&
                value.Status == PvadBackgroundJobStatus.Processing && value.LeaseId == claimedLeaseId &&
                value.LeaseExpiresAt > finalizedAt)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.Status, job.Status)
                .SetProperty(value => value.ResultObjectId, job.ResultObjectId)
                .SetProperty(value => value.DiagnosticsJson, job.DiagnosticsJson)
                .SetProperty(value => value.ProgressPercent, job.ProgressPercent)
                .SetProperty(value => value.CompletedAt, job.CompletedAt)
                .SetProperty(value => value.ExpiresAt, job.ExpiresAt)
                .SetProperty(value => value.MetadataExpiresAt, job.MetadataExpiresAt)
                .SetProperty(value => value.ScheduledAt, job.ScheduledAt)
                .SetProperty(value => value.FailureReason, job.FailureReason)
                .SetProperty(value => value.LeaseId, (Guid?)null)
                .SetProperty(value => value.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(value => value.UpdatedAt, job.UpdatedAt), cancellationToken);
        dbContext.Entry(job).State = EntityState.Detached;
        if (updated == 0)
        {
            outcome = "lease_lost";
            if (generatedResult is not null)
                await storedObjects.DeleteAsync(generatedResult.Id, job.OrganizationId, cancellationToken);
        }
        PvadTelemetry.CompleteDocumentOperation(operationActivity, outcome);
        PvadTelemetry.RecordJobProcessed(job.Type, outcome, stopwatch.Elapsed);
        return true;
    }

    private async Task<PvadBackgroundJob?> ClaimNextAsync(CancellationToken cancellationToken)
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

        job.Status = PvadBackgroundJobStatus.Processing;
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
        var transientExpiry = now.AddHours(settings.TransientRetentionHours);
        var trustExpiry = now.AddHours(settings.PdfTrustInspectionRetentionHours);
        var retainedExpiry = now.AddDays(settings.ResultRetentionDays);
        var metadataExpiry = now.AddDays(settings.TerminalMetadataRetentionDays);
        await dbContext.BackgroundJobs.Where(value =>
                (value.Status == PvadBackgroundJobStatus.Pending ||
                 (value.Status == PvadBackgroundJobStatus.Processing && value.LeaseExpiresAt < now)) &&
                value.Attempts >= value.MaximumAttempts)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.Status, PvadBackgroundJobStatus.DeadLetter)
                .SetProperty(value => value.CompletedAt, now)
                .SetProperty(value => value.LeaseId, (Guid?)null)
                .SetProperty(value => value.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(value => value.FailureReason, "The job exhausted its retry limit.")
                .SetProperty(value => value.ExpiresAt, value => value.RetentionMode == PvadJobRetentionMode.Transient
                    ? (value.Type == PvadJobQueue.PdfTrustInspectionType ? trustExpiry : transientExpiry) : retainedExpiry)
                .SetProperty(value => value.MetadataExpiresAt, metadataExpiry)
                .SetProperty(value => value.UpdatedAt, now), cancellationToken);
        var recovered = await dbContext.BackgroundJobs
            .Where(value =>
                value.Status == PvadBackgroundJobStatus.Processing &&
                value.LeaseExpiresAt < now && value.Attempts < value.MaximumAttempts)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.Status, PvadBackgroundJobStatus.Pending)
                .SetProperty(value => value.LeaseId, (Guid?)null)
                .SetProperty(value => value.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(value => value.ScheduledAt, now)
                .SetProperty(value => value.UpdatedAt, now), cancellationToken);
        PvadTelemetry.RecordJobLeaseRecoveries(recovered);
    }

    private async Task<JobExecutionResult> RenderTemplateAsync(
        PvadBackgroundJob job,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<TemplateRenderJobPayload>(job.PayloadJson)
            ?? throw new PvadPermanentJobException("The template render payload is invalid.");
        if (string.IsNullOrWhiteSpace(payload.TemplateId))
            throw new PvadPermanentJobException("A template ID is required.");

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
            throw new PvadPermanentJobException("The requested template was not found.");
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
                throw new PvadPermanentJobException("The requested template version was not found.");
        }

#pragma warning disable PXA0001
        var document = new PVAD.Pdf.PdfDocument();
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
        PvadBackgroundJob job,
        CancellationToken cancellationToken)
    {
        var payload = Deserialize<DocumentImportJobPayload>(job);
        var extension = payload.Extension.Trim().TrimStart('.').ToLowerInvariant();
        var importer = importers.FirstOrDefault(value => value.SupportedExtensions.Contains(extension))
            ?? throw new PvadPermanentJobException($"No importer is registered for '.{extension}'.");
        var input = await RequireInputAsync(job, cancellationToken);
        await using var inputContent = input.Content;
        var design = await importer.ImportAsync(inputContent, payload.Name, cancellationToken);
        await using var output = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(design), writable: false);
        var result = await storedObjects.StoreAsync(
            job.OrganizationId,
            job.CreatedByUserId,
            "job-result",
            "application/json",
            $"{payload.Name ?? "imported-document"}.pvad.json",
            output,
            cancellationToken);
        return new JobExecutionResult(result, null);
    }

    private async Task<JobExecutionResult> ExportDocumentAsync(
        PvadBackgroundJob job,
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
            throw new PvadPermanentJobException($"The design document is invalid: {exception.Message}");
        }

        PVAD.Core.Contracts.ExportOptions? exportOptions =
            payload.Dpi.HasValue || payload.Quality.HasValue
                ? new PVAD.Core.Contracts.ExportOptions(payload.Dpi, payload.Quality)
                : null;
        ExportResult exported;
        try
        {
            exported = exportUseCase.Execute(new ExportDocumentRequest(design, payload.Format, exportOptions));
        }
        catch (NotSupportedException exception)
        {
            throw new PvadPermanentJobException(exception.Message);
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
        PvadBackgroundJob job,
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
                migration.PvadCode,
                PxaCode = migration.PvadCode,
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
            throw new PvadPermanentJobException(exception.Message);
        }
    }

    private async Task<JobExecutionResult> InspectPdfTrustAsync(
        PvadBackgroundJob job,
        CancellationToken cancellationToken)
    {
        _ = Deserialize<PdfTrustInspectionJobPayload>(job);
        var execution = await pdfTrustInspection.ExecuteAsync(job, cancellationToken);
        return new JobExecutionResult(execution.Result, execution.DiagnosticsJson);
    }

    private async Task<(PvadStoredObject Metadata, Stream Content)> RequireInputAsync(
        PvadBackgroundJob job,
        CancellationToken cancellationToken)
    {
        if (job.InputObjectId is not { } inputObjectId)
            throw new PvadPermanentJobException("The job has no input object.");
        return await storedObjects.OpenAsync(inputObjectId, job.OrganizationId, cancellationToken)
            ?? throw new PvadPermanentJobException("The job input object is unavailable.");
    }

    private static T Deserialize<T>(PvadBackgroundJob job)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(job.PayloadJson)
                ?? throw new JsonException("The payload is empty.");
        }
        catch (JsonException exception)
        {
            throw new PvadPermanentJobException($"The job payload is invalid: {exception.Message}");
        }
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];

    private void ApplyTerminalRetention(PvadBackgroundJob job, DateTimeOffset completedAt)
    {
        job.ExpiresAt = job.RetentionMode == PvadJobRetentionMode.Transient
            ? completedAt.AddHours(job.Type == PvadJobQueue.PdfTrustInspectionType
                ? settings.PdfTrustInspectionRetentionHours
                : settings.TransientRetentionHours)
            : completedAt.AddDays(settings.ResultRetentionDays);
        job.MetadataExpiresAt = completedAt.AddDays(settings.TerminalMetadataRetentionDays);
    }

    private static string ClassifyDocumentOperation(string jobType) =>
        jobType switch
        {
            PvadJobQueue.TemplateRenderType => "rendering",
            PvadJobQueue.DocumentImportType => "import",
            PvadJobQueue.DocumentExportType => "export",
            PvadJobQueue.CodeMigrationType => "migration",
            PvadJobQueue.PdfTrustInspectionType => "pdf_trust_inspection",
            _ => "other",
        };
}

internal sealed record JobExecutionResult(PvadStoredObject Result, string? DiagnosticsJson);

public sealed class PvadPermanentJobException(string message) : Exception(message);
