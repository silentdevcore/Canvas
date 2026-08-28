using Microsoft.Extensions.Options;
using PXA.Importer.Signing;

namespace PXA.WebApi.Services.Pdf;

public sealed class PxaPdfTimestampAuthorityOptions
{
    public const string SectionName = "PdfTrust:TimestampAuthority";

    public string? Endpoint { get; init; }
    public int MaximumResponseBytes { get; init; } = 1024 * 1024;
    public bool RequireTrustedCertificateChain { get; init; } = true;
}

public interface IPxaPdfTimestampAuthorityFactory
{
    IPdfTimestampAuthority? Create();
}

public sealed class PxaPdfTimestampAuthorityFactory(
    IHttpClientFactory httpClientFactory,
    IOptions<PxaPdfTimestampAuthorityOptions> options) : IPxaPdfTimestampAuthorityFactory
{
    public const string HttpClientName = "PXA.Pdf.TimestampAuthority";

    public IPdfTimestampAuthority? Create()
    {
        var configured = options.Value;
        if (string.IsNullOrWhiteSpace(configured.Endpoint))
        {
            return null;
        }

        return new PdfRfc3161TimestampAuthority(
            httpClientFactory.CreateClient(HttpClientName),
            new PdfRfc3161TimestampAuthorityOptions
            {
                Endpoint = new Uri(configured.Endpoint, UriKind.Absolute),
                MaximumResponseBytes = configured.MaximumResponseBytes,
                RequireTrustedCertificateChain = configured.RequireTrustedCertificateChain
            });
    }
}
