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
            user.Property(value => value.CreatedAt).IsRequired();
            user.Property(value => value.UpdatedAt).IsRequired();
        });

        builder.Entity<PxaIdentityRole>(role =>
        {
            role.Property(value => value.Description).HasMaxLength(500);
            role.Property(value => value.IsSystemRole).IsRequired();
        });
    }
}
