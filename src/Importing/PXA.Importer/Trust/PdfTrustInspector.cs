using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Xref;

namespace PXA.Importer.Trust;

public sealed partial class PdfTrustInspector : IPdfTrustInspector
{
    public async Task<PdfTrustReport> InspectAsync(
        Stream pdfStream,
        PdfTrustInspectionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);
        options ??= new PdfTrustInspectionOptions();
        if (options.MaximumFileSizeBytes <= 0 || options.MaximumFileSizeBytes > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The maximum PDF inspection size must be between 1 byte and 2 GiB.");
        }

        var source = await ReadBoundedAsync(
            pdfStream,
            checked((int)options.MaximumFileSizeBytes),
            cancellationToken).ConfigureAwait(false);
        var diagnostics = new List<PdfTrustDiagnostic>();
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(source));
        var version = ReadPdfVersion(source);

        if (version is null)
        {
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST001",
                PdfTrustDiagnosticSeverity.Error,
                "The input does not begin with a supported PDF header."));
        }

        PdfCrossReferenceTable? xref = null;
        PdfObjectGraph? graph = null;
        var structureStatus = PdfTrustStatus.Invalid;
        try
        {
            var importerOptions = new PdfImporterOptions
            {
                LazyObjectLoading = false,
                DeferredStreamDecoding = true,
                ParsePagesInParallel = false,
                MaxParallelPageParsers = 1
            };
            var context = new PdfParseContext(source, importerOptions);
            xref = new PdfCrossReferenceParser().Parse(context);
            graph = new PdfObjectParser(context, xref).ParseDocumentGraph();
            structureStatus = version is null || graph.Objects.Count == 0
                ? PdfTrustStatus.Invalid
                : PdfTrustStatus.Valid;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST002",
                PdfTrustDiagnosticSeverity.Error,
                $"The PDF object graph could not be inspected: {exception.GetType().Name}."));
        }

        var encryption = InspectEncryption(graph, diagnostics);
        var signatures = InspectSignatures(graph, diagnostics);
        var sourceText = Encoding.Latin1.GetString(source);
        var pdfA = InspectPdfAClaim(sourceText, diagnostics);
        var pdfUa = InspectPdfUaClaim(sourceText, diagnostics);
        var revisionCount = xref?.Entries.Count == 0
            ? 0
            : xref!.Entries.Values.Max(entry => entry.Revision) + 1;

        diagnostics.Add(new PdfTrustDiagnostic(
            "PXAPDFTRUST010",
            PdfTrustDiagnosticSeverity.Info,
            "This baseline report inventories trust-related structures but does not yet certify cryptographic validity or standards conformance."));

        var status = structureStatus == PdfTrustStatus.Invalid
            ? PdfTrustStatus.Invalid
            : PdfTrustStatus.Indeterminate;

        return new PdfTrustReport(
            status,
            structureStatus,
            sha256,
            source.LongLength,
            version,
            revisionCount,
            graph?.Objects.Count ?? 0,
            encryption,
            signatures,
            pdfA,
            pdfUa,
            diagnostics);
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream stream,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (stream.CanSeek && stream.Length - stream.Position > maximumBytes)
        {
            throw new InvalidDataException($"The PDF exceeds the {maximumBytes}-byte inspection limit.");
        }

        using var buffer = new MemoryStream(Math.Min(maximumBytes, 64 * 1024));
        var chunk = new byte[64 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > maximumBytes)
            {
                throw new InvalidDataException($"The PDF exceeds the {maximumBytes}-byte inspection limit.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return buffer.ToArray();
    }

    private static string? ReadPdfVersion(ReadOnlySpan<byte> source)
    {
        if (source.Length < 8 || !source[..5].SequenceEqual("%PDF-"u8))
        {
            return null;
        }

        var end = 5;
        while (end < source.Length && end < 12 &&
               (char.IsAsciiDigit((char)source[end]) || source[end] == (byte)'.'))
        {
            end++;
        }

        return end == 5 ? null : Encoding.ASCII.GetString(source[5..end]);
    }

    private static PdfEncryptionInspection InspectEncryption(
        PdfObjectGraph? graph,
        ICollection<PdfTrustDiagnostic> diagnostics)
    {
        if (graph?.Trailer?["Encrypt"] is not { } encryptObject)
        {
            return new PdfEncryptionInspection(false, PdfTrustStatus.NotApplicable);
        }

        var dictionary = Resolve(graph, encryptObject) as PdfDictionary;
        if (dictionary is null)
        {
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST201",
                PdfTrustDiagnosticSeverity.Warning,
                "An encryption entry exists, but its security dictionary could not be resolved."));
            return new PdfEncryptionInspection(true, PdfTrustStatus.Indeterminate);
        }

        var filter = ReadName(graph, dictionary["Filter"]);
        var version = ReadInteger(graph, dictionary["V"]);
        var revision = ReadInteger(graph, dictionary["R"]);
        var length = ReadInteger(graph, dictionary["Length"]);
        var algorithm = DescribeEncryptionAlgorithm(version, revision, dictionary, graph);
        diagnostics.Add(new PdfTrustDiagnostic(
            "PXAPDFTRUST200",
            PdfTrustDiagnosticSeverity.Info,
            $"Encrypted PDF detected ({algorithm ?? "unknown algorithm"}); password and permission validation has not been performed."));

        return new PdfEncryptionInspection(
            true,
            PdfTrustStatus.Indeterminate,
            filter,
            algorithm,
            version,
            revision,
            length);
    }

    private static IReadOnlyList<PdfSignatureInspection> InspectSignatures(
        PdfObjectGraph? graph,
        ICollection<PdfTrustDiagnostic> diagnostics)
    {
        if (graph is null)
        {
            return [];
        }

        var signatures = new List<PdfSignatureInspection>();
        foreach (var indirectObject in graph.Objects.Values.OrderBy(value => value.Id.Number))
        {
            if (indirectObject.Value is not PdfDictionary dictionary || !IsSignatureDictionary(graph, dictionary))
            {
                continue;
            }

            var byteRange = ReadIntegerArray(graph, dictionary["ByteRange"]);
            var contents = Resolve(graph, dictionary["Contents"]) as PdfString;
            signatures.Add(new PdfSignatureInspection(
                indirectObject.Id.Number,
                indirectObject.Id.Generation,
                PdfTrustStatus.Indeterminate,
                ReadName(graph, dictionary["SubFilter"]),
                byteRange,
                contents?.GetDecodedBytes().Length));
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST101",
                PdfTrustDiagnosticSeverity.Warning,
                "A signature dictionary was found, but its byte range, CMS value, certificate chain, timestamp, and revision changes have not yet been validated.",
                indirectObject.Id.Number,
                indirectObject.Id.Generation));
        }

        return signatures;
    }

    private static PdfComplianceClaim InspectPdfAClaim(
        string sourceText,
        ICollection<PdfTrustDiagnostic> diagnostics)
    {
        var part = PdfAPartRegex().Match(sourceText);
        if (!part.Success)
        {
            return new PdfComplianceClaim("PDF/A", null, false, PdfTrustStatus.NotApplicable, "No PDF/A identification metadata was found.");
        }

        var conformance = PdfAConformanceRegex().Match(sourceText);
        var profile = $"PDF/A-{part.Groups[1].Value}{(conformance.Success ? conformance.Groups[1].Value.ToLowerInvariant() : string.Empty)}";
        diagnostics.Add(new PdfTrustDiagnostic(
            "PXAPDFTRUST301",
            PdfTrustDiagnosticSeverity.Warning,
            $"The document claims {profile} in metadata; this is not a conformance validation result."));
        return new PdfComplianceClaim("PDF/A", profile, true, PdfTrustStatus.Indeterminate, "XMP pdfaid identification metadata");
    }

    private static PdfComplianceClaim InspectPdfUaClaim(
        string sourceText,
        ICollection<PdfTrustDiagnostic> diagnostics)
    {
        var part = PdfUaPartRegex().Match(sourceText);
        if (!part.Success)
        {
            return new PdfComplianceClaim("PDF/UA", null, false, PdfTrustStatus.NotApplicable, "No PDF/UA identification metadata was found.");
        }

        var profile = $"PDF/UA-{part.Groups[1].Value}";
        diagnostics.Add(new PdfTrustDiagnostic(
            "PXAPDFTRUST401",
            PdfTrustDiagnosticSeverity.Warning,
            $"The document claims {profile} in metadata; this is not an accessibility or conformance validation result."));
        return new PdfComplianceClaim("PDF/UA", profile, true, PdfTrustStatus.Indeterminate, "XMP pdfuaid identification metadata");
    }

    private static bool IsSignatureDictionary(PdfObjectGraph graph, PdfDictionary dictionary) =>
        string.Equals(ReadName(graph, dictionary["Type"]), "Sig", StringComparison.Ordinal) ||
        string.Equals(ReadName(graph, dictionary["FT"]), "Sig", StringComparison.Ordinal) ||
        dictionary["ByteRange"] is not null && dictionary["Contents"] is not null;

    private static string? DescribeEncryptionAlgorithm(
        int? version,
        int? revision,
        PdfDictionary dictionary,
        PdfObjectGraph graph)
    {
        if (version == 5 && revision is >= 5)
        {
            return "AES-256";
        }

        if (version == 4)
        {
            var streamFilter = ReadName(graph, dictionary["StmF"]);
            var cryptFilters = Resolve(graph, dictionary["CF"]) as PdfDictionary;
            var selected = streamFilter is null ? null : Resolve(graph, cryptFilters?[streamFilter]) as PdfDictionary;
            return string.Equals(ReadName(graph, selected?["CFM"]), "AESV2", StringComparison.Ordinal)
                ? "AES-128"
                : "Crypt filter (algorithm unresolved)";
        }

        return version is 1 or 2 ? "RC4" : null;
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

    private static int? ReadInteger(PdfObjectGraph graph, PdfObject? value) =>
        Resolve(graph, value) switch
        {
            PdfInteger integer when integer.Value is >= int.MinValue and <= int.MaxValue => (int)integer.Value,
            PdfNumber number when number.Value is >= int.MinValue and <= int.MaxValue => (int)number.Value,
            _ => null
        };

    private static IReadOnlyList<long> ReadIntegerArray(PdfObjectGraph graph, PdfObject? value)
    {
        if (Resolve(graph, value) is not PdfArray array)
        {
            return [];
        }

        return array.Items
            .Select(item => Resolve(graph, item))
            .Select(item => item switch
            {
                PdfInteger integer => (long?)integer.Value,
                PdfNumber number when number.Value >= long.MinValue && number.Value <= long.MaxValue => (long?)number.Value,
                _ => null
            })
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToArray();
    }

    [GeneratedRegex(@"<\s*pdfaid:part\s*>\s*([1-4])\s*<\s*/\s*pdfaid:part\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PdfAPartRegex();

    [GeneratedRegex(@"<\s*pdfaid:conformance\s*>\s*([A-Z])\s*<\s*/\s*pdfaid:conformance\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PdfAConformanceRegex();

    [GeneratedRegex(@"<\s*pdfuaid:part\s*>\s*([12])\s*<\s*/\s*pdfuaid:part\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PdfUaPartRegex();
}
