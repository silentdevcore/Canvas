using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Observability;
using PVAD.WebApi.Security;

namespace PVAD.WebApi.Services.Jobs;

public interface IPvadJobQueue
{
    Task<PvadBackgroundJob> EnqueueTemplateRenderAsync(
        string templateId,
        object payload,
        string? templateVersion,
        CancellationToken cancellationToken,
        PvadJobRetentionMode retentionMode = PvadJobRetentionMode.Transient);
    Task<PvadBackgroundJob> EnqueueDocumentJobAsync(
        string type,
        Guid inputObjectId,
        object payload,
        CancellationToken cancellationToken,
        PvadJobRetentionMode retentionMode = PvadJobRetentionMode.Transient);
}

public sealed class PvadJobQueue(
    PvadDbContext dbContext,
    IPvadTenantContext tenantContext,
    Microsoft.Extensions.Options.IOptions<PvadJobOptions> options) : IPvadJobQueue
{
    public const string TemplateRenderType = "designer.template-render";
    public const string DocumentImportType = "document.import";
    public const string DocumentExportType = "document.export";
    public const string CodeMigrationType = "migration.code";
    public const string DesignerCodeExecutionType = "designer.code-execution";
    public const string PdfTrustInspectionType = "pdf.trust-inspection";
    public static IReadOnlyList<string> SupportedTypes { get; } =
    [
        TemplateRenderType,
        DocumentImportType,
        DocumentExportType,
        CodeMigrationType,
        DesignerCodeExecutionType,
        PdfTrustInspectionType,
    ];

    public async Task<PvadBackgroundJob> EnqueueTemplateRenderAsync(
        string templateId,
        object payload,
        string? templateVersion,
        CancellationToken cancellationToken,
        PvadJobRetentionMode retentionMode = PvadJobRetentionMode.Transient)
    {
        var organizationId = tenantContext.OrganizationId ??
            throw new UnauthorizedAccessException("An active organization is required.");
        var userId = tenantContext.UserId ??
            throw new UnauthorizedAccessException("An authenticated user is required.");
        using var activity = PvadTelemetry.StartJobEnqueue(TemplateRenderType);
        var traceContext = PvadTelemetry.CaptureTraceContext();
        var job = new PvadBackgroundJob
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
            ExpiresAt = GetContentExpiry(TemplateRenderType, retentionMode, DateTimeOffset.UtcNow),
            MetadataExpiresAt = DateTimeOffset.UtcNow.AddDays(options.Value.TerminalMetadataRetentionDays),
        };
        dbContext.BackgroundJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        PvadTelemetry.RecordJobEnqueued(job.Type);
        return job;
    }

    public async Task<PvadBackgroundJob> EnqueueDocumentJobAsync(
        string type,
        Guid inputObjectId,
        object payload,
        CancellationToken cancellationToken,
        PvadJobRetentionMode retentionMode = PvadJobRetentionMode.Transient)
    {
        if (type is not (DocumentImportType or DocumentExportType or CodeMigrationType or PdfTrustInspectionType))
            throw new ArgumentException("The document job type is unsupported.", nameof(type));
        var organizationId = tenantContext.OrganizationId ??
            throw new UnauthorizedAccessException("An active organization is required.");
        var userId = tenantContext.UserId ??
            throw new UnauthorizedAccessException("An authenticated user is required.");
        var ownsInput = await dbContext.StoredObjects.AnyAsync(
            value => value.Id == inputObjectId &&
                     value.OrganizationId == organizationId &&
                     value.Status == PvadStoredObjectStatus.Available,
            cancellationToken);
        if (!ownsInput)
            throw new InvalidOperationException("The input object is unavailable.");

        using var activity = PvadTelemetry.StartJobEnqueue(type);
        var traceContext = PvadTelemetry.CaptureTraceContext();
        var job = new PvadBackgroundJob
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
            ExpiresAt = GetContentExpiry(type, retentionMode, DateTimeOffset.UtcNow),
            MetadataExpiresAt = DateTimeOffset.UtcNow.AddDays(options.Value.TerminalMetadataRetentionDays),
        };
        dbContext.BackgroundJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        PvadTelemetry.RecordJobEnqueued(job.Type);
        return job;
    }

    private DateTimeOffset GetContentExpiry(
        string type,
        PvadJobRetentionMode retentionMode,
        DateTimeOffset now) =>
        retentionMode == PvadJobRetentionMode.Transient
            ? now.AddHours(type == PdfTrustInspectionType
                ? options.Value.PdfTrustInspectionRetentionHours
                : options.Value.TransientRetentionHours)
            : now.AddDays(options.Value.ResultRetentionDays);
}

public sealed record TemplateRenderJobPayload(
    string TemplateId,
    JsonElement Payload,
    string? TemplateVersion);

public sealed record DocumentImportJobPayload(string Extension, string? Name);
public sealed record DocumentExportJobPayload(string Format, float? Dpi, int? Quality);
public sealed record CodeMigrationJobPayload(string Framework);
public sealed record PdfTrustInspectionJobPayload(string ValidationPolicy);
