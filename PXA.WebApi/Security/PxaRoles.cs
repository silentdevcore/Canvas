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
                PxaPermissions.SubscriptionsManage,
                PxaPermissions.LicensesManage,
                PxaPermissions.AuditRead,
                PxaPermissions.MailRead,
                PxaPermissions.MailManage,
            ],
            [Manager] =
            [
                PxaPermissions.UsersRead,
                PxaPermissions.UsersUpdate,
                PxaPermissions.AuditRead,
            ],
            [Editor] = [],
            [Viewer] = [],
        };
}
