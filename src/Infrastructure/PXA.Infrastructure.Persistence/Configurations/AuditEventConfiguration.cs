using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events", DatabaseSchemas.Administration);
        builder.HasKey(value => value.Id);

        builder.Property(value => value.Action).HasMaxLength(160).IsRequired();
        builder.Property(value => value.TargetType).HasMaxLength(100).IsRequired();
        builder.Property(value => value.TargetId).HasMaxLength(160).IsRequired();
        builder.Property(value => value.Outcome).HasMaxLength(32).IsRequired();
        builder.Property(value => value.DetailsJson).HasColumnType("jsonb");
        builder.Property(value => value.CreatedAt).IsRequired();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(value => value.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PxaIdentityUser>()
            .WithMany()
            .HasForeignKey(value => value.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(value => new { value.OrganizationId, value.CreatedAt });
        builder.HasIndex(value => value.ActorUserId);
        builder.HasIndex(value => new { value.TargetType, value.TargetId });
    }
}
