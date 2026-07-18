using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class SubscriptionUsageEventConfiguration : IEntityTypeConfiguration<SubscriptionUsageEvent>
{
    public void Configure(EntityTypeBuilder<SubscriptionUsageEvent> builder)
    {
        builder.ToTable("subscription_usage_events", DatabaseSchemas.Administration);
        builder.HasKey(value => value.Id);
        builder.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrganizationSubscription>().WithMany().HasForeignKey(value => value.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.Capability).HasMaxLength(100).IsRequired();
        builder.Property(value => value.Operation).HasMaxLength(100).IsRequired();
        builder.Property(value => value.RequestId).HasMaxLength(160).IsRequired();
        builder.Property(value => value.Source).HasMaxLength(80).IsRequired();
        builder.HasIndex(value => new { value.OrganizationId, value.RequestId }).IsUnique();
        builder.HasIndex(value => new { value.SubscriptionId, value.Capability, value.OccurredAt });
    }
}
