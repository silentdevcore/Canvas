using Microsoft.EntityFrameworkCore;
using Npgsql;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace PXA.Api.Tests;

public sealed class AuditEventImmutabilityTests
{
    [PostgreSqlFact]
    public async Task PostgreSql_rejects_direct_audit_event_updates_and_deletes()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<PxaDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        var auditEvent = new AuditEvent
        {
            Action = "test.created",
            TargetType = "synthetic",
            TargetId = "audit-immutability",
            Outcome = "completed",
        };

        await using (var setupContext = new PxaDbContext(options))
        {
            await setupContext.Database.MigrateAsync();
            setupContext.AuditEvents.Add(auditEvent);
            await setupContext.SaveChangesAsync();
        }

        await using (var mutationContext = new PxaDbContext(options))
        {
            var updateError = await Assert.ThrowsAsync<PostgresException>(() =>
                mutationContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""UPDATE administration.audit_events SET "Outcome" = 'changed' WHERE "Id" = {auditEvent.Id}"""));
            Assert.Equal(PostgresErrorCodes.ObjectNotInPrerequisiteState, updateError.SqlState);
            Assert.Contains("append-only", updateError.MessageText, StringComparison.Ordinal);

            var deleteError = await Assert.ThrowsAsync<PostgresException>(() =>
                mutationContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""DELETE FROM administration.audit_events WHERE "Id" = {auditEvent.Id}"""));
            Assert.Equal(PostgresErrorCodes.ObjectNotInPrerequisiteState, deleteError.SqlState);
            Assert.Contains("append-only", deleteError.MessageText, StringComparison.Ordinal);
        }

        await using var assertionContext = new PxaDbContext(options);
        var persisted = await assertionContext.AuditEvents
            .AsNoTracking()
            .SingleAsync(value => value.Id == auditEvent.Id);
        Assert.Equal("completed", persisted.Outcome);
    }
}
