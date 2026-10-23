using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Xref;
using System.Security.Cryptography.X509Certificates;

namespace PVAD.Importer.Security;

/// <summary>
/// Decrypts a parsed object graph after the password has been authenticated.
///
/// Decryption happens on the typed model rather than on the byte stream, and no decrypted
/// content is ever written anywhere.
/// </summary>
public static class PdfDocumentDecryptor
{
    /// <summary>True when the document declares encryption.</summary>
    public static bool IsEncrypted(PdfCrossReferenceTable? xref) => xref?.Trailer?["Encrypt"] is not null;

    /// <summary>
    /// Authenticates <paramref name="password"/> and decrypts every string and stream the
    /// graph holds. Throws <see cref="PdfPasswordException"/> when the password does not open
    /// the document.
    /// </summary>
    public static PdfDecryptionResult Decrypt(
        PdfCrossReferenceTable xref,
        PdfObjectGraph graph,
        string password)
    {
        ArgumentNullException.ThrowIfNull(xref);
        ArgumentNullException.ThrowIfNull(graph);

        var encryptReference = xref.Trailer?["Encrypt"]
            ?? throw new InvalidOperationException("The document does not declare an /Encrypt dictionary.");
        var encryptId = (encryptReference as PdfReference)?.Id;
        var encrypt = Resolve(graph, encryptReference) as PdfDictionary
            ?? throw new InvalidDataException("The /Encrypt entry does not resolve to a dictionary.");

        if (xref.Entries.Values.Any(static entry => entry.Kind == PdfCrossReferenceEntryKind.Compressed) &&
            xref.Entries.Values.Any(entry => entry.Kind == PdfCrossReferenceEntryKind.Compressed && graph.Resolve(entry.Id) is not null))
        {
            throw new InvalidOperationException(
                "Compressed objects were expanded before decryption. Use ParseAndDecrypt so object streams are decrypted first.");
        }

        PdfDecryptionResult result;
        using IPdfDocumentSecurityHandler handler = PdfStandardSecurityHandler.IsSupported(encrypt)
            ? PdfStandardSecurityHandler.Open(encrypt, password, out result)
            : PdfLegacyStandardSecurityHandler.Open(encrypt, ReadDocumentId(xref), password, out result);
        ApplyDecryption(graph, encryptId, handler, result);
        return result;
    }

    /// <summary>Decrypts an Adobe.PubSec document using the matching X.509 private key.</summary>
    public static PdfDecryptionResult Decrypt(
        PdfCrossReferenceTable xref,
        PdfObjectGraph graph,
        X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(xref);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(certificate);
        var encryptReference = xref.Trailer?["Encrypt"]
            ?? throw new InvalidOperationException("The document does not declare an /Encrypt dictionary.");
        var encryptId = (encryptReference as PdfReference)?.Id;
        var encrypt = Resolve(graph, encryptReference) as PdfDictionary
            ?? throw new InvalidDataException("The /Encrypt entry does not resolve to a dictionary.");
        using IPdfDocumentSecurityHandler handler =
            PdfPublicKeySecurityHandler.Open(encrypt, certificate, out var result);
        ApplyDecryption(graph, encryptId, handler, result);
        return result;
    }

    /// <summary>
    /// Parses and decrypts an encrypted graph in the required order: ordinary objects first,
    /// then encrypted object streams, then the compressed objects contained in those streams.
    /// </summary>
    public static (PdfObjectGraph Graph, PdfDecryptionResult Result) ParseAndDecrypt(
        PdfParseContext context,
        PdfCrossReferenceTable xref,
        string password)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(xref);

        var parser = new PdfObjectParser(context, xref);
        var graph = parser.ParseDocumentGraph(parseCompressedObjects: false);
        var result = Decrypt(xref, graph, password);
        parser.ParseCompressedObjects(graph);
        return (graph, result);
    }

    public static (PdfObjectGraph Graph, PdfDecryptionResult Result) ParseAndDecrypt(
        PdfParseContext context,
        PdfCrossReferenceTable xref,
        X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(xref);
        ArgumentNullException.ThrowIfNull(certificate);
        var parser = new PdfObjectParser(context, xref);
        var graph = parser.ParseDocumentGraph(parseCompressedObjects: false);
        var result = Decrypt(xref, graph, certificate);
        parser.ParseCompressedObjects(graph);
        return (graph, result);
    }

    private static void ApplyDecryption(
        PdfObjectGraph graph,
        PdfObjectId? encryptId,
        IPdfDocumentSecurityHandler handler,
        PdfDecryptionResult result)
    {
        var replacements = new Dictionary<PdfObjectId, PdfObject>(graph.Objects.Count);
        foreach (var indirectObject in graph.Objects.Values)
        {
            if (encryptId is { } id && indirectObject.Id == id) continue;
            replacements[indirectObject.Id] = DecryptValue(
                indirectObject.Value,
                handler,
                result.EncryptMetadata,
                indirectObject.Id,
                depth: 0);
        }

        foreach (var replacement in replacements)
        {
            graph.Resolve(replacement.Key)!.Value = replacement.Value;
        }
    }

    private static PdfObject DecryptValue(
        PdfObject value,
        IPdfDocumentSecurityHandler handler,
        bool encryptMetadata,
        PdfObjectId objectId,
        int depth)
    {
        if (depth > 256)
        {
            throw new InvalidDataException("The document nests objects too deeply to decrypt.");
        }

        switch (value)
        {
            case PdfString text:
                return handler.ShouldDecryptString
                    ? new PdfString(handler.DecryptValue(
                        text.GetDecodedBytes().Span, objectId.Number, objectId.Generation), IsHex: false)
                    : text;
            case PdfArray array:
                return new PdfArray(array.Items.Select(item =>
                    DecryptValue(item, handler, encryptMetadata, objectId, depth + 1)));
            case PdfDictionary dictionary:
                var values = new Dictionary<string, PdfObject>(dictionary.Values.Count, StringComparer.Ordinal);
                foreach (var entry in dictionary.Values)
                {
                    values[entry.Key] = DecryptValue(
                        entry.Value, handler, encryptMetadata, objectId, depth + 1);
                }

                return new PdfDictionary(values);
            case PdfStreamObject streamObject:
                // Cross-reference streams carry trailer values such as /ID and are explicitly
                // outside document encryption. Leave both their dictionary and bytes untouched.
                if (streamObject.Dictionary["Type"] is PdfName { Value: "XRef" })
                {
                    return streamObject;
                }

                var dictionaryValue = (PdfDictionary)DecryptValue(
                    streamObject.Dictionary, handler, encryptMetadata, objectId, depth + 1);
                if (!encryptMetadata && streamObject.Dictionary["Type"] is PdfName { Value: "Metadata" })
                {
                    return new PdfStreamObject(dictionaryValue, streamObject.EncodedBytes);
                }

                if (!handler.ShouldDecryptStream(streamObject.Dictionary))
                {
                    return new PdfStreamObject(dictionaryValue, streamObject.EncodedBytes);
                }

                var decoded = handler is PdfLegacyStandardSecurityHandler legacy
                    ? legacy.DecryptStreamValue(
                        streamObject.EncodedBytes.Span,
                        objectId.Number,
                        objectId.Generation,
                        streamObject.Dictionary)
                    : handler.DecryptValue(
                        streamObject.EncodedBytes.Span, objectId.Number, objectId.Generation);
                dictionaryValue["Length"] = new PdfInteger(decoded.Length);
                return new PdfStreamObject(dictionaryValue, decoded);
            default:
                return value;
        }
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

    private static byte[] ReadDocumentId(PdfCrossReferenceTable xref)
    {
        if (xref.Trailer?["ID"] is PdfArray { Items.Count: > 0 } ids &&
            ids.Items[0] is PdfString id)
        {
            return id.GetDecodedBytes().ToArray();
        }

        return [];
    }
}
