using Microsoft.AspNetCore.Identity;

namespace PXA.Infrastructure.Persistence.Identity;

public sealed class PxaIdentityRole : IdentityRole<Guid>
{
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
}
