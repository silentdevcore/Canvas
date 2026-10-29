using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence.Identity;

namespace PVAD.Infrastructure.Persistence.Configurations;

internal sealed class DesignerFeaturePolicyConfiguration : IEntityTypeConfiguration<DesignerFeaturePolicy>
{
    public void Configure(EntityTypeBuilder<DesignerFeaturePolicy> builder)
    {
        builder.ToTable("designer_feature_policies", DatabaseSchemas.Designer);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.FeatureId).HasMaxLength(160).IsRequired();
        builder.Property(value => value.UpdatedAt).IsRequired();
        builder.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PvadIdentityUser>().WithMany().HasForeignKey(value => value.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => new { value.OrganizationId, value.FeatureId }).IsUnique();
    }
}
