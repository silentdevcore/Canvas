using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Saving;
using PXA.Importer.Security;
using PXA.Importer.Trust;
using PXA.Importer.Xref;

namespace PXA.Importer.Editing;

public sealed record PdfPageImport(ReadOnlyMemory<byte> Source, int PageNumber, int? TargetPageNumber = null);

public sealed record PdfPageCompositionOptions
{
    public bool AllowSignatureInvalidation { get; init; }
    public PdfParseLimits Limits { get; init; } = new();
}

public sealed record PdfPageCompositionResult(byte[] Bytes, int OriginalPageCount, int PageCount);

/// <summary>Clones complete page object graphs between unencrypted PDF documents.</summary>
public sealed class PdfPageComposer
{
    private static readonly string[] InheritablePageKeys =
        ["Resources", "MediaBox", "CropBox", "Rotate", "BleedBox", "TrimBox", "ArtBox"];

    public PdfPageCompositionResult Apply(
        ReadOnlyMemory<byte> target,
        IReadOnlyList<PdfPageImport> imports,
        PdfPageCompositionOptions? options = null)
    {
        if (target.IsEmpty)
            throw new ArgumentException("A non-empty target PDF is required.", nameof(target));
        ArgumentNullException.ThrowIfNull(imports);
        if (imports.Count == 0)
            throw new ArgumentException("At least one page import is required.", nameof(imports));
        options ??= new PdfPageCompositionOptions();

        var targetGraph = ParseUnencrypted(target, options.Limits, "target");
        var targetTree = ReadPageTree(targetGraph);
        var pages = targetTree.Pages.ToList();
        var originalCount = pages.Count;
        var updates = new List<PdfIncrementalObjectUpdate>();
        var nextObjectNumber = targetGraph.Objects.Keys.Max(static id => id.Number) + 1;

        foreach (var import in imports)
        {
            var sourceGraph = ParseUnencrypted(import.Source, options.Limits, "source");
            var sourceTree = ReadPageTree(sourceGraph);
            ValidatePageNumber(import.PageNumber, sourceTree.Pages.Count, nameof(import.PageNumber));
            var sourcePage = sourceTree.Pages[import.PageNumber - 1];
            var cloned = ClonePageGraph(sourceGraph, sourceTree, sourcePage, ref nextObjectNumber);
            updates.AddRange(cloned.Updates);

            var targetIndex = import.TargetPageNumber ?? pages.Count + 1;
            if (targetIndex < 1 || targetIndex > pages.Count + 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(import.TargetPageNumber), targetIndex,
                    $"Target page number must be between 1 and {pages.Count + 1}.");
            }

            pages.Insert(targetIndex - 1, cloned.Page);
        }

        updates.AddRange(BuildPageTreeUpdates(targetTree.RootReference, targetTree.RootDictionary, pages));
        var save = new PdfDocumentSaver().Save(target, new PdfSaveRequest
        {
            Strategy = PdfSaveStrategy.FullRewrite,
            Updates = updates,
            FullRewriteOptions = new PdfFullRewriteOptions
            {
                AllowSignatureInvalidation = options.AllowSignatureInvalidation,
                Limits = options.Limits
            }
        });

        return new PdfPageCompositionResult(save.Bytes, originalCount, pages.Count);
    }

    public PdfPageCompositionResult Merge(
        IReadOnlyList<ReadOnlyMemory<byte>> documents,
        PdfPageCompositionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(documents);
        if (documents.Count == 0 || documents.Any(static document => document.IsEmpty))
            throw new ArgumentException("At least one non-empty PDF is required.", nameof(documents));
        if (documents.Count == 1)
        {
            var count = GetPageCount(documents[0], options?.Limits);
            return new PdfPageCompositionResult(documents[0].ToArray(), count, count);
        }

        var imports = new List<PdfPageImport>();
        for (var documentIndex = 1; documentIndex < documents.Count; documentIndex++)
        {
            var count = GetPageCount(documents[documentIndex], options?.Limits);
            for (var pageNumber = 1; pageNumber <= count; pageNumber++)
                imports.Add(new PdfPageImport(documents[documentIndex], pageNumber));
        }

        return Apply(documents[0], imports, options);
    }

    public int GetPageCount(ReadOnlyMemory<byte> source, PdfParseLimits? limits = null) =>
        ReadPageTree(ParseUnencrypted(source, limits ?? new PdfParseLimits(), "source")).Pages.Count;

    private static PdfObjectGraph ParseUnencrypted(
        ReadOnlyMemory<byte> source,
        PdfParseLimits limits,
        string role)
    {
        if (source.IsEmpty)
            throw new ArgumentException($"The {role} PDF is empty.", nameof(source));
        var context = new PdfParseContext(source, new PdfImporterOptions { Limits = limits });
        var table = new PdfCrossReferenceParser().Parse(context);
        if (table.Diagnostics.Count > 0)
            throw new InvalidDataException($"The {role} PDF cross-reference data is invalid: {table.Diagnostics[0].Code}.");
        if (PdfDocumentDecryptor.IsEncrypted(table))
            throw new NotSupportedException("Cross-document page composition does not accept encrypted PDFs.");
        var graph = new PdfObjectParser(context, table).ParseDocumentGraph();
        if (context.Diagnostics.Count > 0)
            throw new InvalidDataException($"The {role} PDF could not be parsed in full: {context.Diagnostics[0].Code}.");
        return graph;
    }

    private static PageTree ReadPageTree(PdfObjectGraph graph)
    {
        if (graph.Trailer?["Root"] is not PdfReference catalogReference ||
            graph.Resolve(catalogReference.Id)?.Value is not PdfDictionary catalog ||
            catalog["Pages"] is not PdfReference rootReference ||
            graph.Resolve(rootReference.Id)?.Value is not PdfDictionary rootDictionary)
        {
            throw new InvalidDataException("The PDF catalog does not contain a valid indirect page-tree root.");
        }

        var pages = new List<PageEntry>();
        var pageTreeNodes = new HashSet<PdfObjectId>();
        WalkPages(graph, rootReference, rootDictionary,
            new Dictionary<string, PdfObject>(StringComparer.Ordinal), pages, pageTreeNodes, []);
        if (pages.Count == 0)
            throw new InvalidDataException("The PDF does not contain a page.");
        return new PageTree(rootReference, rootDictionary, pages, pageTreeNodes);
    }

    private static void WalkPages(
        PdfObjectGraph graph,
        PdfReference currentReference,
        PdfDictionary current,
        IReadOnlyDictionary<string, PdfObject> inherited,
        ICollection<PageEntry> pages,
        ISet<PdfObjectId> pageTreeNodes,
        HashSet<PdfObjectId> visited)
    {
        if (!visited.Add(currentReference.Id) || visited.Count > 100_000)
            throw new InvalidDataException("The PDF page tree is cyclic or exceeds the supported node limit.");

        var effective = new Dictionary<string, PdfObject>(inherited, StringComparer.Ordinal);
        foreach (var key in InheritablePageKeys)
        {
            if (current[key] is { } value)
                effective[key] = value;
        }

        if (current["Type"] is PdfName { Value: "Page" })
        {
            pages.Add(new PageEntry(currentReference, current, effective));
            return;
        }

        pageTreeNodes.Add(currentReference.Id);
        if (current["Kids"] is not PdfArray kids)
            throw new InvalidDataException("A PDF page-tree node does not contain a /Kids array.");
        foreach (var kid in kids.Items)
        {
            if (kid is not PdfReference childReference ||
                graph.Resolve(childReference.Id)?.Value is not PdfDictionary childDictionary)
                throw new InvalidDataException("A PDF page-tree child is not a valid indirect dictionary.");
            WalkPages(graph, childReference, childDictionary, effective, pages, pageTreeNodes, visited);
        }
    }

    private static ClonedPage ClonePageGraph(
        PdfObjectGraph sourceGraph,
        PageTree sourceTree,
        PageEntry sourcePage,
        ref int nextObjectNumber)
    {
        var objectNumber = nextObjectNumber;
        var mapping = new Dictionary<PdfObjectId, PdfObjectId>();
        var updates = new List<PdfIncrementalObjectUpdate>();
        var newPageId = new PdfObjectId(objectNumber++, 0);
        mapping[sourcePage.Reference.Id] = newPageId;

        PdfObject Clone(PdfObject value, int depth)
        {
            if (depth > 256)
                throw new InvalidDataException("The imported page object graph exceeds the supported depth.");
            switch (value)
            {
                case PdfReference reference:
                    if (sourceTree.PageTreeNodes.Contains(reference.Id))
                        throw new NotSupportedException("The imported page contains an unexpected reference to its source page tree.");
                    if (sourceTree.Pages.Any(page => page.Reference.Id == reference.Id) &&
                        reference.Id != sourcePage.Reference.Id)
                        throw new NotSupportedException("The imported page references another source page; clone the related pages together or remove the cross-page reference first.");
                    if (mapping.TryGetValue(reference.Id, out var mapped))
                        return new PdfReference(mapped);
                    var sourceObject = sourceGraph.Resolve(reference.Id)?.Value
                        ?? throw new InvalidDataException($"The imported page references missing object {reference.Id.Number} {reference.Id.Generation}.");
                    mapped = new PdfObjectId(objectNumber++, 0);
                    mapping[reference.Id] = mapped;
                    var clonedValue = Clone(sourceObject, depth + 1);
                    updates.Add(new PdfIncrementalObjectUpdate(mapped, clonedValue));
                    return new PdfReference(mapped);

                case PdfArray array:
                    return new PdfArray(array.Items.Select(item => Clone(item, depth + 1)));
                case PdfDictionary dictionary:
                    return new PdfDictionary(dictionary.Values.ToDictionary(
                        static entry => entry.Key,
                        entry => Clone(entry.Value, depth + 1),
                        StringComparer.Ordinal));
                case PdfStreamObject stream:
                    return new PdfStreamObject((PdfDictionary)Clone(stream.Dictionary, depth + 1), stream.EncodedBytes.ToArray());
                case PdfString text:
                    return new PdfString(text.Bytes.ToArray(), text.IsHex);
                default:
                    return value with { OriginalId = null, SourceSpan = default };
            }
        }

        var pageValues = sourcePage.Dictionary.Values
            .Where(static entry => entry.Key != "Parent")
            .ToDictionary(static entry => entry.Key, entry => Clone(entry.Value, 0), StringComparer.Ordinal);
        foreach (var key in InheritablePageKeys)
        {
            if (!pageValues.ContainsKey(key) && sourcePage.EffectiveAttributes.TryGetValue(key, out var inherited))
                pageValues[key] = Clone(inherited, 0);
        }
        var pageDictionary = new PdfDictionary(pageValues) { ["Type"] = new PdfName("Page") };
        updates.Add(new PdfIncrementalObjectUpdate(newPageId, pageDictionary));
        nextObjectNumber = objectNumber;
        return new ClonedPage(
            new PageEntry(new PdfReference(newPageId), pageDictionary,
                pageValues.Where(entry => InheritablePageKeys.Contains(entry.Key, StringComparer.Ordinal))
                    .ToDictionary(static entry => entry.Key, static entry => entry.Value, StringComparer.Ordinal)),
            updates);
    }

    private static IReadOnlyList<PdfIncrementalObjectUpdate> BuildPageTreeUpdates(
        PdfReference rootReference,
        PdfDictionary sourceRoot,
        IReadOnlyList<PageEntry> pages)
    {
        var updates = new List<PdfIncrementalObjectUpdate>(pages.Count + 1);
        var root = new PdfDictionary(sourceRoot.Values.ToDictionary(
            static entry => entry.Key, static entry => entry.Value, StringComparer.Ordinal));
        root["Type"] = new PdfName("Pages");
        root["Kids"] = new PdfArray(pages.Select(static page => (PdfObject)page.Reference));
        root["Count"] = new PdfInteger(pages.Count);
        foreach (var key in InheritablePageKeys)
            root[key] = null;
        updates.Add(new PdfIncrementalObjectUpdate(rootReference.Id, root));

        foreach (var page in pages)
        {
            var dictionary = new PdfDictionary(page.Dictionary.Values.ToDictionary(
                static entry => entry.Key, static entry => entry.Value, StringComparer.Ordinal));
            dictionary["Type"] = new PdfName("Page");
            dictionary["Parent"] = rootReference;
            foreach (var key in InheritablePageKeys)
            {
                if (dictionary[key] is null && page.EffectiveAttributes.TryGetValue(key, out var inherited))
                    dictionary[key] = inherited;
            }
            updates.Add(new PdfIncrementalObjectUpdate(page.Reference.Id, dictionary));
        }
        return updates;
    }

    private static void ValidatePageNumber(int pageNumber, int count, string parameterName)
    {
        if (pageNumber < 1 || pageNumber > count)
            throw new ArgumentOutOfRangeException(parameterName, pageNumber, $"Page number must be between 1 and {count}.");
    }

    private sealed record PageTree(
        PdfReference RootReference,
        PdfDictionary RootDictionary,
        IReadOnlyList<PageEntry> Pages,
        IReadOnlySet<PdfObjectId> PageTreeNodes);
    private sealed record PageEntry(
        PdfReference Reference,
        PdfDictionary Dictionary,
        IReadOnlyDictionary<string, PdfObject> EffectiveAttributes);
    private sealed record ClonedPage(PageEntry Page, IReadOnlyList<PdfIncrementalObjectUpdate> Updates);
}
