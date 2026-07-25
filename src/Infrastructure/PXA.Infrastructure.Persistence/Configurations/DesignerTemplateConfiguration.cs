using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class DesignerTemplateConfiguration : IEntityTypeConfiguration<DesignerTemplate>
{
    public void Configure(EntityTypeBuilder<DesignerTemplate> builder)
    {
        builder.ToTable("designer_templates", DatabaseSchemas.Designer);
        builder.HasKey(value => value.Id);
        builder.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PxaIdentityUser>().WithMany().HasForeignKey(value => value.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PxaIdentityUser>().WithMany().HasForeignKey(value => value.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DesignerTemplateVersion>().WithMany().HasForeignKey(value => value.PublishedVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.ExternalId).HasMaxLength(200);
        builder.Property(value => value.Name).HasMaxLength(200).IsRequired();
        builder.Property(value => value.Description).HasMaxLength(2000);
        builder.Property(value => value.Tags).HasColumnType("text[]").IsRequired();
        builder.Property(value => value.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(value => value.Revision).IsConcurrencyToken();
        builder.Property(value => value.DraftJson).HasColumnType("jsonb").IsRequired();
        builder.Property(value => value.DraftChecksum).HasMaxLength(64).IsRequired();
        builder.Property(value => value.SchemaVersion).HasMaxLength(32).IsRequired();
        builder.Property(value => value.DesignerVersion).HasMaxLength(32).IsRequired();
        builder.HasIndex(value => new { value.OrganizationId, value.Status, value.UpdatedAt });
        builder.HasIndex(value => new { value.OrganizationId, value.Name });
        builder.HasIndex(value => new { value.OrganizationId, value.ExternalId }).IsUnique();
        builder.HasIndex(value => new { value.OrganizationId, value.ArchivedAt });
        builder.HasIndex(value => value.Tags).HasMethod("gin");
    }
}
