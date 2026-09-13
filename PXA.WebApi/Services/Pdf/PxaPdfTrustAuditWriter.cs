using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Security;

namespace PXA.WebApi.Services.Pdf;

public sealed record PxaPdfTrustAuditEntry(
    string Operation,
    string Policy,
    string InputSha256,
    string? OutputSha256,
    string Outcome,
    IReadOnlyList<string> DiagnosticCodes,
    string? KeyIdentifier = null,
    string? AuditTargetType = null,
    string? AuditTargetId = null,
    string? AuditDetailsJson = null,
    DateTimeOffset? OccurredAt = null,
    Guid? BackgroundJobId = null);

public interface IPxaPdfTrustAuditWriter
{
    Task WriteAsync(PxaPdfTrustAuditEntry entry, CancellationToken cancellationToken = default);
    Task WriteForTenantAsync(
        Guid organizationId,
        Guid actorUserId,
        PxaPdfTrustAuditEntry entry,
        CancellationToken cancellationToken = default) => WriteAsync(entry, cancellationToken);
}

public sealed class PxaPdfTrustAuditWriter(
    PxaDbContext dbContext,
    IPxaTenantContext tenantContext,
    IHttpContextAccessor httpContextAccessor) : IPxaPdfTrustAuditWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task WriteAsync(PxaPdfTrustAuditEntry entry, CancellationToken cancellationToken = default)
    {
        if (tenantContext.OrganizationId is not { } organizationId || tenantContext.UserId is not { } actorUserId)
            throw new InvalidOperationException("PDF Trust audit requires an authenticated tenant context.");
        await WriteForTenantAsync(organizationId, actorUserId, entry, cancellationToken);
    }

    public async Task WriteForTenantAsync(
        Guid organizationId,
        Guid actorUserId,
        PxaPdfTrustAuditEntry entry,
        CancellationToken cancellationToken = default)
    {
        if (!IsSha256(entry.InputSha256) || entry.OutputSha256 is not null && !IsSha256(entry.OutputSha256))
            throw new ArgumentException("PDF Trust audit hashes must be lowercase SHA-256 values.", nameof(entry));

        var diagnosticCodes = entry.DiagnosticCodes
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (diagnosticCodes.Length > 256 || diagnosticCodes.Any(value => value.Length is 0 or > 80))
            throw new ArgumentException("PDF Trust diagnostic codes exceed their safety limits.", nameof(entry));
        if (entry.BackgroundJobId is { } backgroundJobId &&
            await dbContext.PdfTrustOperationJobs.AnyAsync(
                value => value.BackgroundJobId == backgroundJobId,
                cancellationToken))
        {
            return;
        }

        var traceId = httpContextAccessor.HttpContext?.TraceIdentifier;
        var operationId = Guid.NewGuid();
        var auditEventId = Guid.NewGuid();
        var occurredAt = entry.OccurredAt ?? DateTimeOffset.UtcNow;
        var auditDetails = entry.AuditDetailsJson ?? JsonSerializer.Serialize(new
        {
            entry.Policy,
            entry.InputSha256,
            entry.OutputSha256,
            DiagnosticCodes = diagnosticCodes,
            entry.KeyIdentifier,
            OperationJobId = operationId,
            entry.BackgroundJobId,
            TraceId = traceId,
        }, JsonOptions);

        dbContext.PdfTrustOperationJobs.Add(new PdfTrustOperationJob
        {
            Id = operationId,
            OrganizationId = organizationId,
            ActorUserId = actorUserId,
            AuditEventId = auditEventId,
            BackgroundJobId = entry.BackgroundJobId,
            Operation = entry.Operation,
            Policy = entry.Policy,
            InputSha256 = entry.InputSha256,
            OutputSha256 = entry.OutputSha256,
            Outcome = entry.Outcome,
            DiagnosticCodesJson = JsonSerializer.Serialize(diagnosticCodes, JsonOptions),
            KeyIdentifier = entry.KeyIdentifier,
            TraceId = traceId,
            CreatedAt = occurredAt,
            CompletedAt = occurredAt,
        });
        dbContext.AuditEvents.Add(new AuditEvent
        {
            Id = auditEventId,
            OrganizationId = organizationId,
            ActorUserId = actorUserId,
            Action = entry.Operation,
            TargetType = entry.AuditTargetType ?? "pdf-trust-operation",
            TargetId = entry.AuditTargetId ?? entry.OutputSha256 ?? entry.InputSha256,
            Outcome = entry.Outcome,
            DetailsJson = auditDetails,
            CreatedAt = occurredAt,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
