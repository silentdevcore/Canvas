using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class PdfTrustOperationJobConfiguration : IEntityTypeConfiguration<PdfTrustOperationJob>
{
    public void Configure(EntityTypeBuilder<PdfTrustOperationJob> builder)
    {
        builder.ToTable("pdf_trust_operation_jobs", DatabaseSchemas.Administration);
        builder.HasKey(value => value.Id);
        builder.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PxaIdentityUser>().WithMany().HasForeignKey(value => value.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AuditEvent>().WithOne().HasForeignKey<PdfTrustOperationJob>(value => value.AuditEventId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.Operation).HasMaxLength(160).IsRequired();
        builder.Property(value => value.Policy).HasMaxLength(160).IsRequired();
        builder.Property(value => value.InputSha256).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(value => value.OutputSha256).HasMaxLength(64).IsFixedLength();
        builder.Property(value => value.Outcome).HasMaxLength(32).IsRequired();
        builder.Property(value => value.DiagnosticCodesJson).HasColumnType("jsonb").IsRequired();
        builder.Property(value => value.KeyIdentifier).HasMaxLength(256);
        builder.Property(value => value.TraceId).HasMaxLength(128);
        builder.HasIndex(value => value.AuditEventId).IsUnique();
        builder.HasIndex(value => value.BackgroundJobId).IsUnique();
        builder.HasIndex(value => new { value.OrganizationId, value.CreatedAt });
        builder.HasIndex(value => new { value.Operation, value.CreatedAt });
        builder.HasIndex(value => value.InputSha256);
        builder.HasIndex(value => value.OutputSha256);
    }
}
