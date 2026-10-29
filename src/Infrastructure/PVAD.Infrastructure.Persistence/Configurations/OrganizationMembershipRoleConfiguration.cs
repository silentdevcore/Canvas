using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence.Identity;

namespace PVAD.Infrastructure.Persistence.Configurations;

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

        builder.HasOne<PvadIdentityRole>()
            .WithMany()
            .HasForeignKey(value => value.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PvadIdentityUser>()
            .WithMany()
            .HasForeignKey(value => value.AssignedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(value => new { value.OrganizationMembershipId, value.RoleId })
            .IsUnique();
        builder.HasIndex(value => value.RoleId);
    }
}
