using System.Security.Cryptography;
using System.Text;
using PVAD.Importer.Content;
using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Streams;
using PVAD.Importer.Xref;

namespace PVAD.Importer.EInvoicing;

public sealed class PdfAssociatedFileExtractionOptions
{
    public long MaximumPdfSizeBytes { get; init; } = 100 * 1024 * 1024;
    public int MaximumFileCount { get; init; } = 64;
    public int MaximumFileSizeBytes { get; init; } = 10 * 1024 * 1024;
    public long MaximumTotalFileSizeBytes { get; init; } = 32 * 1024 * 1024;
}

public sealed record PdfExtractedAssociatedFile(
    string FileName,
    string? MimeType,
    string Relationship,
    string? Description,
    ReadOnlyMemory<byte> Content,
    bool SizeMatches,
    bool ChecksumMatches);

/// <summary>Extracts document-associated files through the PDF catalog AF relationship.</summary>
public sealed class PdfAssociatedFileExtractor
{
    public async Task<IReadOnlyList<PdfExtractedAssociatedFile>> ExtractAsync(
        Stream pdfStream,
        PdfAssociatedFileExtractionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);
        options ??= new PdfAssociatedFileExtractionOptions();
        ValidateOptions(options);

        var source = await ReadBoundedAsync(
            pdfStream,
            options.MaximumPdfSizeBytes,
            cancellationToken).ConfigureAwait(false);
        var context = new PdfParseContext(source, new PdfImporterOptions(), cancellationToken);
        var xref = new PdfCrossReferenceParser().Parse(context);
        if (xref.Diagnostics.Count > 0)
        {
            throw new InvalidDataException($"The PDF cross-reference data is invalid: {xref.Diagnostics[0].Code}.");
        }

        var graph = new PdfObjectParser(context, xref).ParseDocumentGraph();
        if (context.Diagnostics.Count > 0 || graph.Objects.Count == 0)
        {
            throw new InvalidDataException("The PDF could not be parsed completely.");
        }
        if (graph.Trailer?["Encrypt"] is not null)
        {
            throw new NotSupportedException("Associated files cannot be extracted before the PDF is decrypted.");
        }

        return Extract(graph, options);
    }

    internal static IReadOnlyList<PdfExtractedAssociatedFile> Extract(
        PdfObjectGraph graph,
        PdfAssociatedFileExtractionOptions options)
    {
        var catalog = Resolve(graph, graph.Trailer?["Root"]) as PdfDictionary;
        var associatedFiles = Resolve(graph, catalog?["AF"]) as PdfArray;
        if (associatedFiles is null)
        {
            return [];
        }
        if (associatedFiles.Items.Count > options.MaximumFileCount)
        {
            throw new InvalidDataException("The PDF exceeds the configured associated-file count limit.");
        }

        var results = new List<PdfExtractedAssociatedFile>(associatedFiles.Items.Count);
        long totalBytes = 0;
        foreach (var item in associatedFiles.Items)
        {
            if (Resolve(graph, item) is not PdfDictionary fileSpecification ||
                ReadName(graph, fileSpecification["Type"]) != "Filespec")
            {
                throw new InvalidDataException("The PDF catalog contains an invalid associated-file reference.");
            }

            var fileName = ReadText(Resolve(graph, fileSpecification["UF"])) ??
                ReadText(Resolve(graph, fileSpecification["F"]));
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new InvalidDataException("An associated file does not provide a filename.");
            }

            var embeddedFiles = Resolve(graph, fileSpecification["EF"]) as PdfDictionary;
            var stream = Resolve(graph, embeddedFiles?["UF"] ?? embeddedFiles?["F"]) as PdfStreamObject
                ?? throw new InvalidDataException($"Associated file '{fileName}' has no embedded payload.");
            var bytes = Decode(stream);
            if (bytes.Length is 0 || bytes.Length > options.MaximumFileSizeBytes)
            {
                throw new InvalidDataException($"Associated file '{fileName}' is empty or exceeds the configured item limit.");
            }
            totalBytes = checked(totalBytes + bytes.Length);
            if (totalBytes > options.MaximumTotalFileSizeBytes)
            {
                throw new InvalidDataException("The PDF exceeds the configured total associated-file size limit.");
            }

            var parameters = Resolve(graph, stream.Dictionary["Params"]) as PdfDictionary;
            var declaredSize = ReadInteger(graph, parameters?["Size"]);
            var checksum = Resolve(graph, parameters?["CheckSum"]) as PdfString;
            var sizeMatches = declaredSize is null || declaredSize == bytes.Length;
            var checksumMatches = checksum is null ||
                CryptographicOperations.FixedTimeEquals(
                    checksum.GetDecodedBytes().Span,
                    MD5.HashData(bytes));

            results.Add(new PdfExtractedAssociatedFile(
                fileName,
                ReadName(graph, stream.Dictionary["Subtype"]),
                ReadName(graph, fileSpecification["AFRelationship"]) ?? "Unspecified",
                ReadText(Resolve(graph, fileSpecification["Desc"])),
                bytes,
                sizeMatches,
                checksumMatches));
        }

        return results;
    }

    private static byte[] Decode(PdfStreamObject stream)
    {
        if (stream.IsDecoded)
        {
            return stream.DecodedBytes.ToArray();
        }

        var decoder = new PdfStreamDecoderRegistry();
        if (decoder.Evaluate(stream.Dictionary["Filter"])
            .Any(static support => support.Status != PdfStreamDecoderSupportStatus.Supported))
        {
            throw new NotSupportedException("An associated file uses an unsupported PDF stream filter.");
        }
        return decoder.Decode(stream).ToArray();
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream source,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        using var destination = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return destination.ToArray();
            }
            if (destination.Length + read > maximumBytes)
            {
                throw new InvalidDataException("The PDF exceeds the configured extraction limit.");
            }
            destination.Write(buffer, 0, read);
        }
    }

    private static void ValidateOptions(PdfAssociatedFileExtractionOptions options)
    {
        if (options.MaximumPdfSizeBytes <= 0 || options.MaximumFileCount <= 0 ||
            options.MaximumFileSizeBytes <= 0 || options.MaximumTotalFileSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "All associated-file extraction limits must be positive.");
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

    private static string? ReadName(PdfObjectGraph graph, PdfObject? value) =>
        Resolve(graph, value) is PdfName name ? name.Value : null;

    private static long? ReadInteger(PdfObjectGraph graph, PdfObject? value) =>
        Resolve(graph, value) is PdfInteger integer ? integer.Value : null;

    private static string? ReadText(PdfObject? value)
    {
        if (value is not PdfString text)
        {
            return null;
        }

        var bytes = text.GetDecodedBytes().Span;
        return bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF
            ? Encoding.BigEndianUnicode.GetString(bytes[2..])
            : Encoding.Latin1.GetString(bytes);
    }
}
