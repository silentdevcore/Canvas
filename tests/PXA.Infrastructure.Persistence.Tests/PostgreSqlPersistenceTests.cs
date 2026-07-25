using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;
using Testcontainers.PostgreSql;

namespace PXA.Infrastructure.Persistence.Tests;

public sealed class PostgreSqlPersistenceTests
{
    [PostgreSqlFact]
    public async Task Migration_persists_an_identity_user_and_organization_membership()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<PxaDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;

        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using (var context = new PxaDbContext(options))
        {
            await context.Database.MigrateAsync();

            context.Users.Add(new PxaIdentityUser
            {
                Id = userId,
                UserName = "persistence-test",
                NormalizedUserName = "PERSISTENCE-TEST",
                Email = "persistence-test@pxa.local",
                NormalizedEmail = "PERSISTENCE-TEST@PXA.LOCAL",
                DisplayName = "Persistence Test",
            });
            context.Organizations.Add(new Organization
            {
                Id = organizationId,
                Name = "Persistence Test Organization",
                Slug = "persistence-test",
            });
            context.OrganizationMemberships.Add(new OrganizationMembership
            {
                OrganizationId = organizationId,
                UserId = userId,
            });

            await context.SaveChangesAsync();
        }

        await using (var verification = new PxaDbContext(options))
        {
            var membership = await verification.OrganizationMemberships.SingleAsync();
            Assert.Equal(organizationId, membership.OrganizationId);
            Assert.Equal(userId, membership.UserId);
        }
    }

    [PostgreSqlFact]
    public async Task Migration_persists_tenant_scoped_job_and_object_metadata()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<PxaDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var objectId = Guid.NewGuid();

        await using (var context = new PxaDbContext(options))
        {
            await context.Database.MigrateAsync();
            context.Users.Add(new PxaIdentityUser
            {
                Id = userId,
                UserName = "job-test",
                NormalizedUserName = "JOB-TEST",
                Email = "job-test@pxa.local",
                NormalizedEmail = "JOB-TEST@PXA.LOCAL",
                DisplayName = "Job Test",
            });
            context.Organizations.Add(new Organization
            {
                Id = organizationId,
                Name = "Job Test Organization",
                Slug = "job-test",
            });
            context.StoredObjects.Add(new PxaStoredObject
            {
                Id = objectId,
                OrganizationId = organizationId,
                CreatedByUserId = userId,
                ObjectKey = $"{organizationId:N}/{objectId:N}",
                Purpose = "job-result",
                ContentType = "application/pdf",
                Length = 3,
                Checksum = new string('a', 64),
            });
            context.BackgroundJobs.Add(new PxaBackgroundJob
            {
                OrganizationId = organizationId,
                CreatedByUserId = userId,
                Type = "test",
                PayloadJson = "{}",
                Status = PxaBackgroundJobStatus.Completed,
                ResultObjectId = objectId,
            });
            await context.SaveChangesAsync();
        }

        await using var verification = new PxaDbContext(options);
        var job = await verification.BackgroundJobs.SingleAsync();
        Assert.Equal(organizationId, job.OrganizationId);
        Assert.Equal(objectId, job.ResultObjectId);
        Assert.Single(await verification.StoredObjects.ToListAsync());
    }
}
