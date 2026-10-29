using System.Security.Claims;
using OpenIddict.Abstractions;

namespace PVAD.WebApi.Security;

public interface IPvadTenantContext
{
    Guid? UserId { get; }
    Guid? OrganizationId { get; }
}

public sealed class PvadTenantContext : IPvadTenantContext
{
    private readonly IHttpContextAccessor httpContextAccessor;

    public PvadTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId =>
        ParseClaim(ClaimTypes.NameIdentifier) ?? ParseClaim(OpenIddictConstants.Claims.Subject);

    public Guid? OrganizationId => ParseClaim(PvadClaimTypes.ActiveOrganization);

    private Guid? ParseClaim(string claimType)
    {
        var value = httpContextAccessor.HttpContext?.User.FindFirstValue(claimType);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
