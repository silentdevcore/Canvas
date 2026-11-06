using System.Text.Json;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Core.ProjectFormats;

namespace PVAD.WebApi.Application.Designer;

public interface IPvadDesignerProjectAuditWriter
{
    Task WriteAsync(
        Guid organizationId,
        Guid userId,
        PvadDesignerProjectAuditRecord record,
        CancellationToken cancellationToken = default);
}

public sealed class PvadDesignerProjectAuditWriter(PvadDbContext dbContext) : IPvadDesignerProjectAuditWriter
{
    public async Task WriteAsync(
        Guid organizationId,
        Guid userId,
        PvadDesignerProjectAuditRecord record,
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
                    record.CanonicalFormatId,
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

public sealed record PvadDesignerProjectAuditRecord(
    string Action,
    string ProjectId,
    string FormatId,
    string Operation,
    string Outcome,
    long? PackageBytes = null,
    int? AssetCount = null,
    bool? RequiresReview = null,
    string? DiagnosticCode = null)
{
    public string? CanonicalFormatId => FormatId switch
    {
        "pxap" or PvadProjectFormat.PvadpFormatId => PvadProjectFormat.PvadpFormatId,
        "pxas" or PvadProjectFormat.PvadsFormatId => PvadProjectFormat.PvadsFormatId,
        _ => null,
    };
}
