using Microsoft.EntityFrameworkCore;
using Npgsql;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace PVAD.Api.Tests;

/// <summary>
/// The database, not only the application, enforces that a stored validation verdict never
/// changes and can only leave the table through bounded retention.
/// </summary>
public sealed class EInvoiceValidationRecordImmutabilityTests
{
    [PostgreSqlFact]
    public async Task PostgreSql_rejects_updates_and_unguarded_deletes_but_allows_retention_purge()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<PvadDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;

        Guid recordId;
        await using (var setupContext = new PvadDbContext(options))
        {
            await setupContext.Database.MigrateAsync();
            var (organizationId, actorUserId) = await SeedTenantAsync(setupContext);
            var auditEvent = new AuditEvent
            {
                OrganizationId = organizationId,
                ActorUserId = actorUserId,
                Action = "pdf.e-invoice.validate",
                TargetType = "e-invoice-validation-record",
                TargetId = "immutability",
                Outcome = "succeeded",
            };
            var record = new EInvoiceValidationRecord
            {
                OrganizationId = organizationId,
                ActorUserId = actorUserId,
                AuditEventId = auditEvent.Id,
                Operation = "validate",
                DocumentSha256 = new string('a', 64),
                Standard = "FacturX",
                StandardVersion = "1.09.2",
                Profile = "BASIC",
                ConformanceStatus = "notEvaluated",
                DiagnosticsJson = "[]",
                RetainUntil = DateTimeOffset.UtcNow.AddDays(-1),
            };
            setupContext.AuditEvents.Add(auditEvent);
            setupContext.EInvoiceValidationRecords.Add(record);
            await setupContext.SaveChangesAsync();
            recordId = record.Id;
        }

        await using (var mutationContext = new PvadDbContext(options))
        {
            var updateError = await Assert.ThrowsAsync<PostgresException>(() =>
                mutationContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE administration.e_invoice_validation_records
                    SET "ConformanceStatus" = 'validated' WHERE "Id" = {recordId}
                    """));
            Assert.Equal(PostgresErrorCodes.ObjectNotInPrerequisiteState, updateError.SqlState);
            Assert.Contains("append-only", updateError.MessageText, StringComparison.Ordinal);

            var deleteError = await Assert.ThrowsAsync<PostgresException>(() =>
                mutationContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    DELETE FROM administration.e_invoice_validation_records WHERE "Id" = {recordId}
                    """));
            Assert.Equal(PostgresErrorCodes.ObjectNotInPrerequisiteState, deleteError.SqlState);
            Assert.Contains("bounded retention", deleteError.MessageText, StringComparison.Ordinal);
        }

        await using (var assertionContext = new PvadDbContext(options))
        {
            var persisted = await assertionContext.EInvoiceValidationRecords
                .AsNoTracking()
                .SingleAsync(value => value.Id == recordId);
            Assert.Equal("notEvaluated", persisted.ConformanceStatus);
        }

        await using (var retentionContext = new PvadDbContext(options))
        {
            await using var transaction = await retentionContext.Database.BeginTransactionAsync();
            await retentionContext.Database.ExecuteSqlRawAsync(
                "SET LOCAL pxa.e_invoice_retention_purge = 'on'");
            var removed = await retentionContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                DELETE FROM administration.e_invoice_validation_records WHERE "Id" = {recordId}
                """);
            await transaction.CommitAsync();
            Assert.Equal(1, removed);
        }

        await using var finalContext = new PvadDbContext(options);
        Assert.Empty(await finalContext.EInvoiceValidationRecords.AsNoTracking().ToArrayAsync());
    }

    private static async Task<(Guid OrganizationId, Guid ActorUserId)> SeedTenantAsync(PvadDbContext dbContext)
    {
        var organization = new Organization { Name = "Evidence Tenant", Slug = "evidence-tenant" };
        var user = new PVAD.Infrastructure.Persistence.Identity.PvadIdentityUser
        {
            Id = Guid.NewGuid(),
            DisplayName = "Evidence Actor",
            UserName = "evidence@example.test",
            NormalizedUserName = "EVIDENCE@EXAMPLE.TEST",
            Email = "evidence@example.test",
            NormalizedEmail = "EVIDENCE@EXAMPLE.TEST",
            SecurityStamp = Guid.NewGuid().ToString("N"),
        };
        dbContext.Organizations.Add(organization);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return (organization.Id, user.Id);
    }
}
