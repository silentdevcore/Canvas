using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations", DatabaseSchemas.Administration);
        builder.HasKey(organization => organization.Id);

        builder.Property(organization => organization.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(organization => organization.Slug)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(organization => organization.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(organization => organization.CreatedAt).IsRequired();
        builder.Property(organization => organization.UpdatedAt).IsRequired();

        builder.HasIndex(organization => organization.Slug).IsUnique();
    }
}
