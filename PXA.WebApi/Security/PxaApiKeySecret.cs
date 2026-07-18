using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace PXA.WebApi.Security;

public static class PxaApiKeySecret
{
    public static (string Secret, string Prefix, string Hash) Create()
    {
        var random = RandomNumberGenerator.GetBytes(32);
        var prefix = $"pxa_{Convert.ToHexString(random.AsSpan(0, 6)).ToLowerInvariant()}";
        var secret = $"{prefix}.{WebEncoders.Base64UrlEncode(random)}";
        return (secret, prefix, Hash(secret));
    }

    public static string Hash(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();
}
