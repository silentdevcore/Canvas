using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class LegalAcceptanceEventConfiguration : IEntityTypeConfiguration<LegalAcceptanceEvent>
{
    public void Configure(EntityTypeBuilder<LegalAcceptanceEvent> builder)
    {
        builder.ToTable("legal_acceptance_events", DatabaseSchemas.Identity);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.DocumentType).HasMaxLength(48).IsRequired();
        builder.Property(value => value.Decision).HasMaxLength(32).IsRequired();
        builder.Property(value => value.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(value => value.Locale).HasMaxLength(16).IsRequired();
        builder.Property(value => value.Source).HasMaxLength(64).IsRequired();
        builder.Property(value => value.CreatedAt).IsRequired();
        builder.HasOne<PxaIdentityUser>().WithMany().HasForeignKey(value => value.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LegalDocumentVersion>().WithMany().HasForeignKey(value => value.LegalDocumentVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => new { value.UserId, value.DocumentType, value.CreatedAt });
        builder.HasIndex(value => new { value.LegalDocumentVersionId, value.CreatedAt });
        builder.HasIndex(value => new
            {
                value.UserId,
                value.OrganizationId,
                value.LegalDocumentVersionId,
            })
            .HasDatabaseName("UX_legal_acceptance_events_user_org_version")
            .HasFilter("\"OrganizationId\" IS NOT NULL")
            .IsUnique();
        builder.HasIndex(value => new { value.UserId, value.LegalDocumentVersionId })
            .HasDatabaseName("UX_legal_acceptance_events_user_global_version")
            .HasFilter("\"OrganizationId\" IS NULL")
            .IsUnique();
    }
}
