using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Xref;

namespace PXA.Importer.Trust;

public sealed partial class PdfTrustInspector : IPdfTrustInspector
{
    private sealed record SignatureFieldBinding(
        PdfObjectId? FieldId,
        string? Name,
        int? PageNumber,
        IReadOnlyList<double>? Rectangle);

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
            var context = new PdfParseContext(source, importerOptions, cancellationToken);
            xref = new PdfCrossReferenceParser().Parse(context);
            graph = new PdfObjectParser(context, xref).ParseDocumentGraph();
            structureStatus = version is null || graph.Objects.Count == 0
                ? PdfTrustStatus.Invalid
                : PdfTrustStatus.Valid;
            foreach (var diagnostic in context.Diagnostics)
            {
                diagnostics.Add(new PdfTrustDiagnostic(
                    diagnostic.Code,
                    PdfTrustDiagnosticSeverity.Error,
                    diagnostic.Message,
                    diagnostic.ObjectNumber,
                    diagnostic.Generation));
            }

            // A document that tripped a parse limit was not read in full, so its structure
            // can never be reported as valid.
            if (context.Diagnostics.Count > 0)
            {
                structureStatus = PdfTrustStatus.Invalid;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST002",
                PdfTrustDiagnosticSeverity.Error,
                $"The PDF object graph could not be inspected: {exception.GetType().Name}."));
        }

        PdfShadowObjectAnalyzer.Analyze(source, xref, graph, diagnostics);
        if (diagnostics.Any(static diagnostic => diagnostic.Code is "PXAPDFTRUST150" or "PXAPDFTRUST151"))
        {
            structureStatus = PdfTrustStatus.Invalid;
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
        signatures = PdfCertificationPolicyValidator.Apply(source, graph, xref, signatures, diagnostics);
        var sourceText = Encoding.Latin1.GetString(source);
        var pdfA = InspectPdfAClaim(sourceText, graph, encryption.IsEncrypted, diagnostics);
        var pdfUa = InspectPdfUaClaim(sourceText, diagnostics);
        var risk = InspectRisk(graph, diagnostics);
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
            "Signature integrity, revision coverage, certificate trust, timestamps, and configured revocation policy are evaluated separately. PDF/A uses the PXA structural validator; independent release validation and dedicated PDF/UA validation remain separate."));

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
            embeddedValidationMaterial,
            risk);
    }

    private static PdfRiskInspection? InspectRisk(
        PdfObjectGraph? graph,
        ICollection<PdfTrustDiagnostic> diagnostics)
    {
        if (graph is null)
        {
            return null;
        }

        var visitedObjects = new HashSet<PdfObject>(ReferenceEqualityComparer.Instance);
        var visitedReferences = new HashSet<PdfObjectId>();
        var counters = new int[7];

        void Visit(PdfObject? value, int depth)
        {
            if (value is null || depth > 64)
            {
                return;
            }
            if (value is PdfReference reference)
            {
                if (visitedReferences.Add(reference.Id) && graph.Objects.TryGetValue(reference.Id, out var indirect))
                {
                    Visit(indirect.Value, depth + 1);
                }
                return;
            }
            if (!visitedObjects.Add(value))
            {
                return;
            }

            if (value is PdfStreamObject stream)
            {
                Visit(stream.Dictionary, depth + 1);
                return;
            }
            if (value is PdfArray array)
            {
                foreach (var item in array.Items) Visit(item, depth + 1);
                return;
            }
            if (value is not PdfDictionary dictionary)
            {
                return;
            }

            var action = Resolve(graph, dictionary["S"]) as PdfName;
            var subtype = Resolve(graph, dictionary["Subtype"]) as PdfName;
            var type = Resolve(graph, dictionary["Type"]) as PdfName;
            if (action?.Value == "JavaScript" || dictionary["JS"] is not null) counters[0]++;
            if (action?.Value == "Launch") counters[1]++;
            if (type?.Value == "Filespec" && dictionary["EF"] is not null) counters[2]++;
            if (subtype?.Value == "FileAttachment") counters[3]++;
            if (subtype?.Value is "RichMedia" or "3D" or "Movie" or "Sound") counters[4]++;
            if (dictionary["XFA"] is not null) counters[5]++;
            if (action?.Value == "URI") counters[6]++;

            foreach (var child in dictionary.Values.Values) Visit(child, depth + 1);
        }

        Visit(graph.Trailer, 0);
        foreach (var indirect in graph.Objects.Values) Visit(indirect.Value, 0);

        var result = new PdfRiskInspection(
            counters[0], counters[1], counters[2], counters[3], counters[4], counters[5], counters[6],
            counters.Take(6).Any(static count => count > 0) ? PdfTrustStatus.Indeterminate : PdfTrustStatus.Valid);
        if (result.HasRiskyContent)
        {
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST020",
                PdfTrustDiagnosticSeverity.Warning,
                "The document contains active or embedded content that requires policy review."));
        }
        return result;
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
        IReadOnlyDictionary<PdfObjectId, IReadOnlyList<SignatureFieldBinding>> bindings,
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
                foreach (var fieldId in fields.Select(static field => field.FieldId).OfType<PdfObjectId>())
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
        var binding = fields?.FirstOrDefault();
        var enriched = inspection with
        {
            FieldName = binding?.Name,
            FieldPageNumber = binding?.PageNumber,
            FieldRectangle = binding?.Rectangle
        };
        return findings.Count == 0
            ? enriched
            : enriched with
            {
                Status = PdfTrustStatus.Invalid,
                CoverageStatus = PdfTrustStatus.Invalid
            };
    }

    private static IReadOnlyDictionary<PdfObjectId, IReadOnlyList<SignatureFieldBinding>> FindSignatureFieldBindings(
        PdfObjectGraph graph)
    {
        var result = new Dictionary<PdfObjectId, List<SignatureFieldBinding>>();
        if (Resolve(graph, graph.Trailer?["Root"]) is not PdfDictionary catalog ||
            Resolve(graph, catalog["AcroForm"]) is not PdfDictionary acroForm ||
            Resolve(graph, acroForm["Fields"]) is not PdfArray fields)
        {
            return result.ToDictionary(static pair => pair.Key, static pair => (IReadOnlyList<SignatureFieldBinding>)pair.Value);
        }

        var widgetPages = FindWidgetPages(graph, catalog);
        var visited = new HashSet<PdfObjectId>();
        foreach (var field in fields.Items)
        {
            CollectSignatureFieldBindings(graph, field, null, null, widgetPages, visited, result);
        }

        return result.ToDictionary(static pair => pair.Key, static pair => (IReadOnlyList<SignatureFieldBinding>)pair.Value);
    }

    private static void CollectSignatureFieldBindings(
        PdfObjectGraph graph,
        PdfObject fieldObject,
        PdfObjectId? inheritedFieldId,
        string? inheritedName,
        IReadOnlyDictionary<PdfObjectId, int> widgetPages,
        ISet<PdfObjectId> visited,
        IDictionary<PdfObjectId, List<SignatureFieldBinding>> result)
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

        var fieldName = Resolve(graph, field["T"]) is PdfString name
            ? DecodeTextString(name)
            : inheritedName;

        if (field["V"] is PdfReference signatureReference &&
            Resolve(graph, signatureReference) is PdfDictionary signature &&
            IsSignatureDictionary(graph, signature))
        {
            if (!result.TryGetValue(signatureReference.Id, out var boundFields))
            {
                boundFields = [];
                result[signatureReference.Id] = boundFields;
            }
            boundFields.Add(CreateSignatureFieldBinding(graph, field, fieldId, fieldName, widgetPages));
        }

        if (Resolve(graph, field["Kids"]) is not PdfArray kids)
        {
            return;
        }

        foreach (var kid in kids.Items)
        {
            CollectSignatureFieldBindings(graph, kid, fieldId, fieldName, widgetPages, visited, result);
        }
    }

    private static SignatureFieldBinding CreateSignatureFieldBinding(
        PdfObjectGraph graph,
        PdfDictionary field,
        PdfObjectId? fieldId,
        string? fieldName,
        IReadOnlyDictionary<PdfObjectId, int> widgetPages)
    {
        PdfObjectId? widgetId = fieldId;
        PdfDictionary widget = field;
        if ((fieldId is null || !widgetPages.ContainsKey(fieldId.Value)) &&
            Resolve(graph, field["Kids"]) is PdfArray kids)
        {
            var candidate = kids.Items.OfType<PdfReference>()
                .FirstOrDefault(kid => widgetPages.ContainsKey(kid.Id));
            if (candidate is not null && Resolve(graph, candidate) is PdfDictionary candidateWidget)
            {
                widgetId = candidate.Id;
                widget = candidateWidget;
            }
        }

        var rectangle = Resolve(graph, widget["Rect"]) is PdfArray rect && rect.Items.Count == 4
            ? rect.Items.Select(item => Resolve(graph, item)).Select(ReadNumber).ToArray()
            : null;
        return new SignatureFieldBinding(
            fieldId,
            fieldName,
            widgetId is { } id && widgetPages.TryGetValue(id, out var pageNumber) ? pageNumber : null,
            rectangle);
    }

    private static IReadOnlyDictionary<PdfObjectId, int> FindWidgetPages(
        PdfObjectGraph graph,
        PdfDictionary catalog)
    {
        var result = new Dictionary<PdfObjectId, int>();
        var visited = new HashSet<PdfObjectId>();
        var pageNumber = 0;

        void Visit(PdfObject? node)
        {
            var nodeId = node is PdfReference reference ? reference.Id : (PdfObjectId?)null;
            if (nodeId is { } id && !visited.Add(id)) return;
            if (Resolve(graph, node) is not PdfDictionary dictionary) return;
            if (Resolve(graph, dictionary["Type"]) is PdfName { Value: "Page" })
            {
                pageNumber++;
                if (Resolve(graph, dictionary["Annots"]) is PdfArray annotations)
                {
                    foreach (var annotation in annotations.Items.OfType<PdfReference>())
                    {
                        result[annotation.Id] = pageNumber;
                        if (Resolve(graph, annotation) is PdfDictionary widget && widget["Parent"] is PdfReference parent)
                        {
                            result[parent.Id] = pageNumber;
                        }
                    }
                }
                return;
            }
            if (Resolve(graph, dictionary["Kids"]) is PdfArray kids)
            {
                foreach (var kid in kids.Items) Visit(kid);
            }
        }

        Visit(catalog["Pages"]);
        return result;
    }

    private static double ReadNumber(PdfObject? value) => value switch
    {
        PdfNumber number => number.Value,
        PdfInteger integer => integer.Value,
        _ => 0
    };

    private static string DecodeTextString(PdfString value)
    {
        var bytes = value.GetDecodedBytes().Span;
        return bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF
            ? Encoding.BigEndianUnicode.GetString(bytes[2..])
            : Encoding.Latin1.GetString(bytes);
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
        PdfObjectGraph? graph,
        bool isEncrypted,
        ICollection<PdfTrustDiagnostic> diagnostics)
    {
        var part = PdfAPartRegex().Match(sourceText);
        if (!part.Success)
        {
            return new PdfComplianceClaim("PDF/A", null, false, PdfTrustStatus.NotApplicable, "No PDF/A identification metadata was found.");
        }

        var conformance = PdfAConformanceRegex().Match(sourceText);
        var profile = $"PDF/A-{part.Groups[1].Value}{(conformance.Success ? conformance.Groups[1].Value.ToLowerInvariant() : string.Empty)}";
        var archiveProfile = string.Equals(profile, "PDF/A-2b", StringComparison.OrdinalIgnoreCase)
            ? PdfArchiveProfile.PdfA2b
            : string.Equals(profile, "PDF/A-3b", StringComparison.OrdinalIgnoreCase)
                ? PdfArchiveProfile.PdfA3b
                : (PdfArchiveProfile?)null;
        if (archiveProfile is null || graph is null || !PdfA2bConformanceValidator.CanValidate(graph, archiveProfile.Value))
        {
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST301",
                PdfTrustDiagnosticSeverity.Warning,
                $"The document claims {profile}; this profile is not validated by the current PXA PDF/A validator."));
            return new PdfComplianceClaim("PDF/A", profile, true, PdfTrustStatus.Indeterminate, "XMP pdfaid identification metadata");
        }

        var validatedAt = DateTimeOffset.UtcNow;
        var isValid = PdfA2bConformanceValidator.Validate(graph, isEncrypted, diagnostics, archiveProfile.Value);
        return new PdfComplianceClaim(
            "PDF/A",
            profile,
            true,
            isValid ? PdfTrustStatus.Valid : PdfTrustStatus.Invalid,
            "XMP identification, catalog OutputIntent, ICC header, encryption, font embedding, actions, annotations, and profile-specific embedded-file rules",
            PdfA2bConformanceValidator.Name,
            validatedAt);
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
