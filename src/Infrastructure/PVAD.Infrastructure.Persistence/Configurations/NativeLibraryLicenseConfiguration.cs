using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence.Identity;

namespace PVAD.Infrastructure.Persistence.Configurations;

internal sealed class NativeLibraryLicenseConfiguration : IEntityTypeConfiguration<NativeLibraryLicense>
{
    public void Configure(EntityTypeBuilder<NativeLibraryLicense> builder)
    {
        builder.ToTable("native_library_licenses", DatabaseSchemas.Administration);
        builder.HasKey(value => value.Id);
        builder.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PvadIdentityUser>().WithMany().HasForeignKey(value => value.IssuedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AuditEvent>().WithMany().HasForeignKey(value => value.AuditEventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<NativeLibraryLicense>().WithMany().HasForeignKey(value => value.PreviousLicenseId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.ArtifactJson).IsRequired().HasMaxLength(65536);
        builder.Property(value => value.CustomerReference).IsRequired().HasMaxLength(160);
        builder.Property(value => value.LicenseModel).IsRequired().HasMaxLength(32);
        builder.Property(value => value.RequestSha256).IsRequired().HasMaxLength(64).IsFixedLength();
        builder.Property(value => value.ArtifactSha256).IsRequired().HasMaxLength(64).IsFixedLength();
        builder.HasIndex(value => value.AuditEventId).IsUnique();
        builder.HasIndex(value => new { value.IssuedByUserId, value.RequestId }).IsUnique();
        builder.HasIndex(value => value.PreviousLicenseId).IsUnique();
        builder.HasIndex(value => new { value.OrganizationId, value.IssuedAt });
    }
}
