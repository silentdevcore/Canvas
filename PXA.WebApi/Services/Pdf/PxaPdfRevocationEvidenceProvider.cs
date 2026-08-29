using System.Formats.Asn1;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using PXA.Importer.Trust;

namespace PXA.WebApi.Services.Pdf;

public sealed class PxaPdfRevocationEvidenceOptions
{
    public const string SectionName = "PdfTrust:RevocationEvidence";

    public bool Enabled { get; init; }
    public string[] AllowedHosts { get; init; } = [];
    public string[] CrlPaths { get; init; } = [];
    public string[] OcspResponsePaths { get; init; } = [];
    public string CacheDirectory { get; init; } = "App_Data/pdf-trust/revocation-cache";
    public int MaximumResponseBytes { get; init; } = 4 * 1024 * 1024;
    public int NetworkTimeoutSeconds { get; init; } = 10;
    public int MaximumOcspAgeHours { get; init; } = 24;
}

public sealed class PxaPdfRevocationEvidenceProvider : IPdfRevocationEvidenceProvider
{
    public const string HttpClientName = "PxaPdfRevocationEvidence";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDataProtector _protector;
    private readonly PxaPdfRevocationEvidenceOptions _options;
    private readonly string _cacheDirectory;
    private readonly IReadOnlyList<(string Source, byte[] Bytes)> _suppliedCrls;
    private readonly IReadOnlyList<(string Source, byte[] Bytes)> _suppliedOcspResponses;
    private readonly HashSet<string> _allowedHosts;
    private readonly ILogger<PxaPdfRevocationEvidenceProvider> _logger;

    public PxaPdfRevocationEvidenceProvider(
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<PxaPdfRevocationEvidenceOptions> options,
        IHostEnvironment environment,
        ILogger<PxaPdfRevocationEvidenceProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _protector = dataProtectionProvider.CreateProtector("PXA.PdfTrust.RevocationEvidence.v1");
        _options = options.Value;
        _logger = logger;
        _allowedHosts = _options.AllowedHosts
            .Select(static host => host.Trim().TrimEnd('.'))
            .Where(static host => host.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _cacheDirectory = ResolvePath(_options.CacheDirectory, environment.ContentRootPath);
        Directory.CreateDirectory(_cacheDirectory);
        _suppliedCrls = _options.CrlPaths.Select(path =>
        {
            var resolved = ResolvePath(path, environment.ContentRootPath);
            if (!File.Exists(resolved))
            {
                throw new InvalidOperationException($"The configured PDF CRL file does not exist: '{path}'.");
            }
            return ($"configured:{Path.GetFileName(resolved)}", ReadCrlBytes(resolved));
        }).ToArray();
        _suppliedOcspResponses = _options.OcspResponsePaths.Select(path =>
        {
            var resolved = ResolvePath(path, environment.ContentRootPath);
            if (!File.Exists(resolved))
            {
                throw new InvalidOperationException($"The configured PDF OCSP response file does not exist: '{path}'.");
            }
            return ($"configured:{Path.GetFileName(resolved)}", File.ReadAllBytes(resolved));
        }).ToArray();
    }

    public async Task<PdfRevocationEvidenceResult> GetEvidenceAsync(
        PdfRevocationEvidenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var candidates = new List<PdfRevocationEvidence>();
        foreach (var supplied in _suppliedCrls)
        {
            if (TryValidate(supplied.Bytes, supplied.Source, request, out var evidence))
            {
                candidates.Add(evidence);
            }
        }

        var distributionPoints = PdfCertificateEvidenceLocator.GetCrlDistributionPointUris(request.Certificate);
        foreach (var endpoint in distributionPoints.Where(IsAllowedEndpoint))
        {
            var cached = await ReadCacheAsync(GetCrlCacheKey(endpoint), cancellationToken).ConfigureAwait(false);
            if (cached is not null && TryValidate(cached, $"cache:{endpoint.Host}", request, out var evidence))
            {
                candidates.Add(evidence);
                continue;
            }

            if (!_options.Enabled || request.Mode != PdfCertificateRevocationMode.Online)
            {
                continue;
            }

            var downloaded = await DownloadAsync(endpoint, cancellationToken).ConfigureAwait(false);
            if (downloaded is null || !TryValidate(downloaded, $"network:{endpoint.Host}", request, out evidence))
            {
                continue;
            }

            await WriteCacheAsync(GetCrlCacheKey(endpoint), downloaded, cancellationToken).ConfigureAwait(false);
            candidates.Add(evidence);
        }

        foreach (var supplied in _suppliedOcspResponses)
        {
            if (TryValidateOcsp(supplied.Bytes, supplied.Source, request, out var evidence))
            {
                candidates.Add(evidence);
            }
        }

        var ocspEndpoints = PdfCertificateEvidenceLocator.GetOcspUris(request.Certificate);
        foreach (var endpoint in ocspEndpoints.Where(IsAllowedEndpoint))
        {
            var cacheKey = GetOcspCacheKey(endpoint, request.Certificate);
            var cached = await ReadCacheAsync(cacheKey, cancellationToken).ConfigureAwait(false);
            if (cached is not null && TryValidateOcsp(cached, $"cache:{endpoint.Host}", request, out var evidence))
            {
                candidates.Add(evidence);
                continue;
            }

            if (!_options.Enabled || request.Mode != PdfCertificateRevocationMode.Online)
            {
                continue;
            }

            var downloaded = await DownloadOcspAsync(endpoint, request, cancellationToken).ConfigureAwait(false);
            if (downloaded is null || !TryValidateOcsp(downloaded, $"network:{endpoint.Host}", request, out evidence))
            {
                continue;
            }
            await WriteCacheAsync(cacheKey, downloaded, cancellationToken).ConfigureAwait(false);
            candidates.Add(evidence);
        }

        if (candidates.Count > 0)
        {
            return AggregateResult(candidates);
        }

        var blockedCount = distributionPoints.Count(endpoint => !IsAllowedEndpoint(endpoint)) +
                           ocspEndpoints.Count(endpoint => !IsAllowedEndpoint(endpoint));
        var detail = blockedCount > 0
            ? $"{blockedCount} revocation endpoint(s) were rejected by the HTTPS allowlist."
            : "No valid supplied, cached, or allowlisted CRL/OCSP evidence was available.";
        return PdfRevocationEvidenceResult.Unavailable(detail);
    }

    internal static PdfRevocationEvidenceResult AggregateResult(
        IReadOnlyCollection<PdfRevocationEvidence> candidates)
    {
        var evidence = candidates
            .OrderBy(static item => item.Status == PdfTrustStatus.Invalid ? 0 : 1)
            .ThenByDescending(static item => item.ThisUpdate)
            .ToArray();
        var status = evidence.Any(static item => item.Status == PdfTrustStatus.Invalid)
            ? PdfTrustStatus.Invalid
            : evidence.Any(static item => item.Status == PdfTrustStatus.Valid)
                ? PdfTrustStatus.Valid
                : PdfTrustStatus.Indeterminate;
        return new PdfRevocationEvidenceResult(status, evidence);
    }

    private bool TryValidate(
        byte[] bytes,
        string source,
        PdfRevocationEvidenceRequest request,
        out PdfRevocationEvidence evidence)
    {
        try
        {
            evidence = PdfCrlEvidenceValidator.Validate(
                bytes,
                request.Certificate,
                request.Issuer,
                request.ValidationTime,
                source);
            return true;
        }
        catch (Exception exception) when (exception is CryptographicException or AsnContentException)
        {
            _logger.LogWarning(
                "Rejected PDF CRL evidence from {Source}: {Reason}",
                source,
                exception.GetType().Name);
            evidence = null!;
            return false;
        }
    }

    private bool TryValidateOcsp(
        byte[] bytes,
        string source,
        PdfRevocationEvidenceRequest request,
        out PdfRevocationEvidence evidence)
    {
        try
        {
            evidence = PdfOcspEvidence.ValidateResponse(
                bytes,
                request.Certificate,
                request.Issuer,
                request.ValidationTime,
                TimeSpan.FromHours(_options.MaximumOcspAgeHours),
                source);
            return true;
        }
        catch (Exception exception) when (exception is CryptographicException or AsnContentException)
        {
            _logger.LogWarning("Rejected PDF OCSP evidence from {Source}: {Reason}", source, exception.GetType().Name);
            evidence = null!;
            return false;
        }
    }

    private bool IsAllowedEndpoint(Uri endpoint) =>
        endpoint.Scheme == Uri.UriSchemeHttps &&
        endpoint.IsDefaultPort &&
        string.IsNullOrEmpty(endpoint.UserInfo) &&
        _allowedHosts.Contains(endpoint.IdnHost.TrimEnd('.'));

    private async Task<byte[]?> DownloadAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Accept.ParseAdd("application/pkix-crl, application/octet-stream;q=0.8");
            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode ||
                response.Content.Headers.ContentLength > _options.MaximumResponseBytes)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await ReadBoundedAsync(stream, _options.MaximumResponseBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException)
        {
            if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            _logger.LogWarning("PDF CRL retrieval failed for {Host}: {Reason}", endpoint.Host, exception.GetType().Name);
            return null;
        }
    }

    private async Task<byte[]?> DownloadOcspAsync(
        Uri endpoint,
        PdfRevocationEvidenceRequest evidenceRequest,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new ByteArrayContent(PdfOcspEvidence.CreateRequest(
                    evidenceRequest.Certificate,
                    evidenceRequest.Issuer))
            };
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/ocsp-request");
            request.Headers.Accept.ParseAdd("application/ocsp-response");
            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > _options.MaximumResponseBytes)
            {
                return null;
            }
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await ReadBoundedAsync(stream, _options.MaximumResponseBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException)
        {
            if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            _logger.LogWarning("PDF OCSP retrieval failed for {Host}: {Reason}", endpoint.Host, exception.GetType().Name);
            return null;
        }
    }

    private async Task<byte[]?> ReadCacheAsync(string cacheKey, CancellationToken cancellationToken)
    {
        var path = GetCachePath(cacheKey);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            return _protector.Unprotect(protectedBytes);
        }
        catch (Exception exception) when (exception is CryptographicException or IOException)
        {
            _logger.LogWarning("Ignored unreadable PDF revocation cache entry {CacheKey}.", Path.GetFileName(path));
            return null;
        }
    }

    private async Task WriteCacheAsync(string cacheKey, byte[] bytes, CancellationToken cancellationToken)
    {
        var path = GetCachePath(cacheKey);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, _protector.Protect(bytes), cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private string GetCachePath(string cacheKey) => Path.Combine(
        _cacheDirectory,
        $"{Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(cacheKey)))}.bin");

    private static string GetCrlCacheKey(Uri endpoint) => $"crl|{endpoint.AbsoluteUri}";

    private static string GetOcspCacheKey(Uri endpoint, System.Security.Cryptography.X509Certificates.X509Certificate2 certificate) =>
        $"ocsp|{endpoint.AbsoluteUri}|{certificate.Thumbprint}";

    private static byte[] ReadCrlBytes(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (!Path.GetExtension(path).Equals(".pem", StringComparison.OrdinalIgnoreCase))
        {
            return bytes;
        }

        var text = System.Text.Encoding.ASCII.GetString(bytes);
        if (!PemEncoding.TryFind(text, out var fields) ||
            !text[fields.Label].SequenceEqual("X509 CRL"))
        {
            throw new InvalidOperationException($"The configured PDF CRL PEM is invalid: '{path}'.");
        }
        return Convert.FromBase64String(text[fields.Base64Data].ToString());
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream stream,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream(Math.Min(maximumBytes, 64 * 1024));
        var chunk = new byte[32 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return buffer.ToArray();
            }
            if (buffer.Length + read > maximumBytes)
            {
                throw new IOException("The CRL response exceeds the configured limit.");
            }
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    private static string ResolvePath(string path, string contentRootPath) =>
        Path.IsPathRooted(path) ? path : Path.GetFullPath(path, contentRootPath);
}
