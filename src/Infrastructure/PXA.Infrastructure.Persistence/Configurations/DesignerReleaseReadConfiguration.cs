using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class DesignerReleaseReadConfiguration : IEntityTypeConfiguration<DesignerReleaseRead>
{
    public void Configure(EntityTypeBuilder<DesignerReleaseRead> builder)
    {
        builder.ToTable("designer_release_reads", DatabaseSchemas.Designer);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Version).HasMaxLength(64).IsRequired();
        builder.Property(value => value.ReadAt).IsRequired();
        builder.HasOne<PxaIdentityUser>().WithMany().HasForeignKey(value => value.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(value => new { value.UserId, value.Version }).IsUnique();
    }
}
