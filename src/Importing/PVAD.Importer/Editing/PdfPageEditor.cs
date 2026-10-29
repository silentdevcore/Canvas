using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Saving;
using PVAD.Importer.Security;
using PVAD.Importer.Trust;
using PVAD.Importer.Xref;

namespace PVAD.Importer.Editing;

/// <summary>Base contract for deterministic, one-based existing-PDF page operations.</summary>
public abstract record PdfPageEditOperation;

/// <summary>Removes the page at <paramref name="PageNumber"/>.</summary>
public sealed record PdfDeletePageOperation(int PageNumber) : PdfPageEditOperation;

/// <summary>Moves a page to its final one-based position.</summary>
public sealed record PdfMovePageOperation(int PageNumber, int TargetPageNumber) : PdfPageEditOperation;

/// <summary>Supported page boundary boxes for existing-PDF editing.</summary>
public enum PdfPageBoxKind
{
    CropBox
}

/// <summary>Sets a page boundary using Nutrient coordinates: left, upper Y, right, lower Y.</summary>
public sealed record PdfSetPageBoxOperation(
    int PageNumber,
    PdfPageBoxKind PageBox,
    double Left,
    double Top,
    double Right,
    double Bottom) : PdfPageEditOperation;

/// <summary>Removes a page boundary so the PDF default/inherited value applies.</summary>
public sealed record PdfRemovePageBoxOperation(int PageNumber, PdfPageBoxKind PageBox) : PdfPageEditOperation;

public sealed record PdfPageEditOptions
{
    public bool AllowSignatureInvalidation { get; init; }
    public string? SourcePassword { get; init; }
    public PdfDocumentEncryptionOptions? Encryption { get; init; }
    public PdfParseLimits Limits { get; init; } = new();

    public override string ToString() =>
        $"{nameof(PdfPageEditOptions)} {{ AllowSignatureInvalidation = {AllowSignatureInvalidation}, " +
        $"SourcePassword = [REDACTED], Encryption = {(Encryption is null ? "null" : "[REDACTED]")} }}";
}

public sealed record PdfPageEditResult(
    byte[] Bytes,
    int OriginalPageCount,
    int PageCount,
    IReadOnlyList<PdfPageEditOperation> AppliedOperations,
    IReadOnlyList<PdfObjectId> InvalidatedSignatures);

public sealed record PdfPageBoxCoordinates(double Left, double Top, double Right, double Bottom);

/// <summary>
/// Reorders or removes pages by updating the existing PDF page tree. Page dictionaries,
/// content streams, resources, annotations, and unknown reachable objects are retained rather
/// than reconstructed through the Designer model.
/// </summary>
public sealed class PdfPageEditor
{
    private static readonly string[] InheritablePageKeys =
        ["Resources", "MediaBox", "CropBox", "Rotate", "BleedBox", "TrimBox", "ArtBox"];

    public PdfPageEditResult Apply(
        ReadOnlyMemory<byte> source,
        IReadOnlyList<PdfPageEditOperation> operations,
        PdfPageEditOptions? options = null)
    {
        if (source.IsEmpty)
            throw new ArgumentException("A non-empty PDF is required.", nameof(source));
        ArgumentNullException.ThrowIfNull(operations);
        if (operations.Count == 0)
            throw new ArgumentException("At least one page operation is required.", nameof(operations));
        options ??= new PdfPageEditOptions();

        if (operations.Any(static operation => operation is PdfSetPageBoxOperation or PdfRemovePageBoxOperation))
        {
            var boxContext = new PdfParseContext(source, new PdfImporterOptions { Limits = options.Limits });
            var boxTable = new PdfCrossReferenceParser().Parse(boxContext);
            if (boxTable.Diagnostics.Count > 0)
                throw new InvalidDataException($"The source PDF cross-reference data is invalid: {boxTable.Diagnostics[0].Code}.");
            if (PdfDocumentDecryptor.IsEncrypted(boxTable))
            {
                throw new NotSupportedException(
                    "Nutrient page-box editing is defined for non-encrypted PDFs; decrypt and re-save through an explicit trusted workflow first.");
            }
        }

        var context = new PdfParseContext(source, new PdfImporterOptions { Limits = options.Limits });
        var xref = new PdfCrossReferenceParser().Parse(context);
        if (xref.Diagnostics.Count > 0)
            throw new InvalidDataException($"The source PDF cross-reference data is invalid: {xref.Diagnostics[0].Code}.");

        var encrypted = PdfDocumentDecryptor.IsEncrypted(xref);
        PdfObjectGraph graph;
        if (encrypted)
        {
            if (options.SourcePassword is null)
                throw new PdfPasswordException("A source password is required to edit an encrypted PDF.");
            if (options.Encryption is null)
                throw new InvalidOperationException("An encrypted source must be re-encrypted after page editing.");

            var decrypted = PdfDocumentDecryptor.ParseAndDecrypt(context, xref, options.SourcePassword);
            if (decrypted.Result.PasswordKind != PdfPasswordKind.Owner)
                throw new UnauthorizedAccessException("Page editing requires the PDF owner password.");
            graph = decrypted.Graph;
        }
        else
        {
            graph = new PdfObjectParser(context, xref).ParseDocumentGraph();
        }

        if (context.Diagnostics.Count > 0)
            throw new InvalidDataException($"The source PDF could not be parsed in full: {context.Diagnostics[0].Code}.");

        var pagesRoot = ResolvePagesRoot(graph);
        var pages = new List<PageEntry>();
        WalkPages(graph, pagesRoot.Reference, pagesRoot.Dictionary, new Dictionary<string, PdfObject>(StringComparer.Ordinal), pages, []);
        if (pages.Count == 0)
            throw new InvalidDataException("The source PDF does not contain a page.");

        var originalCount = pages.Count;
        var pageBoxMutations = new Dictionary<PdfObjectId, PageBoxMutation>();
        var originalPageIds = pages.Select(static page => page.Reference.Id).ToHashSet();
        foreach (var operation in operations)
        {
            switch (operation)
            {
                case PdfDeletePageOperation delete:
                    ValidatePageNumber(delete.PageNumber, pages.Count, nameof(delete.PageNumber));
                    if (pages.Count == 1)
                        throw new InvalidOperationException("A PDF must retain at least one page.");
                    pages.RemoveAt(delete.PageNumber - 1);
                    break;

                case PdfMovePageOperation move:
                    ValidatePageNumber(move.PageNumber, pages.Count, nameof(move.PageNumber));
                    ValidatePageNumber(move.TargetPageNumber, pages.Count, nameof(move.TargetPageNumber));
                    if (move.PageNumber == move.TargetPageNumber)
                        break;
                    var page = pages[move.PageNumber - 1];
                    pages.RemoveAt(move.PageNumber - 1);
                    pages.Insert(move.TargetPageNumber - 1, page);
                    break;

                case PdfSetPageBoxOperation setBox:
                    ValidatePageNumber(setBox.PageNumber, pages.Count, nameof(setBox.PageNumber));
                    ValidatePageBoxKind(setBox.PageBox);
                    ValidateCoordinates(setBox.Left, setBox.Top, setBox.Right, setBox.Bottom);
                    var setPage = pages[setBox.PageNumber - 1];
                    var mediaBox = ReadBox(graph, setPage.EffectiveAttributes, "MediaBox")
                        ?? throw new InvalidDataException("The selected page does not have a valid /MediaBox.");
                    var cropBox = new PdfBox(setBox.Left, setBox.Bottom, setBox.Right, setBox.Top);
                    if (!mediaBox.Contains(cropBox))
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(setBox),
                            "A CropBox must be contained within the effective MediaBox.");
                    }
                    pageBoxMutations[setPage.Reference.Id] = PageBoxMutation.Set(cropBox);
                    break;

                case PdfRemovePageBoxOperation removeBox:
                    ValidatePageNumber(removeBox.PageNumber, pages.Count, nameof(removeBox.PageNumber));
                    ValidatePageBoxKind(removeBox.PageBox);
                    pageBoxMutations[pages[removeBox.PageNumber - 1].Reference.Id] = PageBoxMutation.Remove();
                    break;

                default:
                    throw new NotSupportedException($"Unsupported page operation '{operation.GetType().Name}'.");
            }
        }

        var updates = BuildUpdates(pagesRoot, pages, pageBoxMutations);
        var removedPageIds = originalPageIds
            .Except(pages.Select(static page => page.Reference.Id))
            .ToHashSet();
        var reachableRemovedPages = FindReachableRemovedPages(graph, updates, removedPageIds);
        if (reachableRemovedPages.Count > 0)
        {
            throw new NotSupportedException(
                "A page selected for deletion is still referenced by another reachable PDF object. " +
                "Remove or retarget its bookmarks, destinations, links, or annotations before deleting it.");
        }

        var save = new PdfDocumentSaver().Save(source, new PdfSaveRequest
        {
            Strategy = PdfSaveStrategy.FullRewrite,
            Updates = updates,
            FullRewriteOptions = new PdfFullRewriteOptions
            {
                AllowSignatureInvalidation = options.AllowSignatureInvalidation,
                SourcePassword = options.SourcePassword,
                Encryption = options.Encryption,
                Limits = options.Limits
            }
        });

        return new PdfPageEditResult(
            save.Bytes,
            originalCount,
            pages.Count,
            operations.ToArray(),
            save.InvalidatedSignatures);
    }

    public PdfPageBoxCoordinates ReadPageBox(
        ReadOnlyMemory<byte> source,
        int pageNumber,
        PdfPageBoxKind pageBox,
        PdfParseLimits? limits = null)
    {
        if (source.IsEmpty)
            throw new ArgumentException("A non-empty PDF is required.", nameof(source));
        ValidatePageBoxKind(pageBox);
        limits ??= new PdfParseLimits();
        var context = new PdfParseContext(source, new PdfImporterOptions { Limits = limits });
        var table = new PdfCrossReferenceParser().Parse(context);
        if (table.Diagnostics.Count > 0)
            throw new InvalidDataException($"The source PDF cross-reference data is invalid: {table.Diagnostics[0].Code}.");
        if (PdfDocumentDecryptor.IsEncrypted(table))
            throw new NotSupportedException("Page-box inspection does not accept encrypted PDFs.");
        var graph = new PdfObjectParser(context, table).ParseDocumentGraph();
        var root = ResolvePagesRoot(graph);
        var pages = new List<PageEntry>();
        WalkPages(graph, root.Reference, root.Dictionary,
            new Dictionary<string, PdfObject>(StringComparer.Ordinal), pages, []);
        ValidatePageNumber(pageNumber, pages.Count, nameof(pageNumber));
        var page = pages[pageNumber - 1];
        var box = ReadBox(graph, page.EffectiveAttributes, "CropBox")
            ?? ReadBox(graph, page.EffectiveAttributes, "MediaBox")
            ?? throw new InvalidDataException("The selected page does not have a valid page boundary.");
        return new PdfPageBoxCoordinates(box.Left, box.Top, box.Right, box.Bottom);
    }

    private static PagesRoot ResolvePagesRoot(PdfObjectGraph graph)
    {
        if (graph.Trailer?["Root"] is not PdfReference catalogReference ||
            graph.Resolve(catalogReference.Id)?.Value is not PdfDictionary catalog ||
            catalog["Pages"] is not PdfReference pagesReference ||
            graph.Resolve(pagesReference.Id)?.Value is not PdfDictionary pages)
        {
            throw new InvalidDataException("The PDF catalog does not contain a valid indirect page-tree root.");
        }

        return new PagesRoot(pagesReference, pages);
    }

    private static void WalkPages(
        PdfObjectGraph graph,
        PdfReference currentReference,
        PdfDictionary current,
        IReadOnlyDictionary<string, PdfObject> inherited,
        ICollection<PageEntry> pages,
        HashSet<PdfObjectId> visited)
    {
        if (!visited.Add(currentReference.Id))
            throw new InvalidDataException("The PDF page tree contains a cycle.");
        if (visited.Count > 100_000)
            throw new InvalidDataException("The PDF page tree exceeds the supported node limit.");

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

        if (current["Kids"] is not PdfArray kids)
            throw new InvalidDataException("A PDF page-tree node does not contain a /Kids array.");

        foreach (var kid in kids.Items)
        {
            if (kid is not PdfReference kidReference ||
                graph.Resolve(kidReference.Id)?.Value is not PdfDictionary kidDictionary)
            {
                throw new InvalidDataException("A PDF page-tree child is not a valid indirect dictionary.");
            }

            WalkPages(graph, kidReference, kidDictionary, effective, pages, visited);
        }
    }

    private static IReadOnlyList<PdfIncrementalObjectUpdate> BuildUpdates(
        PagesRoot root,
        IReadOnlyList<PageEntry> pages,
        IReadOnlyDictionary<PdfObjectId, PageBoxMutation> pageBoxMutations)
    {
        var updates = new List<PdfIncrementalObjectUpdate>(pages.Count + 1);
        var rootDictionary = Clone(root.Dictionary);
        rootDictionary["Type"] = new PdfName("Pages");
        rootDictionary["Kids"] = new PdfArray(pages.Select(static page => (PdfObject)page.Reference));
        rootDictionary["Count"] = new PdfInteger(pages.Count);
        foreach (var key in InheritablePageKeys)
            rootDictionary[key] = null;
        updates.Add(new PdfIncrementalObjectUpdate(root.Reference.Id, rootDictionary));

        foreach (var page in pages)
        {
            var dictionary = Clone(page.Dictionary);
            dictionary["Type"] = new PdfName("Page");
            dictionary["Parent"] = root.Reference;
            pageBoxMutations.TryGetValue(page.Reference.Id, out var pageBoxMutation);
            if (pageBoxMutation?.Value is { } box)
                dictionary["CropBox"] = box.ToPdfArray();
            else if (pageBoxMutation?.IsRemoved == true)
                dictionary["CropBox"] = null;
            foreach (var key in InheritablePageKeys)
            {
                if (key == "CropBox" && pageBoxMutation is not null)
                    continue;
                if (dictionary[key] is null && page.EffectiveAttributes.TryGetValue(key, out var inherited))
                    dictionary[key] = inherited;
            }
            updates.Add(new PdfIncrementalObjectUpdate(page.Reference.Id, dictionary));
        }

        return updates;
    }

    private static PdfDictionary Clone(PdfDictionary source) =>
        new(new Dictionary<string, PdfObject>(source.Values, StringComparer.Ordinal));

    private static IReadOnlySet<PdfObjectId> FindReachableRemovedPages(
        PdfObjectGraph graph,
        IReadOnlyList<PdfIncrementalObjectUpdate> updates,
        IReadOnlySet<PdfObjectId> removedPageIds)
    {
        if (removedPageIds.Count == 0 || graph.Trailer is null)
            return new HashSet<PdfObjectId>();

        var replacements = updates.ToDictionary(static update => update.Id, static update => update.Value);
        var visited = new HashSet<PdfObjectId>();
        var found = new HashSet<PdfObjectId>();
        Visit(graph.Trailer, depth: 0);
        return found;

        void Visit(PdfObject value, int depth)
        {
            if (depth > 256)
                throw new InvalidDataException("The PDF object graph exceeds the supported reference depth.");

            switch (value)
            {
                case PdfReference reference when removedPageIds.Contains(reference.Id):
                    found.Add(reference.Id);
                    return;

                case PdfReference reference when visited.Add(reference.Id):
                    var resolved = replacements.TryGetValue(reference.Id, out var replacement)
                        ? replacement
                        : graph.Resolve(reference.Id)?.Value;
                    if (resolved is not null)
                        Visit(resolved, depth + 1);
                    return;

                case PdfArray array:
                    foreach (var item in array.Items)
                        Visit(item, depth + 1);
                    return;

                case PdfDictionary dictionary:
                    foreach (var item in dictionary.Values.Values)
                        Visit(item, depth + 1);
                    return;

                case PdfStreamObject stream:
                    Visit(stream.Dictionary, depth + 1);
                    return;
            }
        }
    }

    private static void ValidatePageNumber(int pageNumber, int count, string parameterName)
    {
        if (pageNumber < 1 || pageNumber > count)
            throw new ArgumentOutOfRangeException(parameterName, pageNumber, $"Page number must be between 1 and {count}.");
    }

    private static void ValidatePageBoxKind(PdfPageBoxKind pageBox)
    {
        if (pageBox != PdfPageBoxKind.CropBox)
            throw new NotSupportedException($"Page box '{pageBox}' is not supported by the existing-PDF crop operation.");
    }

    private static void ValidateCoordinates(double left, double top, double right, double bottom)
    {
        if (!double.IsFinite(left) || !double.IsFinite(top) ||
            !double.IsFinite(right) || !double.IsFinite(bottom) ||
            left >= right || bottom >= top)
        {
            throw new ArgumentOutOfRangeException(
                nameof(left),
                "Page box coordinates must be finite and define a positive rectangle.");
        }
    }

    private static PdfBox? ReadBox(
        PdfObjectGraph graph,
        IReadOnlyDictionary<string, PdfObject> attributes,
        string key)
    {
        if (!attributes.TryGetValue(key, out var value))
            return null;
        value = Resolve(graph, value);
        if (value is not PdfArray { Items.Count: 4 } array)
            return null;
        var numbers = array.Items.Select(item => Resolve(graph, item)).Select(ToNumber).ToArray();
        return numbers.Any(static number => number is null)
            ? null
            : new PdfBox(numbers[0]!.Value, numbers[1]!.Value, numbers[2]!.Value, numbers[3]!.Value);
    }

    private static PdfObject Resolve(PdfObjectGraph graph, PdfObject value) =>
        value is PdfReference reference
            ? graph.Resolve(reference.Id)?.Value
                ?? throw new InvalidDataException($"PDF page boundary references missing object {reference.Id.Number}.")
            : value;

    private static double? ToNumber(PdfObject value) => value switch
    {
        PdfInteger integer => integer.Value,
        PdfNumber number => number.Value,
        _ => null
    };

    private sealed record PagesRoot(PdfReference Reference, PdfDictionary Dictionary);
    private sealed record PageEntry(
        PdfReference Reference,
        PdfDictionary Dictionary,
        IReadOnlyDictionary<string, PdfObject> EffectiveAttributes);

    private sealed record PageBoxMutation(PdfBox? Value, bool IsRemoved)
    {
        public static PageBoxMutation Set(PdfBox value) => new(value, false);
        public static PageBoxMutation Remove() => new(null, true);
    }

    private sealed record PdfBox(double Left, double Bottom, double Right, double Top)
    {
        public bool Contains(PdfBox other) =>
            other.Left >= Left && other.Bottom >= Bottom &&
            other.Right <= Right && other.Top <= Top;

        public PdfArray ToPdfArray() => new([
            new PdfNumber(Left),
            new PdfNumber(Bottom),
            new PdfNumber(Right),
            new PdfNumber(Top)
        ]);
    }
}
