namespace PXA.WebApi.Security;

public static class PxaPermissions
{
    public const string UsersRead = "users.read";
    public const string UsersCreate = "users.create";
    public const string UsersUpdate = "users.update";
    public const string UsersDisable = "users.disable";
    public const string RolesAssign = "roles.assign";
    public const string OrganizationsRead = "organizations.read";
    public const string OrganizationsManage = "organizations.manage";
    public const string SubscriptionsRead = "subscriptions.read";
    public const string SubscriptionsManage = "subscriptions.manage";
    public const string LicensesManage = "licenses.manage";
    public const string AuditRead = "audit.read";
    public const string MailRead = "mail.read";
    public const string MailManage = "mail.manage";

    public static IReadOnlyList<string> All { get; } =
    [
        UsersRead,
        UsersCreate,
        UsersUpdate,
        UsersDisable,
        RolesAssign,
        OrganizationsRead,
        OrganizationsManage,
        SubscriptionsRead,
        SubscriptionsManage,
        LicensesManage,
        AuditRead,
        MailRead,
        MailManage,
    ];
}
