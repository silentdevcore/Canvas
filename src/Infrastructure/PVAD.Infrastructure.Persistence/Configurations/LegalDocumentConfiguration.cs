using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence.Identity;

namespace PVAD.Infrastructure.Persistence.Configurations;

internal sealed class LegalDocumentConfiguration : IEntityTypeConfiguration<LegalDocument>
{
    public void Configure(EntityTypeBuilder<LegalDocument> builder)
    {
        builder.ToTable("legal_documents", DatabaseSchemas.Administration);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Type).HasConversion<string>().HasMaxLength(48).IsRequired();
        builder.Property(value => value.Key).HasMaxLength(80).IsRequired();
        builder.Property(value => value.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(value => value.CreatedAt).IsRequired();
        builder.HasOne<PvadIdentityUser>().WithMany().HasForeignKey(value => value.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => value.Key).IsUnique();
        builder.HasIndex(value => value.Type).IsUnique();
    }
}
