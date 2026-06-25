using System.Collections.Concurrent;
using System.Text.Json;

namespace Canvas.WebApi.Services;

public sealed class PdfViewerAnnotationStore
{
    private readonly ConcurrentDictionary<string, StoredPdfViewerAnnotations> _items = new(StringComparer.OrdinalIgnoreCase);

    public StoredPdfViewerAnnotations Save(
        string documentId,
        int version,
        string? sourceName,
        DateTimeOffset exportedAt,
        JsonElement annotations)
    {
        var stored = new StoredPdfViewerAnnotations(
            documentId,
            version,
            sourceName,
            exportedAt,
            DateTimeOffset.UtcNow,
            annotations.Clone());

        _items[documentId] = stored;
        return stored;
    }

    public bool TryGet(string documentId, out StoredPdfViewerAnnotations annotations) =>
        _items.TryGetValue(documentId, out annotations!);

    public bool Delete(string documentId) => _items.TryRemove(documentId, out _);
}

public sealed record StoredPdfViewerAnnotations(
    string DocumentId,
    int Version,
    string? SourceName,
    DateTimeOffset ExportedAt,
    DateTimeOffset SavedAt,
    JsonElement Annotations);
