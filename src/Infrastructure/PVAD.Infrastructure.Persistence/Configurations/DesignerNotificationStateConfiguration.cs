using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence.Identity;

namespace PVAD.Infrastructure.Persistence.Configurations;

internal sealed class DesignerNotificationStateConfiguration : IEntityTypeConfiguration<DesignerNotificationState>
{
    public void Configure(EntityTypeBuilder<DesignerNotificationState> builder)
    {
        builder.ToTable("designer_notification_states", DatabaseSchemas.Designer);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.UpdatedAt).IsRequired();
        builder.HasOne<DesignerNotification>().WithMany().HasForeignKey(value => value.NotificationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PvadIdentityUser>().WithMany().HasForeignKey(value => value.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(value => new { value.NotificationId, value.UserId }).IsUnique();
        builder.HasIndex(value => new { value.UserId, value.ReadAt, value.DismissedAt });
    }
}
