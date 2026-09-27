using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class EInvoiceValidationRecordConfiguration : IEntityTypeConfiguration<EInvoiceValidationRecord>
{
    public void Configure(EntityTypeBuilder<EInvoiceValidationRecord> builder)
    {
        builder.ToTable("e_invoice_validation_records", DatabaseSchemas.Administration);
        builder.HasKey(value => value.Id);
        builder.HasOne<Organization>().WithMany()
            .HasForeignKey(value => value.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PxaIdentityUser>().WithMany()
            .HasForeignKey(value => value.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AuditEvent>().WithOne()
            .HasForeignKey<EInvoiceValidationRecord>(value => value.AuditEventId)
            .OnDelete(DeleteBehavior.Restrict);
        // A revalidation points at the record it supersedes. Restrict, so retention can never
        // silently drop the earlier verdict while a later one still cites it.
        builder.HasOne<EInvoiceValidationRecord>().WithMany()
            .HasForeignKey(value => value.RevalidatesRecordId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.Operation).HasMaxLength(64).IsRequired();
        builder.Property(value => value.DocumentSha256).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(value => value.ContainerSha256).HasMaxLength(64).IsFixedLength();
        builder.Property(value => value.Standard).HasMaxLength(32).IsRequired();
        builder.Property(value => value.StandardVersion).HasMaxLength(32).IsRequired();
        builder.Property(value => value.Profile).HasMaxLength(64).IsRequired();
        builder.Property(value => value.Syntax).HasMaxLength(32);
        builder.Property(value => value.GuidelineIdentifier).HasMaxLength(256);
        builder.Property(value => value.ConformanceStatus).HasMaxLength(32).IsRequired();
        builder.Property(value => value.ValidatorArtifactSetId).HasMaxLength(160);
        builder.Property(value => value.ValidatorArtifactSetSha256).HasMaxLength(64).IsFixedLength();
        builder.Property(value => value.DiagnosticsJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(value => value.AuditEventId).IsUnique();
        builder.HasIndex(value => new { value.OrganizationId, value.CreatedAt });
        builder.HasIndex(value => new { value.OrganizationId, value.DocumentSha256 });
        builder.HasIndex(value => value.RetainUntil);
        builder.HasIndex(value => value.ValidatorArtifactSetId);
    }
}
