using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("user_sessions", DatabaseSchemas.Identity);
        builder.HasKey(session => session.Id);
        builder.Property(session => session.IpAddressHash).HasMaxLength(64).IsRequired();
        builder.Property(session => session.UserAgent).HasMaxLength(200).IsRequired();
        builder.Property(session => session.RevocationReason).HasMaxLength(100);

        builder.HasOne<PxaIdentityUser>()
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(session => session.OrganizationId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<PxaIdentityUser>()
            .WithMany()
            .HasForeignKey(session => session.RevokedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(session => new { session.UserId, session.RevokedAt, session.ExpiresAt });
        builder.HasIndex(session => new { session.OrganizationId, session.LastSeenAt });
    }
}
