using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class DesignerTemplateVersionConfiguration : IEntityTypeConfiguration<DesignerTemplateVersion>
{
    public void Configure(EntityTypeBuilder<DesignerTemplateVersion> builder)
    {
        builder.ToTable("designer_template_versions", DatabaseSchemas.Designer);
        builder.HasKey(value => value.Id);
        builder.HasOne<DesignerTemplate>().WithMany().HasForeignKey(value => value.TemplateId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PxaIdentityUser>().WithMany().HasForeignKey(value => value.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.Label).HasMaxLength(200);
        builder.Property(value => value.DesignJson).HasColumnType("jsonb").IsRequired();
        builder.Property(value => value.Checksum).HasMaxLength(64).IsRequired();
        builder.Property(value => value.SchemaVersion).HasMaxLength(32).IsRequired();
        builder.Property(value => value.DesignerVersion).HasMaxLength(32).IsRequired();
        builder.HasIndex(value => new { value.TemplateId, value.VersionNumber }).IsUnique();
        builder.HasIndex(value => new { value.OrganizationId, value.CreatedAt });
    }
}
