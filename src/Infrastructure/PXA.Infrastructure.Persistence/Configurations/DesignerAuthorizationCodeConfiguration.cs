using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class DesignerAuthorizationCodeConfiguration
    : IEntityTypeConfiguration<DesignerAuthorizationCode>
{
    public void Configure(EntityTypeBuilder<DesignerAuthorizationCode> builder)
    {
        builder.ToTable("designer_authorization_codes", DatabaseSchemas.Identity);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.CodeHash).HasMaxLength(64).IsRequired();
        builder.Property(value => value.StateHash).HasMaxLength(64).IsRequired();
        builder.Property(value => value.PkceChallenge).HasMaxLength(128).IsRequired();
        builder.Property(value => value.DesignerOrigin).HasMaxLength(256).IsRequired();
        builder.Property(value => value.ReturnPath).HasMaxLength(2048).IsRequired();
        builder.HasIndex(value => value.CodeHash).IsUnique();
        builder.HasIndex(value => new { value.UserId, value.ExpiresAt, value.ConsumedAt });

        builder.HasOne<PxaIdentityUser>()
            .WithMany()
            .HasForeignKey(value => value.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(value => value.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserSession>()
            .WithMany()
            .HasForeignKey(value => value.SourceSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
