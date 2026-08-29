using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using PXA.Importer.Objects;
using PXA.Importer.Streams;

namespace PXA.Importer.Trust;

internal sealed record PdfEmbeddedValidationMaterial(
    IReadOnlyList<byte[]> Certificates,
    IReadOnlyList<byte[]> Crls,
    IReadOnlyList<byte[]> OcspResponses,
    IReadOnlyDictionary<string, PdfEmbeddedVriMaterial> VriEntries,
    int VriEntryCount,
    long TotalBytes)
{
    public PdfEmbeddedVriMaterial? FindForSignature(ReadOnlySpan<byte> signatureValue)
    {
        var key = Convert.ToHexString(SHA1.HashData(signatureValue));
        return VriEntries.GetValueOrDefault(key);
    }
}

internal sealed record PdfEmbeddedVriMaterial(
    IReadOnlyList<byte[]> Certificates,
    IReadOnlyList<byte[]> Crls,
    IReadOnlyList<byte[]> OcspResponses);

internal static class PdfEmbeddedRevocationEvidenceValidator
{
    private static readonly TimeSpan MaximumOcspProducedAtSkew = TimeSpan.FromHours(24);

    public static PdfRevocationEvidenceResult Validate(
        PdfEmbeddedVriMaterial material,
        X509Certificate2 certificate,
        X509Certificate2 issuer,
        DateTimeOffset trustedReferenceTime)
    {
        var candidates = new List<PdfRevocationEvidence>();
        foreach (var crl in material.Crls)
        {
            try
            {
                candidates.Add(PdfCrlEvidenceValidator.ValidateHistorical(
                    crl,
                    certificate,
                    issuer,
                    trustedReferenceTime,
                    "embedded:vri"));
            }
            catch (Exception exception) when (exception is CryptographicException or System.Formats.Asn1.AsnContentException)
            {
                // Other VRI entries can still provide valid evidence for this certificate.
            }
        }

        foreach (var ocsp in material.OcspResponses)
        {
            try
            {
                candidates.Add(PdfOcspEvidence.ValidateHistoricalResponse(
                    ocsp,
                    certificate,
                    issuer,
                    trustedReferenceTime,
                    MaximumOcspProducedAtSkew,
                    "embedded:vri"));
            }
            catch (Exception exception) when (exception is CryptographicException or System.Formats.Asn1.AsnContentException)
            {
                // Other VRI entries can still provide valid evidence for this certificate.
            }
        }

        if (candidates.Count == 0)
        {
            return PdfRevocationEvidenceResult.Unavailable(
                "No signature-bound embedded evidence validates for the certificate and issuer.");
        }

        var ordered = candidates
            .OrderBy(static item => item.Status == PdfTrustStatus.Invalid ? 0 : 1)
            .ThenByDescending(static item => item.ThisUpdate)
            .ToArray();
        var status = ordered.Any(static item => item.Status == PdfTrustStatus.Invalid)
            ? PdfTrustStatus.Invalid
            : ordered.Any(static item => item.Status == PdfTrustStatus.Valid)
                ? PdfTrustStatus.Valid
                : PdfTrustStatus.Indeterminate;
        return new PdfRevocationEvidenceResult(status, ordered);
    }
}

internal static class PdfEmbeddedValidationMaterialExtractor
{
    private const int MaximumItemsPerKind = 256;
    private const int MaximumItemBytes = 4 * 1024 * 1024;
    private const int MaximumTotalBytes = 16 * 1024 * 1024;

    public static PdfEmbeddedValidationMaterial Extract(PdfObjectGraph graph)
    {
        var catalog = Resolve(graph, graph.Trailer?["Root"]) as PdfDictionary;
        var dss = Resolve(graph, catalog?["DSS"]) as PdfDictionary;
        if (dss is null)
        {
            return new PdfEmbeddedValidationMaterial([], [], [],
                new Dictionary<string, PdfEmbeddedVriMaterial>(), 0, 0);
        }

        var certificates = new EvidenceCollector();
        var crls = new EvidenceCollector();
        var ocspResponses = new EvidenceCollector();
        CollectArray(graph, dss["Certs"], certificates);
        CollectArray(graph, dss["CRLs"], crls);
        CollectArray(graph, dss["OCSPs"], ocspResponses);

        var vri = Resolve(graph, dss["VRI"]) as PdfDictionary;
        var vriEntryCount = 0;
        var vriEntries = new Dictionary<string, PdfEmbeddedVriMaterial>(StringComparer.OrdinalIgnoreCase);
        if (vri is not null)
        {
            foreach (var (key, entry) in vri.Values)
            {
                if (Resolve(graph, entry) is not PdfDictionary validationRelatedInformation)
                {
                    continue;
                }

                vriEntryCount++;
                var vriCertificates = new EvidenceCollector();
                var vriCrls = new EvidenceCollector();
                var vriOcspResponses = new EvidenceCollector();
                CollectArray(graph, validationRelatedInformation["Cert"], vriCertificates, certificates);
                CollectArray(graph, validationRelatedInformation["CRL"], vriCrls, crls);
                CollectArray(graph, validationRelatedInformation["OCSP"], vriOcspResponses, ocspResponses);
                if (IsVriKey(key))
                {
                    vriEntries[key] = new PdfEmbeddedVriMaterial(
                        vriCertificates.Items,
                        vriCrls.Items,
                        vriOcspResponses.Items);
                }
            }
        }

        var totalBytes = checked(certificates.TotalBytes + crls.TotalBytes + ocspResponses.TotalBytes);
        if (totalBytes > MaximumTotalBytes)
        {
            throw new InvalidDataException("The embedded PDF validation material exceeds the 16 MiB inspection limit.");
        }

        return new PdfEmbeddedValidationMaterial(
            certificates.Items,
            crls.Items,
            ocspResponses.Items,
            vriEntries,
            vriEntryCount,
            totalBytes);
    }

    private static void CollectArray(
        PdfObjectGraph graph,
        PdfObject? value,
        params EvidenceCollector[] collectors)
    {
        if (Resolve(graph, value) is not PdfArray array)
        {
            return;
        }

        foreach (var item in array.Items)
        {
            if (Resolve(graph, item) is not PdfStreamObject stream)
            {
                continue;
            }

            var decoder = new PdfStreamDecoderRegistry();
            var support = decoder.Evaluate(stream.Dictionary["Filter"]);
            if (support.Any(static item => item.Status != PdfStreamDecoderSupportStatus.Supported))
            {
                throw new NotSupportedException("The embedded validation material uses an unsupported stream filter.");
            }

            var bytes = (stream.IsDecoded ? stream.DecodedBytes : decoder.Decode(stream)).ToArray();
            if (bytes.Length is 0 or > MaximumItemBytes)
            {
                throw new InvalidDataException("An embedded PDF validation item is empty or exceeds the 4 MiB item limit.");
            }
            foreach (var collector in collectors)
            {
                collector.Add(bytes);
            }
        }
    }

    private static bool IsVriKey(string key) =>
        key.Length == 40 && key.All(static value => char.IsAsciiHexDigit(value));

    private static PdfObject? Resolve(PdfObjectGraph graph, PdfObject? value)
    {
        var visited = new HashSet<PdfObjectId>();
        while (value is PdfReference reference && visited.Add(reference.Id))
        {
            value = graph.Resolve(reference.Id)?.Value;
        }
        return value;
    }

    private sealed class EvidenceCollector
    {
        private readonly HashSet<string> _hashes = new(StringComparer.Ordinal);
        private readonly List<byte[]> _items = [];

        public IReadOnlyList<byte[]> Items => _items;
        public long TotalBytes { get; private set; }

        public void Add(byte[] bytes)
        {
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            if (!_hashes.Add(hash))
            {
                return;
            }
            if (_items.Count >= MaximumItemsPerKind)
            {
                throw new InvalidDataException("The embedded PDF validation material exceeds the item-count limit.");
            }
            TotalBytes = checked(TotalBytes + bytes.Length);
            _items.Add(bytes);
        }
    }
}
