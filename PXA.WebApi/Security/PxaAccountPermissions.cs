namespace PXA.WebApi.Security;

public static class PxaAccountPermissions
{
    public const string ProfileManage = "account.profile.manage";
    public const string OrganizationRead = "account.organization.read";
    public const string OrganizationManage = "account.organization.manage";
    public const string MembersRead = "account.members.read";
    public const string MembersInvite = "account.members.invite";
    public const string MembersRemove = "account.members.remove";
    public const string SubscriptionRead = "account.subscription.read";
    public const string LicensesRead = "account.licenses.read";
    public const string ServiceAccountsRead = "account.serviceaccounts.read";
    public const string ServiceAccountsManage = "account.serviceaccounts.manage";
    public const string SessionsManage = "account.sessions.manage";
    public const string ClosureRequest = "account.closure.request";

    public static IReadOnlyList<string> All { get; } =
    [
        ProfileManage,
        OrganizationRead,
        OrganizationManage,
        MembersRead,
        MembersInvite,
        MembersRemove,
        SubscriptionRead,
        LicensesRead,
        ServiceAccountsRead,
        ServiceAccountsManage,
        SessionsManage,
        ClosureRequest,
    ];
}
