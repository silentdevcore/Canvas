using System.Security.Claims;

namespace PXA.WebApi.Security;

public interface IPxaTenantContext
{
    Guid? UserId { get; }
    Guid? OrganizationId { get; }
}

public sealed class PxaTenantContext : IPxaTenantContext
{
    private readonly IHttpContextAccessor httpContextAccessor;

    public PxaTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId => ParseClaim(ClaimTypes.NameIdentifier);

    public Guid? OrganizationId => ParseClaim(PxaClaimTypes.ActiveOrganization);

    private Guid? ParseClaim(string claimType)
    {
        var value = httpContextAccessor.HttpContext?.User.FindFirstValue(claimType);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
