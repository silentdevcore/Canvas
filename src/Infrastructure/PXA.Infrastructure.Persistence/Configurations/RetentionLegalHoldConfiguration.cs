using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class RetentionLegalHoldConfiguration : IEntityTypeConfiguration<RetentionLegalHold>
{
    public void Configure(EntityTypeBuilder<RetentionLegalHold> builder)
    {
        builder.ToTable("retention_legal_holds", DatabaseSchemas.Administration);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Category).HasMaxLength(100).IsRequired();
        builder.Property(value => value.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(value => value.ReleaseReason).HasMaxLength(2000);
        builder.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PxaIdentityUser>().WithMany().HasForeignKey(value => value.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PxaIdentityUser>().WithMany().HasForeignKey(value => value.ReleasedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => new { value.Category, value.OrganizationId })
            .HasDatabaseName("UX_retention_legal_holds_category_org_active")
            .HasFilter("\"OrganizationId\" IS NOT NULL AND \"ReleasedAt\" IS NULL")
            .IsUnique();
        builder.HasIndex(value => value.Category)
            .HasDatabaseName("UX_retention_legal_holds_category_global_active")
            .HasFilter("\"OrganizationId\" IS NULL AND \"ReleasedAt\" IS NULL")
            .IsUnique();
        builder.HasIndex(value => value.CreatedAt);
    }
}
