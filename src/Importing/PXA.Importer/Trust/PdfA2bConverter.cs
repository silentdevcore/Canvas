using System.Security;
using System.Security.Cryptography;
using System.Text;
using PXA.Importer.Content;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Saving;
using PXA.Importer.Streams;
using PXA.Importer.Xref;
using PXA.Pdf;

namespace PXA.Importer.Trust;

/// <summary>
/// Performs a conservative PDF/A-2b conversion. It adds the archival identification and
/// output profile only when the existing document content can be retained without a lossy
/// reconstruction.
/// </summary>
public sealed class PdfA2bConverter(IPdfTrustInspector trustInspector) : IPdfA2bConverter
{
    public async Task<PdfA2bConversionPreflightReport> PreflightAsync(
        Stream pdfStream,
        PdfA2bConversionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var parsed = await ParseAsync(pdfStream, options, cancellationToken).ConfigureAwait(false);
        return BuildPreflight(parsed);
    }

    public async Task<PdfA2bConversionResult> ConvertAsync(
        Stream pdfStream,
        PdfA2bConversionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new PdfA2bConversionOptions();
        var parsed = await ParseAsync(pdfStream, options, cancellationToken).ConfigureAwait(false);
        var preflight = BuildPreflight(parsed);
        if (preflight.Capability == PdfComplianceConversionCapability.Unsupported)
        {
            throw new PdfA2bConversionBlockedException(
                "PDF/A-2b conversion is blocked because the source contains unsupported content.",
                preflight);
        }

        if (preflight.InvalidatesSignatures && !options.AllowSignatureInvalidation)
        {
            throw new PdfA2bConversionBlockedException(
                "PDF/A-2b conversion requires a full rewrite and would invalidate existing signatures.",
                preflight);
        }

        var updates = BuildUpdates(parsed.Graph, parsed.SignatureCount > 0);
        var rewritten = new PdfFullRewriteWriter().RewriteWithUpdates(
            parsed.Source,
            updates,
            new PdfFullRewriteOptions
            {
                PdfVersion = "1.7",
                AllowSignatureInvalidation = options.AllowSignatureInvalidation,
                Limits = parsed.Limits
            });

        await using var validationStream = new MemoryStream(rewritten.Bytes, writable: false);
        var trust = await trustInspector.InspectAsync(
            validationStream,
            new PdfTrustInspectionOptions { MaximumFileSizeBytes = options.MaximumFileSizeBytes },
            cancellationToken).ConfigureAwait(false);
        if (trust.PdfA.Status != PdfTrustStatus.Valid)
        {
            throw new InvalidDataException(
                "The converted document did not pass the PXA PDF/A-2b structural validator; the original remains unchanged.");
        }

        return new PdfA2bConversionResult(
            rewritten.Bytes,
            preflight.SourceSha256,
            Convert.ToHexStringLower(SHA256.HashData(rewritten.Bytes)),
            preflight,
            trust.PdfA);
    }

    private static PdfA2bConversionPreflightReport BuildPreflight(ParsedPdf parsed)
    {
        var findings = new List<PdfComplianceConversionFinding>();
        var validatorDiagnostics = new List<PdfTrustDiagnostic>();
        PdfA2bConformanceValidator.Validate(parsed.Graph, parsed.IsEncrypted, validatorDiagnostics);
        foreach (var diagnostic in validatorDiagnostics.Where(static item =>
                     item.Severity == PdfTrustDiagnosticSeverity.Error &&
                     item.Code is not ("PXAPDFTRUST312" or "PXAPDFTRUST313")))
        {
            findings.Add(new PdfComplianceConversionFinding(
                diagnostic.Code,
                diagnostic.Severity,
                diagnostic.Message,
                diagnostic.ObjectNumber,
                diagnostic.Generation));
        }

        if (HasUnsupportedColorSpace(parsed.Graph))
        {
            findings.Add(new PdfComplianceConversionFinding(
                "PXAPDFACONVERT004",
                PdfTrustDiagnosticSeverity.Error,
                "The source uses a CMYK, Separation, DeviceN, or otherwise unsupported color space. Deterministic color conversion is required before PDF/A-2b conversion."));
        }

        if (HasUnsupportedInteractiveForm(parsed.Graph))
        {
            findings.Add(new PdfComplianceConversionFinding(
                "PXAPDFACONVERT005",
                PdfTrustDiagnosticSeverity.Error,
                "Interactive form fields are not supported by the initial PDF/A-2b conversion path."));
        }

        if (HasEmbeddedFiles(parsed.Graph))
        {
            findings.Add(new PdfComplianceConversionFinding(
                "PXAPDFACONVERT006",
                PdfTrustDiagnosticSeverity.Error,
                "Embedded files and associated-file relationships require an explicit PDF/A-3 workflow and are not converted to PDF/A-2b."));
        }

        if (HasUnsupportedInfoProperties(parsed.Graph))
        {
            findings.Add(new PdfComplianceConversionFinding(
                "PXAPDFACONVERT008",
                PdfTrustDiagnosticSeverity.Error,
                "The information dictionary contains custom or unsupported properties that cannot yet be synchronized with an XMP extension schema."));
        }

        if (parsed.SignatureCount > 0)
        {
            if (HasVisibleSignatureAppearance(parsed.Graph))
            {
                findings.Add(new PdfComplianceConversionFinding(
                    "PXAPDFACONVERT009",
                    PdfTrustDiagnosticSeverity.Error,
                    "A signature field has a visible appearance. PXA cannot remove it without changing visible content; flattening must be implemented before conversion."));
            }

            findings.Add(new PdfComplianceConversionFinding(
                "PXAPDFACONVERT007",
                PdfTrustDiagnosticSeverity.Warning,
                $"A full rewrite will invalidate and remove {parsed.SignatureCount} existing signature object(s). Explicit approval is required."));
        }

        if (findings.All(static item => item.Severity != PdfTrustDiagnosticSeverity.Error))
        {
            findings.Add(new PdfComplianceConversionFinding(
                "PXAPDFACONVERT000",
                PdfTrustDiagnosticSeverity.Info,
                "The source can be converted without discarding visible content. PXA will replace PDF/A identification metadata and the output intent."));
        }

        var capability = findings.Any(static item => item.Severity == PdfTrustDiagnosticSeverity.Error)
            ? PdfComplianceConversionCapability.Unsupported
            : parsed.SignatureCount > 0
                ? PdfComplianceConversionCapability.ReviewRequired
                : PdfComplianceConversionCapability.Ready;

        return new PdfA2bConversionPreflightReport(
            "PDF/A-2b",
            capability,
            Convert.ToHexStringLower(SHA256.HashData(parsed.Source)),
            parsed.Source.Length,
            parsed.Graph.Objects.Count,
            parsed.SignatureCount,
            parsed.SignatureCount > 0,
            true,
            findings);
    }

    private static IReadOnlyList<PdfIncrementalObjectUpdate> BuildUpdates(PdfObjectGraph graph, bool removeSignatures)
    {
        if (graph.Trailer?["Root"] is not PdfReference rootReference ||
            graph.Resolve(rootReference.Id)?.Value is not PdfDictionary catalog)
        {
            throw new InvalidDataException("The PDF catalog could not be resolved for conversion.");
        }

        var nextNumber = graph.Objects.Keys.Max(static id => id.Number) + 1;
        var metadataId = new PdfObjectId(nextNumber++, 0);
        var profileId = new PdfObjectId(nextNumber++, 0);
        var outputIntentId = new PdfObjectId(nextNumber, 0);
        var metadata = new PdfStreamObject(
            new PdfDictionary
            {
                ["Type"] = new PdfName("Metadata"),
                ["Subtype"] = new PdfName("XML")
            },
            BuildXmp(graph));
        var profile = new PdfStreamObject(
            new PdfDictionary
            {
                ["N"] = new PdfInteger(3),
                ["Alternate"] = new PdfName("DeviceRGB")
            },
            PdfComplianceResources.CreateSrgbIccProfile());
        var outputIntent = new PdfDictionary
        {
            ["Type"] = new PdfName("OutputIntent"),
            ["S"] = new PdfName("GTS_PDFA1"),
            ["OutputConditionIdentifier"] = TextString("sRGB IEC61966-2.1"),
            ["Info"] = TextString("PXA sRGB IEC61966-2.1"),
            ["DestOutputProfile"] = new PdfReference(profileId)
        };
        var updatedCatalog = Clone(catalog);
        updatedCatalog["Metadata"] = new PdfReference(metadataId);
        updatedCatalog["OutputIntents"] = new PdfArray([new PdfReference(outputIntentId)]);
        if (removeSignatures)
        {
            updatedCatalog["AcroForm"] = null;
            updatedCatalog["Perms"] = null;
        }

        var updates = new List<PdfIncrementalObjectUpdate>
        {
            new PdfIncrementalObjectUpdate(rootReference.Id, updatedCatalog),
            new PdfIncrementalObjectUpdate(metadataId, metadata),
            new PdfIncrementalObjectUpdate(profileId, profile),
            new PdfIncrementalObjectUpdate(outputIntentId, outputIntent)
        };
        if (removeSignatures)
        {
            AddSignatureWidgetRemovalUpdates(graph, updates);
        }

        return updates;
    }

    private static void AddSignatureWidgetRemovalUpdates(
        PdfObjectGraph graph,
        ICollection<PdfIncrementalObjectUpdate> updates)
    {
        foreach (var pageObject in graph.Objects.Values.Where(item =>
                     item.Value is PdfDictionary page && ReadName(graph, page["Type"]) == "Page"))
        {
            var page = (PdfDictionary)pageObject.Value;
            if (Resolve(graph, page["Annots"]) is not PdfArray annotations)
            {
                continue;
            }

            var retained = annotations.Items.Where(item => !IsSignatureWidget(graph, item)).ToArray();
            if (retained.Length == annotations.Items.Count)
            {
                continue;
            }

            var updatedPage = Clone(page);
            if (retained.Length == 0)
            {
                updatedPage["Annots"] = null;
            }
            else
            {
                updatedPage["Annots"] = new PdfArray(retained);
            }

            updates.Add(new PdfIncrementalObjectUpdate(pageObject.Id, updatedPage));
        }
    }

    private static byte[] BuildXmp(PdfObjectGraph graph)
    {
        var info = Resolve(graph, graph.Trailer?["Info"]) as PdfDictionary;
        var title = ReadText(info?["Title"]);
        var author = ReadText(info?["Author"]);
        var subject = ReadText(info?["Subject"]);
        var keywords = ReadText(info?["Keywords"]);
        var creator = ReadText(info?["Creator"]);
        var producer = ReadText(info?["Producer"]);
        var creationDate = ReadText(info?["CreationDate"]);
        var modificationDate = ReadText(info?["ModDate"]);

        static string Xml(string? value) => SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;
        static string Element(string name, string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : $"<{name}>{Xml(value)}</{name}>";
        static string Alt(string name, string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : $"<{name}><rdf:Alt><rdf:li xml:lang=\"x-default\">{Xml(value)}</rdf:li></rdf:Alt></{name}>";
        static string Seq(string name, string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : $"<{name}><rdf:Seq><rdf:li>{Xml(value)}</rdf:li></rdf:Seq></{name}>";

        var xmp = $"""
            <?xpacket begin="" id="W5M0MpCehiHzreSzNTczkc9d"?>
            <x:xmpmeta xmlns:x="adobe:ns:meta/">
              <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
                <rdf:Description rdf:about="" xmlns:pdfaid="http://www.aiim.org/pdfa/ns/id/"><pdfaid:part>2</pdfaid:part><pdfaid:conformance>B</pdfaid:conformance></rdf:Description>
                <rdf:Description rdf:about="" xmlns:dc="http://purl.org/dc/elements/1.1/">{Alt("dc:title", title)}{Seq("dc:creator", author)}{Alt("dc:description", subject)}</rdf:Description>
                <rdf:Description rdf:about="" xmlns:pdf="http://ns.adobe.com/pdf/1.3/">{Element("pdf:Keywords", keywords)}{Element("pdf:Producer", producer)}</rdf:Description>
                <rdf:Description rdf:about="" xmlns:xmp="http://ns.adobe.com/xap/1.0/">{Element("xmp:CreatorTool", creator)}{Element("xmp:CreateDate", PdfDateToXmp(creationDate))}{Element("xmp:ModifyDate", PdfDateToXmp(modificationDate))}</rdf:Description>
              </rdf:RDF>
            </x:xmpmeta>
            <?xpacket end="w"?>
            """;
        return new UTF8Encoding(false).GetBytes(xmp);
    }

    private static string? PdfDateToXmp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("D:", StringComparison.Ordinal))
        {
            return value;
        }

        var digits = new string(value.Skip(2).TakeWhile(char.IsDigit).ToArray());
        if (digits.Length < 4 || !int.TryParse(digits[..4], out var year))
        {
            return value;
        }

        int Read(int offset, int fallback) => digits.Length >= offset + 2 && int.TryParse(digits.Substring(offset, 2), out var parsed) ? parsed : fallback;
        try
        {
            var suffix = value[(2 + digits.Length)..];
            var offset = TimeSpan.Zero;
            if (suffix.Length >= 3 && suffix[0] is '+' or '-')
            {
                var sign = suffix[0] == '-' ? -1 : 1;
                var offsetDigits = new string(suffix[1..].Where(char.IsDigit).Take(4).ToArray());
                if (offsetDigits.Length >= 2 && int.TryParse(offsetDigits[..2], out var offsetHours))
                {
                    var offsetMinutes = offsetDigits.Length >= 4 && int.TryParse(offsetDigits[2..4], out var minutes) ? minutes : 0;
                    offset = new TimeSpan(sign * offsetHours, sign * offsetMinutes, 0);
                }
            }

            return new DateTimeOffset(year, Read(4, 1), Read(6, 1), Read(8, 0), Read(10, 0), Read(12, 0), offset)
                .ToUniversalTime()
                .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        }
        catch (ArgumentOutOfRangeException)
        {
            return value;
        }
    }

    private static bool HasUnsupportedColorSpace(PdfObjectGraph graph) =>
        graph.Objects.Values.Any(item => ContainsUnsupportedColorSpace(item.Value, graph, new HashSet<PdfObjectId>(), 0)) ||
        HasUnsupportedPageColorOperators(graph);

    private static bool HasUnsupportedPageColorOperators(PdfObjectGraph graph)
    {
        var decoder = new PdfStreamDecoderRegistry();
        var parser = new PdfContentStreamParser();
        foreach (var page in graph.Objects.Values
                     .Select(static item => item.Value)
                     .OfType<PdfDictionary>()
                     .Where(dictionary => ReadName(graph, dictionary["Type"]) == "Page"))
        {
            foreach (var stream in ResolveContentStreams(graph, page["Contents"]))
            {
                IReadOnlyList<PdfContentCommand> commands;
                try
                {
                    var bytes = stream.IsDecoded ? stream.DecodedBytes : decoder.Decode(stream);
                    commands = parser.Parse(bytes);
                }
                catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
                {
                    return true;
                }

                if (commands.Any(command =>
                        command.Operator.Name is "k" or "K" ||
                        command.Operands.Any(operand => ContainsUnsupportedColorSpace(
                            operand,
                            graph,
                            new HashSet<PdfObjectId>(),
                            0))))
                {
                    return true;
                }
            }
        }

        return false;
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

    private static bool ContainsUnsupportedColorSpace(PdfObject value, PdfObjectGraph graph, HashSet<PdfObjectId> visited, int depth)
    {
        if (depth > 64)
        {
            return true;
        }

        if (value is PdfReference reference)
        {
            return visited.Add(reference.Id) && graph.Resolve(reference.Id)?.Value is { } resolved &&
                   ContainsUnsupportedColorSpace(resolved, graph, visited, depth + 1);
        }

        if (value is PdfName { Value: "DeviceCMYK" or "Separation" or "DeviceN" or "Pattern" })
        {
            return true;
        }

        return value switch
        {
            PdfArray array when array.Items.Count >= 2 &&
                                ReadName(graph, array.Items[0]) == "ICCBased" &&
                                Resolve(graph, array.Items[1]) is PdfStreamObject profile &&
                                ReadInteger(graph, profile.Dictionary["N"]) != 3 => true,
            PdfArray array => array.Items.Any(item => ContainsUnsupportedColorSpace(item, graph, visited, depth + 1)),
            PdfDictionary dictionary => dictionary.Values.Values.Any(item => ContainsUnsupportedColorSpace(item, graph, visited, depth + 1)),
            PdfStreamObject stream => ContainsUnsupportedColorSpace(stream.Dictionary, graph, visited, depth + 1),
            _ => false
        };
    }

    private static bool HasUnsupportedInteractiveForm(PdfObjectGraph graph)
    {
        var catalog = Resolve(graph, graph.Trailer?["Root"]) as PdfDictionary;
        if (catalog?["AcroForm"] is { } acroFormValue)
        {
            if (Resolve(graph, acroFormValue) is not PdfDictionary acroForm ||
                Resolve(graph, acroForm["Fields"]) is not PdfArray fields ||
                fields.Items.Any(field => !IsSignatureField(graph, field, null, new HashSet<PdfObjectId>())))
            {
                return true;
            }
        }

        return graph.Objects.Values.Any(item =>
            item.Value is PdfDictionary dictionary &&
            ReadName(graph, dictionary["Subtype"]) == "Widget" &&
            ResolveFieldType(graph, dictionary) != "Sig");
    }

    private static bool IsSignatureField(
        PdfObjectGraph graph,
        PdfObject value,
        string? inheritedType,
        HashSet<PdfObjectId> visited)
    {
        if (value is PdfReference reference && !visited.Add(reference.Id))
        {
            return false;
        }

        if (Resolve(graph, value) is not PdfDictionary field)
        {
            return false;
        }

        var fieldType = ReadName(graph, field["FT"]) ?? inheritedType;
        if (Resolve(graph, field["Kids"]) is PdfArray kids && kids.Items.Count > 0)
        {
            return kids.Items.All(kid => IsSignatureField(graph, kid, fieldType, visited));
        }

        return fieldType == "Sig";
    }

    private static string? ResolveFieldType(PdfObjectGraph graph, PdfDictionary field)
    {
        var visited = new HashSet<PdfObjectId>();
        PdfDictionary? current = field;
        while (current is not null)
        {
            if (ReadName(graph, current["FT"]) is { } fieldType)
            {
                return fieldType;
            }

            if (current["Parent"] is not PdfReference parent ||
                !visited.Add(parent.Id) ||
                graph.Resolve(parent.Id)?.Value is not PdfDictionary parentField)
            {
                return null;
            }

            current = parentField;
        }

        return null;
    }

    private static bool HasVisibleSignatureAppearance(PdfObjectGraph graph) =>
        graph.Objects.Values.Any(item =>
            item.Value is PdfDictionary dictionary &&
            IsSignatureWidget(graph, dictionary) &&
            dictionary["AP"] is not null);

    private static bool IsSignatureWidget(PdfObjectGraph graph, PdfObject value) =>
        Resolve(graph, value) is PdfDictionary dictionary && IsSignatureWidget(graph, dictionary);

    private static bool IsSignatureWidget(PdfObjectGraph graph, PdfDictionary dictionary) =>
        ReadName(graph, dictionary["Subtype"]) == "Widget" && ResolveFieldType(graph, dictionary) == "Sig";

    private static bool HasEmbeddedFiles(PdfObjectGraph graph)
    {
        var catalog = Resolve(graph, graph.Trailer?["Root"]) as PdfDictionary;
        return catalog?["AF"] is not null || graph.Objects.Values.Any(item =>
            item.Value is PdfDictionary dictionary &&
            (ReadName(graph, dictionary["Type"]) == "Filespec" || dictionary["EmbeddedFiles"] is not null));
    }

    private static bool HasUnsupportedInfoProperties(PdfObjectGraph graph)
    {
        if (Resolve(graph, graph.Trailer?["Info"]) is not PdfDictionary info)
        {
            return false;
        }

        var supported = new HashSet<string>(StringComparer.Ordinal)
        {
            "Title", "Author", "Subject", "Keywords", "Creator", "Producer", "CreationDate", "ModDate"
        };
        return info.Values.Keys.Any(key => !supported.Contains(key));
    }

    private static async Task<ParsedPdf> ParseAsync(
        Stream stream,
        PdfA2bConversionOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        options ??= new PdfA2bConversionOptions();
        if (options.MaximumFileSizeBytes <= 0 || options.MaximumFileSizeBytes > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The PDF/A conversion size limit must be between 1 byte and 2 GiB.");
        }

        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (memory.Length + read > options.MaximumFileSizeBytes)
                throw new InvalidDataException("The PDF exceeds the configured conversion limit.");
            memory.Write(buffer, 0, read);
        }

        var source = memory.ToArray();
        var limits = new PdfParseLimits();
        var context = new PdfParseContext(source, new PdfImporterOptions { Limits = limits }, cancellationToken);
        var xref = new PdfCrossReferenceParser().Parse(context);
        if (xref.Diagnostics.Count > 0)
            throw new InvalidDataException($"The source PDF cross-reference data is invalid: {xref.Diagnostics[0].Code}.");
        var graph = new PdfObjectParser(context, xref).ParseDocumentGraph();
        if (context.Diagnostics.Count > 0 || graph.Objects.Count == 0)
            throw new InvalidDataException("The source PDF could not be parsed completely.");

        var signatureCount = graph.Objects.Values.Count(item => item.Value is PdfDictionary dictionary &&
            (ReadName(graph, dictionary["Type"]) is "Sig" or "DocTimeStamp" || dictionary["ByteRange"] is not null && dictionary["Contents"] is not null));
        return new ParsedPdf(source, graph, limits, graph.Trailer?["Encrypt"] is not null, signatureCount);
    }

    private static PdfDictionary Clone(PdfDictionary source) => new(new Dictionary<string, PdfObject>(source.Values, StringComparer.Ordinal));
    private static PdfString TextString(string value) => new(Encoding.UTF8.GetBytes(value), false);
    private static string? ReadText(PdfObject? value) => value is PdfString text ? DecodeTextString(text) : null;
    private static string DecodeTextString(PdfString value)
    {
        var bytes = value.GetDecodedBytes().Span;
        return bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF
            ? Encoding.BigEndianUnicode.GetString(bytes[2..])
            : Encoding.Latin1.GetString(bytes);
    }
    private static string? ReadName(PdfObjectGraph graph, PdfObject? value) => Resolve(graph, value) is PdfName name ? name.Value : null;
    private static int? ReadInteger(PdfObjectGraph graph, PdfObject? value) => Resolve(graph, value) switch
    {
        PdfInteger integer when integer.Value is >= int.MinValue and <= int.MaxValue => (int)integer.Value,
        PdfNumber number when number.Value is >= int.MinValue and <= int.MaxValue => (int)number.Value,
        _ => null
    };
    private static PdfObject? Resolve(PdfObjectGraph graph, PdfObject? value)
    {
        var visited = new HashSet<PdfObjectId>();
        while (value is PdfReference reference && visited.Add(reference.Id)) value = graph.Resolve(reference.Id)?.Value;
        return value;
    }

    private sealed record ParsedPdf(byte[] Source, PdfObjectGraph Graph, PdfParseLimits Limits, bool IsEncrypted, int SignatureCount);
}
