using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class DesignerElementFavoriteConfiguration : IEntityTypeConfiguration<DesignerElementFavorite>
{
    public void Configure(EntityTypeBuilder<DesignerElementFavorite> builder)
    {
        builder.ToTable("designer_element_favorites", DatabaseSchemas.Designer);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.ElementType).HasMaxLength(80).IsRequired();
        builder.Property(value => value.CreatedAt).IsRequired();
        builder.HasOne<PxaIdentityUser>().WithMany().HasForeignKey(value => value.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(value => new { value.UserId, value.ElementType }).IsUnique();
    }
}
