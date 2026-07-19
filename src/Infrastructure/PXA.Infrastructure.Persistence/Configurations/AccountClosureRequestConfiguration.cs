using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class AccountClosureRequestConfiguration : IEntityTypeConfiguration<AccountClosureRequest>
{
    public void Configure(EntityTypeBuilder<AccountClosureRequest> builder)
    {
        builder.ToTable("account_closure_requests", DatabaseSchemas.Administration);
        builder.HasKey(value => value.Id);

        builder.Property(value => value.TargetType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(value => value.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(value => value.Reason).HasMaxLength(2000);
        builder.Property(value => value.RequestedAt).IsRequired();
        builder.Property(value => value.ScheduledPurgeAt).IsRequired();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(value => value.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PxaIdentityUser>()
            .WithMany()
            .HasForeignKey(value => value.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(value => new { value.TargetType, value.TargetId, value.Status });
        builder.HasIndex(value => value.OrganizationId);
    }
}
