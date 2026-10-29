using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Infrastructure.Persistence.Identity;

namespace PVAD.Infrastructure.Persistence.Tests;

public sealed class PvadDbContextModelTests
{
    [Theory]
    [InlineData("pxa-retained")]
    [InlineData("pvad-new")]
    [InlineData("customer-owned")]
    public void Persistence_registration_preserves_explicit_database_and_login_names(string database)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPvadPersistence($"Host=localhost;Database={database};Username=retained-owner;Password=synthetic-not-a-real-secret");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var source = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
        var configured = new NpgsqlConnectionStringBuilder(source.ConnectionString);
        Assert.Equal(database, configured.Database);
        Assert.Equal("retained-owner", configured.Username);
        var context = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
        Assert.Equal(database, context.Database.GetDbConnection().Database);
        Assert.NotEmpty(context.Database.GetMigrations());
        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    [Fact]
    public void Model_uses_separate_identity_and_administration_schemas()
    {
        using var context = CreateContext();

        var organization = context.Model.FindEntityType(typeof(Organization));
        var membership = context.Model.FindEntityType(typeof(OrganizationMembership));
        var user = context.Model.FindEntityType(typeof(PvadIdentityUser));

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

    [Fact]
    public void Model_stores_element_favorites_per_user_without_an_organization_scope()
    {
        using var context = CreateContext();
        var favorite = context.Model.FindEntityType(typeof(DesignerElementFavorite));

        Assert.Equal(DatabaseSchemas.Designer, favorite?.GetSchema());
        Assert.Equal("designer_element_favorites", favorite?.GetTableName());
        Assert.Null(favorite?.FindProperty("OrganizationId"));
        Assert.Contains(favorite!.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(DesignerElementFavorite.UserId), nameof(DesignerElementFavorite.ElementType)]));
    }

    [Fact]
    public void Model_scopes_membership_roles_and_audit_events_to_administration()
    {
        using var context = CreateContext();

        var membershipRole = context.Model.FindEntityType(typeof(OrganizationMembershipRole));
        var auditEvent = context.Model.FindEntityType(typeof(AuditEvent));

        Assert.Equal(DatabaseSchemas.Administration, membershipRole?.GetSchema());
        Assert.Equal("organization_membership_roles", membershipRole?.GetTableName());
        Assert.Equal(DatabaseSchemas.Administration, auditEvent?.GetSchema());
        Assert.Equal("audit_events", auditEvent?.GetTableName());

        var uniqueIndex = Assert.Single(
            membershipRole!.GetIndexes(),
            index => index.Properties.Select(property => property.Name)
                .SequenceEqual([
                    nameof(OrganizationMembershipRole.OrganizationMembershipId),
                    nameof(OrganizationMembershipRole.RoleId),
                ]));
        Assert.True(uniqueIndex.IsUnique);

        var role = context.Model.FindEntityType(typeof(PvadIdentityRole));
        Assert.NotNull(role?.FindProperty(nameof(PvadIdentityRole.OrganizationId)));
        Assert.NotNull(role?.FindProperty(nameof(PvadIdentityRole.CustomKey)));
        Assert.NotNull(role?.FindProperty(nameof(PvadIdentityRole.DisplayName)));
        Assert.Contains(role!.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(PvadIdentityRole.OrganizationId), nameof(PvadIdentityRole.CustomKey),
            ]));
    }

    [Fact]
    public void Model_enforces_unique_action_tokens_and_mail_idempotency()
    {
        using var context = CreateContext();
        var actionToken = context.Model.FindEntityType(typeof(IdentityActionToken));
        var mailMessage = context.Model.FindEntityType(typeof(MailOutboxMessage));

        Assert.Equal("identity_action_tokens", actionToken?.GetTableName());
        Assert.Equal(DatabaseSchemas.Administration, actionToken?.GetSchema());
        Assert.Contains(actionToken!.GetIndexes(), index =>
            index.IsUnique && index.Properties.Single().Name == nameof(IdentityActionToken.TokenHash));

        Assert.Equal("mail_outbox", mailMessage?.GetTableName());
        Assert.Equal(DatabaseSchemas.Administration, mailMessage?.GetSchema());
        Assert.Contains(mailMessage!.GetIndexes(), index =>
            index.IsUnique && index.Properties.Single().Name == nameof(MailOutboxMessage.IdempotencyKey));
    }

    [Fact]
    public void Model_scopes_background_jobs_and_stored_objects_to_organizations()
    {
        using var context = CreateContext();
        var job = context.Model.FindEntityType(typeof(PvadBackgroundJob));
        var storedObject = context.Model.FindEntityType(typeof(PvadStoredObject));

        Assert.Equal("background_jobs", job?.GetTableName());
        Assert.Equal(DatabaseSchemas.Administration, job?.GetSchema());
        Assert.Contains(job!.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(PvadBackgroundJob.OrganizationId), nameof(PvadBackgroundJob.CreatedAt)]));

        Assert.Equal("stored_objects", storedObject?.GetTableName());
        Assert.Equal(DatabaseSchemas.Administration, storedObject?.GetSchema());
        Assert.Contains(storedObject!.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Single().Name == nameof(PvadStoredObject.ObjectKey));
        Assert.Contains(storedObject.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([
                    nameof(PvadStoredObject.OrganizationId),
                    nameof(PvadStoredObject.Purpose),
                    nameof(PvadStoredObject.CreatedAt),
                ]));
    }

    [Fact]
    public void Model_persists_immutable_pdf_trust_jobs_with_unique_audit_correlation()
    {
        using var context = CreateContext();
        var job = context.Model.FindEntityType(typeof(PdfTrustOperationJob));

        Assert.Equal("pdf_trust_operation_jobs", job?.GetTableName());
        Assert.Equal(DatabaseSchemas.Administration, job?.GetSchema());
        Assert.Contains(job!.GetIndexes(), index =>
            index.IsUnique && index.Properties.Single().Name == nameof(PdfTrustOperationJob.AuditEventId));
        Assert.Contains(job.GetIndexes(), index =>
            index.IsUnique && index.Properties.Single().Name == nameof(PdfTrustOperationJob.BackgroundJobId));
        Assert.Contains(job.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(PdfTrustOperationJob.OrganizationId), nameof(PdfTrustOperationJob.CreatedAt)]));
    }

    [Fact]
    public void Model_persists_legal_governance_and_evidence_in_the_expected_schemas()
    {
        using var context = CreateContext();
        var document = context.Model.FindEntityType(typeof(LegalDocument));
        var version = context.Model.FindEntityType(typeof(LegalDocumentVersion));
        var approval = context.Model.FindEntityType(typeof(LegalPublicationApproval));
        var acceptance = context.Model.FindEntityType(typeof(LegalAcceptanceEvent));

        Assert.Equal(DatabaseSchemas.Administration, document?.GetSchema());
        Assert.Equal("legal_documents", document?.GetTableName());
        Assert.Equal(DatabaseSchemas.Administration, version?.GetSchema());
        Assert.Equal("legal_document_versions", version?.GetTableName());
        Assert.Equal(DatabaseSchemas.Administration, approval?.GetSchema());
        Assert.Equal("legal_publication_approvals", approval?.GetTableName());
        Assert.Equal(DatabaseSchemas.Identity, acceptance?.GetSchema());
        Assert.Equal("legal_acceptance_events", acceptance?.GetTableName());
        Assert.Contains(document!.GetIndexes(), index =>
            index.IsUnique && index.Properties.Single().Name == nameof(LegalDocument.Key));
        Assert.Contains(document.GetIndexes(), index =>
            index.IsUnique && index.Properties.Single().Name == nameof(LegalDocument.Type));
        Assert.Contains(version!.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(LegalDocumentVersion.LegalDocumentId),
                nameof(LegalDocumentVersion.Locale),
                nameof(LegalDocumentVersion.Audience),
                nameof(LegalDocumentVersion.Version),
            ]));
        Assert.Contains(acceptance!.GetIndexes(), index =>
            index.IsUnique &&
            index.GetDatabaseName() == "UX_legal_acceptance_events_user_org_version" &&
            index.GetFilter() == "\"OrganizationId\" IS NOT NULL");
        Assert.Contains(acceptance.GetIndexes(), index =>
            index.IsUnique &&
            index.GetDatabaseName() == "UX_legal_acceptance_events_user_global_version" &&
            index.GetFilter() == "\"OrganizationId\" IS NULL");
    }

    private static PvadDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PvadDbContext>()
            .UseNpgsql("Host=localhost;Database=pxa-model-tests;Username=pxa")
            .Options;

        return new PvadDbContext(options);
    }
}
