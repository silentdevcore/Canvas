using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class SubscriptionLifecycleEventConfiguration : IEntityTypeConfiguration<SubscriptionLifecycleEvent>
{
    public void Configure(EntityTypeBuilder<SubscriptionLifecycleEvent> builder)
    {
        builder.ToTable("subscription_lifecycle_events", DatabaseSchemas.Administration);
        builder.HasKey(value => value.Id);
        builder.HasOne<OrganizationSubscription>().WithMany().HasForeignKey(value => value.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.Action).HasMaxLength(100).IsRequired();
        builder.Property(value => value.PreviousStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(value => value.CurrentStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(value => new { value.SubscriptionId, value.CreatedAt });
    }
}
