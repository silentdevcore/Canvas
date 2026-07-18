using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class OfflineLicenseConfiguration : IEntityTypeConfiguration<OfflineLicense>
{
    public void Configure(EntityTypeBuilder<OfflineLicense> builder)
    {
        builder.ToTable("offline_licenses", DatabaseSchemas.Administration);
        builder.HasKey(value => value.Id);
        builder.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrganizationSubscription>().WithMany().HasForeignKey(value => value.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.LicenseNumber).HasMaxLength(80).IsRequired();
        builder.Property(value => value.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(value => value.EnvelopeJson).IsRequired();
        builder.Property(value => value.Signature).HasMaxLength(512).IsRequired();
        builder.Property(value => value.KeyId).HasMaxLength(100).IsRequired();
        builder.Property(value => value.Algorithm).HasMaxLength(40).IsRequired();
        builder.Property(value => value.RevocationReason).HasMaxLength(500);
        builder.HasIndex(value => value.LicenseNumber).IsUnique();
        builder.HasIndex(value => new { value.OrganizationId, value.Status });
    }
}
