using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Saving;
using PVAD.Importer.Xref;

namespace PVAD.Importer.Signing;

public sealed record PdfDocumentTimestampOptions
{
    public string FieldName { get; init; } = "DocumentTimestamp1";
    public int ContentsPlaceholderBytes { get; init; } = 32 * 1024;
}

public sealed record PdfDocumentTimestampResult(
    byte[] Bytes,
    string FieldName,
    string DigestAlgorithm,
    DateTimeOffset Timestamp,
    string TimestampAuthoritySubject,
    int RevisionCount);

public interface IPdfDocumentTimestampService
{
    Task<PdfDocumentTimestampResult> AddAsync(
        ReadOnlyMemory<byte> source,
        IPdfTimestampAuthority timestampAuthority,
        PdfDocumentTimestampOptions? options = null,
        CancellationToken cancellationToken = default);
}

public sealed class PdfDocumentTimestampService : IPdfDocumentTimestampService
{
    private const long ByteRangeMarker1 = 8_777_777_771;
    private const long ByteRangeMarker2 = 8_777_777_772;
    private const long ByteRangeMarker3 = 8_777_777_773;
    private const byte ContentsMarkerByte = 0xB6;

    public async Task<PdfDocumentTimestampResult> AddAsync(
        ReadOnlyMemory<byte> source,
        IPdfTimestampAuthority timestampAuthority,
        PdfDocumentTimestampOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(timestampAuthority);
        options ??= new PdfDocumentTimestampOptions();
        ValidateOptions(options);
        var parsed = Parse(source);
        var updates = CreateUpdates(parsed.Graph, options, out var timestampObjectId);
        var incremental = new PdfIncrementalUpdateWriter().Append(source, updates);
        var timestampedBytes = incremental.Bytes;
        var marker = Convert.ToHexString(
            Enumerable.Repeat(ContentsMarkerByte, options.ContentsPlaceholderBytes).ToArray());
        var contentHexOffset = FindUniqueAscii(timestampedBytes, marker, source.Length);
        var contentsStart = contentHexOffset - 1;
        var contentsEnd = contentHexOffset + marker.Length + 1;
        if (contentsStart < source.Length || timestampedBytes[contentsStart] != (byte)'<' ||
            timestampedBytes[contentsEnd - 1] != (byte)'>')
        {
            throw new InvalidDataException("The PDF document timestamp placeholder could not be resolved safely.");
        }

        PatchFixedWidthInteger(timestampedBytes, ByteRangeMarker1, contentsStart, source.Length);
        PatchFixedWidthInteger(timestampedBytes, ByteRangeMarker2, contentsEnd, source.Length);
        PatchFixedWidthInteger(timestampedBytes, ByteRangeMarker3, timestampedBytes.Length - contentsEnd, source.Length);
        using var documentHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        documentHash.AppendData(timestampedBytes.AsSpan(0, contentsStart));
        documentHash.AppendData(timestampedBytes.AsSpan(contentsEnd));
        var digest = documentHash.GetHashAndReset();
        var token = await timestampAuthority.GetTimestampTokenAsync(
            digest,
            HashAlgorithmName.SHA256,
            cancellationToken).ConfigureAwait(false);
        if (token.Cms.Length > options.ContentsPlaceholderBytes)
        {
            throw new InvalidOperationException(
                $"The RFC 3161 token requires {token.Cms.Length} bytes, exceeding the {options.ContentsPlaceholderBytes}-byte PDF placeholder.");
        }

        var tokenHex = Convert.ToHexString(token.Cms);
        Encoding.ASCII.GetBytes(tokenHex, timestampedBytes.AsSpan(contentHexOffset, tokenHex.Length));
        timestampedBytes.AsSpan(contentHexOffset + tokenHex.Length, marker.Length - tokenHex.Length).Fill((byte)'0');
        var finalDocument = Parse(timestampedBytes);
        ValidateSavedTimestamp(
            finalDocument.Graph,
            timestampObjectId,
            contentsStart,
            contentsEnd,
            timestampedBytes.Length,
            token.Cms.Length);
        return new PdfDocumentTimestampResult(
            timestampedBytes,
            options.FieldName.Trim(),
            "SHA-256",
            token.Timestamp,
            token.AuthoritySubject,
            finalDocument.Table.Revisions.Count);
    }

    private static IReadOnlyList<PdfIncrementalObjectUpdate> CreateUpdates(
        PdfObjectGraph graph,
        PdfDocumentTimestampOptions options,
        out PdfObjectId timestampObjectId)
    {
        if (graph.Trailer?["Encrypt"] is not null)
        {
            throw new NotSupportedException("Timestamping encrypted PDFs is not enabled until incremental encryption is supported.");
        }
        if (graph.Trailer?["Root"] is not PdfReference rootReference ||
            graph.Resolve(rootReference.Id)?.Value is not PdfDictionary catalog)
        {
            throw new InvalidDataException("The PDF catalog root could not be resolved for document timestamping.");
        }

        var nextObjectNumber = graph.Objects.Keys.Max(static id => id.Number) + 1;
        timestampObjectId = new PdfObjectId(nextObjectNumber++, 0);
        var fieldObjectId = new PdfObjectId(nextObjectNumber, 0);
        var timestampDictionary = new PdfDictionary
        {
            ["Type"] = new PdfName("DocTimeStamp"),
            ["Filter"] = new PdfName("Adobe.PPKLite"),
            ["SubFilter"] = new PdfName("ETSI.RFC3161"),
            ["ByteRange"] = new PdfArray([
                new PdfInteger(0),
                new PdfInteger(ByteRangeMarker1),
                new PdfInteger(ByteRangeMarker2),
                new PdfInteger(ByteRangeMarker3)
            ]),
            ["Contents"] = new PdfString(
                Enumerable.Repeat(ContentsMarkerByte, options.ContentsPlaceholderBytes).ToArray(),
                IsHex: false)
        };
        var fieldReference = new PdfReference(fieldObjectId);
        var fieldDictionary = new PdfDictionary
        {
            ["FT"] = new PdfName("Sig"),
            ["T"] = EncodePdfString(options.FieldName.Trim()),
            ["V"] = new PdfReference(timestampObjectId)
        };
        var updates = new List<PdfIncrementalObjectUpdate>
        {
            new(timestampObjectId, timestampDictionary),
            new(fieldObjectId, fieldDictionary)
        };
        switch (catalog["AcroForm"])
        {
            case PdfReference acroFormReference
                when graph.Resolve(acroFormReference.Id)?.Value is PdfDictionary acroForm:
                updates.Add(new PdfIncrementalObjectUpdate(
                    acroFormReference.Id,
                    AddField(graph, acroForm, fieldReference)));
                break;
            case PdfDictionary directAcroForm:
                var catalogWithDirectForm = CloneDictionary(catalog);
                catalogWithDirectForm["AcroForm"] = AddField(graph, directAcroForm, fieldReference);
                updates.Add(new PdfIncrementalObjectUpdate(rootReference.Id, catalogWithDirectForm));
                break;
            case null:
                var catalogWithNewForm = CloneDictionary(catalog);
                catalogWithNewForm["AcroForm"] = AddField(graph, new PdfDictionary(), fieldReference);
                updates.Add(new PdfIncrementalObjectUpdate(rootReference.Id, catalogWithNewForm));
                break;
            default:
                throw new InvalidDataException("The existing /AcroForm entry is not a supported dictionary or reference.");
        }
        return updates;
    }

    private static PdfDictionary AddField(PdfObjectGraph graph, PdfDictionary acroForm, PdfReference field)
    {
        var updated = CloneDictionary(acroForm);
        var fields = Resolve(graph, acroForm["Fields"]) switch
        {
            PdfArray array => new PdfArray(array.Items),
            null => new PdfArray(),
            _ => throw new InvalidDataException("The PDF /AcroForm /Fields entry is not an array.")
        };
        fields.Add(field);
        updated["Fields"] = fields;
        updated["SigFlags"] = new PdfInteger(3);
        return updated;
    }

    private static void ValidateSavedTimestamp(
        PdfObjectGraph graph,
        PdfObjectId timestampObjectId,
        long contentsStart,
        long contentsEnd,
        int fileLength,
        int tokenLength)
    {
        if (graph.Resolve(timestampObjectId)?.Value is not PdfDictionary timestamp ||
            timestamp["Type"] is not PdfName { Value: "DocTimeStamp" } ||
            timestamp["SubFilter"] is not PdfName { Value: "ETSI.RFC3161" } ||
            timestamp["ByteRange"] is not PdfArray byteRange ||
            byteRange.Items.OfType<PdfInteger>().Select(static item => item.Value).ToArray() is not
                [0, var firstLength, var secondStart, var secondLength] ||
            firstLength != contentsStart || secondStart != contentsEnd ||
            secondLength != fileLength - contentsEnd ||
            timestamp["Contents"] is not PdfString contents ||
            contents.GetDecodedBytes().Length < tokenLength)
        {
            throw new InvalidDataException("Post-save validation could not verify the PDF document timestamp.");
        }
    }

    private static void ValidateOptions(PdfDocumentTimestampOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.FieldName) || options.FieldName.Trim().Length > 200)
        {
            throw new ArgumentException("The document timestamp field name must contain 1 to 200 characters.", nameof(options));
        }
        if (options.ContentsPlaceholderBytes is < 8 * 1024 or > 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The timestamp placeholder must be between 8 KiB and 1 MiB.");
        }
    }

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

    private static PdfDictionary CloneDictionary(PdfDictionary source) =>
        new(source.Values.ToDictionary(static entry => entry.Key, static entry => entry.Value, StringComparer.Ordinal));

    private static PdfString EncodePdfString(string value) =>
        new(Encoding.BigEndianUnicode.GetPreamble().Concat(Encoding.BigEndianUnicode.GetBytes(value)).ToArray(), IsHex: false);

    private static int FindUniqueAscii(byte[] bytes, string value, int startOffset)
    {
        var pattern = Encoding.ASCII.GetBytes(value);
        var relative = bytes.AsSpan(startOffset).IndexOf(pattern);
        if (relative < 0 || bytes.AsSpan(startOffset + relative + pattern.Length).IndexOf(pattern) >= 0)
        {
            throw new InvalidDataException("A unique PDF document timestamp placeholder was not found.");
        }
        return startOffset + relative;
    }

    private static void PatchFixedWidthInteger(byte[] bytes, long marker, long value, int startOffset)
    {
        if (value is < 0 or > 9_999_999_999L)
        {
            throw new NotSupportedException("PDF timestamp byte ranges currently support offsets up to ten decimal digits.");
        }
        var markerBytes = Encoding.ASCII.GetBytes(marker.ToString(CultureInfo.InvariantCulture));
        var relative = bytes.AsSpan(startOffset).IndexOf(markerBytes);
        if (relative < 0)
        {
            throw new InvalidDataException("A PDF document timestamp byte-range marker was not found.");
        }
        Encoding.ASCII.GetBytes(value.ToString("0000000000", CultureInfo.InvariantCulture), bytes.AsSpan(startOffset + relative, 10));
    }

    private sealed record ParsedPdf(PdfCrossReferenceTable Table, PdfObjectGraph Graph);
}
