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

    Task<PdfSigningResult> SignAsync(
        ReadOnlyMemory<byte> source,
        IPdfSigningKey signingKey,
        PdfSigningOptions? options = null,
        IPdfTimestampAuthority? timestampAuthority = null,
        CancellationToken cancellationToken = default);
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
        if (options?.Profile == PdfPadesProfile.BaselineT)
        {
            throw new InvalidOperationException("PAdES Baseline T requires SignAsync and a timestamp authority.");
        }

        return SignCoreAsync(source, signingKey, options, timestampAuthority: null, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    public Task<PdfSigningResult> SignAsync(
        ReadOnlyMemory<byte> source,
        IPdfSigningKey signingKey,
        PdfSigningOptions? options = null,
        IPdfTimestampAuthority? timestampAuthority = null,
        CancellationToken cancellationToken = default) =>
        SignCoreAsync(source, signingKey, options, timestampAuthority, cancellationToken);

    private static async Task<PdfSigningResult> SignCoreAsync(
        ReadOnlyMemory<byte> source,
        IPdfSigningKey signingKey,
        PdfSigningOptions? options,
        IPdfTimestampAuthority? timestampAuthority,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        options ??= new PdfSigningOptions();
        ValidateOptions(options);
        if (options.Profile == PdfPadesProfile.BaselineT && timestampAuthority is null)
        {
            throw new InvalidOperationException("PAdES Baseline T requires a configured RFC 3161 timestamp authority.");
        }

        var signingTime = options.SigningTime ?? DateTimeOffset.UtcNow;
        ValidateCertificate(signingKey.Certificate, signingTime);
        var parsed = Parse(source);
        var updates = CreateSignatureUpdates(
            parsed.Graph,
            signingKey.Certificate,
            options,
            signingTime,
            out var signatureObjectId);
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
        cancellationToken.ThrowIfCancellationRequested();
        var cmsResult = options.Profile == PdfPadesProfile.BaselineT
            ? await PdfCadesCmsBuilder.CreateDetachedWithTimestampAsync(
                digest,
                signingKey,
                signingTime,
                timestampAuthority!,
                cancellationToken).ConfigureAwait(false)
            : PdfCadesCmsBuilder.CreateDetached(digest, signingKey, signingTime);
        var cms = cmsResult.Cms;
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
            finalDocument.Table.Revisions.Count,
            cmsResult.TimestampToken?.Timestamp,
            cmsResult.TimestampToken?.AuthoritySubject);
    }

    private static IReadOnlyList<PdfIncrementalObjectUpdate> CreateSignatureUpdates(
        PdfObjectGraph graph,
        X509Certificate2 certificate,
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

        var requestedPageNumber = options.Appearance?.PageNumber ?? 1;
        var pageReference = FindPageReference(graph, catalog, requestedPageNumber)
            ?? throw new ArgumentOutOfRangeException(
                nameof(options),
                $"The PDF does not contain page {requestedPageNumber} for the signature widget.");
        if (graph.Resolve(pageReference.Id)?.Value is not PdfDictionary pageDictionary)
        {
            throw new InvalidDataException("The PDF signature page could not be resolved.");
        }

        var nextObjectNumber = graph.Objects.Keys.Max(static id => id.Number) + 1;
        signatureObjectId = new PdfObjectId(nextObjectNumber++, 0);
        var fieldObjectId = new PdfObjectId(nextObjectNumber++, 0);
        var appearanceObjectId = options.Appearance is null
            ? (PdfObjectId?)null
            : new PdfObjectId(nextObjectNumber, 0);
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

        var rectangle = options.Appearance is null
            ? new PdfArray([
                new PdfInteger(0),
                new PdfInteger(0),
                new PdfInteger(1),
                new PdfInteger(1)
            ])
            : CreateRectangle(options.Appearance);
        var fieldDictionary = new PdfDictionary
        {
            ["Type"] = new PdfName("Annot"),
            ["Subtype"] = new PdfName("Widget"),
            ["FT"] = new PdfName("Sig"),
            ["T"] = EncodePdfString(options.FieldName.Trim()),
            ["V"] = new PdfReference(signatureObjectId),
            ["Rect"] = rectangle,
            ["F"] = new PdfInteger(options.Appearance is null ? 34 : 4),
            ["P"] = pageReference
        };
        if (appearanceObjectId is { } appearanceId)
        {
            fieldDictionary["AP"] = new PdfDictionary
            {
                ["N"] = new PdfReference(appearanceId)
            };
        }
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
        if (appearanceObjectId is { } appearanceReference && options.Appearance is { } appearance)
        {
            updates.Add(new PdfIncrementalObjectUpdate(
                appearanceReference,
                CreateAppearance(certificate, options, appearance, signingTime)));
        }

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

    private static PdfArray CreateRectangle(PdfSignatureAppearanceOptions appearance) =>
        new([
            new PdfNumber(appearance.X),
            new PdfNumber(appearance.Y),
            new PdfNumber(appearance.X + appearance.Width),
            new PdfNumber(appearance.Y + appearance.Height)
        ]);

    private static PdfStreamObject CreateAppearance(
        X509Certificate2 certificate,
        PdfSigningOptions options,
        PdfSignatureAppearanceOptions appearance,
        DateTimeOffset signingTime)
    {
        var signer = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        if (string.IsNullOrWhiteSpace(signer))
        {
            signer = certificate.Subject;
        }

        var lines = new List<string>
        {
            string.IsNullOrWhiteSpace(appearance.Title) ? "Digitally signed" : appearance.Title.Trim(),
            signer,
            signingTime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)
        };
        if (!string.IsNullOrWhiteSpace(options.Reason))
        {
            lines.Add(options.Reason.Trim());
        }

        var fontSize = Math.Clamp(appearance.Height / Math.Max(lines.Count * 1.45, 5), 6, 11);
        var leading = fontSize * 1.25;
        var content = new StringBuilder()
            .AppendLine("q")
            .AppendLine("0.98 0.98 0.97 rg")
            .AppendFormat(CultureInfo.InvariantCulture, "0 0 {0:0.###} {1:0.###} re f\n", appearance.Width, appearance.Height)
            .AppendLine("0.25 0.29 0.33 RG")
            .AppendLine("0.8 w")
            .AppendFormat(CultureInfo.InvariantCulture, "0.4 0.4 {0:0.###} {1:0.###} re S\n", appearance.Width - 0.8, appearance.Height - 0.8)
            .AppendLine("BT")
            .AppendFormat(CultureInfo.InvariantCulture, "/F1 {0:0.###} Tf\n", fontSize)
            .AppendFormat(CultureInfo.InvariantCulture, "7 {0:0.###} Td\n", appearance.Height - fontSize - 6);
        for (var index = 0; index < lines.Count; index++)
        {
            if (index > 0)
            {
                content.AppendFormat(CultureInfo.InvariantCulture, "0 -{0:0.###} Td\n", leading);
            }

            content.Append('(').Append(EscapeAppearanceText(lines[index])).AppendLine(") Tj");
        }

        content.AppendLine("ET").AppendLine("Q");
        var font = new PdfDictionary
        {
            ["Type"] = new PdfName("Font"),
            ["Subtype"] = new PdfName("Type1"),
            ["BaseFont"] = new PdfName("Helvetica"),
            ["Encoding"] = new PdfName("WinAnsiEncoding")
        };
        var resources = new PdfDictionary
        {
            ["Font"] = new PdfDictionary { ["F1"] = font }
        };
        var dictionary = new PdfDictionary
        {
            ["Type"] = new PdfName("XObject"),
            ["Subtype"] = new PdfName("Form"),
            ["FormType"] = new PdfInteger(1),
            ["BBox"] = new PdfArray([
                new PdfInteger(0),
                new PdfInteger(0),
                new PdfNumber(appearance.Width),
                new PdfNumber(appearance.Height)
            ]),
            ["Resources"] = resources
        };
        return new PdfStreamObject(dictionary, Encoding.ASCII.GetBytes(content.ToString()));
    }

    private static string EscapeAppearanceText(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            result.Append(character switch
            {
                '\\' => "\\\\",
                '(' => "\\(",
                ')' => "\\)",
                >= ' ' and <= '~' => character.ToString(),
                _ => "?"
            });
        }

        return result.ToString();
    }

    private static PdfReference? FindPageReference(
        PdfObjectGraph graph,
        PdfDictionary catalog,
        int pageNumber)
    {
        var visited = new HashSet<PdfObjectId>();
        var pages = new List<PdfReference>();
        CollectPageReferences(graph, catalog["Pages"], visited, pages);
        if (pageNumber >= 1 && pageNumber <= pages.Count)
        {
            return pages[pageNumber - 1];
        }

        var fallback = graph.Objects.Values
            .Where(static item => item.Value is PdfDictionary { } dictionary &&
                dictionary["Type"] is PdfName { Value: "Page" })
            .OrderBy(static item => item.Id.Number)
            .ThenBy(static item => item.Id.Generation)
            .Select(static item => new PdfReference(item.Id))
            .ToArray();
        return pageNumber >= 1 && pageNumber <= fallback.Length ? fallback[pageNumber - 1] : null;
    }

    private static void CollectPageReferences(
        PdfObjectGraph graph,
        PdfObject? candidate,
        HashSet<PdfObjectId> visited,
        ICollection<PdfReference> pages)
    {
        if (candidate is not PdfReference reference || !visited.Add(reference.Id) ||
            graph.Resolve(reference.Id)?.Value is not PdfDictionary dictionary)
        {
            return;
        }

        if (dictionary["Type"] is PdfName { Value: "Page" })
        {
            pages.Add(reference);
            return;
        }

        if (Resolve(graph, dictionary["Kids"]) is not PdfArray kids)
        {
            return;
        }

        foreach (var kid in kids.Items)
        {
            CollectPageReferences(graph, kid, visited, pages);
        }
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
        if (options.Profile is not (PdfPadesProfile.BaselineB or PdfPadesProfile.BaselineT))
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

        if (options.Appearance is { } appearance &&
            (appearance.PageNumber < 1 ||
             !double.IsFinite(appearance.X) || appearance.X < 0 ||
             !double.IsFinite(appearance.Y) || appearance.Y < 0 ||
             !double.IsFinite(appearance.Width) || appearance.Width < 24 || appearance.Width > 2000 ||
             !double.IsFinite(appearance.Height) || appearance.Height < 24 || appearance.Height > 2000 ||
             appearance.Title?.Length > 200))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Visible signature appearance values or page number are outside the supported range.");
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
