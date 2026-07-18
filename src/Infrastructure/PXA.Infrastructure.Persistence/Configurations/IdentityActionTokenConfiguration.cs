using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class IdentityActionTokenConfiguration : IEntityTypeConfiguration<IdentityActionToken>
{
    public void Configure(EntityTypeBuilder<IdentityActionToken> builder)
    {
        builder.ToTable("identity_action_tokens", DatabaseSchemas.Administration);
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Purpose).HasMaxLength(64).IsRequired();
        builder.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(token => token.RecipientEmail).HasMaxLength(320).IsRequired();
        builder.Property(token => token.MetadataJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => new { token.UserId, token.Purpose, token.ExpiresAt });
        builder.HasOne<PxaIdentityUser>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Organization>().WithMany().HasForeignKey(token => token.OrganizationId).OnDelete(DeleteBehavior.Cascade);
    }
}
