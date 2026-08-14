using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class PxaBackgroundJobConfiguration : IEntityTypeConfiguration<PxaBackgroundJob>
{
    public void Configure(EntityTypeBuilder<PxaBackgroundJob> builder)
    {
        builder.ToTable("background_jobs", DatabaseSchemas.Administration);
        builder.HasKey(value => value.Id);
        builder.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PxaIdentityUser>().WithMany().HasForeignKey(value => value.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PxaStoredObject>().WithMany().HasForeignKey(value => value.InputObjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PxaStoredObject>().WithMany().HasForeignKey(value => value.ResultObjectId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.Type).HasMaxLength(100).IsRequired();
        builder.Property(value => value.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(value => value.TraceParent).HasMaxLength(128);
        builder.Property(value => value.TraceState).HasMaxLength(512);
        builder.Property(value => value.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(value => value.RetentionMode).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(value => value.DiagnosticsJson).HasColumnType("jsonb");
        builder.Property(value => value.FailureReason).HasMaxLength(2000);
        builder.HasIndex(value => new { value.Status, value.ScheduledAt });
        builder.HasIndex(value => new { value.OrganizationId, value.CreatedAt });
        builder.HasIndex(value => value.LeaseExpiresAt);
        builder.HasIndex(value => value.ExpiresAt);
        builder.HasIndex(value => value.MetadataExpiresAt);
    }
}
