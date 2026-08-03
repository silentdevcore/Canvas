using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class LegalPublicationApprovalConfiguration : IEntityTypeConfiguration<LegalPublicationApproval>
{
    public void Configure(EntityTypeBuilder<LegalPublicationApproval> builder)
    {
        builder.ToTable("legal_publication_approvals", DatabaseSchemas.Administration);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Decision).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(value => value.Comment).HasMaxLength(2000);
        builder.Property(value => value.CreatedAt).IsRequired();
        builder.HasOne<LegalDocumentVersion>().WithMany().HasForeignKey(value => value.LegalDocumentVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PxaIdentityUser>().WithMany().HasForeignKey(value => value.ReviewerUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => new { value.LegalDocumentVersionId, value.CreatedAt });
    }
}
