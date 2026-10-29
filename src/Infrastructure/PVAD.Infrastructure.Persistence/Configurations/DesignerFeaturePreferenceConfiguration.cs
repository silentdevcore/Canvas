using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence.Identity;

namespace PVAD.Infrastructure.Persistence.Configurations;

internal sealed class DesignerFeaturePreferenceConfiguration : IEntityTypeConfiguration<DesignerFeaturePreference>
{
    public void Configure(EntityTypeBuilder<DesignerFeaturePreference> builder)
    {
        builder.ToTable("designer_feature_preferences", DatabaseSchemas.Designer);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.FeatureId).HasMaxLength(160).IsRequired();
        builder.Property(value => value.UpdatedAt).IsRequired();
        builder.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PvadIdentityUser>().WithMany().HasForeignKey(value => value.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(value => new { value.OrganizationId, value.UserId, value.FeatureId }).IsUnique();
    }
}
