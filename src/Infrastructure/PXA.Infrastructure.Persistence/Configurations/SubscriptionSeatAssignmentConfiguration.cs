using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class SubscriptionSeatAssignmentConfiguration : IEntityTypeConfiguration<SubscriptionSeatAssignment>
{
    public void Configure(EntityTypeBuilder<SubscriptionSeatAssignment> builder)
    {
        builder.ToTable("subscription_seat_assignments", DatabaseSchemas.Administration);
        builder.HasKey(value => value.Id);
        builder.HasOne<OrganizationSubscription>().WithMany().HasForeignKey(value => value.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<OrganizationMembership>().WithMany().HasForeignKey(value => value.OrganizationMembershipId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => new { value.SubscriptionId, value.OrganizationMembershipId }).IsUnique();
    }
}
