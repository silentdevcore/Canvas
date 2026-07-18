using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class SubscriptionEntitlementConfiguration : IEntityTypeConfiguration<SubscriptionEntitlement>
{
    public void Configure(EntityTypeBuilder<SubscriptionEntitlement> builder)
    {
        builder.ToTable("subscription_entitlements", DatabaseSchemas.Administration);
        builder.HasKey(value => value.Id);
        builder.HasOne<OrganizationSubscription>().WithMany().HasForeignKey(value => value.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(value => value.Capability).HasMaxLength(100).IsRequired();
        builder.Property(value => value.Unit).HasMaxLength(40);
        builder.Property(value => value.Source).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(value => new { value.SubscriptionId, value.Capability }).IsUnique();
    }
}
