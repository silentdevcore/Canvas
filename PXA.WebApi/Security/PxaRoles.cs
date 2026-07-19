namespace PXA.WebApi.Security;

public static class PxaRoles
{
    public const string SystemAdministrator = "System Administrator";
    public const string OrganizationAdministrator = "Organization Administrator";
    public const string Manager = "Manager";
    public const string Editor = "Editor";
    public const string Viewer = "Viewer";

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Permissions { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [SystemAdministrator] = PxaPermissions.All,
            [OrganizationAdministrator] =
            [
                PxaPermissions.UsersRead,
                PxaPermissions.UsersCreate,
                PxaPermissions.UsersUpdate,
                PxaPermissions.UsersDisable,
                PxaPermissions.RolesAssign,
                PxaPermissions.OrganizationsRead,
                PxaPermissions.OrganizationsManage,
                PxaPermissions.SubscriptionsRead,
                PxaPermissions.LicensesRead,
                PxaPermissions.ServiceAccountsRead,
                PxaPermissions.ServiceAccountsManage,
                PxaPermissions.AuditRead,
                PxaPermissions.MailRead,
                PxaPermissions.MailManage,
                PxaAccountPermissions.ProfileManage,
                PxaAccountPermissions.OrganizationRead,
                PxaAccountPermissions.OrganizationManage,
                PxaAccountPermissions.MembersRead,
                PxaAccountPermissions.MembersInvite,
                PxaAccountPermissions.MembersRemove,
                PxaAccountPermissions.SubscriptionRead,
                PxaAccountPermissions.LicensesRead,
                PxaAccountPermissions.ServiceAccountsRead,
                PxaAccountPermissions.ServiceAccountsManage,
                PxaAccountPermissions.SessionsManage,
                PxaAccountPermissions.ClosureRequest,
            ],
            [Manager] =
            [
                PxaPermissions.UsersRead,
                PxaPermissions.UsersUpdate,
                PxaPermissions.AuditRead,
                PxaAccountPermissions.ProfileManage,
                PxaAccountPermissions.OrganizationRead,
                PxaAccountPermissions.MembersRead,
                PxaAccountPermissions.SubscriptionRead,
                PxaAccountPermissions.LicensesRead,
                PxaAccountPermissions.ServiceAccountsRead,
                PxaAccountPermissions.SessionsManage,
            ],
            [Editor] =
            [
                PxaAccountPermissions.ProfileManage,
                PxaAccountPermissions.OrganizationRead,
                PxaAccountPermissions.SubscriptionRead,
                PxaAccountPermissions.LicensesRead,
                PxaAccountPermissions.SessionsManage,
            ],
            [Viewer] =
            [
                PxaAccountPermissions.ProfileManage,
                PxaAccountPermissions.OrganizationRead,
                PxaAccountPermissions.SubscriptionRead,
                PxaAccountPermissions.LicensesRead,
                PxaAccountPermissions.SessionsManage,
            ],
        };
}
