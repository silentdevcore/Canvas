using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Canvas.WebApi.Services;

public sealed class PdfViewerAnnotationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly ConcurrentDictionary<string, StoredPdfViewerAnnotations> _items = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _storageRoot;

    public PdfViewerAnnotationStore(IConfiguration configuration, IWebHostEnvironment environment)
        : this(ResolveStorageRoot(configuration, environment))
    {
    }

    public PdfViewerAnnotationStore(string storageRoot)
    {
        _storageRoot = storageRoot;
        Directory.CreateDirectory(_storageRoot);
    }

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

        WriteToDisk(stored);
        _items[documentId] = stored;
        return stored;
    }

    public bool TryGet(string documentId, out StoredPdfViewerAnnotations annotations)
    {
        if (_items.TryGetValue(documentId, out annotations!))
            return true;

        var path = GetPath(documentId);
        if (!File.Exists(path))
            return false;

        try
        {
            var stored = JsonSerializer.Deserialize<StoredPdfViewerAnnotations>(File.ReadAllText(path), JsonOptions);
            if (stored is null)
                return false;

            annotations = stored;
            _items[stored.DocumentId] = stored;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public bool Delete(string documentId)
    {
        var removed = _items.TryRemove(documentId, out _);
        var path = GetPath(documentId);
        if (!File.Exists(path))
            return removed;

        File.Delete(path);
        return true;
    }

    private void WriteToDisk(StoredPdfViewerAnnotations stored)
    {
        var path = GetPath(stored.DocumentId);
        var tempPath = Path.Combine(_storageRoot, $"{Path.GetFileNameWithoutExtension(path)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(tempPath, JsonSerializer.Serialize(stored, JsonOptions));
        File.Move(tempPath, path, overwrite: true);
    }

    private string GetPath(string documentId) => Path.Combine(_storageRoot, $"{HashDocumentId(documentId)}.json");

    private static string ResolveStorageRoot(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["PdfViewer:AnnotationStoragePath"];
        if (string.IsNullOrWhiteSpace(configured))
            return Path.Combine(environment.ContentRootPath, "App_Data", "pdf-viewer-annotations");

        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(environment.ContentRootPath, configured);
    }

    private static string HashDocumentId(string documentId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(documentId.Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public sealed record StoredPdfViewerAnnotations(
    string DocumentId,
    int Version,
    string? SourceName,
    DateTimeOffset ExportedAt,
    DateTimeOffset SavedAt,
    JsonElement Annotations);
