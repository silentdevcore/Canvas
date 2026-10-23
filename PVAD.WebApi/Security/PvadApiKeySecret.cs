using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace PVAD.WebApi.Security;

public static class PvadApiKeySecret
{
    public static (string Secret, string Prefix, string Hash) Create()
    {
        var random = RandomNumberGenerator.GetBytes(32);
        var prefix = $"pvad_{Convert.ToHexString(random.AsSpan(0, 6)).ToLowerInvariant()}";
        var secret = $"{prefix}.{WebEncoders.Base64UrlEncode(random)}";
        return (secret, prefix, Hash(secret));
    }

    public static string Hash(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();

    public static bool IsSupported(string secret) => secret.Length <= 256 &&
        (secret.StartsWith("pvad_", StringComparison.Ordinal) || secret.StartsWith("pxa_", StringComparison.Ordinal));

    public static bool IsBearer(string authorization) => authorization.StartsWith("Bearer ", StringComparison.Ordinal) &&
        IsSupported(authorization["Bearer ".Length..]);
}
