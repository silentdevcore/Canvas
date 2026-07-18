using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationMembershipRoleConfiguration
    : IEntityTypeConfiguration<OrganizationMembershipRole>
{
    public void Configure(EntityTypeBuilder<OrganizationMembershipRole> builder)
    {
        builder.ToTable("organization_membership_roles", DatabaseSchemas.Administration);
        builder.HasKey(value => value.Id);

        builder.Property(value => value.CreatedAt).IsRequired();

        builder.HasOne<OrganizationMembership>()
            .WithMany()
            .HasForeignKey(value => value.OrganizationMembershipId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<PxaIdentityRole>()
            .WithMany()
            .HasForeignKey(value => value.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PxaIdentityUser>()
            .WithMany()
            .HasForeignKey(value => value.AssignedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(value => new { value.OrganizationMembershipId, value.RoleId })
            .IsUnique();
        builder.HasIndex(value => value.RoleId);
    }
}
