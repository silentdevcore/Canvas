using Microsoft.Extensions.Options;
using PVAD.Importer.Signing;

namespace PVAD.WebApi.Services.Pdf;

public sealed class PvadPdfTimestampAuthorityOptions
{
    public const string SectionName = "PdfTrust:TimestampAuthority";

    public string? Endpoint { get; init; }
    public int MaximumResponseBytes { get; init; } = 1024 * 1024;
    public bool RequireTrustedCertificateChain { get; init; } = true;
}

public interface IPvadPdfTimestampAuthorityFactory
{
    IPdfTimestampAuthority? Create();
}

public sealed class PvadPdfTimestampAuthorityFactory(
    IHttpClientFactory httpClientFactory,
    IOptions<PvadPdfTimestampAuthorityOptions> options) : IPvadPdfTimestampAuthorityFactory
{
    public const string HttpClientName = "PVAD.Pdf.TimestampAuthority";

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
