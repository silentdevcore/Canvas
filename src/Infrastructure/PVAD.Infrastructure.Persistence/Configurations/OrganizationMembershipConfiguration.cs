using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence.Identity;

namespace PVAD.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.ToTable("organization_memberships", DatabaseSchemas.Administration);
        builder.HasKey(membership => membership.Id);

        builder.Property(membership => membership.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(membership => membership.CreatedAt).IsRequired();
        builder.Property(membership => membership.UpdatedAt).IsRequired();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(membership => membership.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<PvadIdentityUser>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(membership => new { membership.OrganizationId, membership.UserId })
            .IsUnique();
        builder.HasIndex(membership => membership.UserId);
    }
}
