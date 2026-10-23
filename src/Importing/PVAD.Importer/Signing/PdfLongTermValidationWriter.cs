using System.Security.Cryptography;
using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Saving;
using PVAD.Importer.Streams;
using PVAD.Importer.Xref;

namespace PVAD.Importer.Signing;

internal sealed record PdfLongTermValidationWriteResult(byte[] Bytes, int RevisionCount);

internal static class PdfLongTermValidationWriter
{
    private const int MaximumItemsPerKind = 256;
    private const int MaximumItemBytes = 4 * 1024 * 1024;
    private const int MaximumTotalBytes = 16 * 1024 * 1024;

    public static PdfLongTermValidationWriteResult Append(
        ReadOnlyMemory<byte> signedPdf,
        ReadOnlySpan<byte> cmsSignatureValue,
        PdfLongTermValidationMaterial material)
    {
        ArgumentNullException.ThrowIfNull(material);
        if (signedPdf.IsEmpty || cmsSignatureValue.IsEmpty)
        {
            throw new ArgumentException("A signed PDF and its CMS signature value are required.");
        }

        ValidateMaterial(material);
        var parsed = Parse(signedPdf);
        if (parsed.Graph.Trailer?["Root"] is not PdfReference rootReference ||
            parsed.Graph.Resolve(rootReference.Id)?.Value is not PdfDictionary catalog)
        {
            throw new InvalidDataException("The PDF catalog root could not be resolved for long-term validation.");
        }

        var existingDss = Resolve(parsed.Graph, catalog["DSS"]) as PdfDictionary;
        var nextObjectNumber = parsed.Graph.Objects.Keys.Max(static id => id.Number) + 1;
        var updates = new List<PdfIncrementalObjectUpdate>();
        var certificates = new MaterialCollection(parsed.Graph, existingDss?["Certs"]);
        var crls = new MaterialCollection(parsed.Graph, existingDss?["CRLs"]);
        var ocspResponses = new MaterialCollection(parsed.Graph, existingDss?["OCSPs"]);

        var signatureCertificates = material.Certificates
            .Concat(material.TrustAnchors)
            .Select(bytes => certificates.Add(bytes, ref nextObjectNumber, updates))
            .ToArray();
        var signatureCrls = material.Crls
            .Select(bytes => crls.Add(bytes, ref nextObjectNumber, updates))
            .ToArray();
        var signatureOcspResponses = material.OcspResponses
            .Select(bytes => ocspResponses.Add(bytes, ref nextObjectNumber, updates))
            .ToArray();

        var vriKey = Convert.ToHexString(SHA1.HashData(cmsSignatureValue));
        var existingVri = Resolve(parsed.Graph, existingDss?["VRI"]) as PdfDictionary;
        var vriValues = existingVri?.Values
            .Where(entry => !entry.Key.Equals(vriKey, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(static entry => entry.Key, static entry => entry.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, PdfObject>(StringComparer.Ordinal);
        var matchingVri = existingVri?.Values
            .FirstOrDefault(entry => entry.Key.Equals(vriKey, StringComparison.OrdinalIgnoreCase)).Value;
        var vriEntry = CloneDictionary(Resolve(parsed.Graph, matchingVri) as PdfDictionary);
        vriEntry["Cert"] = MergeReferences(parsed.Graph, vriEntry["Cert"], signatureCertificates);
        if (signatureCrls.Length > 0)
        {
            vriEntry["CRL"] = MergeReferences(parsed.Graph, vriEntry["CRL"], signatureCrls);
        }
        if (signatureOcspResponses.Length > 0)
        {
            vriEntry["OCSP"] = MergeReferences(parsed.Graph, vriEntry["OCSP"], signatureOcspResponses);
        }
        vriValues[vriKey] = vriEntry;

        var dss = CloneDictionary(existingDss);
        dss["Certs"] = certificates.ToArray();
        if (crls.Count > 0)
        {
            dss["CRLs"] = crls.ToArray();
        }
        if (ocspResponses.Count > 0)
        {
            dss["OCSPs"] = ocspResponses.ToArray();
        }
        dss["VRI"] = new PdfDictionary(vriValues);

        var dssId = new PdfObjectId(nextObjectNumber, 0);
        updates.Add(new PdfIncrementalObjectUpdate(dssId, dss));
        var updatedCatalog = CloneDictionary(catalog);
        updatedCatalog["DSS"] = new PdfReference(dssId);
        updates.Add(new PdfIncrementalObjectUpdate(rootReference.Id, updatedCatalog));

        var result = new PdfIncrementalUpdateWriter().Append(signedPdf, updates);
        return new PdfLongTermValidationWriteResult(result.Bytes, result.RevisionCount);
    }

    private static void ValidateMaterial(PdfLongTermValidationMaterial material)
    {
        if (material.TrustAnchors.Count == 0)
        {
            throw new InvalidOperationException("PAdES Baseline LT requires at least one explicit trust anchor.");
        }

        if (material.Certificates.Count + material.TrustAnchors.Count > MaximumItemsPerKind ||
            material.Crls.Count > MaximumItemsPerKind ||
            material.OcspResponses.Count > MaximumItemsPerKind)
        {
            throw new InvalidDataException("The long-term validation material exceeds the 256-item limit.");
        }

        var allItems = material.Certificates
            .Concat(material.TrustAnchors)
            .Concat(material.Crls)
            .Concat(material.OcspResponses)
            .ToArray();
        if (allItems.Any(static bytes => bytes.Length is 0 or > MaximumItemBytes) ||
            allItems.Sum(static bytes => (long)bytes.Length) > MaximumTotalBytes)
        {
            throw new InvalidDataException("The long-term validation material exceeds its item or total byte limit.");
        }
    }

    private static PdfArray MergeReferences(
        PdfObjectGraph graph,
        PdfObject? existing,
        IEnumerable<PdfObject> additions)
    {
        var values = Resolve(graph, existing) is PdfArray array
            ? array.Items.ToList()
            : [];
        var existingReferences = values.OfType<PdfReference>().Select(static item => item.Id).ToHashSet();
        foreach (var addition in additions)
        {
            if (addition is not PdfReference reference || existingReferences.Add(reference.Id))
            {
                values.Add(addition);
            }
        }
        return new PdfArray(values);
    }

    private static PdfDictionary CloneDictionary(PdfDictionary? source) =>
        new(source?.Values.ToDictionary(static entry => entry.Key, static entry => entry.Value, StringComparer.Ordinal));

    private static ParsedPdf Parse(ReadOnlyMemory<byte> source)
    {
        var context = new PdfParseContext(source, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        if (table.Diagnostics.Count > 0)
        {
            throw new InvalidDataException($"The PDF revision chain is invalid: {table.Diagnostics[0].Code}.");
        }
        return new ParsedPdf(table, new PdfObjectParser(context, table).ParseDocumentGraph());
    }

    private static PdfObject? Resolve(PdfObjectGraph graph, PdfObject? value)
    {
        var visited = new HashSet<PdfObjectId>();
        while (value is PdfReference reference && visited.Add(reference.Id))
        {
            value = graph.Resolve(reference.Id)?.Value;
        }
        return value;
    }

    private sealed class MaterialCollection
    {
        private readonly List<PdfObject> _items = [];
        private readonly Dictionary<string, PdfObject> _referencesByHash = new(StringComparer.Ordinal);

        public MaterialCollection(PdfObjectGraph graph, PdfObject? source)
        {
            if (Resolve(graph, source) is not PdfArray array)
            {
                return;
            }
            foreach (var item in array.Items)
            {
                _items.Add(item);
                if (TryReadBytes(graph, item, out var bytes))
                {
                    _referencesByHash.TryAdd(Hash(bytes), item);
                }
            }
        }

        public int Count => _items.Count;

        public PdfObject Add(
            byte[] bytes,
            ref int nextObjectNumber,
            ICollection<PdfIncrementalObjectUpdate> updates)
        {
            var hash = Hash(bytes);
            if (_referencesByHash.TryGetValue(hash, out var existing))
            {
                return existing;
            }
            var id = new PdfObjectId(nextObjectNumber++, 0);
            var reference = new PdfReference(id);
            _items.Add(reference);
            _referencesByHash.Add(hash, reference);
            updates.Add(new PdfIncrementalObjectUpdate(id, new PdfStreamObject(new PdfDictionary(), bytes)));
            return reference;
        }

        public PdfArray ToArray() => new(_items);

        private static bool TryReadBytes(PdfObjectGraph graph, PdfObject value, out byte[] bytes)
        {
            bytes = [];
            if (Resolve(graph, value) is not PdfStreamObject stream)
            {
                return false;
            }
            try
            {
                var decoder = new PdfStreamDecoderRegistry();
                if (decoder.Evaluate(stream.Dictionary["Filter"])
                    .Any(static item => item.Status != PdfStreamDecoderSupportStatus.Supported))
                {
                    return false;
                }
                bytes = (stream.IsDecoded ? stream.DecodedBytes : decoder.Decode(stream)).ToArray();
                return bytes.Length > 0;
            }
            catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
            {
                return false;
            }
        }

        private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    }

    private sealed record ParsedPdf(PdfCrossReferenceTable Table, PdfObjectGraph Graph);
}
