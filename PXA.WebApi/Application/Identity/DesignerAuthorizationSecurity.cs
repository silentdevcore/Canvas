using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace PXA.WebApi.Application.Identity;

internal static class DesignerAuthorizationSecurity
{
    internal static string? NormalizeOrigin(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            return null;
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }

    internal static bool TryValidateReturnPath(string value, out string normalized)
    {
        normalized = value.Trim();
        return normalized.Length is > 0 and <= 2048 &&
               normalized.StartsWith('/') &&
               !normalized.StartsWith("//", StringComparison.Ordinal) &&
               !normalized.Contains('\r') &&
               !normalized.Contains('\n');
    }

    internal static bool IsValidPkceChallenge(string value) =>
        value.Length is >= 43 and <= 128 &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or '~');

    internal static bool IsValidVerifier(string value) => IsValidPkceChallenge(value);

    internal static bool IsValidState(string value) =>
        value.Length is >= 32 and <= 256 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    internal static string CreatePkceChallenge(string verifier) =>
        WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    internal static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
