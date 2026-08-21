using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Observability;
using PXA.WebApi.Security;

namespace PXA.WebApi.Services.Jobs;

public interface IPxaJobQueue
{
    Task<PxaBackgroundJob> EnqueueTemplateRenderAsync(
        string templateId,
        object payload,
        string? templateVersion,
        CancellationToken cancellationToken,
        PxaJobRetentionMode retentionMode = PxaJobRetentionMode.Transient);
    Task<PxaBackgroundJob> EnqueueDocumentJobAsync(
        string type,
        Guid inputObjectId,
        object payload,
        CancellationToken cancellationToken,
        PxaJobRetentionMode retentionMode = PxaJobRetentionMode.Transient);
}

public sealed class PxaJobQueue(
    PxaDbContext dbContext,
    IPxaTenantContext tenantContext,
    Microsoft.Extensions.Options.IOptions<PxaJobOptions> options) : IPxaJobQueue
{
    public const string TemplateRenderType = "designer.template-render";
    public const string DocumentImportType = "document.import";
    public const string DocumentExportType = "document.export";
    public const string CodeMigrationType = "migration.code";
    public const string DesignerCodeExecutionType = "designer.code-execution";
    public static IReadOnlyList<string> SupportedTypes { get; } =
    [
        TemplateRenderType,
        DocumentImportType,
        DocumentExportType,
        CodeMigrationType,
        DesignerCodeExecutionType,
    ];

    public async Task<PxaBackgroundJob> EnqueueTemplateRenderAsync(
        string templateId,
        object payload,
        string? templateVersion,
        CancellationToken cancellationToken,
        PxaJobRetentionMode retentionMode = PxaJobRetentionMode.Transient)
    {
        var organizationId = tenantContext.OrganizationId ??
            throw new UnauthorizedAccessException("An active organization is required.");
        var userId = tenantContext.UserId ??
            throw new UnauthorizedAccessException("An authenticated user is required.");
        using var activity = PxaTelemetry.StartJobEnqueue(TemplateRenderType);
        var traceContext = PxaTelemetry.CaptureTraceContext();
        var job = new PxaBackgroundJob
        {
            OrganizationId = organizationId,
            CreatedByUserId = userId,
            Type = TemplateRenderType,
            PayloadJson = JsonSerializer.Serialize(new TemplateRenderJobPayload(
                templateId,
                JsonSerializer.SerializeToElement(payload),
                templateVersion)),
            TraceParent = traceContext.TraceParent,
            TraceState = traceContext.TraceState,
            MaximumAttempts = options.Value.MaximumAttempts,
            RetentionMode = retentionMode,
            ExpiresAt = GetContentExpiry(retentionMode, DateTimeOffset.UtcNow),
            MetadataExpiresAt = DateTimeOffset.UtcNow.AddDays(options.Value.TerminalMetadataRetentionDays),
        };
        dbContext.BackgroundJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        PxaTelemetry.RecordJobEnqueued(job.Type);
        return job;
    }

    public async Task<PxaBackgroundJob> EnqueueDocumentJobAsync(
        string type,
        Guid inputObjectId,
        object payload,
        CancellationToken cancellationToken,
        PxaJobRetentionMode retentionMode = PxaJobRetentionMode.Transient)
    {
        if (type is not (DocumentImportType or DocumentExportType or CodeMigrationType))
            throw new ArgumentException("The document job type is unsupported.", nameof(type));
        var organizationId = tenantContext.OrganizationId ??
            throw new UnauthorizedAccessException("An active organization is required.");
        var userId = tenantContext.UserId ??
            throw new UnauthorizedAccessException("An authenticated user is required.");
        var ownsInput = await dbContext.StoredObjects.AnyAsync(
            value => value.Id == inputObjectId &&
                     value.OrganizationId == organizationId &&
                     value.Status == PxaStoredObjectStatus.Available,
            cancellationToken);
        if (!ownsInput)
            throw new InvalidOperationException("The input object is unavailable.");

        using var activity = PxaTelemetry.StartJobEnqueue(type);
        var traceContext = PxaTelemetry.CaptureTraceContext();
        var job = new PxaBackgroundJob
        {
            OrganizationId = organizationId,
            CreatedByUserId = userId,
            Type = type,
            InputObjectId = inputObjectId,
            PayloadJson = JsonSerializer.Serialize(payload),
            TraceParent = traceContext.TraceParent,
            TraceState = traceContext.TraceState,
            MaximumAttempts = options.Value.MaximumAttempts,
            RetentionMode = retentionMode,
            ExpiresAt = GetContentExpiry(retentionMode, DateTimeOffset.UtcNow),
            MetadataExpiresAt = DateTimeOffset.UtcNow.AddDays(options.Value.TerminalMetadataRetentionDays),
        };
        dbContext.BackgroundJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        PxaTelemetry.RecordJobEnqueued(job.Type);
        return job;
    }

    private DateTimeOffset GetContentExpiry(PxaJobRetentionMode retentionMode, DateTimeOffset now) =>
        retentionMode == PxaJobRetentionMode.Transient
            ? now.AddHours(options.Value.TransientRetentionHours)
            : now.AddDays(options.Value.ResultRetentionDays);
}

public sealed record TemplateRenderJobPayload(
    string TemplateId,
    JsonElement Payload,
    string? TemplateVersion);

public sealed record DocumentImportJobPayload(string Extension, string? Name);
public sealed record DocumentExportJobPayload(string Format, float? Dpi, int? Quality);
public sealed record CodeMigrationJobPayload(string Framework);
