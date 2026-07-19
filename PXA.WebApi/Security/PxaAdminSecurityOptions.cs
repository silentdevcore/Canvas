using Microsoft.Extensions.Options;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.WebApi.Security;

public sealed class PxaAdminSecurityOptions
{
    public const string SectionName = "AdminSecurity";

    public bool RequireExplicitSystemOperators { get; set; }
    public List<string> SystemOperatorEmails { get; set; } = [];
}

public sealed class PxaSystemOperatorAccess(IOptions<PxaAdminSecurityOptions> options)
{
    private readonly PxaAdminSecurityOptions options = options.Value;

    public bool IsAuthorized(PxaIdentityUser user)
    {
        if (!options.RequireExplicitSystemOperators)
            return true;

        var email = user.Email?.Trim();
        return !string.IsNullOrEmpty(email) && options.SystemOperatorEmails.Any(
            configuredEmail => string.Equals(
                configuredEmail.Trim(),
                email,
                StringComparison.OrdinalIgnoreCase));
    }
}
