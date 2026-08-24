using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/pxa/v1/admin/roles")]
public sealed class AdminRolesController : ControllerBase
{
    private static readonly RoleDefinition[] RoleDefinitions =
    [
        new("organization-administrator", PxaRoles.OrganizationAdministrator,
            "Manages organization users, access, commercial visibility, mail, and operational administration."),
        new("manager", PxaRoles.Manager,
            "Reviews users and audit activity and can update non-privileged user information."),
        new("editor", PxaRoles.Editor,
            "Uses licensed product workflows without organization administration permissions."),
        new("viewer", PxaRoles.Viewer,
            "Receives read-only product access where the subscription and application allow it."),
    ];

    private static readonly IReadOnlyDictionary<string, PermissionDefinition> PermissionDefinitions =
        BuildPermissionDefinitions();

    private static IReadOnlyDictionary<string, PermissionDefinition> BuildPermissionDefinitions()
    {
        var definitions = new Dictionary<string, PermissionDefinition>(StringComparer.Ordinal)
        {
            [PxaPermissions.UsersRead] = new("Users", "View organization users and membership state."),
            [PxaPermissions.UsersCreate] = new("Users", "Invite and create organization users."),
            [PxaPermissions.UsersUpdate] = new("Users", "Update organization user information."),
            [PxaPermissions.UsersDisable] = new("Users", "Disable or restore organization users."),
            [PxaPermissions.RolesAssign] = new("Identity", "Assign protected organization roles."),
            [PxaPermissions.OrganizationsRead] = new("Organizations", "View organization settings and members."),
            [PxaPermissions.OrganizationsManage] = new("Organizations", "Update organization settings and memberships."),
            [PxaPermissions.SubscriptionsRead] = new("Commercial", "View subscriptions, seats, entitlements, and usage."),
            [PxaPermissions.SubscriptionsManage] = new("Commercial", "Change subscription lifecycle and entitlements."),
            [PxaPermissions.LicensesRead] = new("Commercial", "View and validate offline licenses."),
            [PxaPermissions.LicensesManage] = new("Commercial", "Issue and revoke offline licenses."),
            [PxaPermissions.ServiceAccountsRead] = new("Access", "View service accounts and API-key metadata."),
            [PxaPermissions.ServiceAccountsManage] = new("Access", "Create and revoke service accounts and API keys."),
            [PxaPermissions.AuditRead] = new("Operations", "Search and inspect tenant audit history."),
            [PxaPermissions.MailRead] = new("Operations", "View transactional mail delivery metadata."),
            [PxaPermissions.MailManage] = new("Operations", "Retry or cancel transactional mail delivery."),
            [PxaPermissions.LegalRead] = new("Legal", "Read legal documents and publication history."),
            [PxaPermissions.LegalAuthor] = new("Legal", "Create and edit legal document drafts."),
            [PxaPermissions.LegalApprove] = new("Legal", "Review and publish legal document versions."),
            [PxaDesignerPermissions.Access] = new("Designer access", "Open PXA Designer in the active organization."),
            [PxaDesignerPermissions.TemplatesRead] = new("Designer templates", "View templates and immutable versions."),
            [PxaDesignerPermissions.TemplatesCreate] = new("Designer templates", "Create organization templates."),
            [PxaDesignerPermissions.TemplatesEdit] = new("Designer templates", "Edit template drafts and metadata."),
            [PxaDesignerPermissions.TemplatesArchive] = new("Designer templates", "Archive and restore templates."),
            [PxaDesignerPermissions.TemplatesVersion] = new("Designer templates", "Create immutable template versions."),
            [PxaDesignerPermissions.TemplatesPublish] = new("Designer templates", "Publish an approved template version."),
            [PxaDesignerPermissions.AssetsRead] = new("Designer assets", "View and use organization-bound Designer assets."),
            [PxaDesignerPermissions.AssetsManage] = new("Designer assets", "Upload and delete Designer assets."),
            [PxaDesignerPermissions.CodeRead] = new("Designer code", "View code workspaces and source maps."),
            [PxaDesignerPermissions.CodeEdit] = new("Designer code", "Edit, validate, convert, and restore code drafts."),
            [PxaDesignerPermissions.CodeExecute] = new("Designer code", "Execute supported code in the isolated worker."),
            [PxaDesignerPermissions.CodeApply] = new("Designer code", "Apply a code result to a template draft."),
            [PxaDesignerPermissions.PreferencesManage] = new("Designer preferences", "Manage personal Designer preferences and favorites."),
            [PxaDesignerPermissions.NotificationsRead] = new("Designer notifications", "Read and dismiss personal Designer notifications."),
            [PxaDesignerPermissions.JobsRead] = new("Designer jobs", "View tenant-scoped background job status and results."),
            [PxaDesignerPermissions.JobsCancel] = new("Designer jobs", "Cancel pending Designer background jobs."),
            [PxaDesignerPermissions.SymbolsRender] = new("Designer rendering", "Render supported symbols and codes in the Designer."),
            [PxaDesignerPermissions.ExperimentalFeatures] = new("Designer features", "Opt in to organization-approved experimental features."),
            [PxaDesignerPermissions.FeaturesManage] = new("Designer features", "Manage organization-wide feature policies."),
            [PxaDesignerPermissions.PdfAccess] = new("Designer PDF", "Open the PDF workspace."),
            [PxaDesignerPermissions.PdfCreate] = new("Designer PDF", "Create PDF documents."),
            [PxaDesignerPermissions.PdfEdit] = new("Designer PDF", "Edit PDF designs and PDF code workspaces."),
            [PxaDesignerPermissions.PdfTemplatesRead] = new("Designer PDF", "Browse and use PDF templates."),
            [PxaDesignerPermissions.PdfImport] = new("Designer PDF", "Import source documents into PDF designs."),
            [PxaDesignerPermissions.PdfConvert] = new("Designer PDF", "Convert supported files into PDF."),
            [PxaDesignerPermissions.PdfExport] = new("Designer PDF", "Export PDF designs in supported formats."),
            [PxaDesignerPermissions.PdfPrint] = new("Designer PDF", "Render and print PDF documents."),
            [PxaDesignerPermissions.DocumentEdit] = new("Designer documents", "Run document editing and transformation operations."),
            [PxaDesignerPermissions.Import] = new("Designer documents", "Import supported document and image formats."),
            [PxaDesignerPermissions.Export] = new("Designer documents", "Render and export documents in supported formats."),
            [PxaDesignerPermissions.MigrationRead] = new("Designer migration", "View supported migration providers and coverage."),
            [PxaDesignerPermissions.MigrationExecute] = new("Designer migration", "Execute and preview code or report migrations."),
            [PxaDesignerPermissions.MigrationPdfRead] = new("Designer PDF migration", "View PDF code migration providers."),
            [PxaDesignerPermissions.MigrationPdfExecute] = new("Designer PDF migration", "Execute PDF code migrations for allowed providers."),
            [PxaDesignerPermissions.MigrationSpreadsheetRead] = new("Designer Spreadsheet migration", "View Spreadsheet code migration providers."),
            [PxaDesignerPermissions.MigrationSpreadsheetExecute] = new("Designer Spreadsheet migration", "Execute Spreadsheet code migrations for allowed providers."),
            [PxaDesignerPermissions.MigrationReportRead] = new("Designer Report migration", "View report Designer migration providers."),
            [PxaDesignerPermissions.MigrationReportExecute] = new("Designer Report migration", "Execute report Designer migrations for allowed providers."),
            [PxaDesignerPermissions.SpreadsheetAccess] = new("Designer spreadsheet", "Open the Spreadsheet workspace."),
            [PxaDesignerPermissions.SpreadsheetCreate] = new("Designer spreadsheet", "Create Spreadsheet documents."),
            [PxaDesignerPermissions.SpreadsheetRead] = new("Designer spreadsheet", "View and validate Spreadsheet documents."),
            [PxaDesignerPermissions.SpreadsheetEdit] = new("Designer spreadsheet", "Create, calculate, fill, sort, and edit Spreadsheets."),
            [PxaDesignerPermissions.SpreadsheetImport] = new("Designer spreadsheet", "Import supported Spreadsheet formats."),
            [PxaDesignerPermissions.SpreadsheetExport] = new("Designer spreadsheet", "Render and export Spreadsheet documents."),
            [PxaDesignerPermissions.PdfViewerRead] = new("Designer PDF Viewer", "View PDFs, forms, and annotation data."),
            [PxaDesignerPermissions.PdfViewerAnnotate] = new("Designer PDF Viewer", "Create, embed, flatten, and remove PDF annotations."),
            [PxaDesignerPermissions.PdfViewerForms] = new("Designer PDF Viewer", "Fill PDF form fields."),
            [PxaDesignerPermissions.PdfViewerRedact] = new("Designer PDF Viewer", "Apply irreversible PDF redactions."),
        };

        foreach (var permission in PxaDesignerMigrationPermissions.All)
        {
            var segments = permission.Split('.');
            var domain = segments[2];
            var provider = segments[4];
            definitions[permission] = new PermissionDefinition(
                $"Designer {domain} migration providers",
                $"Execute {provider} migrations in the {domain} domain.");
        }

        return definitions;
    }

    private readonly PxaDbContext dbContext;
    private readonly IPxaTenantContext tenantContext;

    public AdminRolesController(PxaDbContext dbContext, IPxaTenantContext tenantContext)
    {
        this.dbContext = dbContext;
        this.tenantContext = tenantContext;
    }

    [HttpGet]
    [Authorize(Policy = PxaPermissions.UsersRead)]
    public async Task<ActionResult<AdminRoleCatalogResponse>> GetRoles(CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is not { } organizationId)
            return MissingOrganization();
        var roleNames = RoleDefinitions.Select(value => value.Name).ToArray();
        var counts = await (
                from assignment in dbContext.OrganizationMembershipRoles.AsNoTracking()
                join membership in dbContext.OrganizationMemberships.AsNoTracking()
                    on assignment.OrganizationMembershipId equals membership.Id
                join role in dbContext.Roles.AsNoTracking() on assignment.RoleId equals role.Id
                where membership.OrganizationId == organizationId &&
                      membership.Status != OrganizationMembershipStatus.Removed &&
                      roleNames.Contains(role.Name!)
                group assignment by role.Name into grouped
                select new { Name = grouped.Key!, Count = grouped.Count() })
            .ToDictionaryAsync(value => value.Name, value => value.Count, cancellationToken);
        return Ok(new AdminRoleCatalogResponse(
            RoleDefinitions.Select(value => ToRole(value, counts.GetValueOrDefault(value.Name))).ToArray(),
            PermissionDefinitions.Select(value => new AdminPermissionResponse(
                    value.Key, value.Value.Group, value.Value.Description))
                .OrderBy(value => value.Group).ThenBy(value => value.Key).ToArray()));
    }

    [HttpGet("{roleKey}")]
    [Authorize(Policy = PxaPermissions.UsersRead)]
    public async Task<ActionResult<AdminRoleDetailResponse>> GetRole(
        string roleKey,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.OrganizationId is not { } organizationId)
            return MissingOrganization();
        var definition = FindRole(roleKey);
        if (definition is null)
            return NotFound();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query =
            from assignment in dbContext.OrganizationMembershipRoles.AsNoTracking()
            join membership in dbContext.OrganizationMemberships.AsNoTracking()
                on assignment.OrganizationMembershipId equals membership.Id
            join role in dbContext.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
            where membership.OrganizationId == organizationId &&
                  membership.Status != OrganizationMembershipStatus.Removed &&
                  role.Name == definition.Name
            orderby user.DisplayName, user.Email
            select new RoleMemberRecord(
                user.Id, membership.Id, user.DisplayName, user.Email ?? string.Empty,
                user.IsActive, membership.Status, assignment.CreatedAt, assignment.AssignedByUserId);
        var total = await query.CountAsync(cancellationToken);
        var records = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var assignerIds = records.Where(value => value.AssignedByUserId is not null)
            .Select(value => value.AssignedByUserId!.Value).Distinct().ToArray();
        var assigners = await dbContext.Users.AsNoTracking().Where(value => assignerIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, value => value.DisplayName, cancellationToken);
        return Ok(new AdminRoleDetailResponse(
            ToRole(definition, total),
            records.Select(value => new AdminRoleMemberResponse(
                value.UserId,
                value.MembershipId,
                value.DisplayName,
                value.Email,
                value.IsActive,
                value.MembershipStatus.ToString(),
                value.AssignedAt,
                value.AssignedByUserId,
                value.AssignedByUserId is { } id ? assigners.GetValueOrDefault(id, "System") : "System"))
                .ToArray(),
            page,
            pageSize,
            total));
    }

    [HttpPut("{roleKey}/members/{userId:guid}")]
    [Authorize(Policy = PxaPermissions.RolesAssign)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("roles.member.assign")]
    public async Task<IActionResult> AssignMember(
        string roleKey,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is not { } organizationId || tenantContext.UserId is not { } actorUserId)
            return MissingOrganization();
        if (userId == actorUserId)
            return ConflictProblem("Administrators cannot change their own active role assignment.");
        var definition = FindRole(roleKey);
        if (definition is null)
            return NotFound();
        var membership = await dbContext.OrganizationMemberships.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId && value.UserId == userId &&
            value.Status != OrganizationMembershipStatus.Removed, cancellationToken);
        var user = await dbContext.Users.SingleOrDefaultAsync(value => value.Id == userId, cancellationToken);
        if (membership is null || user is null)
            return NotFound();
        var role = await dbContext.Roles.SingleOrDefaultAsync(value =>
            value.Name == definition.Name && value.IsSystemRole, cancellationToken);
        if (role is null)
            return ConflictProblem("The protected role definition has not been initialized.");
        if (await dbContext.OrganizationMembershipRoles.AnyAsync(value =>
                value.OrganizationMembershipId == membership.Id && value.RoleId == role.Id, cancellationToken))
            return NoContent();

        dbContext.OrganizationMembershipRoles.Add(new OrganizationMembershipRole
        {
            OrganizationMembershipId = membership.Id,
            RoleId = role.Id,
            AssignedByUserId = actorUserId,
        });
        RevokeSessions(user);
        AddAudit(organizationId, actorUserId, "roles.member.assign", roleKey, userId, definition.Name);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{roleKey}/members/{userId:guid}")]
    [Authorize(Policy = PxaPermissions.RolesAssign)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("roles.member.revoke")]
    public async Task<IActionResult> RevokeMember(
        string roleKey,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is not { } organizationId || tenantContext.UserId is not { } actorUserId)
            return MissingOrganization();
        if (userId == actorUserId)
            return ConflictProblem("Administrators cannot change their own active role assignment.");
        var definition = FindRole(roleKey);
        if (definition is null)
            return NotFound();
        var membership = await dbContext.OrganizationMemberships.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId && value.UserId == userId &&
            value.Status != OrganizationMembershipStatus.Removed, cancellationToken);
        var user = await dbContext.Users.SingleOrDefaultAsync(value => value.Id == userId, cancellationToken);
        if (membership is null || user is null)
            return NotFound();
        var roleId = await dbContext.Roles.Where(value => value.Name == definition.Name && value.IsSystemRole)
            .Select(value => (Guid?)value.Id).SingleOrDefaultAsync(cancellationToken);
        if (roleId is null)
            return NotFound();
        var assignment = await dbContext.OrganizationMembershipRoles.SingleOrDefaultAsync(value =>
            value.OrganizationMembershipId == membership.Id && value.RoleId == roleId, cancellationToken);
        if (assignment is null)
            return NoContent();
        if (definition.Name == PxaRoles.OrganizationAdministrator &&
            membership.Status == OrganizationMembershipStatus.Active && user.IsActive &&
            await IsLastActiveAdministratorAsync(organizationId, roleId.Value, cancellationToken))
            return ConflictProblem("The last active Organization Administrator role cannot be removed.");

        dbContext.OrganizationMembershipRoles.Remove(assignment);
        RevokeSessions(user);
        AddAudit(organizationId, actorUserId, "roles.member.revoke", roleKey, userId, definition.Name);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<bool> IsLastActiveAdministratorAsync(
        Guid organizationId,
        Guid roleId,
        CancellationToken cancellationToken) =>
        await (
                from assignment in dbContext.OrganizationMembershipRoles
                join membership in dbContext.OrganizationMemberships
                    on assignment.OrganizationMembershipId equals membership.Id
                join user in dbContext.Users on membership.UserId equals user.Id
                where membership.OrganizationId == organizationId && assignment.RoleId == roleId &&
                      membership.Status == OrganizationMembershipStatus.Active && user.IsActive
                select membership.UserId)
            .Distinct().CountAsync(cancellationToken) <= 1;

    private static RoleDefinition? FindRole(string roleKey) =>
        RoleDefinitions.SingleOrDefault(value => string.Equals(value.Key, roleKey, StringComparison.OrdinalIgnoreCase));

    private static AdminRoleResponse ToRole(RoleDefinition definition, int memberCount)
    {
        var permissions = PxaRoles.Permissions[definition.Name]
            .Select(key => PermissionDefinitions.TryGetValue(key, out var metadata)
                ? new AdminPermissionResponse(key, metadata.Group, metadata.Description)
                : new AdminPermissionResponse(key, "Other", key))
            .OrderBy(value => value.Group).ThenBy(value => value.Key).ToArray();
        return new AdminRoleResponse(
            definition.Key, definition.Name, definition.Description, true, memberCount, permissions);
    }

    private static void RevokeSessions(PXA.Infrastructure.Persistence.Identity.PxaIdentityUser user)
    {
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void AddAudit(
        Guid organizationId,
        Guid actorUserId,
        string action,
        string roleKey,
        Guid userId,
        string roleName) =>
        dbContext.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = organizationId,
            ActorUserId = actorUserId,
            Action = action,
            TargetType = "role",
            TargetId = roleKey,
            Outcome = "succeeded",
            DetailsJson = JsonSerializer.Serialize(new { UserId = userId, Role = roleName }),
        });

    private ObjectResult MissingOrganization() => Problem(
        statusCode: StatusCodes.Status403Forbidden, title: "Organization context required");

    private ObjectResult ConflictProblem(string detail) => Problem(
        statusCode: StatusCodes.Status409Conflict, title: "Role operation rejected", detail: detail);

    private sealed record RoleDefinition(string Key, string Name, string Description);
    private sealed record PermissionDefinition(string Group, string Description);
    private sealed record RoleMemberRecord(
        Guid UserId,
        Guid MembershipId,
        string DisplayName,
        string Email,
        bool IsActive,
        OrganizationMembershipStatus MembershipStatus,
        DateTimeOffset AssignedAt,
        Guid? AssignedByUserId);
}

public sealed record AdminRoleCatalogResponse(
    IReadOnlyList<AdminRoleResponse> Roles,
    IReadOnlyList<AdminPermissionResponse> Permissions);
public sealed record AdminRoleResponse(
    string Key,
    string Name,
    string Description,
    bool IsProtected,
    int MemberCount,
    IReadOnlyList<AdminPermissionResponse> Permissions);
public sealed record AdminPermissionResponse(string Key, string Group, string Description);
public sealed record AdminRoleDetailResponse(
    AdminRoleResponse Role,
    IReadOnlyList<AdminRoleMemberResponse> Members,
    int Page,
    int PageSize,
    int Total);
public sealed record AdminRoleMemberResponse(
    Guid UserId,
    Guid MembershipId,
    string DisplayName,
    string Email,
    bool IsActive,
    string MembershipStatus,
    DateTimeOffset AssignedAt,
    Guid? AssignedByUserId,
    string AssignedByName);
