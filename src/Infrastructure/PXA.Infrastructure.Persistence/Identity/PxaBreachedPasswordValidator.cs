using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace PXA.Infrastructure.Persistence.Identity;

public sealed class PxaBreachedPasswordValidator(
    IOptions<PxaPasswordSecurityOptions> options)
    : IPasswordValidator<PxaIdentityUser>
{
    private readonly HashSet<string> breachedHashes = options.Value.BreachedPasswordSha256
        .Select(value => value.Trim().ToUpperInvariant())
        .ToHashSet(StringComparer.Ordinal);

    public Task<IdentityResult> ValidateAsync(
        UserManager<PxaIdentityUser> manager,
        PxaIdentityUser user,
        string? password)
    {
        if (string.IsNullOrEmpty(password))
            return Task.FromResult(IdentityResult.Success);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
        return Task.FromResult(breachedHashes.Contains(hash)
            ? IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordBreached",
                Description = "Choose a password that is not present in the known compromised-password list.",
            })
            : IdentityResult.Success);
    }
}
