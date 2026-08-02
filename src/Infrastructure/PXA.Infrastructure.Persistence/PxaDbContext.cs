using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence;

public sealed class PxaDbContext
    : IdentityDbContext<PxaIdentityUser, PxaIdentityRole, Guid>
{
    public PxaDbContext(DbContextOptions<PxaDbContext> options)
        : base(options)
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();
    public DbSet<OrganizationMembershipRole> OrganizationMembershipRoles => Set<OrganizationMembershipRole>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<IdentityActionToken> IdentityActionTokens => Set<IdentityActionToken>();
    public DbSet<MailOutboxMessage> MailOutboxMessages => Set<MailOutboxMessage>();
    public DbSet<OrganizationSubscription> OrganizationSubscriptions => Set<OrganizationSubscription>();
    public DbSet<SubscriptionEntitlement> SubscriptionEntitlements => Set<SubscriptionEntitlement>();
    public DbSet<SubscriptionSeatAssignment> SubscriptionSeatAssignments => Set<SubscriptionSeatAssignment>();
    public DbSet<SubscriptionLifecycleEvent> SubscriptionLifecycleEvents => Set<SubscriptionLifecycleEvent>();
    public DbSet<SubscriptionUsageEvent> SubscriptionUsageEvents => Set<SubscriptionUsageEvent>();
    public DbSet<OfflineLicense> OfflineLicenses => Set<OfflineLicense>();
    public DbSet<ServiceAccount> ServiceAccounts => Set<ServiceAccount>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<DesignerAuthorizationCode> DesignerAuthorizationCodes => Set<DesignerAuthorizationCode>();
    public DbSet<DesignerTemplate> DesignerTemplates => Set<DesignerTemplate>();
    public DbSet<DesignerTemplateVersion> DesignerTemplateVersions => Set<DesignerTemplateVersion>();
    public DbSet<AccountClosureRequest> AccountClosureRequests => Set<AccountClosureRequest>();
    public DbSet<UserConsentEvent> UserConsentEvents => Set<UserConsentEvent>();
    public DbSet<PxaBackgroundJob> BackgroundJobs => Set<PxaBackgroundJob>();
    public DbSet<PxaStoredObject> StoredObjects => Set<PxaStoredObject>();
    public DbSet<DesignerFeaturePreference> DesignerFeaturePreferences => Set<DesignerFeaturePreference>();
    public DbSet<DesignerFeaturePolicy> DesignerFeaturePolicies => Set<DesignerFeaturePolicy>();
    public DbSet<DesignerReleaseRead> DesignerReleaseReads => Set<DesignerReleaseRead>();
    public DbSet<DesignerNotification> DesignerNotifications => Set<DesignerNotification>();
    public DbSet<DesignerNotificationState> DesignerNotificationStates => Set<DesignerNotificationState>();
    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();
    public DbSet<LegalDocumentVersion> LegalDocumentVersions => Set<LegalDocumentVersion>();
    public DbSet<LegalPublicationApproval> LegalPublicationApprovals => Set<LegalPublicationApproval>();
    public DbSet<LegalAcceptanceEvent> LegalAcceptanceEvents => Set<LegalAcceptanceEvent>();
    public DbSet<RetentionLegalHold> RetentionLegalHolds => Set<RetentionLegalHold>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureAuditEventsAreAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureAuditEventsAreAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureIdentityTables(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(PxaDbContext).Assembly);
    }

    private static void ConfigureIdentityTables(ModelBuilder builder)
    {
        builder.Entity<PxaIdentityUser>().ToTable("users", DatabaseSchemas.Identity);
        builder.Entity<PxaIdentityRole>().ToTable("roles", DatabaseSchemas.Identity);
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles", DatabaseSchemas.Identity);
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims", DatabaseSchemas.Identity);
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins", DatabaseSchemas.Identity);
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims", DatabaseSchemas.Identity);
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens", DatabaseSchemas.Identity);

        builder.Entity<PxaIdentityUser>(user =>
        {
            user.Property(value => value.DisplayName).HasMaxLength(200).IsRequired();
            user.Property(value => value.PendingEmail).HasMaxLength(320);
            user.Property(value => value.Locale).HasMaxLength(16).IsRequired();
            user.Property(value => value.Country).HasMaxLength(2);
            user.Property(value => value.TermsAcceptedVersion).HasMaxLength(64);
            user.Property(value => value.PrivacyAcknowledgedVersion).HasMaxLength(64);
            user.Property(value => value.MarketingConsentSource).HasMaxLength(64);
            user.Property(value => value.CreatedAt).IsRequired();
            user.Property(value => value.UpdatedAt).IsRequired();
        });

        builder.Entity<PxaIdentityRole>(role =>
        {
            role.Property(value => value.Description).HasMaxLength(500);
            role.Property(value => value.IsSystemRole).IsRequired();
        });
    }

    private void EnsureAuditEventsAreAppendOnly()
    {
        if (ChangeTracker.Entries<AuditEvent>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException(
                "Audit events are append-only and cannot be modified or deleted.");
        }

        if (ChangeTracker.Entries<LegalAcceptanceEvent>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<LegalPublicationApproval>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException(
                "Legal acceptance and publication approval evidence is append-only.");
        }

        foreach (var entry in ChangeTracker.Entries<LegalDocumentVersion>().Where(entry =>
                     entry.State is EntityState.Modified or EntityState.Deleted))
        {
            var original = entry.Property(value => value.Status).OriginalValue;
            if (entry.State == EntityState.Modified &&
                (original == LegalDocumentStatus.Published ||
                 original == LegalDocumentStatus.Scheduled) &&
                entry.Entity.Status == LegalDocumentStatus.Retired &&
                entry.Properties.Where(property => property.Metadata.Name != nameof(LegalDocumentVersion.Status) &&
                                                   property.Metadata.Name != nameof(LegalDocumentVersion.RetiredAt))
                    .All(property => !property.IsModified))
            {
                continue;
            }

            if (original is LegalDocumentStatus.Published or LegalDocumentStatus.Scheduled or LegalDocumentStatus.Retired)
            {
                throw new InvalidOperationException(
                    "Published, scheduled, and retired legal document versions are immutable.");
            }
        }
    }
}
