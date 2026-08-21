using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class DesignerCodeWorkspaceVersionConfiguration : IEntityTypeConfiguration<DesignerCodeWorkspaceVersion>
{
    public void Configure(EntityTypeBuilder<DesignerCodeWorkspaceVersion> builder)
    {
        builder.ToTable("designer_code_workspace_versions", DatabaseSchemas.Designer);
        builder.HasKey(value => value.Id);
        builder.HasOne<DesignerCodeWorkspace>().WithMany().HasForeignKey(value => value.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DesignerTemplate>().WithMany().HasForeignKey(value => value.TemplateId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DesignerTemplateVersion>().WithMany().HasForeignKey(value => value.TemplateVersionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PxaIdentityUser>().WithMany().HasForeignKey(value => value.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.JsonDraft).HasColumnType("text").IsRequired();
        builder.Property(value => value.CSharpModelDraft).HasColumnType("text").IsRequired();
        builder.Property(value => value.CSharpPdfDraft).HasColumnType("text").IsRequired();
        builder.Property(value => value.CSharpBase64Draft).HasColumnType("text").IsRequired();
        builder.Property(value => value.CanonicalDesignJson).HasColumnType("jsonb").IsRequired();
        builder.Property(value => value.SourceMapJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(value => value.TemplateVersionId).IsUnique();
        builder.HasIndex(value => new { value.OrganizationId, value.CreatedAt });
    }
}
