using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PVAD.Importer.Analysis;
using PVAD.Importer.Content;
using PVAD.Importer.Document;
using PVAD.Importer.Graphics;
using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Saving;
using PVAD.Importer.Xref;

namespace PVAD.Importer.Redaction;

/// <summary>
/// Applies redaction as a bounded edit rather than a regeneration.
///
/// Only pages carrying a region are rewritten; every other object in the document is carried
/// through untouched. The result is saved as a full rewrite so that no earlier revision
/// retains the removed content, and is then reparsed and verified before it is returned.
/// </summary>
public sealed class PdfRedactionApplier(IPdfRedactionPreflight preflight) : IPdfRedactionApplier
{
    /// <summary>
    /// Findings that sanitization or the full rewrite genuinely resolve. Everything else —
    /// in particular any geometric ambiguity — blocks the apply.
    /// </summary>
    private static readonly Dictionary<string, Func<PdfRedactionSanitizationPolicy, bool>> ResolvableFindings = new(StringComparer.Ordinal)
    {
        ["PXAPDFREDACT102"] = static _ => true,
        ["PXAPDFREDACT103"] = static policy => policy.RemoveEmbeddedFiles,
        ["PXAPDFREDACT104"] = static policy => policy.RemoveDocumentMetadata,
        ["PXAPDFREDACT107"] = static policy => policy.RemoveActionsAndJavaScript,
        ["PXAPDFREDACT202"] = static policy => policy.RemoveOverlappingAnnotations,
        ["PXAPDFREDACT207"] = static policy => policy.RemoveOverlappingAnnotations,
        ["PXAPDFREDACT108"] = static policy => policy.RemoveHiddenRepresentations,
        ["PXAPDFREDACT203"] = static policy => policy.RemoveHiddenRepresentations
    };

    private readonly BoundingBoxCalculator _bounds = new();

    public async Task<PdfRedactionApplyResult> ApplyAsync(
        Stream pdfStream,
        IReadOnlyList<PdfRedactionRegion> regions,
        PdfRedactionApplyOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);
        ArgumentNullException.ThrowIfNull(regions);
        options ??= new PdfRedactionApplyOptions();

        var source = await ReadAllAsync(pdfStream, cancellationToken).ConfigureAwait(false);
        await using var preflightStream = new MemoryStream(source, writable: false);
        var report = await preflight
            .PreflightAsync(preflightStream, regions, options.Preflight, cancellationToken)
            .ConfigureAwait(false);

        var blocking = CollectBlockingFindings(report, options.Sanitization);
        if (blocking.Count > 0)
        {
            throw new PdfRedactionBlockedException(
                $"Redaction was not applied because {blocking.Count} finding(s) describe content that cannot be confidently removed: " +
                string.Join(", ", blocking.Select(static finding => finding.Code).Distinct(StringComparer.Ordinal)) + ".",
                report);
        }

        var context = new PdfParseContext(source, new PdfImporterOptions(), cancellationToken);
        var table = new PdfCrossReferenceParser().Parse(context);
        var graph = new PdfObjectParser(context, table).ParseDocumentGraph();
        var document = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter()).Build(graph);

        var regionsByPage = regions
            .GroupBy(static region => region.PageNumber)
            .ToDictionary(static group => group.Key, static group => group.Select(static region => region.Bounds).ToArray());

        var removedText = new List<string>();
        var removedElements = 0;
        var updates = new List<PdfIncrementalObjectUpdate>();
        var rewrittenPages = new List<int>();
        var rewriter = new PdfContentStreamRewriter();
        var nextObjectNumber = graph.Objects.Keys.Max(static id => id.Number) + 1;

        foreach (var (pageNumber, pageRegions) in regionsByPage.OrderBy(static entry => entry.Key))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = document.Pages.ElementAtOrDefault(pageNumber - 1);
            if (page is null)
            {
                continue;
            }

            PdfDictionary? resourceOverride = null;
            var originalImageResources = CollectImageResourceNames(page.GraphicsObjects, includeDeleted: true);
            var originalPatternResources = CollectPatternResourceNames(page.GraphicsObjects, includeDeleted: true);
            var removedOnPage = RedactImages(
                graph,
                page,
                pageRegions,
                updates,
                ref nextObjectNumber,
                ref resourceOverride,
                page.GraphicsObjects,
                depth: 0,
                []);
            PruneUnusedImageResources(
                graph,
                page,
                originalImageResources,
                ref resourceOverride);
            foreach (var element in page.GraphicsObjects)
            {
                removedOnPage += MarkCovered(element, pageRegions, removedText, depth: 0, []);
            }
            PruneUnusedPatternResources(
                graph,
                page,
                originalPatternResources,
                ref resourceOverride);

            if (removedOnPage == 0)
            {
                continue;
            }

            removedElements += removedOnPage;
            rewrittenPages.Add(pageNumber);
            AppendContentStreamUpdates(graph, page, rewriter.Rewrite(page), resourceOverride, updates);
        }

        var removedAnnotations = 0;
        if (options.Sanitization.RemoveOverlappingAnnotations)
        {
            foreach (var (pageNumber, pageRegions) in regionsByPage.OrderBy(static entry => entry.Key))
            {
                var page = document.Pages.ElementAtOrDefault(pageNumber - 1);
                if (page is not null)
                {
                    removedAnnotations += RemoveOverlappingAnnotations(graph, page, pageRegions, updates);
                }
            }
        }

        if (options.Sanitization.RemovePageThumbnails)
        {
            RemovePageThumbnails(graph, document, updates);
        }

        var appliedSanitizations = SanitizeCatalog(graph, options.Sanitization, updates).ToList();
        appliedSanitizations.AddRange(PdfRedactionSanitizer.Sanitize(graph, options.Sanitization, updates));

        var edited = updates.Count == 0
            ? source
            : new PdfIncrementalUpdateWriter().Append(source, updates).Bytes;
        // The intermediate revision still contains the original content streams; the full
        // rewrite is what drops them.
        var rewritten = new PdfFullRewriteWriter().Rewrite(
            edited,
            new PdfFullRewriteOptions { AllowSignatureInvalidation = true });

        var verification = Verify(rewritten.Bytes, regionsByPage, removedText, cancellationToken);
        var audit = await CreateAuditRecordAsync(
            source,
            rewritten.Bytes,
            regions,
            appliedSanitizations,
            verification,
            options,
            cancellationToken).ConfigureAwait(false);
        return new PdfRedactionApplyResult(
            rewritten.Bytes,
            report,
            removedElements,
            removedAnnotations,
            rewrittenPages,
            verification,
            audit);
    }

    private static IReadOnlyList<PdfRedactionFinding> CollectBlockingFindings(
        PdfRedactionPreflightReport report,
        PdfRedactionSanitizationPolicy sanitization)
    {
        var findings = report.DocumentFindings
            .Concat(report.Regions.SelectMany(static region => region.Findings))
            .Where(finding => finding.Capability != PdfRedactionCapability.Removable)
            .Where(finding => !(ResolvableFindings.TryGetValue(finding.Code, out var resolves) && resolves(sanitization)))
            .ToArray();
        if (findings.Length > 0)
        {
            return findings;
        }

        // Partial coverage does not always raise a finding of its own, but it always means the
        // smallest removable unit is larger than the region.
        return report.Regions
            .SelectMany(static region => region.Covered.Select(covered => (region, covered)))
            .Where(static item => item.covered.Capability != PdfRedactionCapability.Removable)
            .Select(static item => new PdfRedactionFinding(
                "PXAPDFREDACT210",
                item.covered.Capability,
                $"Covered {item.covered.Kind} content is not fully contained in the region.",
                item.region.PageNumber,
                item.region.RegionIndex))
            .ToArray();
    }

    private int MarkCovered(
        PdfGraphicsElement element,
        IReadOnlyList<PdfRectangle> regions,
        ICollection<string> removedText,
        int depth,
        IReadOnlyList<PdfGroupElement> ancestors)
    {
        if (depth > 64 || element.IsDeleted)
        {
            return 0;
        }

        if (element is PdfGroupElement group)
        {
            var removedInGroup = 0;
            var nested = ancestors.Append(group).ToArray();
            foreach (var child in group.Children)
            {
                removedInGroup += MarkCovered(child, regions, removedText, depth + 1, nested);
            }

            if (group.Children.Count > 0 && group.Children.All(static child => child.IsDeleted))
            {
                group.IsDeleted = true;
            }

            return removedInGroup;
        }

        // Images are handled before content rewriting. A contained image is already marked
        // deleted; a partial image points at a newly scrubbed resource and must remain.
        if (element is PdfImageElement)
        {
            return 0;
        }

        var bounds = PdfRedactionVisibility.EffectiveBounds(element, _bounds, ancestors, out _);
        var coverage = regions
            .Select(region => PdfRedactionGeometry.Classify(bounds, region))
            .DefaultIfEmpty(PdfRedactionCoverage.None)
            .Max();
        if (coverage == PdfRedactionCoverage.None)
        {
            return 0;
        }

        if (coverage == PdfRedactionCoverage.Partial && element is PdfTextElement partialText)
        {
            if (!PdfTextRedactionEngine.TryRedactPartial(partialText, regions, removedText, out var reason))
            {
                throw new InvalidDataException(
                    $"A preflight-approved text run could not be redacted safely during apply. {reason}");
            }

            return 1;
        }

        element.IsDeleted = true;
        if (element is PdfTextElement text && !string.IsNullOrWhiteSpace(text.Text))
        {
            removedText.Add(text.Text);
        }

        return 1;
    }

    private static void AppendContentStreamUpdates(
        PdfObjectGraph graph,
        PdfPageModel page,
        ReadOnlyMemory<byte> content,
        PdfDictionary? resourceOverride,
        List<PdfIncrementalObjectUpdate> updates)
    {
        if (page.OriginalReference is not { } pageId ||
            graph.Resolve(pageId)?.Value is not PdfDictionary pageDictionary)
        {
            throw new InvalidDataException("A redacted page could not be resolved in the object graph.");
        }

        var streamIds = ReadContentStreamIds(pageDictionary);
        if (streamIds.Count == 0)
        {
            throw new NotSupportedException("A redacted page does not use indirect content streams.");
        }

        // The first stream carries the rewritten page; the rest are emptied so that no original
        // operator survives anywhere in the page's content.
        UpsertUpdate(updates, new PdfIncrementalObjectUpdate(
            streamIds[0],
            new PdfStreamObject(new PdfDictionary(), content)));
        for (var index = 1; index < streamIds.Count; index++)
        {
            UpsertUpdate(updates, new PdfIncrementalObjectUpdate(
                streamIds[index],
                new PdfStreamObject(new PdfDictionary(), ReadOnlyMemory<byte>.Empty)));
        }

        if (streamIds.Count > 1 || resourceOverride is not null)
        {
            var updated = CurrentDictionary(graph, pageId, updates, pageDictionary);
            if (streamIds.Count > 1)
            {
                updated["Contents"] = new PdfReference(streamIds[0]);
            }

            if (resourceOverride is not null)
            {
                updated["Resources"] = resourceOverride;
            }

            UpsertUpdate(updates, new PdfIncrementalObjectUpdate(pageId, updated));
        }
    }

    private int RedactImages(
        PdfObjectGraph graph,
        PdfPageModel page,
        IReadOnlyList<PdfRectangle> regions,
        List<PdfIncrementalObjectUpdate> updates,
        ref int nextObjectNumber,
        ref PdfDictionary? resourceOverride,
        IEnumerable<PdfGraphicsElement> elements,
        int depth,
        IReadOnlyList<PdfGroupElement> ancestors)
    {
        if (depth > 64)
        {
            throw new InvalidDataException("Image redaction content exceeds the maximum nesting depth.");
        }

        var changed = 0;
        foreach (var element in elements)
        {
            if (element.IsDeleted)
            {
                continue;
            }

            if (element is PdfGroupElement group)
            {
                var nested = ancestors.Append(group).ToArray();
                changed += RedactImages(
                    graph, page, regions, updates, ref nextObjectNumber, ref resourceOverride,
                    group.Children, depth + 1, nested);
                continue;
            }

            if (element is not PdfImageElement image)
            {
                continue;
            }

            var bounds = PdfRedactionVisibility.EffectiveBounds(image, _bounds, ancestors, out _);
            var coverage = regions
                .Select(region => PdfRedactionGeometry.Classify(bounds, region))
                .DefaultIfEmpty(PdfRedactionCoverage.None)
                .Max();
            if (coverage == PdfRedactionCoverage.None)
            {
                continue;
            }

            if (coverage == PdfRedactionCoverage.Contained)
            {
                image.IsDeleted = true;
                changed++;
                continue;
            }

            if (!PdfImageRedactionEngine.TryCreateRedactedStream(
                    graph, page.Resources, image, regions, out var redactedResult, out var reason))
            {
                throw new InvalidDataException(
                    $"A preflight-approved image could not be redacted safely during apply. {reason}");
            }

            if (string.IsNullOrEmpty(image.ResourceName))
            {
                image.ReplacementStream = PdfImageRedactionEngine.MakeInlineSafe(redactedResult.Image);
            }
            else
            {
                var redacted = redactedResult.Image;
                foreach (var mask in redactedResult.Masks)
                {
                    var maskId = new PdfObjectId(nextObjectNumber++, 0);
                    redacted.Dictionary[mask.DictionaryKey] = new PdfReference(maskId);
                    updates.Add(new PdfIncrementalObjectUpdate(maskId, mask.Stream));
                }

                resourceOverride ??= Clone(page.Resources);
                var xObjects = Resolve(graph, resourceOverride["XObject"]) is PdfDictionary existing
                    ? Clone(existing)
                    : new PdfDictionary();
                var name = NextResourceName(xObjects, image.ResourceName);
                var id = new PdfObjectId(nextObjectNumber++, 0);
                xObjects[name] = new PdfReference(id);
                resourceOverride["XObject"] = xObjects;
                image.ResourceName = name;
                updates.Add(new PdfIncrementalObjectUpdate(id, redacted));
            }

            changed++;
        }

        return changed;
    }

    private static string NextResourceName(PdfDictionary resources, string original)
    {
        for (var index = 1; ; index++)
        {
            var candidate = $"{original}_PXARedacted{index}";
            if (resources[candidate] is null)
            {
                return candidate;
            }
        }
    }

    private static HashSet<string> CollectImageResourceNames(
        IEnumerable<PdfGraphicsElement> elements,
        bool includeDeleted)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in elements)
        {
            if (element is PdfGroupElement group)
            {
                names.UnionWith(CollectImageResourceNames(group.Children, includeDeleted));
            }
            else if (element is PdfImageElement image &&
                     (includeDeleted || !image.IsDeleted) &&
                     !string.IsNullOrEmpty(image.ResourceName))
            {
                names.Add(image.ResourceName);
            }
        }

        return names;
    }

    private static void PruneUnusedImageResources(
        PdfObjectGraph graph,
        PdfPageModel page,
        IReadOnlySet<string> originalNames,
        ref PdfDictionary? resourceOverride)
    {
        var activeNames = CollectImageResourceNames(page.GraphicsObjects, includeDeleted: false);
        var unusedNames = originalNames.Where(name => !activeNames.Contains(name)).ToArray();
        if (unusedNames.Length == 0)
        {
            return;
        }

        resourceOverride ??= Clone(page.Resources);
        if (Resolve(graph, resourceOverride["XObject"]) is not PdfDictionary existing)
        {
            return;
        }

        var xObjects = Clone(existing);
        foreach (var name in unusedNames)
        {
            xObjects[name] = null;
        }

        resourceOverride["XObject"] = xObjects;
    }

    private static HashSet<string> CollectPatternResourceNames(
        IEnumerable<PdfGraphicsElement> elements,
        bool includeDeleted)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in elements)
        {
            if (element is PdfGroupElement group)
            {
                names.UnionWith(CollectPatternResourceNames(group.Children, includeDeleted));
                continue;
            }

            if (!includeDeleted && element.IsDeleted)
            {
                continue;
            }

            if (UsesPatternFill(element) && !string.IsNullOrEmpty(element.FillPatternResourceName))
            {
                names.Add(element.FillPatternResourceName);
            }

            if (UsesPatternStroke(element) && !string.IsNullOrEmpty(element.StrokePatternResourceName))
            {
                names.Add(element.StrokePatternResourceName);
            }
        }

        return names;
    }

    private static bool UsesPatternFill(PdfGraphicsElement element) => element switch
    {
        PdfTextElement => true,
        PdfPathElement path => path.SourceCommand.Operator.Name is "f" or "F" or "f*" or "B" or "B*" or "b" or "b*",
        _ => false
    };

    private static bool UsesPatternStroke(PdfGraphicsElement element) => element switch
    {
        PdfTextElement => true,
        PdfPathElement path => path.SourceCommand.Operator.Name is "S" or "s" or "B" or "B*" or "b" or "b*",
        _ => false
    };

    private static void PruneUnusedPatternResources(
        PdfObjectGraph graph,
        PdfPageModel page,
        IReadOnlySet<string> originalNames,
        ref PdfDictionary? resourceOverride)
    {
        var activeNames = CollectPatternResourceNames(page.GraphicsObjects, includeDeleted: false);
        var unusedNames = originalNames.Where(name => !activeNames.Contains(name)).ToArray();
        if (unusedNames.Length == 0)
        {
            return;
        }

        resourceOverride ??= Clone(page.Resources);
        if (Resolve(graph, resourceOverride["Pattern"]) is not PdfDictionary existing)
        {
            return;
        }

        var patterns = Clone(existing);
        foreach (var name in unusedNames)
        {
            patterns[name] = null;
        }

        resourceOverride["Pattern"] = patterns;
    }

    private static List<PdfObjectId> ReadContentStreamIds(PdfDictionary pageDictionary) =>
        pageDictionary["Contents"] switch
        {
            PdfReference reference => [reference.Id],
            PdfArray array => array.Items.OfType<PdfReference>().Select(static item => item.Id).ToList(),
            _ => []
        };

    private static int RemoveOverlappingAnnotations(
        PdfObjectGraph graph,
        PdfPageModel page,
        IReadOnlyList<PdfRectangle> regions,
        List<PdfIncrementalObjectUpdate> updates)
    {
        if (page.OriginalReference is not { } pageId ||
            graph.Resolve(pageId)?.Value is not PdfDictionary pageDictionary ||
            Resolve(graph, pageDictionary["Annots"]) is not PdfArray annotations)
        {
            return 0;
        }

        var retained = new List<PdfObject>();
        var removed = 0;
        foreach (var item in annotations.Items)
        {
            if (Resolve(graph, item) is PdfDictionary annotation &&
                ReadRectangle(graph, annotation["Rect"]) is { } rectangle &&
                regions.Any(region => rectangle.Intersects(region)))
            {
                removed++;
                if (item is PdfReference reference)
                {
                    // Clear the object as well, so a removed field value cannot be recovered
                    // from an object that the annotation array no longer references.
                    UpsertUpdate(updates, new PdfIncrementalObjectUpdate(reference.Id, PdfNull.Value));
                }

                continue;
            }

            retained.Add(item);
        }

        if (removed == 0)
        {
            return 0;
        }

        var updated = CurrentDictionary(graph, pageId, updates, pageDictionary);
        updated["Annots"] = new PdfArray(retained);
        UpsertUpdate(updates, new PdfIncrementalObjectUpdate(pageId, updated));
        return removed;
    }

    private static void RemovePageThumbnails(
        PdfObjectGraph graph,
        PdfDocumentModel document,
        List<PdfIncrementalObjectUpdate> updates)
    {
        foreach (var page in document.Pages)
        {
            if (page.OriginalReference is not { } pageId ||
                graph.Resolve(pageId)?.Value is not PdfDictionary pageDictionary ||
                pageDictionary["Thumb"] is null)
            {
                continue;
            }

            var updated = CurrentDictionary(graph, pageId, updates, pageDictionary);
            updated["Thumb"] = null;
            UpsertUpdate(updates, new PdfIncrementalObjectUpdate(pageId, updated));
        }
    }

    private static IReadOnlyList<string> SanitizeCatalog(
        PdfObjectGraph graph,
        PdfRedactionSanitizationPolicy policy,
        ICollection<PdfIncrementalObjectUpdate> updates)
    {
        var applied = new List<string>();
        if (graph.Trailer?["Root"] is not PdfReference rootReference ||
            graph.Resolve(rootReference.Id)?.Value is not PdfDictionary catalog)
        {
            return applied;
        }

        var updated = Clone(catalog);
        var changed = false;

        if (policy.RemoveDocumentMetadata)
        {
            if (updated["Metadata"] is not null)
            {
                updated["Metadata"] = null;
                changed = true;
            }

            if (graph.Trailer["Info"] is PdfReference infoReference)
            {
                updates.Add(new PdfIncrementalObjectUpdate(infoReference.Id, new PdfDictionary()));
            }

            applied.Add("documentMetadata");
        }

        if (policy.RemoveEmbeddedFiles)
        {
            if (Resolve(graph, updated["Names"]) is PdfDictionary names && names["EmbeddedFiles"] is not null)
            {
                var updatedNames = Clone(names);
                updatedNames["EmbeddedFiles"] = null;
                updated["Names"] = updatedNames;
                changed = true;
            }

            foreach (var indirectObject in graph.Objects.Values)
            {
                if (indirectObject.Value is PdfDictionary dictionary &&
                    dictionary["Type"] is PdfName { Value: "Filespec" })
                {
                    updates.Add(new PdfIncrementalObjectUpdate(indirectObject.Id, PdfNull.Value));
                }
            }

            applied.Add("embeddedFiles");
        }

        if (policy.RemoveActionsAndJavaScript)
        {
            foreach (var key in new[] { "OpenAction", "AA" })
            {
                if (updated[key] is not null)
                {
                    updated[key] = null;
                    changed = true;
                }
            }

            if (Resolve(graph, updated["Names"]) is PdfDictionary actionNames && actionNames["JavaScript"] is not null)
            {
                var updatedNames = Clone(actionNames);
                updatedNames["JavaScript"] = null;
                updated["Names"] = updatedNames;
                changed = true;
            }

            applied.Add("actionsAndJavaScript");
        }

        if (policy.RemoveOverlappingAnnotations)
        {
            applied.Add("overlappingAnnotations");
        }

        if (policy.RemovePageThumbnails)
        {
            applied.Add("pageThumbnails");
        }

        if (changed)
        {
            updates.Add(new PdfIncrementalObjectUpdate(rootReference.Id, updated));
        }

        return applied;
    }

    private PdfRedactionVerificationReport Verify(
        byte[] result,
        IReadOnlyDictionary<int, PdfRectangle[]> regionsByPage,
        IReadOnlyList<string> removedText,
        CancellationToken cancellationToken)
    {
        var findings = new List<PdfRedactionFinding>();
        var context = new PdfParseContext(result, new PdfImporterOptions(), cancellationToken);
        var table = new PdfCrossReferenceParser().Parse(context);
        var graph = new PdfObjectParser(context, table).ParseDocumentGraph();
        var document = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter()).Build(graph);

        var residualElements = 0;
        foreach (var (pageNumber, regions) in regionsByPage)
        {
            var page = document.Pages.ElementAtOrDefault(pageNumber - 1);
            if (page is null)
            {
                continue;
            }

            foreach (var element in page.GraphicsObjects)
            {
                residualElements += CountCovered(graph, page.Resources, element, regions, depth: 0, []);
            }
        }

        if (residualElements > 0)
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT301",
                PdfRedactionCapability.Unsupported,
                $"{residualElements} element(s) still intersect a redaction region after apply."));
        }

        // Match extracted text only inside the redacted regions. The same text may legitimately
        // occur elsewhere in a document and must not turn a successful redaction into a false
        // failure merely because its string value is equal.
        var removedSet = removedText
            .Where(static text => text.Trim().Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        var residualMatches = 0;
        foreach (var (pageNumber, regions) in regionsByPage)
        {
            var page = document.Pages.ElementAtOrDefault(pageNumber - 1);
            if (page is null)
            {
                continue;
            }

            residualMatches += page.GraphicsObjects.Sum(element =>
                CountCoveredTextMatches(element, regions, removedSet, depth: 0, []));
        }
        if (residualMatches > 0)
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT302",
                PdfRedactionCapability.Unsupported,
                $"{residualMatches} removed text run(s) are still extractable from the result."));
        }

        PdfRedactionResidueScanner.Scan(graph, document, regionsByPage, findings);

        if (table.Revisions.Count != 1)
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT303",
                PdfRedactionCapability.Unsupported,
                $"The result contains {table.Revisions.Count} revisions; earlier revisions may retain removed content."));
        }

        return new PdfRedactionVerificationReport(
            findings.Count == 0,
            residualElements,
            residualMatches,
            findings);
    }

    private int CountCovered(
        PdfObjectGraph graph,
        PdfDictionary resources,
        PdfGraphicsElement element,
        IReadOnlyList<PdfRectangle> regions,
        int depth,
        IReadOnlyList<PdfGroupElement> ancestors)
    {
        if (depth > 64 || element.IsDeleted)
        {
            return 0;
        }

        if (element is PdfGroupElement group)
        {
            var nested = ancestors.Append(group).ToArray();
            return group.Children.Sum(child => CountCovered(graph, resources, child, regions, depth + 1, nested));
        }

        if (element is PdfImageElement image &&
            PdfImageRedactionEngine.IsSecurelyRedacted(graph, resources, image, regions, out _))
        {
            return 0;
        }

        var bounds = PdfRedactionVisibility.EffectiveBounds(element, _bounds, ancestors, out _);
        return regions.Any(region => PdfRedactionGeometry.Classify(bounds, region) != PdfRedactionCoverage.None) ? 1 : 0;
    }

    private int CountCoveredTextMatches(
        PdfGraphicsElement element,
        IReadOnlyList<PdfRectangle> regions,
        IReadOnlySet<string> removedText,
        int depth,
        IReadOnlyList<PdfGroupElement> ancestors)
    {
        if (depth > 64 || element.IsDeleted)
        {
            return 0;
        }

        if (element is PdfGroupElement group)
        {
            var nested = ancestors.Append(group).ToArray();
            return group.Children.Sum(child =>
                CountCoveredTextMatches(child, regions, removedText, depth + 1, nested));
        }

        if (element is not PdfTextElement text || !removedText.Contains(text.Text))
        {
            return 0;
        }

        var bounds = PdfRedactionVisibility.EffectiveBounds(text, _bounds, ancestors, out _);
        return regions.Any(region => PdfRedactionGeometry.Classify(bounds, region) != PdfRedactionCoverage.None) ? 1 : 0;
    }

    private static async Task<PdfRedactionAuditRecord> CreateAuditRecordAsync(
        byte[] source,
        byte[] result,
        IReadOnlyList<PdfRedactionRegion> regions,
        IReadOnlyList<string> appliedSanitizations,
        PdfRedactionVerificationReport verification,
        PdfRedactionApplyOptions options,
        CancellationToken cancellationToken)
    {
        var sourceSha = Convert.ToHexStringLower(SHA256.HashData(source));
        var resultSha = Convert.ToHexStringLower(SHA256.HashData(result));
        var appliedAt = DateTimeOffset.UtcNow;
        var recordSha = PdfRedactionAuditSigner.ComputeRecordHash(
            sourceSha,
            resultSha,
            appliedAt,
            options.Actor,
            options.Reason,
            regions,
            appliedSanitizations,
            verification.Passed);

        return new PdfRedactionAuditRecord(
            sourceSha,
            resultSha,
            appliedAt,
            options.Actor,
            options.Reason,
            regions,
            appliedSanitizations,
            verification.Passed,
            recordSha,
            await PdfRedactionAuditSigner.SignAsync(
                recordSha,
                options.AuditSigningKey,
                options.AuditTimestampAuthority,
                cancellationToken).ConfigureAwait(false));
    }

    private static PdfDictionary Clone(PdfDictionary source) =>
        new(new Dictionary<string, PdfObject>(source.Values, StringComparer.Ordinal));

    private static PdfDictionary CurrentDictionary(
        PdfObjectGraph graph,
        PdfObjectId id,
        IReadOnlyList<PdfIncrementalObjectUpdate> updates,
        PdfDictionary fallback) =>
        Clone(updates.LastOrDefault(update => update.Id == id)?.Value as PdfDictionary ??
              graph.Resolve(id)?.Value as PdfDictionary ??
              fallback);

    private static void UpsertUpdate(
        List<PdfIncrementalObjectUpdate> updates,
        PdfIncrementalObjectUpdate update)
    {
        updates.RemoveAll(candidate => candidate.Id == update.Id);
        updates.Add(update);
    }

    private static PdfRectangle? ReadRectangle(PdfObjectGraph graph, PdfObject? value)
    {
        if (Resolve(graph, value) is not PdfArray array || array.Items.Count < 4)
        {
            return null;
        }

        var numbers = new double[4];
        for (var index = 0; index < 4; index++)
        {
            switch (Resolve(graph, array.Items[index]))
            {
                case PdfInteger integer:
                    numbers[index] = integer.Value;
                    break;
                case PdfNumber number:
                    numbers[index] = number.Value;
                    break;
                default:
                    return null;
            }
        }

        return new PdfRectangle(
            Math.Min(numbers[0], numbers[2]),
            Math.Min(numbers[1], numbers[3]),
            Math.Abs(numbers[2] - numbers[0]),
            Math.Abs(numbers[3] - numbers[1]));
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

    private static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }
}
