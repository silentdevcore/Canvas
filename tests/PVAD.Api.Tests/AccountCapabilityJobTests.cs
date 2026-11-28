using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Infrastructure.Persistence.Identity;
using PVAD.Licensing;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Jobs;
using Testcontainers.PostgreSql;

namespace PVAD.Api.Tests;

public sealed class AccountCapabilityJobTests
{
    [PostgreSqlFact]
    public async Task ExecutionRechecksDowngradedAccountsRevokedApprovalsAndCancelledSubscriptions()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<PvadDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var db = new PvadDbContext(options);
        await db.Database.MigrateAsync();
        foreach (var change in new[] { "private-account", "revoked-migration", "cancelled-subscription" })
        {
            var organizationId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            db.Users.Add(new PvadIdentityUser { Id = userId, UserName = userId.ToString(), DisplayName = "Synthetic job owner" });
            db.Organizations.Add(new Organization { Id = organizationId, Name = change, Slug = organizationId.ToString() });
            var subscription = new OrganizationSubscription
            {
                OrganizationId = organizationId, AccountType = SubscriptionAccountType.Company, Status = SubscriptionStatus.Active,
            };
            db.OrganizationSubscriptions.Add(subscription);
            var approval = new SubscriptionEntitlement
            {
                SubscriptionId = subscription.Id, Capability = "migration", Enabled = true,
                Source = EntitlementSource.NegotiatedOverride,
            };
            db.SubscriptionEntitlements.Add(approval);
            var input = new PvadStoredObject
            {
                OrganizationId = organizationId, CreatedByUserId = userId, ObjectKey = "never-read/" + Guid.NewGuid(),
                Purpose = "job-input", ContentType = "application/json", Length = 2, Checksum = new string('a', 64),
            };
            db.StoredObjects.Add(input);
            await db.SaveChangesAsync();
            var settings = Options.Create(new PvadJobOptions());
            var queue = new PvadJobQueue(db, new Tenant(userId, organizationId), settings);
            PvadBackgroundJob job;
            using (PvadAccountCapabilityContext.Enter(PvadLicenseAccountKind.Company, ["migration.pdf.code"]))
            {
                job = change == "revoked-migration"
                    ? await queue.EnqueueDocumentJobAsync(PvadJobQueue.CodeMigrationType, input.Id, new CodeMigrationJobPayload("synthetic"), default)
                    : await queue.EnqueueDocumentJobAsync(PvadJobQueue.DocumentExportType, input.Id, new DocumentExportJobPayload("png", null, null), default);
            }
            if (change == "private-account") subscription.AccountType = SubscriptionAccountType.IndividualDeveloper;
            if (change == "revoked-migration") approval.Enabled = false;
            if (change == "cancelled-subscription") subscription.Status = SubscriptionStatus.Cancelled;
            await db.SaveChangesAsync();

            // Null processing dependencies are deliberate sentinels: admission must
            // reject the job before storage, exporter or migration code is reached.
            var processor = new PvadJobProcessor(db, null!, [], null!, null!, null!, settings, NullLogger<PvadJobProcessor>.Instance);
            Assert.True(await processor.ProcessNextAsync(default));
            await db.Entry(job).ReloadAsync();
            Assert.Equal(PvadBackgroundJobStatus.DeadLetter, job.Status);
            Assert.Equal(1, job.Attempts);
            Assert.Null(job.ResultObjectId);
            Assert.Contains(change == "cancelled-subscription" ? "Account policy is unavailable" : "account does not permit", job.FailureReason);
        }
    }

    private sealed record Tenant(Guid CurrentUserId, Guid CurrentOrganizationId) : IPvadTenantContext
    {
        public Guid? UserId => CurrentUserId;
        public Guid? OrganizationId => CurrentOrganizationId;
    }
}
