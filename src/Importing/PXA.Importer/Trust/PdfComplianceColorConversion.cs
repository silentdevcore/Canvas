using PXA.Importer.Color;
using PXA.Importer.Objects;
using PXA.Importer.Saving;

namespace PXA.Importer.Trust;

public sealed record PdfColorConversionAnalysis(
    int ConvertibleOperatorCount,
    IReadOnlyList<PdfComplianceConversionFinding> Blocking,
    IReadOnlyList<PdfIncrementalObjectUpdate> Updates)
{
    public int ConvertibleImageCount { get; init; }

    public bool IsBlocked => Blocking.Count > 0;
}

/// <summary>
/// Converts device-dependent colour in page and form content to sRGB so that a document can
/// meet a PDF/A output intent.
///
/// Colour used by content-stream operators and bounded losslessly decoded image samples is
/// converted. Encoded image formats and unsupported image semantics remain fail-closed.
/// </summary>
internal static class PdfComplianceColorConversion
{
    public static PdfColorConversionAnalysis Analyze(PdfObjectGraph graph)
    {
        var blocking = new List<PdfComplianceConversionFinding>();
        var updates = new List<PdfIncrementalObjectUpdate>();
        var converted = 0;
        var convertedImages = 0;
        var resolve = CreateResolver(graph);

        foreach (var indirectObject in EnumerateReachableImages(graph, resolve))
        {
            var stream = (PdfStreamObject)indirectObject.Value;
            var conversion = PdfImageColorConverter.Convert(stream, resolve);
            if (conversion.BlockingReason is { } reason)
            {
                var imageSpace = PdfSrgbColorConverter.Resolve(stream.Dictionary["ColorSpace"], resolve);
                blocking.Add(new PdfComplianceConversionFinding(
                    "PXAPDFACONVERT010",
                    PdfTrustDiagnosticSeverity.Error,
                    $"Image XObject uses the {Describe(imageSpace)} colour space. {reason}",
                    indirectObject.Id.Number,
                    indirectObject.Id.Generation));
            }
            else if (conversion is { Converted: true, Stream: not null })
            {
                convertedImages++;
                updates.Add(new PdfIncrementalObjectUpdate(indirectObject.Id, conversion.Stream));
            }
        }

        foreach (var holder in EnumerateContentHolders(graph, resolve))
        {
            var rewriter = new PdfContentColorRewriter(resolve, holder.ColorSpaces, holder.Patterns);
            byte[] decoded;
            try
            {
                decoded = Decode(holder.Stream);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                blocking.Add(new PdfComplianceConversionFinding(
                    "PXAPDFACONVERT011",
                    PdfTrustDiagnosticSeverity.Error,
                    "A content stream could not be decoded, so its colour usage cannot be assessed.",
                    holder.Id.Number,
                    holder.Id.Generation));
                continue;
            }

            PdfColorRewriteResult result;
            try
            {
                result = rewriter.Rewrite(decoded);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                blocking.Add(new PdfComplianceConversionFinding(
                    "PXAPDFACONVERT014",
                    PdfTrustDiagnosticSeverity.Error,
                    "A content stream could not be analyzed safely, so colour conversion was blocked.",
                    holder.Id.Number,
                    holder.Id.Generation));
                continue;
            }
            foreach (var name in result.UnconvertibleColorSpaceNames)
            {
                var message = name.StartsWith("Shading", StringComparison.Ordinal)
                    ? "Content uses a shading resource whose colour samples cannot yet be converted safely."
                    : name.StartsWith("Pattern:", StringComparison.Ordinal)
                        ? "Content selects an uncolored, shading, missing, or malformed pattern. Only colored tiling pattern streams can currently be converted safely."
                    : name == "InlineImage"
                        ? "Content contains an inline image without a demonstrably safe DeviceGray or DeviceRGB colour space."
                        : $"Content uses the colour space resource '/{name}', which cannot be converted to sRGB deterministically.";
                blocking.Add(new PdfComplianceConversionFinding(
                    "PXAPDFACONVERT012",
                    PdfTrustDiagnosticSeverity.Error,
                    message,
                    holder.Id.Number,
                    holder.Id.Generation));
            }

            if (result.ConvertedOperatorCount == 0)
            {
                continue;
            }

            converted += result.ConvertedOperatorCount;
            var dictionary = Clone(holder.Stream.Dictionary);
            // The rewritten content is stored uncompressed, so /Filter must not claim otherwise.
            dictionary["Filter"] = null;
            dictionary["DecodeParms"] = null;
            dictionary["Length"] = new PdfInteger(result.Content.Length);
            updates.Add(new PdfIncrementalObjectUpdate(
                holder.Id,
                new PdfStreamObject(dictionary, result.Content)));
        }

        return new PdfColorConversionAnalysis(converted, blocking, updates)
        {
            ConvertibleImageCount = convertedImages
        };
    }

    private static IEnumerable<ContentHolder> EnumerateContentHolders(
        PdfObjectGraph graph,
        Func<PdfObject?, PdfObject?> resolve)
    {
        foreach (var indirectObject in graph.Objects.Values.OrderBy(static value => value.Id.Number))
        {
            if (indirectObject.Value is not PdfDictionary page ||
                ReadName(resolve, page["Type"]) != "Page")
            {
                continue;
            }

            var resources = ReadInheritedResources(graph, resolve, page);
            var colorSpaces = ReadColorSpaceResources(resolve, resources);
            var patterns = ReadPatternResources(resolve, resources);
            foreach (var contentId in ReadContentStreamIds(page))
            {
                if (graph.Resolve(contentId)?.Value is PdfStreamObject content)
                {
                    yield return new ContentHolder(contentId, content, colorSpaces, patterns);
                }
            }
        }

        foreach (var indirectObject in EnumerateReachableForms(graph, resolve))
        {
            if (indirectObject.Value is PdfStreamObject form)
            {
                yield return new ContentHolder(
                    indirectObject.Id,
                    form,
                    ReadColorSpaceResources(resolve, form.Dictionary["Resources"]),
                    ReadPatternResources(resolve, form.Dictionary["Resources"]));
            }
        }

        foreach (var indirectObject in EnumerateReachablePatterns(graph, resolve))
        {
            if (indirectObject.Value is PdfStreamObject pattern &&
                ReadInteger(resolve, pattern.Dictionary["PatternType"]) == 1 &&
                ReadInteger(resolve, pattern.Dictionary["PaintType"]) == 1)
            {
                yield return new ContentHolder(
                    indirectObject.Id,
                    pattern,
                    ReadColorSpaceResources(resolve, pattern.Dictionary["Resources"]),
                    ReadPatternResources(resolve, pattern.Dictionary["Resources"]));
            }
        }
    }

    private static IEnumerable<PdfIndirectObject> EnumerateReachableImages(
        PdfObjectGraph graph,
        Func<PdfObject?, PdfObject?> resolve) =>
        EnumerateReachableXObjects(graph, resolve, "Image");

    private static IEnumerable<PdfIndirectObject> EnumerateReachableForms(
        PdfObjectGraph graph,
        Func<PdfObject?, PdfObject?> resolve) =>
        EnumerateReachableXObjects(graph, resolve, "Form");

    private static IEnumerable<PdfIndirectObject> EnumerateReachablePatterns(
        PdfObjectGraph graph,
        Func<PdfObject?, PdfObject?> resolve) =>
        EnumerateReachableXObjects(graph, resolve, "Pattern");

    private static IEnumerable<PdfIndirectObject> EnumerateReachableXObjects(
        PdfObjectGraph graph,
        Func<PdfObject?, PdfObject?> resolve,
        string requestedSubtype)
    {
        var visited = new HashSet<PdfObjectId>();
        var matches = new List<PdfIndirectObject>();
        foreach (var page in graph.Objects.Values
                     .Where(item => item.Value is PdfDictionary dictionary && ReadName(resolve, dictionary["Type"]) == "Page")
                     .OrderBy(static item => item.Id.Number))
        {
            VisitResources(ReadInheritedResources(graph, resolve, (PdfDictionary)page.Value));
        }

        return matches;

        void VisitResources(PdfObject? resources)
        {
            if (resolve(resources) is not PdfDictionary resourceDictionary)
            {
                return;
            }

            if (resolve(resourceDictionary["XObject"]) is PdfDictionary xObjects)
            {
                foreach (var reference in xObjects.Values.Values.OfType<PdfReference>())
                {
                    if (!visited.Add(reference.Id))
                    {
                        continue;
                    }

                    var indirectObject = graph.Resolve(reference.Id);
                    if (indirectObject?.Value is not PdfStreamObject stream)
                    {
                        continue;
                    }

                    var subtype = ReadName(resolve, stream.Dictionary["Subtype"]);
                    if (subtype == requestedSubtype)
                    {
                        matches.Add(indirectObject);
                    }

                    if (subtype == "Form")
                    {
                        VisitResources(stream.Dictionary["Resources"] ?? resources);
                    }
                }
            }

            if (resolve(resourceDictionary["Pattern"]) is not PdfDictionary patterns)
            {
                return;
            }

            foreach (var reference in patterns.Values.Values.OfType<PdfReference>())
            {
                if (!visited.Add(reference.Id))
                {
                    continue;
                }

                var indirectObject = graph.Resolve(reference.Id);
                if (indirectObject?.Value is not PdfStreamObject pattern)
                {
                    continue;
                }

                if (requestedSubtype == "Pattern")
                {
                    matches.Add(indirectObject);
                }

                VisitResources(pattern.Dictionary["Resources"]);
            }
        }
    }

    private static PdfObject? ReadInheritedResources(
        PdfObjectGraph graph,
        Func<PdfObject?, PdfObject?> resolve,
        PdfDictionary page)
    {
        var current = page;
        var visited = new HashSet<PdfObjectId>();
        while (true)
        {
            if (current["Resources"] is { } resources)
            {
                return resources;
            }

            if (current["Parent"] is not PdfReference parent || !visited.Add(parent.Id) ||
                graph.Resolve(parent.Id)?.Value is not PdfDictionary parentDictionary)
            {
                return null;
            }

            current = parentDictionary;
        }
    }

    private static IReadOnlyDictionary<string, PdfObject> ReadColorSpaceResources(
        Func<PdfObject?, PdfObject?> resolve,
        PdfObject? resources)
    {
        if (resolve(resources) is not PdfDictionary resourceDictionary ||
            resolve(resourceDictionary["ColorSpace"]) is not PdfDictionary colorSpaces)
        {
            return new Dictionary<string, PdfObject>(StringComparer.Ordinal);
        }

        return colorSpaces.Values.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, PdfObject> ReadPatternResources(
        Func<PdfObject?, PdfObject?> resolve,
        PdfObject? resources)
    {
        if (resolve(resources) is not PdfDictionary resourceDictionary ||
            resolve(resourceDictionary["Pattern"]) is not PdfDictionary patterns)
        {
            return new Dictionary<string, PdfObject>(StringComparer.Ordinal);
        }

        return patterns.Values.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value,
            StringComparer.Ordinal);
    }

    private static IEnumerable<PdfObjectId> ReadContentStreamIds(PdfDictionary page) =>
        page["Contents"] switch
        {
            PdfReference reference => [reference.Id],
            PdfArray array => array.Items.OfType<PdfReference>().Select(static item => item.Id),
            _ => []
        };

    private static byte[] Decode(PdfStreamObject stream) =>
        stream.IsDecoded
            ? stream.DecodedBytes.ToArray()
            : new Streams.PdfStreamDecoderRegistry().Decode(stream).ToArray();

    private static string Describe(PdfResolvedColorSpace space) => space.Family switch
    {
        PdfColorSpaceFamily.DeviceCmyk => "DeviceCMYK",
        PdfColorSpaceFamily.Separation => "Separation",
        PdfColorSpaceFamily.DeviceN => "DeviceN",
        PdfColorSpaceFamily.IccBased => "ICCBased",
        PdfColorSpaceFamily.Indexed => "Indexed",
        PdfColorSpaceFamily.Lab => "Lab",
        PdfColorSpaceFamily.Pattern => "Pattern",
        _ => "unsupported"
    };

    private static Func<PdfObject?, PdfObject?> CreateResolver(PdfObjectGraph graph) => value =>
    {
        var visited = new HashSet<PdfObjectId>();
        while (value is PdfReference reference && visited.Add(reference.Id))
        {
            value = graph.Resolve(reference.Id)?.Value;
        }

        return value;
    };

    private static string? ReadName(Func<PdfObject?, PdfObject?> resolve, PdfObject? value) =>
        resolve(value) is PdfName name ? name.Value : null;

    private static int? ReadInteger(Func<PdfObject?, PdfObject?> resolve, PdfObject? value) =>
        resolve(value) switch
        {
            PdfInteger integer when integer.Value is >= int.MinValue and <= int.MaxValue => (int)integer.Value,
            PdfNumber number when number.Value is >= int.MinValue and <= int.MaxValue => (int)number.Value,
            _ => null
        };

    private static PdfDictionary Clone(PdfDictionary source) =>
        new(new Dictionary<string, PdfObject>(source.Values, StringComparer.Ordinal));

    private sealed record ContentHolder(
        PdfObjectId Id,
        PdfStreamObject Stream,
        IReadOnlyDictionary<string, PdfObject> ColorSpaces,
        IReadOnlyDictionary<string, PdfObject> Patterns);
}
