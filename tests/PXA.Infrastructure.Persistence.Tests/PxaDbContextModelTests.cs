using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Tests;

public sealed class PxaDbContextModelTests
{
    [Fact]
    public void Model_uses_separate_identity_and_administration_schemas()
    {
        using var context = CreateContext();

        var organization = context.Model.FindEntityType(typeof(Organization));
        var membership = context.Model.FindEntityType(typeof(OrganizationMembership));
        var user = context.Model.FindEntityType(typeof(PxaIdentityUser));

        Assert.Equal(DatabaseSchemas.Administration, organization?.GetSchema());
        Assert.Equal("organizations", organization?.GetTableName());
        Assert.Equal(DatabaseSchemas.Administration, membership?.GetSchema());
        Assert.Equal("organization_memberships", membership?.GetTableName());
        Assert.Equal(DatabaseSchemas.Identity, user?.GetSchema());
        Assert.Equal("users", user?.GetTableName());
    }

    [Fact]
    public void Model_enforces_one_membership_per_user_and_organization()
    {
        using var context = CreateContext();
        var membership = context.Model.FindEntityType(typeof(OrganizationMembership));

        var uniqueIndex = Assert.Single(
            membership!.GetIndexes(),
            index => index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(OrganizationMembership.OrganizationId), nameof(OrganizationMembership.UserId)]));

        Assert.True(uniqueIndex.IsUnique);
    }

    private static PxaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PxaDbContext>()
            .UseNpgsql("Host=localhost;Database=pxa-model-tests;Username=pxa")
            .Options;

        return new PxaDbContext(options);
    }
}
