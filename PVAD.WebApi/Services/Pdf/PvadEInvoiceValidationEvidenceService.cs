using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Generator;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Security;

namespace PVAD.WebApi.Services.Pdf;

public sealed class PvadEInvoiceEvidenceOptions
{
    public const string SectionName = "Pvad:EInvoices:Evidence";

    /// <summary>How long a stored validation report is retained. Bounded on purpose.</summary>
    public int RetentionDays { get; set; } = 3_650;

    /// <summary>Upper bound for one history or export response.</summary>
    public int MaximumPageSize { get; set; } = 200;

    /// <summary>How often the API removes evidence whose retention window has closed.</summary>
    public int CleanupIntervalMinutes { get; set; } = 60;

    /// <summary>Maximum number of expired records removed by one cleanup pass.</summary>
    public int CleanupBatchSize { get; set; } = 1_000;
}

/// <summary>One stored validation verdict, as it is handed back to a caller.</summary>
public sealed record PvadEInvoiceValidationEvidence(
    Guid Id,
    string Operation,
    string DocumentSha256,
    string? ContainerSha256,
    string Standard,
    string StandardVersion,
    string Profile,
    string? Syntax,
    string? GuidelineIdentifier,
    bool IsStructurallyValid,
    bool IsConformanceValidated,
    string ConformanceStatus,
    string? ValidatorArtifactSetId,
    string? ValidatorArtifactSetSha256,
    IReadOnlyList<PvadEInvoiceDiagnostic> Diagnostics,
    Guid? RevalidatesRecordId,
    Guid ActorUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset RetainUntil);

/// <summary>What has to be recorded about one validation.</summary>
public sealed record PvadEInvoiceValidationEvidenceEntry(
    string Operation,
    string DocumentSha256,
    string? ContainerSha256,
    PvadEInvoiceOptions InvoiceOptions,
    PvadEInvoiceValidationReport Report,
    Guid? RevalidatesRecordId = null);

public interface IPvadEInvoiceValidationEvidenceService
{
    Task<PvadEInvoiceValidationEvidence> RecordAsync(
        PvadEInvoiceValidationEvidenceEntry entry,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PvadEInvoiceValidationEvidence>> GetHistoryAsync(
        string? documentSha256,
        int limit,
        CancellationToken cancellationToken = default);

    Task<PvadEInvoiceValidationEvidence?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Stores and reads historical validation evidence. Every read is scoped to the caller's
/// organization inside this service, so no route parameter can widen it, and every write is
/// paired with an audit event.
/// </summary>
public sealed class PvadEInvoiceValidationEvidenceService(
    PvadDbContext dbContext,
    IPvadTenantContext tenantContext,
    IOptions<PvadEInvoiceEvidenceOptions> options,
    TimeProvider? timeProvider = null) : IPvadEInvoiceValidationEvidenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PvadEInvoiceEvidenceOptions settings = options.Value;
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async Task<PvadEInvoiceValidationEvidence> RecordAsync(
        PvadEInvoiceValidationEvidenceEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var (organizationId, actorUserId) = RequireTenant();
        if (!IsSha256(entry.DocumentSha256) ||
            (entry.ContainerSha256 is not null && !IsSha256(entry.ContainerSha256)))
        {
            throw new ArgumentException(
                "Validation evidence checksums must be lowercase SHA-256 values.",
                nameof(entry));
        }
        if (string.IsNullOrWhiteSpace(entry.Operation) || entry.Operation.Length > 64)
        {
            throw new ArgumentException(
                "A validation evidence operation between 1 and 64 characters is required.",
                nameof(entry));
        }

        // A revalidation may only chain onto a record the same tenant owns.
        if (entry.RevalidatesRecordId is { } previousId &&
            !await dbContext.EInvoiceValidationRecords.AnyAsync(
                value => value.Id == previousId && value.OrganizationId == organizationId,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "The superseded validation record does not exist in this organization.");
        }

        var now = clock.GetUtcNow();
        var auditEventId = Guid.NewGuid();
        var record = new EInvoiceValidationRecord
        {
            OrganizationId = organizationId,
            ActorUserId = actorUserId,
            AuditEventId = auditEventId,
            Operation = entry.Operation,
            DocumentSha256 = entry.DocumentSha256,
            ContainerSha256 = entry.ContainerSha256,
            Standard = entry.InvoiceOptions.Standard.ToString(),
            StandardVersion = Bounded(entry.InvoiceOptions.Version, 32),
            Profile = Bounded(entry.InvoiceOptions.Profile, 64),
            Syntax = Optional(entry.Report.Syntax, 32),
            GuidelineIdentifier = Optional(entry.Report.GuidelineIdentifier, 256),
            IsStructurallyValid = entry.Report.IsStructurallyValid,
            IsConformanceValidated = entry.Report.IsConformanceValidated,
            ConformanceStatus = Bounded(entry.Report.ConformanceStatus, 32),
            ValidatorArtifactSetId = Optional(entry.Report.ValidatorArtifactSetId, 160),
            ValidatorArtifactSetSha256 = entry.Report.ValidatorArtifactSetSha256,
            DiagnosticsJson = JsonSerializer.Serialize(entry.Report.Diagnostics, JsonOptions),
            RevalidatesRecordId = entry.RevalidatesRecordId,
            CreatedAt = now,
            RetainUntil = now.AddDays(Math.Clamp(settings.RetentionDays, 1, 36_500))
        };

        dbContext.EInvoiceValidationRecords.Add(record);
        dbContext.AuditEvents.Add(new AuditEvent
        {
            Id = auditEventId,
            OrganizationId = organizationId,
            ActorUserId = actorUserId,
            Action = "pdf.e-invoice." + entry.Operation,
            TargetType = "e-invoice-validation-record",
            TargetId = record.Id.ToString(),
            Outcome = record.IsStructurallyValid ? "succeeded" : "failed",
            // Deliberately no invoice content: only checksums, states and diagnostic codes.
            DetailsJson = JsonSerializer.Serialize(new
            {
                record.DocumentSha256,
                record.ContainerSha256,
                record.Standard,
                record.StandardVersion,
                record.Profile,
                record.ConformanceStatus,
                record.ValidatorArtifactSetId,
                record.ValidatorArtifactSetSha256,
                DiagnosticCodes = entry.Report.Diagnostics
                    .Select(static item => item.Code)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                record.RevalidatesRecordId
            }, JsonOptions),
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Project(record);
    }

    public async Task<IReadOnlyList<PvadEInvoiceValidationEvidence>> GetHistoryAsync(
        string? documentSha256,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var (organizationId, _) = RequireTenant();
        if (documentSha256 is not null && !IsSha256(documentSha256))
        {
            throw new ArgumentException(
                "A document checksum filter must be a lowercase SHA-256 value.",
                nameof(documentSha256));
        }

        var now = clock.GetUtcNow();
        var records = await dbContext.EInvoiceValidationRecords
            .AsNoTracking()
            .Where(value =>
                value.OrganizationId == organizationId &&
                value.RetainUntil > now &&
                (documentSha256 == null || value.DocumentSha256 == documentSha256))
            .OrderByDescending(value => value.CreatedAt)
            .ThenByDescending(value => value.Id)
            .Take(Math.Clamp(limit, 1, Math.Max(settings.MaximumPageSize, 1)))
            .ToArrayAsync(cancellationToken);
        return records.Select(Project).ToArray();
    }

    public async Task<PvadEInvoiceValidationEvidence?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var (organizationId, _) = RequireTenant();
        var now = clock.GetUtcNow();
        var record = await dbContext.EInvoiceValidationRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.Id == id &&
                    value.OrganizationId == organizationId &&
                    value.RetainUntil > now,
                cancellationToken);
        return record is null ? null : Project(record);
    }

    /// <summary>
    /// Removes reports whose retention window has closed. The database rejects a delete unless
    /// this transaction-scoped setting is present, so retention is the only way out of the table.
    /// </summary>
    public async Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var expired = await dbContext.EInvoiceValidationRecords
            .Where(value => value.RetainUntil <= now)
            .OrderBy(value => value.RetainUntil)
            .Take(Math.Clamp(settings.CleanupBatchSize, 1, 10_000))
            .ToArrayAsync(cancellationToken);
        if (expired.Length == 0)
        {
            return 0;
        }

        // A record a later revalidation still cites is kept, so the chain stays provable.
        var citedIds = await dbContext.EInvoiceValidationRecords
            .Where(value => value.RevalidatesRecordId != null)
            .Select(value => value.RevalidatesRecordId!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var removable = expired.Where(value => !citedIds.Contains(value.Id)).ToArray();
        if (removable.Length == 0)
        {
            return 0;
        }

        if (dbContext.Database.IsRelational())
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await dbContext.Database.ExecuteSqlRawAsync(
                "SET LOCAL pxa.e_invoice_retention_purge = 'on'",
                cancellationToken);
            dbContext.EInvoiceValidationRecords.RemoveRange(removable);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return removable.Length;
        }

        dbContext.EInvoiceValidationRecords.RemoveRange(removable);
        await dbContext.SaveChangesAsync(cancellationToken);
        return removable.Length;
    }

    private (Guid OrganizationId, Guid ActorUserId) RequireTenant() =>
        tenantContext.OrganizationId is { } organizationId && tenantContext.UserId is { } actorUserId
            ? (organizationId, actorUserId)
            : throw new InvalidOperationException(
                "Electronic-invoice validation evidence requires an authenticated tenant context.");

    private static PvadEInvoiceValidationEvidence Project(EInvoiceValidationRecord record) => new(
        record.Id,
        record.Operation,
        record.DocumentSha256,
        record.ContainerSha256,
        record.Standard,
        record.StandardVersion,
        record.Profile,
        record.Syntax,
        record.GuidelineIdentifier,
        record.IsStructurallyValid,
        record.IsConformanceValidated,
        record.ConformanceStatus,
        record.ValidatorArtifactSetId,
        record.ValidatorArtifactSetSha256,
        JsonSerializer.Deserialize<PvadEInvoiceDiagnostic[]>(record.DiagnosticsJson, JsonOptions) ?? [],
        record.RevalidatesRecordId,
        record.ActorUserId,
        record.CreatedAt,
        record.RetainUntil);

    private static string Bounded(string? value, int maximumLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return "unknown";
        }
        return trimmed.Length <= maximumLength ? trimmed : trimmed[..maximumLength];
    }

    private static string? Optional(string? value, int maximumLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        return trimmed.Length <= maximumLength ? trimmed : trimmed[..maximumLength];
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

/// <summary>Runs bounded validation-evidence retention in the deployed API process.</summary>
public sealed class PvadEInvoiceValidationEvidenceRetentionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<PvadEInvoiceEvidenceOptions> options,
    ILogger<PvadEInvoiceValidationEvidenceRetentionWorker> logger) : BackgroundService
{
    private readonly TimeSpan interval = TimeSpan.FromMinutes(options.Value.CleanupIntervalMinutes);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                await using var scope = scopeFactory.CreateAsyncScope();
                var removed = await scope.ServiceProvider
                    .GetRequiredService<IPvadEInvoiceValidationEvidenceService>()
                    .PurgeExpiredAsync(stoppingToken);
                if (removed > 0)
                {
                    logger.LogInformation(
                        "Removed {Count} expired electronic-invoice validation evidence records.",
                        removed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Electronic-invoice validation evidence retention failed.");
            }
        }
    }
}
