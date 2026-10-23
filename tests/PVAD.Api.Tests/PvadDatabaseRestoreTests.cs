using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Infrastructure.Persistence.Identity;
using Testcontainers.PostgreSql;

namespace PVAD.Api.Tests;

public sealed class PvadDatabaseRestoreTests
{
    [PostgreSqlFact]
    public async Task Dump_restore_preserves_representative_identity_template_legal_and_license_records()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        var sourceOptions = new DbContextOptionsBuilder<PvadDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options;
        await using var source = new PvadDbContext(sourceOptions);
        await source.Database.MigrateAsync();
        var organization = new Organization { Name = "Synthetic restore", Slug = "synthetic-restore" };
        var user = new PvadIdentityUser
        {
            Id = Guid.NewGuid(), DisplayName = "Synthetic restore owner",
            UserName = "restore@example.test", Email = "restore@example.test", EmailConfirmed = true,
        };
        var subscription = new OrganizationSubscription
        {
            OrganizationId = organization.Id, Edition = SubscriptionEdition.Enterprise,
            AccountType = SubscriptionAccountType.Company, Status = SubscriptionStatus.Active,
            DeploymentMode = SubscriptionDeploymentMode.OnPremise,
        };
        const string draft = "{\"template\":{\"name\":\"Synthetic restore\",\"pages\":[]}}";
        var template = new DesignerTemplate
        {
            OrganizationId = organization.Id, CreatedByUserId = user.Id, UpdatedByUserId = user.Id,
            Name = "Synthetic restore", DraftJson = draft,
            DraftChecksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(draft))),
            SchemaVersion = "1.0", DesignerVersion = "1.1.0", Revision = 7,
        };
        var document = new LegalDocument
        {
            Key = "synthetic-terms", DisplayName = "Synthetic terms",
            Type = LegalDocumentType.TermsAndConditions, CreatedByUserId = user.Id,
        };
        var version = new LegalDocumentVersion
        {
            LegalDocumentId = document.Id, Version = "synthetic-1", Locale = "en",
            SourceMarkdown = "Synthetic terms", RenderedHtml = "<p>Synthetic terms</p>",
            ContentHash = new string('a', 64), Status = LegalDocumentStatus.Published,
            CreatedByUserId = user.Id,
        };
        var acceptance = new LegalAcceptanceEvent
        {
            UserId = user.Id, OrganizationId = organization.Id, LegalDocumentVersionId = version.Id,
            DocumentType = "terms", Decision = "accepted", ContentHash = version.ContentHash,
            Locale = "en", Source = "synthetic-restore-test",
        };
        const string envelope = "{\"customer\":\"synthetic-restore\"}";
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signature = Convert.ToBase64String(signingKey.SignData(Encoding.UTF8.GetBytes(envelope), HashAlgorithmName.SHA256));
        var license = new OfflineLicense
        {
            OrganizationId = organization.Id, SubscriptionId = subscription.Id,
            LicenseNumber = "synthetic-restore-license", EnvelopeJson = envelope,
            Signature = signature, KeyId = "synthetic-test-key", Algorithm = "ES256",
            ValidFrom = DateTimeOffset.UtcNow, ValidUntil = DateTimeOffset.UtcNow.AddDays(1),
            IssuedByUserId = user.Id,
        };
        var audit = new AuditEvent
        {
            Action = "synthetic.restore.seed", TargetType = "organization",
            TargetId = organization.Id.ToString(), Outcome = "completed",
        };
        source.AddRange(organization, user, subscription, template, document, version, acceptance, license, audit);
        await source.SaveChangesAsync();
        await source.Entry(template).ReloadAsync();
        var migrations = (await source.Database.GetAppliedMigrationsAsync()).ToArray();

        foreach (var command in new[]
        {
            new[] { "pg_dump", "-U", "postgres", "-d", "postgres", "-Fc", "-f", "/tmp/pvad-restore.dump" },
            new[] { "createdb", "-U", "postgres", "pvad_restored" },
            new[] { "pg_restore", "-U", "postgres", "-d", "pvad_restored", "--exit-on-error", "/tmp/pvad-restore.dump" },
        })
        {
            var result = await postgres.ExecAsync(command);
            Assert.True(result.ExitCode == 0, result.Stderr);
        }

        var targetConnection = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString()) { Database = "pvad_restored" };
        await using var target = new PvadDbContext(new DbContextOptionsBuilder<PvadDbContext>()
            .UseNpgsql(targetConnection.ConnectionString).Options);
        Assert.Empty(await target.Database.GetPendingMigrationsAsync());
        Assert.Equal(migrations, await target.Database.GetAppliedMigrationsAsync());
        Assert.Equal(user.Email, (await target.Users.SingleAsync()).Email);
        Assert.Equal(organization.Id, (await target.Organizations.SingleAsync()).Id);
        var restoredTemplate = await target.DesignerTemplates.SingleAsync();
        Assert.Equal(template.Id, restoredTemplate.Id);
        Assert.Equal(template.Revision, restoredTemplate.Revision);
        Assert.Equal(template.DraftJson, restoredTemplate.DraftJson);
        Assert.Equal(template.DraftChecksum, restoredTemplate.DraftChecksum);
        Assert.Equal(version.ContentHash, (await target.LegalDocumentVersions.SingleAsync()).ContentHash);
        Assert.Equal(acceptance.Id, (await target.LegalAcceptanceEvents.SingleAsync()).Id);
        var restoredLicense = await target.OfflineLicenses.SingleAsync();
        Assert.Equal(envelope, restoredLicense.EnvelopeJson);
        Assert.Equal(signature, restoredLicense.Signature);
        Assert.True(signingKey.VerifyData(Encoding.UTF8.GetBytes(restoredLicense.EnvelopeJson),
            Convert.FromBase64String(restoredLicense.Signature), HashAlgorithmName.SHA256));
        Assert.Equal(audit.Id, (await target.AuditEvents.SingleAsync()).Id);
        var update = await Assert.ThrowsAsync<PostgresException>(() => target.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE administration.audit_events SET "Outcome" = 'changed' WHERE "Id" = {audit.Id}"""));
        Assert.Equal(PostgresErrorCodes.ObjectNotInPrerequisiteState, update.SqlState);
    }
}
