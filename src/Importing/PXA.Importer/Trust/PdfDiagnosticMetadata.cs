using PXA.Importer.Objects;

namespace PXA.Importer.Trust;

internal static class PdfDiagnosticMetadata
{
    private const int MaximumTraversalDepth = 64;

    public static IReadOnlyList<PdfTrustDiagnostic> Enrich(
        IEnumerable<PdfTrustDiagnostic> diagnostics,
        PdfObjectGraph? graph)
    {
        var pageMap = BuildObjectPageMap(graph);
        return diagnostics.Select(diagnostic => diagnostic with
        {
            PageNumber = diagnostic.PageNumber ?? FindPage(
                pageMap,
                diagnostic.ObjectNumber,
                diagnostic.Generation),
            LocalizationKey = diagnostic.LocalizationKey ?? $"diagnostics.{diagnostic.Code}",
            LocalizationParameters = diagnostic.LocalizationParameters ?? BuildParameters(
                diagnostic.Code,
                diagnostic.Severity.ToString(),
                diagnostic.PageNumber ?? FindPage(pageMap, diagnostic.ObjectNumber, diagnostic.Generation),
                diagnostic.ObjectNumber,
                diagnostic.Generation,
                diagnostic.ElementId)
        }).ToArray();
    }

    public static IReadOnlyList<PdfComplianceConversionFinding> Enrich(
        IEnumerable<PdfComplianceConversionFinding> findings,
        PdfObjectGraph graph)
    {
        var pageMap = BuildObjectPageMap(graph);
        return findings.Select(finding => finding with
        {
            PageNumber = finding.PageNumber ?? FindPage(
                pageMap,
                finding.ObjectNumber,
                finding.Generation),
            LocalizationKey = finding.LocalizationKey ?? $"diagnostics.{finding.Code}",
            LocalizationParameters = finding.LocalizationParameters ?? BuildParameters(
                finding.Code,
                finding.Severity.ToString(),
                finding.PageNumber ?? FindPage(pageMap, finding.ObjectNumber, finding.Generation),
                finding.ObjectNumber,
                finding.Generation,
                finding.ElementId)
        }).ToArray();
    }

    private static IReadOnlyDictionary<string, string> BuildParameters(
        string code,
        string severity,
        int? pageNumber,
        int? objectNumber,
        int? generation,
        string? elementId)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["code"] = code,
            ["severity"] = severity
        };
        if (pageNumber is { } page) parameters["pageNumber"] = page.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (objectNumber is { } number) parameters["objectNumber"] = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (generation is { } objectGeneration) parameters["generation"] = objectGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(elementId)) parameters["elementId"] = elementId;
        return parameters;
    }

    private static int? FindPage(
        IReadOnlyDictionary<PdfObjectId, int?> pageMap,
        int? objectNumber,
        int? generation) =>
        objectNumber is { } number && pageMap.TryGetValue(new PdfObjectId(number, generation ?? 0), out var page)
            ? page
            : null;

    private static IReadOnlyDictionary<PdfObjectId, int?> BuildObjectPageMap(PdfObjectGraph? graph)
    {
        var result = new Dictionary<PdfObjectId, int?>();
        if (graph?.Trailer?["Root"] is not PdfReference rootReference ||
            graph.Resolve(rootReference.Id)?.Value is not PdfDictionary catalog)
        {
            return result;
        }

        var pages = new List<(PdfObjectId Id, PdfDictionary Dictionary)>();
        var pageTreeVisited = new HashSet<PdfObjectId>();

        void CollectPages(PdfObject? value, int depth)
        {
            if (depth > MaximumTraversalDepth) return;
            var id = value is PdfReference reference ? reference.Id : value?.OriginalId;
            if (id is { } objectId && !pageTreeVisited.Add(objectId)) return;
            if (Resolve(graph, value) is not PdfDictionary dictionary) return;
            if (ReadName(graph, dictionary["Type"]) == "Page")
            {
                if (id is { } pageId) pages.Add((pageId, dictionary));
                return;
            }
            if (Resolve(graph, dictionary["Kids"]) is not PdfArray kids) return;
            foreach (var kid in kids.Items) CollectPages(kid, depth + 1);
        }

        CollectPages(catalog["Pages"], 0);
        var pageNumbers = pages
            .Select((page, index) => (page.Id, PageNumber: index + 1))
            .ToDictionary(item => item.Id, item => item.PageNumber);

        for (var index = 0; index < pages.Count; index++)
        {
            var pageNumber = index + 1;
            var visited = new HashSet<PdfObjectId>();
            Map(pages[index].Dictionary, pages[index].Id, pageNumber, 0, visited);
        }

        return result;

        void Map(
            PdfObject? value,
            PdfObjectId? knownId,
            int pageNumber,
            int depth,
            HashSet<PdfObjectId> visited)
        {
            if (value is null || depth > MaximumTraversalDepth) return;
            var id = knownId ?? (value is PdfReference reference ? reference.Id : value.OriginalId);
            if (id is { } objectId)
            {
                if (pageNumbers.TryGetValue(objectId, out var referencedPage) && referencedPage != pageNumber) return;
                if (!visited.Add(objectId)) return;
                if (result.TryGetValue(objectId, out var existing) && existing != pageNumber)
                    result[objectId] = null;
                else if (!result.ContainsKey(objectId))
                    result[objectId] = pageNumber;
            }

            var resolved = Resolve(graph, value);
            switch (resolved)
            {
                case PdfStreamObject stream:
                    foreach (var entry in stream.Dictionary.Values)
                    {
                        if (entry.Key is "Parent" or "P") continue;
                        Map(entry.Value, null, pageNumber, depth + 1, visited);
                    }
                    break;
                case PdfArray array:
                    foreach (var item in array.Items) Map(item, null, pageNumber, depth + 1, visited);
                    break;
                case PdfDictionary dictionary:
                    foreach (var entry in dictionary.Values)
                    {
                        if (entry.Key is "Parent" or "P") continue;
                        Map(entry.Value, null, pageNumber, depth + 1, visited);
                    }
                    break;
            }
        }
    }

    private static PdfObject? Resolve(PdfObjectGraph graph, PdfObject? value) =>
        value is PdfReference reference ? graph.Resolve(reference.Id)?.Value : value;

    private static string? ReadName(PdfObjectGraph graph, PdfObject? value) =>
        Resolve(graph, value) is PdfName name ? name.Value : null;
}
