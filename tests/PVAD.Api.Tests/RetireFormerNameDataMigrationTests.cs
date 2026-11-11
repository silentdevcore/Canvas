using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace PVAD.Api.Tests;

/// <summary>The hard-cut data migration changes only exact former diagnostic codes in code fields.</summary>
public sealed class RetireFormerNameDataMigrationTests
{
    private const string Before = "20260927050514_AddEInvoiceValidationRecords";
    private const string Migration = "20261109201328_RetireFormerNameData";

    private static readonly (string Diagnostics, string? Failure)[] Inputs =
    [
        ("""["PXAPDFTRUST010", "PXAPDFTRUST999", "see PXAPDFTRUST010", 42, null, {"Code": "PXAPDFTRUST010"}]""", "PXAPDFTRUST010"),
        ("""{"fidelity": "PXA-report.pxap", "diagnosticCodes": ["PXACODE042", "CANMIG100"], "nested": {"diagnosticCodes": ["PXACODE042"]}}""", "PXACODE042"),
        ("""[{"Code": "PXAMIG001", "Severity": "Error", "Message": "Source PXAMIG001 in PXA-Invoice.cs", "PXAMIG001": "key"}]""", "Failed reading PXA-report.pdf: PXACODE042"),
        ("""{"diagnosticCodes": "PXACODE042"}""", null),
        ("""[]""", "PXACODE042 "),
    ];

    [PostgreSqlFact]
    public async Task Up_changes_only_exact_codes_in_code_fields_and_down_restores_the_originals()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<PvadDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        var ids = await SeedAsync(options, Inputs);
        await MigrateAsync(options, Migration);

        var migrated = await ReadAsync(options, ids);
        Assert.Equal("""["PVADPDFTRUST010", "PXAPDFTRUST999", "see PXAPDFTRUST010", 42, null, {"Code": "PVADPDFTRUST010"}]""", migrated[0].Diagnostics);
        Assert.Equal("PVADPDFTRUST010", migrated[0].Failure);
        Assert.Equal("""{"nested": {"diagnosticCodes": ["PXACODE042"]}, "fidelity": "PXA-report.pxap", "diagnosticCodes": ["PVADCODE042", "CANMIG100"]}""", migrated[1].Diagnostics);
        Assert.Equal("PVADCODE042", migrated[1].Failure);
        Assert.Equal("""[{"Code": "PVADMIG001", "Message": "Source PXAMIG001 in PXA-Invoice.cs", "Severity": "Error", "PXAMIG001": "key"}]""", migrated[2].Diagnostics);
        Assert.Equal("Failed reading PXA-report.pdf: PXACODE042", migrated[2].Failure);
        Assert.Equal("""{"diagnosticCodes": "PXACODE042"}""", migrated[3].Diagnostics);
        Assert.Null(migrated[3].Failure);
        Assert.Equal("[]", migrated[4].Diagnostics);
        Assert.Equal("PXACODE042 ", migrated[4].Failure);
        Assert.False(await BackupTableExistsAsync(options), "Up must not keep a copy of job content outside job retention.");

        await MigrateAsync(options, Before);
        var restored = await ReadAsync(options, ids);
        for (var index = 0; index < Inputs.Length; index++)
        {
            AssertJsonEqual(Inputs[index].Diagnostics, restored[index].Diagnostics);
            Assert.Equal(Inputs[index].Failure, restored[index].Failure);
        }

        await MigrateAsync(options, Migration);
        Assert.Equal(migrated, await ReadAsync(options, ids));
    }

    [PostgreSqlFact]
    public async Task Down_keeps_newer_results_and_never_recreates_purged_or_deleted_content()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<PvadDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        var ids = await SeedAsync(options,
        [
            ("""["PVADCODE042", "PXACODE041"]""", "PVADCODE042"), // already current before Up
            ("""["PXACODE010"]""", "PXACODE010"),                 // processed again after Up
            ("""[]""", "PXACODE020"),                             // retried after Up
            ("""["PXACODE030"]""", "PXACODE030"),                 // content purged after Up
            ("""["PXACODE031"]""", "PXACODE031"),                 // deleted after Up
        ]);
        await MigrateAsync(options, Migration);
        var migrated = await ReadAsync(options, ids);
        Assert.Equal(("""["PVADCODE042", "PVADCODE041"]""", "PVADCODE042"), migrated[0]);

        await using (var context = new PvadDbContext(options))
        {
            await context.Database.ExecuteSqlInterpolatedAsync($$"""
                UPDATE administration.background_jobs SET "DiagnosticsJson" = '[{"Code": "PVADCODE011", "Message": "newer result"}]'::jsonb, "FailureReason" = NULL WHERE "Id" = {{ids[1]}};
                UPDATE administration.background_jobs SET "DiagnosticsJson" = '["PVADCODE021"]'::jsonb, "FailureReason" = 'PVADCODE021', "Attempts" = 2 WHERE "Id" = {{ids[2]}};
                UPDATE administration.background_jobs SET "DiagnosticsJson" = NULL, "FailureReason" = NULL WHERE "Id" = {{ids[3]}};
                DELETE FROM administration.background_jobs WHERE "Id" = {{ids[4]}};
                """);
        }

        await MigrateAsync(options, Before);
        var rolledBack = await ReadAsync(options, ids[..4]);
        // Documented limitation: a code that was already current before Up also returns to its former value.
        Assert.Equal(("""["PXACODE042", "PXACODE041"]""", "PXACODE042"), rolledBack[0]);
        Assert.Equal(("""[{"Code": "PXACODE011", "Message": "newer result"}]""", null), rolledBack[1]);
        Assert.Equal(("""["PXACODE021"]""", "PXACODE021"), rolledBack[2]);
        Assert.Equal((null, null), rolledBack[3]);
        await using (var context = new PvadDbContext(options))
            Assert.False(await context.BackgroundJobs.AnyAsync(job => job.Id == ids[4]));

        await MigrateAsync(options, Migration);
        Assert.Equal(("""["PVADCODE042", "PVADCODE041"]""", "PVADCODE042"), (await ReadAsync(options, ids[..1]))[0]);
    }

    [PostgreSqlFact]
    public async Task Follow_up_migration_drops_a_job_backup_left_by_an_unreleased_revision()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<PvadDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await MigrateAsync(options, Migration);
        await using (var context = new PvadDbContext(options))
            await context.Database.ExecuteSqlRawAsync("""
                CREATE TABLE administration.former_name_job_backup ("JobId" uuid PRIMARY KEY, "DiagnosticsJson" jsonb NULL, "FailureReason" character varying NULL);
                INSERT INTO administration.former_name_job_backup VALUES (gen_random_uuid(), '["PXACODE042"]', 'PXACODE042');
                """);
        await MigrateAsync(options, DropBackup);
        Assert.False(await BackupTableExistsAsync(options));
        await MigrateAsync(options, Migration);
        Assert.False(await BackupTableExistsAsync(options), "Rollback must not recreate retained job content.");
    }

    private const string DropBackup = "20261111100000_DropFormerNameJobBackup";

    private static async Task MigrateAsync(DbContextOptions<PvadDbContext> options, string target)
    {
        await using var context = new PvadDbContext(options);
        await context.GetService<IMigrator>().MigrateAsync(target);
    }

    private static async Task<List<Guid>> SeedAsync(DbContextOptions<PvadDbContext> options, (string Diagnostics, string? Failure)[] inputs)
    {
        var ids = new List<Guid>();
        await using var context = new PvadDbContext(options);
        await context.GetService<IMigrator>().MigrateAsync(Before);
        var organization = new Organization { Name = "Migration Tenant", Slug = "migration-tenant" };
        var user = new PVAD.Infrastructure.Persistence.Identity.PvadIdentityUser
        {
            Id = Guid.NewGuid(), DisplayName = "Migration Actor", UserName = "migration@example.test",
            NormalizedUserName = "MIGRATION@EXAMPLE.TEST", Email = "migration@example.test",
            NormalizedEmail = "MIGRATION@EXAMPLE.TEST", SecurityStamp = Guid.NewGuid().ToString("N"),
        };
        context.Organizations.Add(organization);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        foreach (var (diagnostics, failure) in inputs)
        {
            var id = Guid.NewGuid();
            ids.Add(id);
            await context.Database.ExecuteSqlInterpolatedAsync($$"""
                INSERT INTO administration.background_jobs
                    ("Id", "OrganizationId", "CreatedByUserId", "Type", "PayloadJson", "Status", "Attempts",
                     "MaximumAttempts", "ScheduledAt", "ProgressPercent", "DiagnosticsJson", "FailureReason",
                     "RetentionMode", "ExpiresAt", "MetadataExpiresAt", "CreatedAt", "UpdatedAt")
                VALUES ({{id}}, {{organization.Id}}, {{user.Id}}, 'synthetic', '{}'::jsonb, 'Completed', 1,
                     3, now(), 100, {{diagnostics}}::jsonb, {{failure}}, 'Transient', now(), now(), now(), now())
                """);
        }
        return ids;
    }

    private static async Task<bool> BackupTableExistsAsync(DbContextOptions<PvadDbContext> options)
    {
        await using var context = new PvadDbContext(options);
        return await context.Database.SqlQueryRaw<bool>(
            "SELECT to_regclass('administration.former_name_job_backup') IS NOT NULL AS \"Value\"").SingleAsync();
    }

    private static void AssertJsonEqual(string expected, string? actual) =>
        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(
            System.Text.Json.Nodes.JsonNode.Parse(expected), System.Text.Json.Nodes.JsonNode.Parse(actual!)), expected);

    private static async Task<List<(string? Diagnostics, string? Failure)>> ReadAsync(
        DbContextOptions<PvadDbContext> options, IReadOnlyList<Guid> ids)
    {
        await using var context = new PvadDbContext(options);
        var rows = new List<(string?, string?)>();
        foreach (var id in ids)
        {
            var job = await context.BackgroundJobs.AsNoTracking().SingleAsync(value => value.Id == id);
            rows.Add((job.DiagnosticsJson, job.FailureReason));
        }
        return rows;
    }
}
