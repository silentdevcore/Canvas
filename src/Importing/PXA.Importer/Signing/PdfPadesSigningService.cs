using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Saving;
using PXA.Importer.Xref;

namespace PXA.Importer.Signing;

public interface IPdfPadesSigningService
{
    PdfSigningResult Sign(ReadOnlyMemory<byte> source, IPdfSigningKey signingKey, PdfSigningOptions? options = null);
}

public sealed class PdfPadesSigningService : IPdfPadesSigningService
{
    private const long ByteRangeMarker1 = 8_888_888_881;
    private const long ByteRangeMarker2 = 8_888_888_882;
    private const long ByteRangeMarker3 = 8_888_888_883;
    private const byte ContentsMarkerByte = 0xA5;

    public PdfSigningResult Sign(
        ReadOnlyMemory<byte> source,
        IPdfSigningKey signingKey,
        PdfSigningOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        options ??= new PdfSigningOptions();
        ValidateOptions(options);

        var signingTime = options.SigningTime ?? DateTimeOffset.UtcNow;
        ValidateCertificate(signingKey.Certificate, signingTime);
        var parsed = Parse(source);
        var updates = CreateSignatureUpdates(parsed.Graph, options, signingTime, out var signatureObjectId);
        var incrementalResult = new PdfIncrementalUpdateWriter().Append(source, updates);
        var signedBytes = incrementalResult.Bytes;

        var contentMarker = Convert.ToHexString(Enumerable.Repeat(ContentsMarkerByte, options.ContentsPlaceholderBytes).ToArray());
        var contentHexOffset = FindUniqueAscii(signedBytes, contentMarker, source.Length);
        var contentsStart = contentHexOffset - 1;
        var contentsEnd = contentHexOffset + contentMarker.Length + 1;
        if (contentsStart < source.Length || signedBytes[contentsStart] != (byte)'<' || signedBytes[contentsEnd - 1] != (byte)'>')
        {
            throw new InvalidDataException("The PDF signature contents placeholder could not be resolved safely.");
        }

        PatchFixedWidthInteger(signedBytes, ByteRangeMarker1, contentsStart, source.Length);
        PatchFixedWidthInteger(signedBytes, ByteRangeMarker2, contentsEnd, source.Length);
        PatchFixedWidthInteger(signedBytes, ByteRangeMarker3, signedBytes.Length - contentsEnd, source.Length);

        using var documentHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        documentHash.AppendData(signedBytes.AsSpan(0, contentsStart));
        documentHash.AppendData(signedBytes.AsSpan(contentsEnd));
        var digest = documentHash.GetHashAndReset();
        var cms = PdfCadesCmsBuilder.CreateDetached(digest, signingKey, signingTime).Cms;
        if (cms.Length > options.ContentsPlaceholderBytes)
        {
            throw new InvalidOperationException(
                $"The CMS signature requires {cms.Length} bytes, exceeding the {options.ContentsPlaceholderBytes}-byte PDF placeholder.");
        }

        var cmsHex = Convert.ToHexString(cms);
        Encoding.ASCII.GetBytes(cmsHex, signedBytes.AsSpan(contentHexOffset, cmsHex.Length));
        signedBytes.AsSpan(contentHexOffset + cmsHex.Length, contentMarker.Length - cmsHex.Length).Fill((byte)'0');

        var finalDocument = Parse(signedBytes);
        ValidateSavedSignature(
            finalDocument.Graph,
            signatureObjectId,
            contentsStart,
            contentsEnd,
            signedBytes.Length,
            cms.Length);

        return new PdfSigningResult(
            signedBytes,
            options.Profile,
            options.FieldName.Trim(),
            "SHA-256",
            signingKey.Certificate.Subject,
            signingKey.Certificate.Thumbprint,
            signingTime,
            finalDocument.Table.Revisions.Count);
    }

    private static IReadOnlyList<PdfIncrementalObjectUpdate> CreateSignatureUpdates(
        PdfObjectGraph graph,
        PdfSigningOptions options,
        DateTimeOffset signingTime,
        out PdfObjectId signatureObjectId)
    {
        if (graph.Trailer?["Encrypt"] is not null)
        {
            throw new NotSupportedException("Signing encrypted PDFs is not enabled until incremental encryption is supported.");
        }

        if (graph.Trailer?["Root"] is not PdfReference rootReference ||
            graph.Resolve(rootReference.Id)?.Value is not PdfDictionary catalog)
        {
            throw new InvalidDataException("The PDF catalog root could not be resolved for signing.");
        }


        EnsureFieldNameIsAvailable(graph, catalog, options.FieldName.Trim());

        var pageReference = FindFirstPageReference(graph, catalog)
            ?? throw new InvalidDataException("The PDF does not contain an indirect page object for the signature widget.");
        if (graph.Resolve(pageReference.Id)?.Value is not PdfDictionary pageDictionary)
        {
            throw new InvalidDataException("The PDF signature page could not be resolved.");
        }

        var nextObjectNumber = graph.Objects.Keys.Max(static id => id.Number) + 1;
        signatureObjectId = new PdfObjectId(nextObjectNumber++, 0);
        var fieldObjectId = new PdfObjectId(nextObjectNumber, 0);
        var signatureDictionary = new PdfDictionary
        {
            ["Type"] = new PdfName("Sig"),
            ["Filter"] = new PdfName("Adobe.PPKLite"),
            ["SubFilter"] = new PdfName("ETSI.CAdES.detached"),
            ["ByteRange"] = new PdfArray([
                new PdfInteger(0),
                new PdfInteger(ByteRangeMarker1),
                new PdfInteger(ByteRangeMarker2),
                new PdfInteger(ByteRangeMarker3)
            ]),
            ["Contents"] = new PdfString(
                Enumerable.Repeat(ContentsMarkerByte, options.ContentsPlaceholderBytes).ToArray(),
                IsHex: false),
            ["M"] = EncodePdfString(signingTime.ToUniversalTime().ToString("'D:'yyyyMMddHHmmss'Z'", CultureInfo.InvariantCulture))
        };
        AddOptionalString(signatureDictionary, "Reason", options.Reason);
        AddOptionalString(signatureDictionary, "Location", options.Location);
        AddOptionalString(signatureDictionary, "ContactInfo", options.ContactInfo);

        var fieldDictionary = new PdfDictionary
        {
            ["Type"] = new PdfName("Annot"),
            ["Subtype"] = new PdfName("Widget"),
            ["FT"] = new PdfName("Sig"),
            ["T"] = EncodePdfString(options.FieldName.Trim()),
            ["V"] = new PdfReference(signatureObjectId),
            ["Rect"] = new PdfArray([
                new PdfInteger(0),
                new PdfInteger(0),
                new PdfInteger(1),
                new PdfInteger(1)
            ]),
            ["F"] = new PdfInteger(34),
            ["P"] = pageReference
        };
        var fieldReference = new PdfReference(fieldObjectId);
        var pageWithWidget = CloneDictionary(pageDictionary);
        var annotations = Resolve(graph, pageDictionary["Annots"]) switch
        {
            PdfArray array => new PdfArray(array.Items),
            null => new PdfArray(),
            _ => throw new InvalidDataException("The page /Annots entry is not an array.")
        };
        annotations.Add(fieldReference);
        pageWithWidget["Annots"] = annotations;

        var updates = new List<PdfIncrementalObjectUpdate>
        {
            new(signatureObjectId, signatureDictionary),
            new(fieldObjectId, fieldDictionary),
            new(pageReference.Id, pageWithWidget)
        };

        switch (catalog["AcroForm"])
        {
            case PdfReference acroFormReference
                when graph.Resolve(acroFormReference.Id)?.Value is PdfDictionary acroForm:
                updates.Add(new PdfIncrementalObjectUpdate(
                    acroFormReference.Id,
                    AddSignatureField(acroForm, graph, fieldReference)));
                break;
            case PdfDictionary directAcroForm:
                var catalogWithDirectForm = CloneDictionary(catalog);
                catalogWithDirectForm["AcroForm"] = AddSignatureField(directAcroForm, graph, fieldReference);
                updates.Add(new PdfIncrementalObjectUpdate(rootReference.Id, catalogWithDirectForm));
                break;
            case null:
                var catalogWithNewForm = CloneDictionary(catalog);
                catalogWithNewForm["AcroForm"] = AddSignatureField(new PdfDictionary(), graph, fieldReference);
                updates.Add(new PdfIncrementalObjectUpdate(rootReference.Id, catalogWithNewForm));
                break;
            default:
                throw new InvalidDataException("The existing /AcroForm entry is not a supported dictionary or reference.");
        }

        return updates;
    }

    private static PdfReference? FindFirstPageReference(PdfObjectGraph graph, PdfDictionary catalog)
    {
        var visited = new HashSet<PdfObjectId>();
        var fromTree = FindFirstPageReference(graph, catalog["Pages"], visited);
        if (fromTree is not null)
        {
            return fromTree;
        }

        var fallback = graph.Objects.Values
            .Where(static item => item.Value is PdfDictionary { } dictionary &&
                dictionary["Type"] is PdfName { Value: "Page" })
            .OrderBy(static item => item.Id.Number)
            .ThenBy(static item => item.Id.Generation)
            .FirstOrDefault();
        return fallback is null ? null : new PdfReference(fallback.Id);
    }

    private static PdfReference? FindFirstPageReference(
        PdfObjectGraph graph,
        PdfObject? candidate,
        HashSet<PdfObjectId> visited)
    {
        if (candidate is not PdfReference reference || !visited.Add(reference.Id) ||
            graph.Resolve(reference.Id)?.Value is not PdfDictionary dictionary)
        {
            return null;
        }

        if (dictionary["Type"] is PdfName { Value: "Page" })
        {
            return reference;
        }

        if (Resolve(graph, dictionary["Kids"]) is not PdfArray kids)
        {
            return null;
        }

        foreach (var kid in kids.Items)
        {
            var page = FindFirstPageReference(graph, kid, visited);
            if (page is not null)
            {
                return page;
            }
        }

        return null;
    }

    private static PdfDictionary AddSignatureField(
        PdfDictionary source,
        PdfObjectGraph graph,
        PdfReference fieldReference)
    {
        var result = CloneDictionary(source);
        var fields = Resolve(graph, source["Fields"]) switch
        {
            PdfArray array => new PdfArray(array.Items),
            null => new PdfArray(),
            _ => throw new InvalidDataException("The AcroForm /Fields entry is not an array.")
        };
        fields.Add(fieldReference);
        result["Fields"] = fields;
        result["SigFlags"] = new PdfInteger(3);
        return result;
    }

    private static void EnsureFieldNameIsAvailable(PdfObjectGraph graph, PdfDictionary catalog, string fieldName)
    {
        if (Resolve(graph, catalog["AcroForm"]) is not PdfDictionary acroForm ||
            Resolve(graph, acroForm["Fields"]) is not PdfArray fields)
        {
            return;
        }

        var visited = new HashSet<PdfObjectId>();
        foreach (var field in fields.Items)
        {
            if (ContainsFieldName(graph, field, fieldName, visited))
            {
                throw new InvalidOperationException($"A PDF form field named '{fieldName}' already exists.");
            }
        }
    }

    private static bool ContainsFieldName(
        PdfObjectGraph graph,
        PdfObject fieldObject,
        string fieldName,
        HashSet<PdfObjectId> visited)
    {
        if (fieldObject is PdfReference reference && !visited.Add(reference.Id))
        {
            return false;
        }

        if (Resolve(graph, fieldObject) is not PdfDictionary field)
        {
            return false;
        }

        if (Resolve(graph, field["T"]) is PdfString name &&
            string.Equals(DecodePdfString(name), fieldName, StringComparison.Ordinal))
        {
            return true;
        }

        return Resolve(graph, field["Kids"]) is PdfArray kids &&
            kids.Items.Any(kid => ContainsFieldName(graph, kid, fieldName, visited));
    }

    private static ParsedPdf Parse(ReadOnlyMemory<byte> source)
    {
        var context = new PdfParseContext(source, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        if (table.Diagnostics.Count > 0)
        {
            throw new InvalidDataException($"The PDF revision chain is invalid: {table.Diagnostics[0].Code}.");
        }

        var graph = new PdfObjectParser(context, table).ParseDocumentGraph();
        return new ParsedPdf(table, graph);
    }

    private static void ValidateSavedSignature(
        PdfObjectGraph graph,
        PdfObjectId signatureObjectId,
        long contentsStart,
        long contentsEnd,
        int fileLength,
        int cmsLength)
    {
        if (graph.Resolve(signatureObjectId)?.Value is not PdfDictionary signature ||
            signature["ByteRange"] is not PdfArray byteRange ||
            byteRange.Items.OfType<PdfInteger>().Select(static item => item.Value).ToArray() is not
                [0, var firstLength, var secondStart, var secondLength] ||
            firstLength != contentsStart ||
            secondStart != contentsEnd ||
            secondLength != fileLength - contentsEnd ||
            signature["Contents"] is not PdfString contents ||
            contents.GetDecodedBytes().Length < cmsLength)
        {
            throw new InvalidDataException("Post-sign validation could not verify the PDF signature dictionary and byte ranges.");
        }
    }

    private static void ValidateOptions(PdfSigningOptions options)
    {
        if (options.Profile != PdfPadesProfile.BaselineB)
        {
            throw new NotSupportedException($"PAdES profile '{options.Profile}' is not supported yet.");
        }

        if (string.IsNullOrWhiteSpace(options.FieldName) || options.FieldName.Trim().Length > 200)
        {
            throw new ArgumentException("The signature field name must contain 1 to 200 characters.", nameof(options));
        }

        if (options.ContentsPlaceholderBytes is < 8 * 1024 or > 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The CMS placeholder must be between 8 KiB and 1 MiB.");
        }
    }

    private static void ValidateCertificate(X509Certificate2 certificate, DateTimeOffset signingTime)
    {
        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException("The signing certificate does not expose a private key.");
        }

        if (signingTime.UtcDateTime < certificate.NotBefore.ToUniversalTime() ||
            signingTime.UtcDateTime > certificate.NotAfter.ToUniversalTime())
        {
            throw new InvalidOperationException("The signing certificate is not valid at the requested signing time.");
        }

        var keyUsage = certificate.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();
        if (keyUsage is not null &&
            (keyUsage.KeyUsages & (X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation)) == 0)
        {
            throw new InvalidOperationException("The certificate key usage does not permit digital signatures.");
        }
    }

    private static int FindUniqueAscii(byte[] bytes, string value, int startOffset)
    {
        var pattern = Encoding.ASCII.GetBytes(value);
        var relative = bytes.AsSpan(startOffset).IndexOf(pattern);
        if (relative < 0)
        {
            throw new InvalidDataException("A PDF signing placeholder was not found.");
        }

        var absolute = startOffset + relative;
        if (bytes.AsSpan(absolute + pattern.Length).IndexOf(pattern) >= 0)
        {
            throw new InvalidDataException("A PDF signing placeholder was not unique.");
        }

        return absolute;
    }

    private static void PatchFixedWidthInteger(byte[] bytes, long marker, long value, int startOffset)
    {
        if (value is < 0 or > 9_999_999_999L)
        {
            throw new NotSupportedException("PDF signature byte ranges currently support offsets up to ten decimal digits.");
        }

        var markerText = marker.ToString(CultureInfo.InvariantCulture);
        var offset = FindUniqueAscii(bytes, markerText, startOffset);
        var replacement = value.ToString("D10", CultureInfo.InvariantCulture);
        Encoding.ASCII.GetBytes(replacement, bytes.AsSpan(offset, replacement.Length));
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

    private static PdfDictionary CloneDictionary(PdfDictionary source) =>
        new(new Dictionary<string, PdfObject>(source.Values, StringComparer.Ordinal));

    private static void AddOptionalString(PdfDictionary dictionary, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            dictionary[key] = EncodePdfString(value.Trim());
        }
    }

    private static PdfString EncodePdfString(string value)
    {
        var textBytes = Encoding.BigEndianUnicode.GetBytes(value);
        var bytes = new byte[textBytes.Length + 2];
        bytes[0] = 0xFE;
        bytes[1] = 0xFF;
        textBytes.CopyTo(bytes, 2);
        return new PdfString(bytes, IsHex: false);
    }

    private static string DecodePdfString(PdfString value)
    {
        var bytes = value.GetDecodedBytes().Span;
        return bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF
            ? Encoding.BigEndianUnicode.GetString(bytes[2..])
            : Encoding.Latin1.GetString(bytes);
    }

    private sealed record ParsedPdf(PdfCrossReferenceTable Table, PdfObjectGraph Graph);
}
