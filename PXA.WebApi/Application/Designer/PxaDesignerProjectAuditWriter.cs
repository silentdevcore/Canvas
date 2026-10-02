using System.Text.Json;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;

namespace PXA.WebApi.Application.Designer;

public interface IPxaDesignerProjectAuditWriter
{
    Task WriteAsync(
        Guid organizationId,
        Guid userId,
        PxaDesignerProjectAuditRecord record,
        CancellationToken cancellationToken = default);
}

public sealed class PxaDesignerProjectAuditWriter(PxaDbContext dbContext) : IPxaDesignerProjectAuditWriter
{
    public async Task WriteAsync(
        Guid organizationId,
        Guid userId,
        PxaDesignerProjectAuditRecord record,
        CancellationToken cancellationToken = default)
    {
        dbContext.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = organizationId,
            ActorUserId = userId,
            Action = record.Action,
            TargetType = "designer_project",
            TargetId = string.IsNullOrWhiteSpace(record.ProjectId) ? "unknown" : record.ProjectId,
            Outcome = record.Outcome,
            DetailsJson = JsonSerializer.Serialize(new
                {
                    record.FormatId,
                    record.Operation,
                    record.PackageBytes,
                    record.AssetCount,
                    record.RequiresReview,
                    record.DiagnosticCode,
                },
                new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed record PxaDesignerProjectAuditRecord(
    string Action,
    string ProjectId,
    string FormatId,
    string Operation,
    string Outcome,
    long? PackageBytes = null,
    int? AssetCount = null,
    bool? RequiresReview = null,
    string? DiagnosticCode = null);
