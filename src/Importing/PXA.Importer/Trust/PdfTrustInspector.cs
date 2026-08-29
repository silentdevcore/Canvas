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

        var signaturePolicy = options.SignatureValidationPolicy ??
            throw new ArgumentException("A signature validation policy is required.", nameof(options));
        if (!Enum.IsDefined(signaturePolicy.RevocationMode) ||
            !Enum.IsDefined(signaturePolicy.RevocationFlag) ||
            signaturePolicy.NetworkTimeout <= TimeSpan.Zero ||
            signaturePolicy.NetworkTimeout > TimeSpan.FromMinutes(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The signature revocation policy or network timeout is invalid.");
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
        var embeddedValidationMaterial = InspectEmbeddedValidationMaterial(
            graph,
            diagnostics,
            out var embeddedValidationMaterialSource);
        var signatures = await InspectSignaturesAsync(
            source,
            graph,
            xref,
            signaturePolicy,
            embeddedValidationMaterialSource,
            diagnostics,
            cancellationToken).ConfigureAwait(false);
        signatures = PromoteBaselineLta(signatures, diagnostics);
        var sourceText = Encoding.Latin1.GetString(source);
        var pdfA = InspectPdfAClaim(sourceText, diagnostics);
        var pdfUa = InspectPdfUaClaim(sourceText, diagnostics);
        var revisions = xref?.Revisions.Select(revision => new PdfRevisionInspection(
                revision.SequenceNumber,
                revision.Kind.ToString(),
                revision.CrossReferenceOffset,
                revision.EndOffset,
                revision.Sha256,
                revision.EntryCount))
            .ToArray() ?? [];
        var revisionCount = revisions.Length > 0
            ? revisions.Length
            : xref?.Entries.Count == 0
                ? 0
                : xref!.Entries.Values.Max(entry => entry.Revision) + 1;

        if (xref is not null)
        {
            diagnostics.AddRange(xref.Diagnostics.Select(diagnostic => new PdfTrustDiagnostic(
                diagnostic.Code,
                PdfTrustDiagnosticSeverity.Error,
                diagnostic.Message)));
            if (xref.Diagnostics.Count > 0)
            {
                structureStatus = PdfTrustStatus.Invalid;
            }
        }

        diagnostics.Add(new PdfTrustDiagnostic(
            "PXAPDFTRUST010",
            PdfTrustDiagnosticSeverity.Info,
            "Signature integrity, revision coverage, certificate trust, timestamps, and configured revocation policy are evaluated separately; PDF/A and PDF/UA metadata claims still require dedicated conformance validators."));

        var status = structureStatus == PdfTrustStatus.Invalid ||
                     signatures.Any(signature => signature.Status == PdfTrustStatus.Invalid)
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
            revisions,
            diagnostics,
            embeddedValidationMaterial);
    }

    private static PdfEmbeddedValidationMaterialInspection? InspectEmbeddedValidationMaterial(
        PdfObjectGraph? graph,
        ICollection<PdfTrustDiagnostic> diagnostics,
        out PdfEmbeddedValidationMaterial? material)
    {
        material = null;
        if (graph is null)
        {
            return null;
        }

        try
        {
            material = PdfEmbeddedValidationMaterialExtractor.Extract(graph);
            if (material.Certificates.Count == 0 && material.Crls.Count == 0 &&
                material.OcspResponses.Count == 0 && material.VriEntryCount == 0)
            {
                return null;
            }

            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST121",
                PdfTrustDiagnosticSeverity.Info,
                $"Embedded PAdES validation material was found ({material.Certificates.Count} certificate(s), {material.Crls.Count} CRL(s), {material.OcspResponses.Count} OCSP response(s), {material.VriEntryCount} VRI entry/entries). Signature-bound B-LT validation is reported for each matching signature."));
            return new PdfEmbeddedValidationMaterialInspection(
                material.Certificates.Count,
                material.Crls.Count,
                material.OcspResponses.Count,
                material.VriEntryCount,
                material.TotalBytes,
                PdfTrustStatus.Indeterminate);
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or CryptographicException)
        {
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST122",
                PdfTrustDiagnosticSeverity.Warning,
                $"Embedded PAdES validation material could not be inspected: {exception.GetType().Name}."));
            return new PdfEmbeddedValidationMaterialInspection(0, 0, 0, 0, 0, PdfTrustStatus.Unsupported);
        }
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

    private static async Task<IReadOnlyList<PdfSignatureInspection>> InspectSignaturesAsync(
        byte[] source,
        PdfObjectGraph? graph,
        PdfCrossReferenceTable? xref,
        PdfSignatureValidationPolicy validationPolicy,
        PdfEmbeddedValidationMaterial? embeddedValidationMaterial,
        ICollection<PdfTrustDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        if (graph is null)
        {
            return [];
        }

        var signatures = new List<PdfSignatureInspection>();
        var bindings = FindSignatureFieldBindings(graph);
        foreach (var indirectObject in graph.Objects.Values.OrderBy(value => value.Id.Number))
        {
            if (indirectObject.Value is not PdfDictionary dictionary || !IsSignatureDictionary(graph, dictionary))
            {
                continue;
            }

            var result = await PdfSignatureValidator.ValidateAsync(
                source,
                indirectObject.Id,
                dictionary,
                xref?.Revisions ?? [],
                validationPolicy,
                embeddedValidationMaterial,
                cancellationToken).ConfigureAwait(false);
            var inspection = ValidateSignatureBinding(
                result.Inspection,
                indirectObject.Id,
                bindings,
                xref,
                out var bindingDiagnostics);
            signatures.Add(inspection);
            foreach (var diagnostic in result.Diagnostics)
            {
                diagnostics.Add(diagnostic);
            }
            foreach (var diagnostic in bindingDiagnostics)
            {
                diagnostics.Add(diagnostic);
            }
        }

        return signatures;
    }

    private static PdfSignatureInspection ValidateSignatureBinding(
        PdfSignatureInspection inspection,
        PdfObjectId signatureId,
        IReadOnlyDictionary<PdfObjectId, IReadOnlyList<PdfObjectId?>> bindings,
        PdfCrossReferenceTable? xref,
        out IReadOnlyList<PdfTrustDiagnostic> diagnostics)
    {
        var findings = new List<PdfTrustDiagnostic>();
        if (!bindings.TryGetValue(signatureId, out var fields) || fields.Count == 0)
        {
            findings.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST131",
                PdfTrustDiagnosticSeverity.Error,
                "The signature dictionary is not reachable from the current AcroForm field tree.",
                signatureId.Number,
                signatureId.Generation));
        }
        else if (fields.Count > 1)
        {
            findings.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST132",
                PdfTrustDiagnosticSeverity.Error,
                "Multiple current AcroForm fields reference the same signature dictionary.",
                signatureId.Number,
                signatureId.Generation));
        }

        if (xref is not null)
        {
            var signatureDefinitions = xref.RevisionEntries
                .Where(entry => entry.Id == signatureId && !entry.IsFree)
                .Select(static entry => (entry.Revision, entry.Offset, entry.Kind, entry.ObjectStreamNumber, entry.ObjectStreamIndex))
                .Distinct()
                .Count();
            if (signatureDefinitions > 1)
            {
                findings.Add(new PdfTrustDiagnostic(
                    "PXAPDFTRUST133",
                    PdfTrustDiagnosticSeverity.Error,
                    "The signature object was redefined by a later incremental revision.",
                    signatureId.Number,
                    signatureId.Generation));
            }

            if (inspection.SignedRevisionEndOffset is { } signedRevisionEnd && fields is not null)
            {
                foreach (var fieldId in fields.OfType<PdfObjectId>())
                {
                    if (!xref.Entries.TryGetValue(fieldId, out var fieldEntry))
                    {
                        continue;
                    }

                    var fieldRevision = xref.Revisions.FirstOrDefault(
                        revision => revision.DistanceFromLatest == fieldEntry.Revision);
                    if (fieldRevision is not null && fieldRevision.EndOffset > signedRevisionEnd)
                    {
                        findings.Add(new PdfTrustDiagnostic(
                            "PXAPDFTRUST134",
                            PdfTrustDiagnosticSeverity.Error,
                            "The current signature field was introduced or replaced after the signed revision.",
                            signatureId.Number,
                            signatureId.Generation));
                        break;
                    }
                }
            }
        }

        diagnostics = findings;
        return findings.Count == 0
            ? inspection
            : inspection with
            {
                Status = PdfTrustStatus.Invalid,
                CoverageStatus = PdfTrustStatus.Invalid
            };
    }

    private static IReadOnlyDictionary<PdfObjectId, IReadOnlyList<PdfObjectId?>> FindSignatureFieldBindings(
        PdfObjectGraph graph)
    {
        var result = new Dictionary<PdfObjectId, List<PdfObjectId?>>();
        if (Resolve(graph, graph.Trailer?["Root"]) is not PdfDictionary catalog ||
            Resolve(graph, catalog["AcroForm"]) is not PdfDictionary acroForm ||
            Resolve(graph, acroForm["Fields"]) is not PdfArray fields)
        {
            return result.ToDictionary(static pair => pair.Key, static pair => (IReadOnlyList<PdfObjectId?>)pair.Value);
        }

        var visited = new HashSet<PdfObjectId>();
        foreach (var field in fields.Items)
        {
            CollectSignatureFieldBindings(graph, field, null, visited, result);
        }

        return result.ToDictionary(static pair => pair.Key, static pair => (IReadOnlyList<PdfObjectId?>)pair.Value);
    }

    private static void CollectSignatureFieldBindings(
        PdfObjectGraph graph,
        PdfObject fieldObject,
        PdfObjectId? inheritedFieldId,
        ISet<PdfObjectId> visited,
        IDictionary<PdfObjectId, List<PdfObjectId?>> result)
    {
        var fieldId = fieldObject is PdfReference reference ? reference.Id : inheritedFieldId;
        if (fieldId is { } id && !visited.Add(id))
        {
            return;
        }

        if (Resolve(graph, fieldObject) is not PdfDictionary field)
        {
            return;
        }

        if (field["V"] is PdfReference signatureReference &&
            Resolve(graph, signatureReference) is PdfDictionary signature &&
            IsSignatureDictionary(graph, signature))
        {
            if (!result.TryGetValue(signatureReference.Id, out var boundFields))
            {
                boundFields = [];
                result[signatureReference.Id] = boundFields;
            }
            boundFields.Add(fieldId);
        }

        if (Resolve(graph, field["Kids"]) is not PdfArray kids)
        {
            return;
        }

        foreach (var kid in kids.Items)
        {
            CollectSignatureFieldBindings(graph, kid, fieldId, visited, result);
        }
    }

    private static IReadOnlyList<PdfSignatureInspection> PromoteBaselineLta(
        IReadOnlyList<PdfSignatureInspection> signatures,
        ICollection<PdfTrustDiagnostic> diagnostics)
    {
        var archiveTimestamps = signatures
            .Where(static signature =>
                signature.IsDocumentTimestamp &&
                signature.IntegrityStatus == PdfTrustStatus.Valid &&
                signature.TimestampStatus == PdfTrustStatus.Valid &&
                signature.SignedRevisionEndOffset is not null)
            .OrderBy(static signature => signature.SignedRevisionEndOffset)
            .ToArray();
        if (archiveTimestamps.Length == 0)
        {
            return signatures;
        }

        return signatures.Select(signature =>
        {
            if (signature.IsDocumentTimestamp || signature.PadesProfile != "B-LT" ||
                signature.EmbeddedValidationMaterial?.Status != PdfTrustStatus.Valid ||
                signature.SignedRevisionEndOffset is not { } signatureRevisionEnd)
            {
                return signature;
            }
            var archiveTimestamp = archiveTimestamps.LastOrDefault(candidate =>
                candidate.SignedRevisionEndOffset > signatureRevisionEnd);
            if (archiveTimestamp is null)
            {
                return signature;
            }
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST130",
                PdfTrustDiagnosticSeverity.Info,
                "A trusted RFC 3161 document timestamp protects the signature-bound B-LT validation material.",
                signature.ObjectNumber,
                signature.Generation));
            return signature with
            {
                PadesProfile = "B-LTA",
                ArchiveTimestampStatus = PdfTrustStatus.Valid,
                ArchiveTimestampTime = archiveTimestamp.TimestampTime,
                ArchiveTimestampAuthoritySubject = archiveTimestamp.TimestampAuthoritySubject
            };
        }).ToArray();
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

    [GeneratedRegex(@"<\s*pdfaid:part\s*>\s*([1-4])\s*<\s*/\s*pdfaid:part\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PdfAPartRegex();

    [GeneratedRegex(@"<\s*pdfaid:conformance\s*>\s*([A-Z])\s*<\s*/\s*pdfaid:conformance\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PdfAConformanceRegex();

    [GeneratedRegex(@"<\s*pdfuaid:part\s*>\s*([12])\s*<\s*/\s*pdfuaid:part\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PdfUaPartRegex();
}
