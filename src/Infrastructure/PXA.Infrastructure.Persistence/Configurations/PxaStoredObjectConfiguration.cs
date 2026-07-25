using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class PxaStoredObjectConfiguration : IEntityTypeConfiguration<PxaStoredObject>
{
    public void Configure(EntityTypeBuilder<PxaStoredObject> builder)
    {
        builder.ToTable("stored_objects", DatabaseSchemas.Administration);
        builder.HasKey(value => value.Id);
        builder.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PxaIdentityUser>().WithMany().HasForeignKey(value => value.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.ObjectKey).HasMaxLength(300).IsRequired();
        builder.Property(value => value.Purpose).HasMaxLength(100).IsRequired();
        builder.Property(value => value.ContentType).HasMaxLength(200).IsRequired();
        builder.Property(value => value.FileName).HasMaxLength(255);
        builder.Property(value => value.Checksum).HasMaxLength(64).IsRequired();
        builder.Property(value => value.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.HasIndex(value => value.ObjectKey).IsUnique();
        builder.HasIndex(value => new { value.OrganizationId, value.Purpose, value.CreatedAt });
        builder.HasIndex(value => new { value.OrganizationId, value.Status });
    }
}
