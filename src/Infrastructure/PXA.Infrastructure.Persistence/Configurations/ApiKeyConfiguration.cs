using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys", DatabaseSchemas.Administration);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Name).HasMaxLength(160).IsRequired();
        builder.Property(value => value.Prefix).HasMaxLength(32).IsRequired();
        builder.Property(value => value.SecretHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(value => value.CreatedAt).IsRequired();
        builder.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ServiceAccount>().WithMany().HasForeignKey(value => value.ServiceAccountId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(value => value.SecretHash).IsUnique();
        builder.HasIndex(value => value.Prefix);
        builder.HasIndex(value => new { value.OrganizationId, value.ServiceAccountId, value.RevokedAt });
    }
}
