using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence.Identity;

namespace PVAD.Infrastructure.Persistence.Configurations;

internal sealed class NativeLicenseRegistrationJobConfiguration : IEntityTypeConfiguration<NativeLicenseRegistrationJob>
{
    public void Configure(EntityTypeBuilder<NativeLicenseRegistrationJob> builder)
    {
        builder.ToTable("native_license_registration_jobs", DatabaseSchemas.Administration, table =>
            table.HasCheckConstraint("CK_native_registration_single_owner", "(\"OrganizationId\" IS NOT NULL) <> (\"OwnerUserId\" IS NOT NULL)"));
        builder.HasKey(value => value.Id);
        builder.Property(value => value.OwnerKey).IsRequired().HasMaxLength(80);
        builder.Property(value => value.Status).HasConversion<string>().HasMaxLength(24);
        builder.Property(value => value.AccountType).HasConversion<string>().HasMaxLength(32);
        builder.Property(value => value.ContractJson).HasMaxLength(4096);
        builder.Property(value => value.FailureCode).HasMaxLength(100);
        builder.HasIndex(value => value.OwnerKey).IsUnique();
        builder.HasIndex(value => new { value.Status, value.ScheduledAt });
        builder.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Organization>().WithMany().HasForeignKey(value => value.SourceOrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrganizationSubscription>().WithMany().HasForeignKey(value => value.SourceSubscriptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PvadIdentityUser>().WithMany().HasForeignKey(value => value.SourceUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PvadIdentityUser>().WithMany().HasForeignKey(value => value.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<NativeLibraryLicense>().WithMany().HasForeignKey(value => value.LicenseId).OnDelete(DeleteBehavior.Restrict);
    }
}
