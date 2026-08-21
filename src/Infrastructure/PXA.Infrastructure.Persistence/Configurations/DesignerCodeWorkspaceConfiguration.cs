using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class DesignerCodeWorkspaceConfiguration : IEntityTypeConfiguration<DesignerCodeWorkspace>
{
    public void Configure(EntityTypeBuilder<DesignerCodeWorkspace> builder)
    {
        builder.ToTable("designer_code_workspaces", DatabaseSchemas.Designer);
        builder.HasKey(value => value.Id);
        builder.HasOne<DesignerTemplate>().WithMany().HasForeignKey(value => value.TemplateId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PxaIdentityUser>().WithMany().HasForeignKey(value => value.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.JsonDraft).HasColumnType("text").IsRequired();
        builder.Property(value => value.CSharpModelDraft).HasColumnType("text").IsRequired();
        builder.Property(value => value.CSharpPdfDraft).HasColumnType("text").IsRequired();
        builder.Property(value => value.CSharpBase64Draft).HasColumnType("text").IsRequired();
        builder.Property(value => value.CanonicalDesignJson).HasColumnType("jsonb").IsRequired();
        builder.Property(value => value.SourceMapJson).HasColumnType("jsonb").IsRequired();
        builder.Property(value => value.JsonChecksum).HasMaxLength(64).IsRequired();
        builder.Property(value => value.CSharpModelChecksum).HasMaxLength(64).IsRequired();
        builder.Property(value => value.CSharpPdfChecksum).HasMaxLength(64).IsRequired();
        builder.Property(value => value.CSharpBase64Checksum).HasMaxLength(64).IsRequired();
        builder.Property(value => value.CanonicalChecksum).HasMaxLength(64).IsRequired();
        builder.Property(value => value.Revision).IsConcurrencyToken();
        builder.HasIndex(value => value.TemplateId).IsUnique();
        builder.HasIndex(value => new { value.OrganizationId, value.UpdatedAt });
    }
}
