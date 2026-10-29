using Microsoft.Extensions.Options;
using PVAD.Infrastructure.Persistence.Identity;

namespace PVAD.WebApi.Security;

public sealed class PvadAdminSecurityOptions
{
    public const string SectionName = "AdminSecurity";

    public bool RequireExplicitSystemOperators { get; set; }
    public List<string> SystemOperatorEmails { get; set; } = [];
}

public sealed class PvadSystemOperatorAccess(IOptions<PvadAdminSecurityOptions> options)
{
    private readonly PvadAdminSecurityOptions options = options.Value;

    public bool IsAuthorized(PvadIdentityUser user)
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
