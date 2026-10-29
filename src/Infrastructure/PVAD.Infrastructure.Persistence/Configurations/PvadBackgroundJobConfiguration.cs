using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence.Identity;

namespace PVAD.Infrastructure.Persistence.Configurations;

internal sealed class PvadBackgroundJobConfiguration : IEntityTypeConfiguration<PvadBackgroundJob>
{
    public void Configure(EntityTypeBuilder<PvadBackgroundJob> builder)
    {
        builder.ToTable("background_jobs", DatabaseSchemas.Administration);
        builder.HasKey(value => value.Id);
        builder.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PvadIdentityUser>().WithMany().HasForeignKey(value => value.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PvadStoredObject>().WithMany().HasForeignKey(value => value.InputObjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PvadStoredObject>().WithMany().HasForeignKey(value => value.ResultObjectId).OnDelete(DeleteBehavior.Restrict);
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
