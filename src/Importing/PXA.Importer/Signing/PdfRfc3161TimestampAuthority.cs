using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace PXA.Importer.Signing;

public sealed record PdfRfc3161TimestampAuthorityOptions
{
    public required Uri Endpoint { get; init; }
    public int MaximumResponseBytes { get; init; } = 1024 * 1024;
    public bool RequireTrustedCertificateChain { get; init; } = true;
}

public sealed class PdfRfc3161TimestampAuthority : IPdfTimestampAuthority
{
    private const string TimestampingExtendedKeyUsageOid = "1.3.6.1.5.5.7.3.8";
    private readonly HttpClient _httpClient;
    private readonly PdfRfc3161TimestampAuthorityOptions _options;

    public PdfRfc3161TimestampAuthority(
        HttpClient httpClient,
        PdfRfc3161TimestampAuthorityOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (!_options.Endpoint.IsAbsoluteUri || _options.Endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("The RFC 3161 timestamp endpoint must be an absolute HTTPS URI.", nameof(options));
        }

        if (_options.MaximumResponseBytes is < 1024 or > 16 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The timestamp response limit must be between 1 KiB and 16 MiB.");
        }
    }

    public async Task<PdfTimestampToken> GetTimestampTokenAsync(
        ReadOnlyMemory<byte> signatureDigest,
        HashAlgorithmName digestAlgorithm,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetTimestampTokenCoreAsync(
                signatureDigest,
                digestAlgorithm,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is
            HttpRequestException or CryptographicException or InvalidDataException or TaskCanceledException)
        {
            throw new PdfTimestampAuthorityException(
                "The RFC 3161 timestamp authority request failed validation.",
                exception);
        }
    }

    private async Task<PdfTimestampToken> GetTimestampTokenCoreAsync(
        ReadOnlyMemory<byte> signatureDigest,
        HashAlgorithmName digestAlgorithm,
        CancellationToken cancellationToken)
    {
        if (signatureDigest.IsEmpty)
        {
            throw new ArgumentException("A signature digest is required for RFC 3161 timestamping.", nameof(signatureDigest));
        }

        if (digestAlgorithm != HashAlgorithmName.SHA256 &&
            digestAlgorithm != HashAlgorithmName.SHA384 &&
            digestAlgorithm != HashAlgorithmName.SHA512)
        {
            throw new NotSupportedException("RFC 3161 timestamping requires SHA-256 or stronger.");
        }

        var nonce = RandomNumberGenerator.GetBytes(16);
        var request = Rfc3161TimestampRequest.CreateFromHash(
            signatureDigest,
            digestAlgorithm,
            requestedPolicyId: null,
            nonce: nonce,
            requestSignerCertificates: true,
            extensions: null);
        using var content = new ByteArrayContent(request.Encode());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/timestamp-query");
        using var message = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint) { Content = content };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/timestamp-reply"));
        using var response = await _httpClient.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (!string.Equals(
                response.Content.Headers.ContentType?.MediaType,
                "application/timestamp-reply",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new CryptographicException("The RFC 3161 endpoint returned an unexpected content type.");
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var responseBytes = await ReadBoundedAsync(
            responseStream,
            _options.MaximumResponseBytes,
            cancellationToken).ConfigureAwait(false);
        var token = request.ProcessResponse(responseBytes, out var bytesConsumed);
        if (bytesConsumed != responseBytes.Length)
        {
            throw new CryptographicException("The RFC 3161 response contains trailing data.");
        }

        var signedCms = token.AsSignedCms();
        if (!token.VerifySignatureForHash(
                signatureDigest.Span,
                digestAlgorithm,
                out var signerCertificate,
                signedCms.Certificates) ||
            signerCertificate is null)
        {
            throw new CryptographicException("The RFC 3161 timestamp token signature or message imprint is invalid.");
        }

        using (signerCertificate)
        {
            ValidateTimestampCertificate(signerCertificate, signedCms.Certificates, token.TokenInfo.Timestamp);
            return new PdfTimestampToken(
                signedCms.Encode(),
                token.TokenInfo.Timestamp,
                signerCertificate.Subject,
                signerCertificate.Thumbprint);
        }
    }

    private void ValidateTimestampCertificate(
        X509Certificate2 certificate,
        X509Certificate2Collection certificates,
        DateTimeOffset timestamp)
    {
        if (timestamp.UtcDateTime < certificate.NotBefore.ToUniversalTime() ||
            timestamp.UtcDateTime > certificate.NotAfter.ToUniversalTime())
        {
            throw new CryptographicException("The timestamp certificate was not valid at the asserted time.");
        }

        var enhancedKeyUsage = certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault();
        if (enhancedKeyUsage is null ||
            !enhancedKeyUsage.Critical ||
            enhancedKeyUsage.EnhancedKeyUsages.Count != 1 ||
            enhancedKeyUsage.EnhancedKeyUsages[0].Value != TimestampingExtendedKeyUsageOid)
        {
            throw new CryptographicException("The timestamp certificate must have a critical, exclusive time-stamping EKU.");
        }

        if (!_options.RequireTrustedCertificateChain)
        {
            return;
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationTime = timestamp.UtcDateTime;
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid(TimestampingExtendedKeyUsageOid));
        foreach (var candidate in certificates.Cast<X509Certificate2>()
                     .Where(candidate => candidate.Thumbprint != certificate.Thumbprint))
        {
            chain.ChainPolicy.ExtraStore.Add(candidate);
        }

        if (!chain.Build(certificate))
        {
            throw new CryptographicException("The timestamp certificate chain is not trusted by the configured host trust store.");
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream stream,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        using var output = new MemoryStream(Math.Min(maximumBytes, 64 * 1024));
        var buffer = new byte[16 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (output.Length + read > maximumBytes)
            {
                throw new InvalidDataException("The RFC 3161 timestamp response exceeds the configured limit.");
            }

            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }
}
