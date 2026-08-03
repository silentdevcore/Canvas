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
        var job = context.Model.FindEntityType(typeof(PxaBackgroundJob));
        var storedObject = context.Model.FindEntityType(typeof(PxaStoredObject));

        Assert.Equal("background_jobs", job?.GetTableName());
        Assert.Equal(DatabaseSchemas.Administration, job?.GetSchema());
        Assert.Contains(job!.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(PxaBackgroundJob.OrganizationId), nameof(PxaBackgroundJob.CreatedAt)]));

        Assert.Equal("stored_objects", storedObject?.GetTableName());
        Assert.Equal(DatabaseSchemas.Administration, storedObject?.GetSchema());
        Assert.Contains(storedObject!.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Single().Name == nameof(PxaStoredObject.ObjectKey));
        Assert.Contains(storedObject.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([
                    nameof(PxaStoredObject.OrganizationId),
                    nameof(PxaStoredObject.Purpose),
                    nameof(PxaStoredObject.CreatedAt),
                ]));
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

    private static PxaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PxaDbContext>()
            .UseNpgsql("Host=localhost;Database=pxa-model-tests;Username=pxa")
            .Options;

        return new PxaDbContext(options);
    }
}
