using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class LegalDocumentVersionConfiguration : IEntityTypeConfiguration<LegalDocumentVersion>
{
    public void Configure(EntityTypeBuilder<LegalDocumentVersion> builder)
    {
        builder.ToTable("legal_document_versions", DatabaseSchemas.Administration);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Version).HasMaxLength(64).IsRequired();
        builder.Property(value => value.Locale).HasMaxLength(16).IsRequired();
        builder.Property(value => value.Audience).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(value => value.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(value => value.SourceMarkdown).HasColumnType("text").IsRequired();
        builder.Property(value => value.RenderedHtml).HasColumnType("text").IsRequired();
        builder.Property(value => value.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(value => value.ChangeSummary).HasMaxLength(2000);
        builder.HasOne<LegalDocument>().WithMany().HasForeignKey(value => value.LegalDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PxaIdentityUser>().WithMany().HasForeignKey(value => value.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PxaIdentityUser>().WithMany().HasForeignKey(value => value.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PxaIdentityUser>().WithMany().HasForeignKey(value => value.PublishedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LegalDocumentVersion>().WithMany().HasForeignKey(value => value.PreviousVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => new { value.LegalDocumentId, value.Locale, value.Audience, value.Version })
            .IsUnique();
        builder.HasIndex(value => new { value.LegalDocumentId, value.Locale, value.Audience, value.Status, value.EffectiveAt });
        builder.HasIndex(value => value.ContentHash);
    }
}
