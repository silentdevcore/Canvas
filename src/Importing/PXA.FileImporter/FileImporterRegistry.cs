namespace PXA.FileImporter;

/// <summary>
/// Factory and registry for built-in Power Dox Automation file importers.
/// </summary>
public static class FileImporterRegistry
{
    private static readonly IReadOnlyDictionary<string, Func<IFileImporter>> Factories =
        new Dictionary<string, Func<IFileImporter>>(StringComparer.OrdinalIgnoreCase)
        {
            [FileImporterKeys.Doc] = static () => new DocFileImporter(),
            [FileImporterKeys.Docx] = static () => new DocxFileImporter(),
            [FileImporterKeys.Image] = static () => new ImageFileImporter(),
            [FileImporterKeys.Odt] = static () => new OdtFileImporter(),
            [FileImporterKeys.Pdf] = static () => new PdfFileImporter(),
            [FileImporterKeys.Pptx] = static () => new PptxFileImporter(),
            [FileImporterKeys.Svg] = static () => new SvgFileImporter(),
        };

    /// <summary>
    /// Gets all registered importer keys.
    /// </summary>
    public static IReadOnlyCollection<string> Keys => Factories.Keys.ToArray();

    /// <summary>
    /// Creates an importer by key or supported extension.
    /// </summary>
    public static IFileImporter Create(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var normalizedKey = Normalize(key);
        return Factories.TryGetValue(normalizedKey, out var factory)
            ? factory()
            : throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown PXA file importer key.");
    }

    /// <summary>
    /// Tries to create an importer by key or supported extension.
    /// </summary>
    public static bool TryCreate(string key, out IFileImporter importer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var normalizedKey = Normalize(key);
        if (Factories.TryGetValue(normalizedKey, out var factory))
        {
            importer = factory();
            return true;
        }

        importer = null!;
        return false;
    }

    private static string Normalize(string key) => key.Trim().TrimStart('.').ToLowerInvariant() switch
    {
        "jpg" or "jpeg" or "png" or "gif" or "webp" or "bmp" or "tif" or "tiff" => FileImporterKeys.Image,
        var normalized => normalized,
    };
}
