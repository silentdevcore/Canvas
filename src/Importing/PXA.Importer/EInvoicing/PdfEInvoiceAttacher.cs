using System.Security.Cryptography;
using System.Text;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Saving;
using PXA.Importer.Streams;
using PXA.Importer.Trust;
using PXA.Importer.Xref;
using PXA.Pdf;

namespace PXA.Importer.EInvoicing;

/// <summary>
/// Embeds invoice XML into a PDF that already exists.
///
/// The default strategy is an incremental update: the original bytes become the prefix of the
/// result, so no page object, content stream, font or image can change. Only a source whose
/// archival identification forbids embedded files is rewritten, and only after the caller has
/// approved that explicitly. The caller's input stream is never written to; when an operation
/// is blocked the original file is simply left alone.
/// </summary>
public sealed class PdfEInvoiceAttacher(IPdfA2bConverter archiveConverter) : IPdfEInvoiceAttacher
{
    internal const int MaximumFileNameLength = 120;
    internal const int MaximumDescriptionLength = 512;

    /// <summary>
    /// Filenames the maintained standards reserve for the invoice payload. A second file with
    /// one of these names would make the hybrid document ambiguous for every reader.
    /// </summary>
    private static readonly HashSet<string> ReservedInvoiceFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "factur-x.xml", "zugferd-invoice.xml", "ZUGFeRD-invoice.xml", "xrechnung.xml", "order-x.xml"
    };

    public async Task<PdfEInvoiceAttachmentPreflightReport> PreflightAsync(
        Stream pdfStream,
        PdfEInvoiceAttachmentPayload payload,
        PdfEInvoiceAttachmentOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new PdfEInvoiceAttachmentOptions();
        ValidatePayload(payload);
        ValidateOptions(options);
        var source = await ParseAsync(pdfStream, options, cancellationToken).ConfigureAwait(false);
        return BuildPreflight(source, payload, options);
    }

    public async Task<PdfEInvoiceAttachmentResult> AttachAsync(
        Stream pdfStream,
        PdfEInvoiceAttachmentPayload payload,
        PdfEInvoiceAttachmentOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new PdfEInvoiceAttachmentOptions();
        ValidatePayload(payload);
        ValidateOptions(options);
        var source = await ParseAsync(pdfStream, options, cancellationToken).ConfigureAwait(false);
        var preflight = BuildPreflight(source, payload, options);

        if (preflight.Capability == PdfComplianceConversionCapability.Unsupported)
        {
            throw new PdfEInvoiceAttachmentBlockedException(
                "The invoice cannot be attached to this PDF; the original file is unchanged.",
                preflight);
        }
        if (preflight.RequiresArchiveConversion && !options.AllowArchiveConversion)
        {
            throw new PdfEInvoiceAttachmentBlockedException(
                "Attaching the invoice requires a PDF/A-3b conversion of the source. Approve the conversion explicitly to continue.",
                preflight);
        }
        if (preflight.SignatureCount > 0 && !options.AllowSignatureInvalidation)
        {
            throw new PdfEInvoiceAttachmentBlockedException(
                preflight.InvalidatesSignatures
                    ? "The required conversion would invalidate existing signatures. Approve signature invalidation explicitly to continue."
                    : "Attaching the invoice creates a revision after the existing signature. Approve the signed-document modification explicitly to continue.",
                preflight);
        }

        var working = source;
        if (preflight.RequiresArchiveConversion)
        {
            working = await ConvertAsync(source, options, preflight, cancellationToken).ConfigureAwait(false);
        }

        var updates = BuildAttachmentUpdates(working, payload, options);
        byte[] bytes;
        try
        {
            bytes = new PdfIncrementalUpdateWriter().Append(working.Bytes, updates).Bytes;
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            throw new PdfEInvoiceAttachmentBlockedException(
                $"The invoice attachment could not be written: {exception.Message} The original file is unchanged.",
                preflight);
        }

        if (bytes.LongLength > options.MaximumOutputSizeBytes)
        {
            throw new PdfEInvoiceAttachmentBlockedException(
                $"The resulting PDF would be {bytes.LongLength} bytes and exceeds the configured output limit of {options.MaximumOutputSizeBytes} bytes.",
                preflight);
        }

        var verified = Verify(bytes, source, payload, preflight, options, cancellationToken);
        return new PdfEInvoiceAttachmentResult(
            bytes,
            preflight.SourceSha256,
            Convert.ToHexStringLower(SHA256.HashData(bytes)),
            Convert.ToHexStringLower(SHA256.HashData(payload.Xml.Span)),
            payload.FileName,
            bytes.LongLength,
            verified.PageCount,
            verified.PagesUnchanged,
            preflight.Strategy,
            preflight);
    }

    private async Task<ParsedSource> ConvertAsync(
        ParsedSource source,
        PdfEInvoiceAttachmentOptions options,
        PdfEInvoiceAttachmentPreflightReport preflight,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new MemoryStream(source.Bytes, writable: false);
            var converted = await archiveConverter.ConvertAsync(
                stream,
                new PdfA2bConversionOptions
                {
                    Profile = PdfArchiveProfile.PdfA3b,
                    MaximumFileSizeBytes = options.MaximumPdfSizeBytes,
                    AllowSignatureInvalidation = options.AllowSignatureInvalidation
                },
                cancellationToken).ConfigureAwait(false);
            return Parse(converted.Bytes);
        }
        catch (Exception exception) when (
            exception is PdfA2bConversionBlockedException or InvalidDataException or NotSupportedException)
        {
            throw new PdfEInvoiceAttachmentBlockedException(
                $"The PDF/A-3b conversion required for this attachment failed: {exception.Message} The original file is unchanged.",
                preflight);
        }
    }

    // ---------------------------------------------------------------- preflight

    private static PdfEInvoiceAttachmentPreflightReport BuildPreflight(
        ParsedSource source,
        PdfEInvoiceAttachmentPayload payload,
        PdfEInvoiceAttachmentOptions options)
    {
        var findings = new List<PdfComplianceConversionFinding>();
        var conflicts = FindConflictingAttachments(source.Graph, payload.FileName);

        if (source.IsEncrypted)
        {
            findings.Add(Error(
                "PXAEINVATTACH001",
                "The source PDF is encrypted. Decrypt it before an invoice can be attached."));
        }
        if (source.Pages.Count == 0)
        {
            findings.Add(Error(
                "PXAEINVATTACH003",
                "The source PDF does not contain a resolvable page. PXA does not attach invoices to page-less documents."));
        }

        var requiresConversion = !source.IsPdfA3;
        if (requiresConversion)
        {
            findings.Add(new PdfComplianceConversionFinding(
                "PXAEINVATTACH004",
                PdfTrustDiagnosticSeverity.Warning,
                source.ArchiveProfile is null
                    ? "The source is not identified as PDF/A-3b, so a hybrid invoice requires a PDF/A-3b conversion. This rewrites the file and needs explicit approval."
                    : source.ArchiveProfile.StartsWith("PDF/A-3", StringComparison.OrdinalIgnoreCase)
                        ? $"The source identifies itself as {source.ArchiveProfile} but does not pass PXA's structural PDF/A-3b validation. A corrective conversion is required and needs explicit approval."
                    : $"The source is identified as {source.ArchiveProfile}, which does not permit embedded files. A PDF/A-3b conversion is required and needs explicit approval."));
        }
        else
        {
            findings.Add(new PdfComplianceConversionFinding(
                "PXAEINVATTACH002",
                PdfTrustDiagnosticSeverity.Info,
                "The source already identifies PDF/A-3b. The invoice is appended as a new revision and no existing object is rewritten."));
        }

        if (source.SignatureCount > 0)
        {
            findings.Add(requiresConversion
                ? new PdfComplianceConversionFinding(
                    "PXAEINVATTACH008",
                    PdfTrustDiagnosticSeverity.Warning,
                    $"The required rewrite removes {source.SignatureCount} existing signature object(s). Explicit approval is required.")
                : new PdfComplianceConversionFinding(
                    "PXAEINVATTACH007",
                    PdfTrustDiagnosticSeverity.Warning,
                    $"{source.SignatureCount} existing signature(s) stay cryptographically intact, but readers will report the document as modified after signing."));
        }

        if (conflicts.Count > 0)
        {
            if (!options.ReplaceExistingInvoiceAttachment)
            {
                findings.Add(Error(
                    "PXAEINVATTACH010",
                    $"The PDF already carries the invoice attachment(s) {string.Join(", ", conflicts.Select(static item => item.Name))}. Replacing an existing invoice requires explicit approval."));
            }
            else if (conflicts.FirstOrDefault(static item => !item.SafelyRemovable) is { } retained)
            {
                findings.Add(Error(
                    "PXAEINVATTACH016",
                    $"The existing invoice attachment '{retained.Name}' is also referenced outside the document catalog, for example by a file-attachment annotation. PXA does not remove it, because that would change visible or interactive content."));
            }
            else
            {
                findings.Add(new PdfComplianceConversionFinding(
                    "PXAEINVATTACH009",
                    PdfTrustDiagnosticSeverity.Warning,
                    $"{conflicts.Count} existing invoice attachment(s) are dereferenced in the new revision. In an incremental update their bytes remain readable in the earlier revision of the file."));
            }
        }

        if (!source.HasReadableXmp && !requiresConversion)
        {
            findings.Add(new PdfComplianceConversionFinding(
                "PXAEINVATTACH012",
                PdfTrustDiagnosticSeverity.Warning,
                "The document XMP packet could not be read and is replaced by a minimal PDF/A-3b packet. Metadata it contained is not carried over."));
        }

        var estimatedOutput = source.Bytes.LongLength + payload.Xml.Length + EstimatedOverheadBytes(payload);
        if (estimatedOutput > options.MaximumOutputSizeBytes)
        {
            findings.Add(Error(
                "PXAEINVATTACH011",
                $"The result would be about {estimatedOutput} bytes and exceeds the configured output limit of {options.MaximumOutputSizeBytes} bytes."));
        }

        if (source.EmbeddedFileNames.Count > 0 && conflicts.Count == 0)
        {
            findings.Add(new PdfComplianceConversionFinding(
                "PXAEINVATTACH015",
                PdfTrustDiagnosticSeverity.Info,
                $"{source.EmbeddedFileNames.Count} unrelated embedded file(s) are retained and stay associated with the document."));
        }

        if (findings.All(static item => item.Severity != PdfTrustDiagnosticSeverity.Error))
        {
            findings.Add(new PdfComplianceConversionFinding(
                "PXAEINVATTACH000",
                PdfTrustDiagnosticSeverity.Info,
                $"The invoice can be attached while all {source.Pages.Count} visible page(s) are preserved."));
        }

        var blocked = findings.Any(static item => item.Severity == PdfTrustDiagnosticSeverity.Error);
        var requiresApproval = requiresConversion || conflicts.Count > 0 || source.SignatureCount > 0;
        var capability = blocked
            ? PdfComplianceConversionCapability.Unsupported
            : requiresApproval
                ? PdfComplianceConversionCapability.ReviewRequired
                : PdfComplianceConversionCapability.Ready;

        return new PdfEInvoiceAttachmentPreflightReport(
            PdfArchiveProfileName,
            capability,
            requiresConversion
                ? PdfEInvoiceAttachmentStrategy.ArchiveConversion
                : PdfEInvoiceAttachmentStrategy.IncrementalUpdate,
            Convert.ToHexStringLower(SHA256.HashData(source.Bytes)),
            source.Bytes.LongLength,
            source.Pages.Count,
            source.SignatureCount,
            source.ArchiveProfile,
            requiresConversion,
            requiresApproval,
            requiresConversion && source.SignatureCount > 0,
            true,
            conflicts.Select(static item => item.Name).ToArray(),
            estimatedOutput,
            PdfDiagnosticMetadata.Enrich(findings, source.Graph));
    }

    private const string PdfArchiveProfileName = "PDF/A-3b";

    private static long EstimatedOverheadBytes(PdfEInvoiceAttachmentPayload payload) =>
        4096 + (payload.XmpMetadata?.Properties.Count ?? 0) * 512L;

    /// <summary>
    /// A conflict is either the exact filename PXA is about to write or another file using one
    /// of the reserved invoice names, because both make the embedded invoice ambiguous.
    /// </summary>
    private static IReadOnlyList<ConflictingAttachment> FindConflictingAttachments(
        PdfObjectGraph graph,
        string fileName)
    {
        var catalogId = graph.Trailer?["Root"] is PdfReference root ? root.Id : (PdfObjectId?)null;
        var conflicts = FindFileSpecifications(graph)
            .Where(item => item.Name is not null &&
                (string.Equals(item.Name, fileName, StringComparison.OrdinalIgnoreCase) ||
                    (ReservedInvoiceFileNames.Contains(item.Name) && ReservedInvoiceFileNames.Contains(fileName))))
            .ToArray();
        if (conflicts.Length == 0)
        {
            return [];
        }

        var external = CountExternalReferences(
            graph,
            catalogId,
            conflicts.Select(static item => item.Id).ToHashSet());
        return conflicts
            .Select(item => new ConflictingAttachment(
                item.Id,
                item.Name!,
                EmbeddedStreamIds(graph, item.Dictionary),
                !external.Contains(item.Id)))
            .OrderBy(static item => item.Name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Finds file specifications that something other than the catalog points at - a
    /// /FileAttachment annotation, a page /AF array or a structure element. Dereferencing one
    /// of those would change what the document shows, so replacement is refused instead.
    /// </summary>
    private static IReadOnlySet<PdfObjectId> CountExternalReferences(
        PdfObjectGraph graph,
        PdfObjectId? catalogId,
        IReadOnlySet<PdfObjectId> candidates)
    {
        var referenced = new HashSet<PdfObjectId>();
        foreach (var indirectObject in graph.Objects.Values)
        {
            if (indirectObject.Id == catalogId || candidates.Contains(indirectObject.Id))
            {
                continue;
            }

            Walk(indirectObject.Value, 0);
        }

        return referenced;

        void Walk(PdfObject? value, int depth)
        {
            if (value is null || depth > 64)
            {
                return;
            }

            switch (value)
            {
                case PdfReference reference when candidates.Contains(reference.Id):
                    referenced.Add(reference.Id);
                    break;
                case PdfArray array:
                    foreach (var item in array.Items)
                    {
                        Walk(item, depth + 1);
                    }
                    break;
                case PdfDictionary dictionary:
                    foreach (var item in dictionary.Values.Values)
                    {
                        Walk(item, depth + 1);
                    }
                    break;
                case PdfStreamObject stream:
                    Walk(stream.Dictionary, depth + 1);
                    break;
            }
        }
    }

    private static IReadOnlyList<PdfObjectId> EmbeddedStreamIds(PdfObjectGraph graph, PdfDictionary specification) =>
        Resolve(graph, specification["EF"]) is PdfDictionary embedded
            ? embedded.Values.Values.OfType<PdfReference>().Select(static item => item.Id).Distinct().ToArray()
            : [];

    private static IReadOnlyList<string> ReadEmbeddedFileNames(PdfObjectGraph graph) =>
        FindFileSpecifications(graph)
            .Select(static item => item.Name)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToArray()!;

    private static IReadOnlyList<(PdfObjectId Id, PdfDictionary Dictionary, string? Name)> FindFileSpecifications(
        PdfObjectGraph graph) =>
        graph.Objects.Values
            .Where(static item => item.Value is PdfDictionary dictionary && dictionary["EF"] is not null)
            .Select(item => (
                item.Id,
                Dictionary: (PdfDictionary)item.Value,
                Name: ReadText(Resolve(graph, ((PdfDictionary)item.Value)["UF"])) ??
                    ReadText(Resolve(graph, ((PdfDictionary)item.Value)["F"]))))
            .OrderBy(static item => item.Id.Number)
            .ToArray();

    private sealed record ConflictingAttachment(
        PdfObjectId Id,
        string Name,
        IReadOnlyList<PdfObjectId> StreamIds,
        bool SafelyRemovable);

    // ---------------------------------------------------------------- object updates

    private static IReadOnlyList<PdfIncrementalObjectUpdate> BuildAttachmentUpdates(
        ParsedSource source,
        PdfEInvoiceAttachmentPayload payload,
        PdfEInvoiceAttachmentOptions options)
    {
        var graph = source.Graph;
        if (graph.Trailer?["Root"] is not PdfReference rootReference ||
            Resolve(graph, rootReference) is not PdfDictionary catalog)
        {
            throw new InvalidDataException("The PDF catalog could not be resolved for the invoice attachment.");
        }

        var nextNumber = graph.Objects.Keys.Max(static id => id.Number) + 1;
        var streamId = new PdfObjectId(nextNumber++, 0);
        var fileSpecificationId = new PdfObjectId(nextNumber++, 0);
        var metadataId = Resolve(graph, catalog["Metadata"]) is PdfStreamObject && catalog["Metadata"] is PdfReference existingMetadata
            ? existingMetadata.Id
            : new PdfObjectId(nextNumber++, 0);

        var content = payload.Xml.ToArray();
        var embedded = new PdfStreamObject(
            new PdfDictionary
            {
                ["Type"] = new PdfName("EmbeddedFile"),
                ["Subtype"] = new PdfName(payload.MimeType),
                ["Params"] = new PdfDictionary
                {
                    ["Size"] = new PdfInteger(content.Length),
                    ["CheckSum"] = new PdfString(MD5.HashData(content), false),
                    ["ModDate"] = new PdfString(Encoding.ASCII.GetBytes(PdfDate(DateTimeOffset.UtcNow)), false)
                }
            },
            content);

        var fileSpecification = new PdfDictionary
        {
            ["Type"] = new PdfName("Filespec"),
            ["F"] = new PdfString(Encoding.ASCII.GetBytes(AsciiFileName(payload.FileName)), false),
            ["UF"] = new PdfString(Utf16BigEndian(payload.FileName), false),
            ["AFRelationship"] = new PdfName(payload.Relationship.ToString()),
            ["EF"] = new PdfDictionary
            {
                ["F"] = new PdfReference(streamId),
                ["UF"] = new PdfReference(streamId)
            }
        };
        if (!string.IsNullOrWhiteSpace(payload.Description))
        {
            fileSpecification["Desc"] = new PdfString(Utf16BigEndian(payload.Description), false);
        }

        var replaced = options.ReplaceExistingInvoiceAttachment
            ? FindConflictingAttachments(graph, payload.FileName)
            : [];
        var removed = replaced.Select(static item => item.Id).ToHashSet();

        var updatedCatalog = Clone(catalog);
        updatedCatalog["AF"] = BuildAssociatedFileArray(graph, catalog, fileSpecificationId, removed);
        updatedCatalog["Metadata"] = new PdfReference(metadataId);
        if (BuildEmbeddedFileNameTree(graph, catalog, payload.FileName, fileSpecificationId, removed) is { } names)
        {
            updatedCatalog["Names"] = names;
        }

        var metadata = new PdfStreamObject(
            new PdfDictionary
            {
                ["Type"] = new PdfName("Metadata"),
                ["Subtype"] = new PdfName("XML")
            },
            PdfEInvoiceXmpWriter.Build(source.Xmp, payload.XmpMetadata, out _));

        var updates = new List<PdfIncrementalObjectUpdate>
        {
            new(streamId, embedded),
            new(fileSpecificationId, fileSpecification),
            new(metadataId, metadata),
            new(rootReference.Id, updatedCatalog)
        };

        // An incremental update cannot delete an object, and PDF/A-3 refuses a file
        // specification that no /AF array reaches. Overwriting the replaced objects with null
        // makes them undefined in the new revision instead of leaving them orphaned.
        foreach (var conflict in replaced)
        {
            updates.Add(new PdfIncrementalObjectUpdate(conflict.Id, new PdfNull()));
            foreach (var id in conflict.StreamIds)
            {
                updates.Add(new PdfIncrementalObjectUpdate(id, new PdfNull()));
            }
        }

        return updates;
    }

    private static PdfArray BuildAssociatedFileArray(
        PdfObjectGraph graph,
        PdfDictionary catalog,
        PdfObjectId fileSpecificationId,
        IReadOnlySet<PdfObjectId> removed)
    {
        var existing = Resolve(graph, catalog["AF"]) as PdfArray;
        var items = (existing?.Items ?? [])
            .Where(item => item is not PdfReference reference || !removed.Contains(reference.Id))
            .ToList();
        items.Add(new PdfReference(fileSpecificationId));
        return new PdfArray(items);
    }

    /// <summary>
    /// Keeps the /EmbeddedFiles name tree in sync so ordinary readers list the invoice as an
    /// attachment. A tree that already uses /Kids is left untouched: rebalancing it could move
    /// entries the source owns, and PDF/A-3 reaches the file through /AF regardless.
    /// </summary>
    private static PdfDictionary? BuildEmbeddedFileNameTree(
        PdfObjectGraph graph,
        PdfDictionary catalog,
        string fileName,
        PdfObjectId fileSpecificationId,
        IReadOnlySet<PdfObjectId> removed)
    {
        var names = Resolve(graph, catalog["Names"]) as PdfDictionary;
        var embeddedFiles = Resolve(graph, names?["EmbeddedFiles"]) as PdfDictionary;
        if (embeddedFiles?["Kids"] is not null)
        {
            return null;
        }

        var entries = new List<(string Name, PdfObject Value)>();
        if (Resolve(graph, embeddedFiles?["Names"]) is PdfArray existing)
        {
            for (var index = 0; index + 1 < existing.Items.Count; index += 2)
            {
                var key = ReadText(Resolve(graph, existing.Items[index]));
                var value = existing.Items[index + 1];
                if (key is null || (value is PdfReference reference && removed.Contains(reference.Id)))
                {
                    continue;
                }

                entries.Add((key, value));
            }
        }

        entries.RemoveAll(entry => string.Equals(entry.Name, fileName, StringComparison.Ordinal));
        entries.Add((fileName, new PdfReference(fileSpecificationId)));
        entries.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));

        var items = new List<PdfObject>(entries.Count * 2);
        foreach (var entry in entries)
        {
            items.Add(new PdfString(Utf16BigEndian(entry.Name), false));
            items.Add(entry.Value);
        }

        var updatedNames = names is null ? new PdfDictionary() : Clone(names);
        updatedNames["EmbeddedFiles"] = new PdfDictionary { ["Names"] = new PdfArray(items) };
        return updatedNames;
    }

    // ---------------------------------------------------------------- verification

    /// <summary>
    /// Re-reads the produced document and proves the three claims the operation makes: the
    /// invoice comes back byte for byte, the page content is untouched, and the result still
    /// satisfies the PXA PDF/A-3b structural validator.
    /// </summary>
    private static (int PageCount, bool PagesUnchanged) Verify(
        byte[] bytes,
        ParsedSource source,
        PdfEInvoiceAttachmentPayload payload,
        PdfEInvoiceAttachmentPreflightReport preflight,
        PdfEInvoiceAttachmentOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ParsedSource result;
        try
        {
            result = Parse(bytes);
        }
        catch (InvalidDataException exception)
        {
            throw new PdfEInvoiceAttachmentBlockedException(
                $"The attachment result failed post-save parsing: {exception.Message} The original file is unchanged.",
                preflight);
        }

        // The limits here only have to admit everything the result legitimately contains -
        // including attachments the source already carried, which may be larger than the
        // invoice - because this pass exists to compare bytes, not to re-impose input bounds.
        var attachments = PdfAssociatedFileExtractor.Extract(
            result.Graph,
            new PdfAssociatedFileExtractionOptions
            {
                MaximumPdfSizeBytes = options.MaximumOutputSizeBytes,
                MaximumFileSizeBytes = (int)Math.Min(options.MaximumOutputSizeBytes, int.MaxValue),
                MaximumTotalFileSizeBytes = options.MaximumOutputSizeBytes
            });
        var attached = attachments.SingleOrDefault(item =>
            string.Equals(item.FileName, payload.FileName, StringComparison.Ordinal));
        if (attached is null ||
            !attached.SizeMatches ||
            !attached.ChecksumMatches ||
            !attached.Content.Span.SequenceEqual(payload.Xml.Span))
        {
            throw new PdfEInvoiceAttachmentBlockedException(
                "The attached invoice could not be extracted byte for byte from the result. The original file is unchanged.",
                preflight);
        }

        var diagnostics = new List<PdfTrustDiagnostic>();
        PdfA2bConformanceValidator.Validate(
            result.Graph,
            result.IsEncrypted,
            diagnostics,
            PdfArchiveProfile.PdfA3b);
        if (diagnostics.Any(static item => item.Severity == PdfTrustDiagnosticSeverity.Error))
        {
            var first = diagnostics.First(static item => item.Severity == PdfTrustDiagnosticSeverity.Error);
            throw new PdfEInvoiceAttachmentBlockedException(
                $"The result did not pass the PXA PDF/A-3b structural validator ({first.Code}). The original file is unchanged.",
                preflight);
        }

        var pagesUnchanged =
            result.Pages.Count == source.Pages.Count &&
            result.Pages.SequenceEqual(source.Pages, StringComparer.Ordinal);
        if (preflight.Strategy == PdfEInvoiceAttachmentStrategy.IncrementalUpdate && !pagesUnchanged)
        {
            throw new PdfEInvoiceAttachmentBlockedException(
                "The incremental attachment changed visible page content, which must never happen. The original file is unchanged.",
                preflight);
        }

        return (result.Pages.Count, pagesUnchanged);
    }

    // ---------------------------------------------------------------- parsing

    private static void ValidatePayload(PdfEInvoiceAttachmentPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Xml.IsEmpty)
        {
            throw new ArgumentException("The invoice XML cannot be empty.", nameof(payload));
        }
        if (!IsSafeFileName(payload.FileName))
        {
            throw new ArgumentException(
                "The attachment filename must be a plain ASCII file name without path separators or control characters.",
                nameof(payload));
        }
        if (!IsSafeMimeType(payload.MimeType))
        {
            throw new ArgumentException("The attachment MIME type is not a valid media type.", nameof(payload));
        }
        if (!Enum.IsDefined(payload.Relationship))
        {
            throw new ArgumentException("The associated-file relationship is not supported.", nameof(payload));
        }
        if (payload.Description is { Length: > MaximumDescriptionLength })
        {
            throw new ArgumentException(
                $"The attachment description cannot exceed {MaximumDescriptionLength} characters.",
                nameof(payload));
        }
    }

    private static void ValidateOptions(PdfEInvoiceAttachmentOptions options)
    {
        if (options.MaximumPdfSizeBytes <= 0 || options.MaximumPdfSizeBytes > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The attachment source size limit must be between 1 byte and 2 GiB.");
        }
        if (options.MaximumOutputSizeBytes <= 0 || options.MaximumOutputSizeBytes > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The attachment output size limit must be between 1 byte and 2 GiB.");
        }
    }

    internal static bool IsSafeFileName(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= MaximumFileNameLength &&
        value.Trim() == value &&
        value is not ("." or "..") &&
        value.All(static character =>
            character is >= ' ' and <= '~' &&
            character is not ('/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|'));

    internal static bool IsSafeMimeType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 127)
        {
            return false;
        }

        var slash = value.IndexOf('/');
        if (slash <= 0 || slash != value.LastIndexOf('/') || slash == value.Length - 1)
        {
            return false;
        }

        return value.All(static character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '/' or '.' or '+' or '-' or '_');
    }

    private static async Task<ParsedSource> ParseAsync(
        Stream pdfStream,
        PdfEInvoiceAttachmentOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await pdfStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
            if (memory.Length + read > options.MaximumPdfSizeBytes)
            {
                throw new InvalidDataException("The PDF exceeds the configured attachment size limit.");
            }

            memory.Write(buffer, 0, read);
        }

        return Parse(memory.ToArray(), cancellationToken);
    }

    private static ParsedSource Parse(byte[] bytes, CancellationToken cancellationToken = default)
    {
        if (bytes.Length == 0)
        {
            throw new InvalidDataException("The PDF is empty.");
        }

        var limits = new PdfParseLimits();
        var context = new PdfParseContext(bytes, new PdfImporterOptions { Limits = limits }, cancellationToken);
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

        var signatureCount = graph.Objects.Values.Count(item =>
            item.Value is PdfDictionary dictionary &&
            (ReadName(graph, dictionary["Type"]) is "Sig" or "DocTimeStamp" ||
                (dictionary["ByteRange"] is not null && dictionary["Contents"] is not null)));
        var xmp = ReadDocumentXmp(graph);
        return new ParsedSource(
            bytes,
            graph,
            graph.Trailer?["Encrypt"] is not null,
            signatureCount,
            ComputePageDigests(graph),
            ReadArchiveProfile(xmp),
            IsStructurallyValidPdfA3(graph, graph.Trailer?["Encrypt"] is not null),
            xmp,
            !xmp.IsEmpty,
            ReadEmbeddedFileNames(graph));
    }

    private static bool IsStructurallyValidPdfA3(PdfObjectGraph graph, bool isEncrypted)
    {
        var diagnostics = new List<PdfTrustDiagnostic>();
        return PdfA2bConformanceValidator.Validate(
            graph,
            isEncrypted,
            diagnostics,
            PdfArchiveProfile.PdfA3b);
    }

    private static ReadOnlyMemory<byte> ReadDocumentXmp(PdfObjectGraph graph)
    {
        if (Resolve(graph, graph.Trailer?["Root"]) is not PdfDictionary catalog ||
            Resolve(graph, catalog["Metadata"]) is not PdfStreamObject metadata)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        try
        {
            var bytes = DecodeStream(metadata);
            return bytes.Length <= PdfEInvoiceXmpWriter.MaximumXmpBytes ? bytes : ReadOnlyMemory<byte>.Empty;
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
    }

    /// <summary>Reads the archival profile the source claims, for example "PDF/A-2B".</summary>
    private static string? ReadArchiveProfile(ReadOnlyMemory<byte> xmp)
    {
        if (xmp.IsEmpty)
        {
            return null;
        }

        var text = Encoding.UTF8.GetString(xmp.Span);
        var part = ReadXmpValue(text, "pdfaid:part");
        if (part is null)
        {
            return null;
        }

        var conformance = ReadXmpValue(text, "pdfaid:conformance");
        return conformance is null ? $"PDF/A-{part}" : $"PDF/A-{part}{conformance.ToUpperInvariant()}";
    }

    private static string? ReadXmpValue(string xmp, string name)
    {
        var open = xmp.IndexOf($"<{name}>", StringComparison.OrdinalIgnoreCase);
        if (open >= 0)
        {
            var start = open + name.Length + 2;
            var end = xmp.IndexOf($"</{name}>", start, StringComparison.OrdinalIgnoreCase);
            return end > start ? xmp[start..end].Trim() : null;
        }

        var attribute = xmp.IndexOf($"{name}=\"", StringComparison.OrdinalIgnoreCase);
        if (attribute < 0)
        {
            return null;
        }

        var valueStart = attribute + name.Length + 2;
        var valueEnd = xmp.IndexOf('"', valueStart);
        return valueEnd > valueStart ? xmp[valueStart..valueEnd].Trim() : null;
    }

    /// <summary>
    /// Digests every page's decoded content streams in page-tree order. Comparing the digests
    /// before and after the attachment proves that no visible page was replaced or redrawn.
    /// </summary>
    private static IReadOnlyList<string> ComputePageDigests(PdfObjectGraph graph)
    {
        const int maximumDepth = 64;
        var digests = new List<string>();
        if (Resolve(graph, graph.Trailer?["Root"]) is not PdfDictionary catalog)
        {
            return digests;
        }

        var visited = new HashSet<PdfObjectId>();
        Collect(catalog["Pages"], 0);
        return digests;

        void Collect(PdfObject? value, int depth)
        {
            if (depth > maximumDepth || digests.Count >= 10_000)
            {
                return;
            }
            if (value is PdfReference reference && !visited.Add(reference.Id))
            {
                return;
            }
            if (Resolve(graph, value) is not PdfDictionary dictionary)
            {
                return;
            }
            if (ReadName(graph, dictionary["Type"]) == "Page")
            {
                digests.Add(DigestPage(graph, dictionary));
                return;
            }
            if (Resolve(graph, dictionary["Kids"]) is not PdfArray kids)
            {
                return;
            }

            foreach (var kid in kids.Items)
            {
                Collect(kid, depth + 1);
            }
        }
    }

    private static string DigestPage(PdfObjectGraph graph, PdfDictionary page)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var key in new[] { "MediaBox", "CropBox", "Rotate" })
        {
            if (Resolve(graph, page[key]) is { } value)
            {
                hash.AppendData(Encoding.UTF8.GetBytes($"{key}:{Describe(graph, value)}"));
            }
        }

        foreach (var stream in ResolveContentStreams(graph, page["Contents"]))
        {
            try
            {
                hash.AppendData(DecodeStream(stream).Span);
            }
            catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
            {
                // An undecodable stream is still compared by its stored bytes, which change
                // whenever the page is rewritten.
                hash.AppendData(stream.EncodedBytes.Span);
            }
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static string Describe(PdfObjectGraph graph, PdfObject value) => value switch
    {
        PdfInteger integer => integer.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        PdfNumber number => number.Value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture),
        PdfName name => name.Value,
        PdfArray array => string.Join(",", array.Items.Select(item =>
            Resolve(graph, item) is { } resolved ? Describe(graph, resolved) : "?")),
        _ => value.GetType().Name
    };

    private static ReadOnlyMemory<byte> DecodeStream(PdfStreamObject stream)
    {
        if (stream.IsDecoded)
        {
            return stream.DecodedBytes;
        }

        var registry = new PdfStreamDecoderRegistry();
        if (registry.Evaluate(stream.Dictionary["Filter"])
            .Any(static support => support.Status != PdfStreamDecoderSupportStatus.Supported))
        {
            throw new NotSupportedException("The stream uses an unsupported PDF filter.");
        }

        return registry.Decode(stream);
    }

    private static IEnumerable<PdfStreamObject> ResolveContentStreams(PdfObjectGraph graph, PdfObject? value)
    {
        var resolved = Resolve(graph, value);
        if (resolved is PdfStreamObject stream)
        {
            yield return stream;
            yield break;
        }
        if (resolved is not PdfArray array)
        {
            yield break;
        }

        foreach (var item in array.Items)
        {
            if (Resolve(graph, item) is PdfStreamObject itemStream)
            {
                yield return itemStream;
            }
        }
    }

    // ---------------------------------------------------------------- primitives

    private static PdfComplianceConversionFinding Error(string code, string message) =>
        new(code, PdfTrustDiagnosticSeverity.Error, message);

    private static PdfDictionary Clone(PdfDictionary source) =>
        new(new Dictionary<string, PdfObject>(source.Values, StringComparer.Ordinal));

    private static byte[] Utf16BigEndian(string value) =>
        [0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes(value)];

    private static string AsciiFileName(string value) =>
        new(value.Select(static character => character is >= ' ' and <= '~' ? character : '_').ToArray());

    private static string PdfDate(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("'D:'yyyyMMddHHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture);

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

    private static string? ReadName(PdfObjectGraph graph, PdfObject? value) =>
        Resolve(graph, value) is PdfName name ? name.Value : null;

    private static PdfObject? Resolve(PdfObjectGraph graph, PdfObject? value)
    {
        var visited = new HashSet<PdfObjectId>();
        while (value is PdfReference reference && visited.Add(reference.Id))
        {
            value = graph.Resolve(reference.Id)?.Value;
        }

        return value;
    }

    private sealed record ParsedSource(
        byte[] Bytes,
        PdfObjectGraph Graph,
        bool IsEncrypted,
        int SignatureCount,
        IReadOnlyList<string> Pages,
        string? ArchiveProfile,
        bool IsPdfA3,
        ReadOnlyMemory<byte> Xmp,
        bool HasReadableXmp,
        IReadOnlyList<string> EmbeddedFileNames);
}
