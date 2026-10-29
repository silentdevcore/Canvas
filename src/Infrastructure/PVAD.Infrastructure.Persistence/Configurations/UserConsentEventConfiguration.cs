using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence.Identity;

namespace PVAD.Infrastructure.Persistence.Configurations;

internal sealed class UserConsentEventConfiguration : IEntityTypeConfiguration<UserConsentEvent>
{
    public void Configure(EntityTypeBuilder<UserConsentEvent> builder)
    {
        builder.ToTable("user_consent_events", DatabaseSchemas.Identity);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.ConsentType).HasMaxLength(32).IsRequired();
        builder.Property(value => value.Decision).HasMaxLength(32).IsRequired();
        builder.Property(value => value.PolicyVersion).HasMaxLength(64);
        builder.Property(value => value.Source).HasMaxLength(64).IsRequired();
        builder.Property(value => value.CreatedAt).IsRequired();
        builder.HasOne<PvadIdentityUser>()
            .WithMany()
            .HasForeignKey(value => value.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(value => new { value.UserId, value.ConsentType, value.CreatedAt });
    }
}
