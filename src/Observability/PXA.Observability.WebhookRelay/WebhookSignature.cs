using System.Security.Cryptography;
using System.Text;

namespace PXA.Observability.WebhookRelay;

public static class WebhookSignature
{
    public static string Create(long timestamp, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> secret)
    {
        var timestampBytes = Encoding.UTF8.GetBytes($"{timestamp}\n");
        var signedContent = new byte[timestampBytes.Length + payload.Length];
        timestampBytes.CopyTo(signedContent, 0);
        payload.CopyTo(signedContent.AsSpan(timestampBytes.Length));
        var digest = HMACSHA256.HashData(secret, signedContent);
        CryptographicOperations.ZeroMemory(signedContent);
        return $"sha256={Convert.ToHexString(digest).ToLowerInvariant()}";
    }
}
