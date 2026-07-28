using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class DesignerNotificationConfiguration : IEntityTypeConfiguration<DesignerNotification>
{
    public void Configure(EntityTypeBuilder<DesignerNotification> builder)
    {
        builder.ToTable("designer_notifications", DatabaseSchemas.Designer);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Category).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(value => value.Severity).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(value => value.Title).HasMaxLength(200).IsRequired();
        builder.Property(value => value.Message).HasMaxLength(2000).IsRequired();
        builder.Property(value => value.ActionLabel).HasMaxLength(80);
        builder.Property(value => value.ActionUrl).HasMaxLength(500);
        builder.Property(value => value.CreatedAt).IsRequired();
        builder.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PxaIdentityUser>().WithMany().HasForeignKey(value => value.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(value => new { value.OrganizationId, value.CreatedAt });
        builder.HasIndex(value => new { value.UserId, value.CreatedAt });
        builder.HasIndex(value => value.ExpiresAt);
    }
}
