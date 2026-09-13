using System.Text.Json;
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
    Guid? JobId = null);

public interface IPxaPdfTrustAuditWriter
{
    Task WriteAsync(PxaPdfTrustAuditEntry entry, CancellationToken cancellationToken = default);
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
        if (!IsSha256(entry.InputSha256) || entry.OutputSha256 is not null && !IsSha256(entry.OutputSha256))
            throw new ArgumentException("PDF Trust audit hashes must be lowercase SHA-256 values.", nameof(entry));

        var traceId = httpContextAccessor.HttpContext?.TraceIdentifier;
        dbContext.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = organizationId,
            ActorUserId = actorUserId,
            Action = entry.Operation,
            TargetType = "pdf-trust-operation",
            TargetId = entry.OutputSha256 ?? entry.InputSha256,
            Outcome = entry.Outcome,
            DetailsJson = JsonSerializer.Serialize(new
            {
                entry.Policy,
                entry.InputSha256,
                entry.OutputSha256,
                DiagnosticCodes = entry.DiagnosticCodes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal),
                entry.KeyIdentifier,
                entry.JobId,
                TraceId = traceId,
            }, JsonOptions),
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
