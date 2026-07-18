using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationSubscriptionConfiguration : IEntityTypeConfiguration<OrganizationSubscription>
{
    public void Configure(EntityTypeBuilder<OrganizationSubscription> builder)
    {
        builder.ToTable("subscriptions", DatabaseSchemas.Administration);
        builder.HasKey(value => value.Id);
        builder.HasIndex(value => value.OrganizationId).IsUnique();
        builder.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.Edition).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(value => value.AccountType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(value => value.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(value => value.BillingPeriod).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(value => value.DeploymentMode).HasConversion<string>().HasMaxLength(24).IsRequired();
    }
}
