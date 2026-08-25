using Microsoft.AspNetCore.Identity;

namespace PXA.Infrastructure.Persistence.Identity;

public sealed class PxaIdentityRole : IdentityRole<Guid>
{
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public Guid? OrganizationId { get; set; }
    public string? CustomKey { get; set; }
    public string? DisplayName { get; set; }
    public bool IsArchived { get; set; }
}
