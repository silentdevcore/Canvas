namespace Canvas.Pdf;

/// <summary>
/// Resolves BCP-47 language tags to Noto font files and caches the loaded fonts.
/// Font files must be placed in the configured directory (default: "fonts/" next to the assembly).
/// Returns false gracefully when a font file is not present — the caller falls back to Type1.
/// </summary>
public sealed class PdfFontLoader
{
    private static readonly HashSet<string> RtlLanguages =
        new(StringComparer.OrdinalIgnoreCase) { "ar", "he", "fa", "ur", "yi", "dv" };

    // BCP-47 prefix -> font filename relative to fonts directory
    private static readonly (string Prefix, string FileName)[] LanguageFontMap =
    [
        ("ar",   "NotoSansArabic-Regular.ttf"),
        ("ur",   "NotoSansArabic-Regular.ttf"),
        ("fa",   "NotoSansArabic-Regular.ttf"),
        ("he",   "NotoSansHebrew-Regular.ttf"),
        ("yi",   "NotoSansHebrew-Regular.ttf"),
        ("zh",   "NotoSansSC-Regular.otf"),
        ("ja",   "NotoSansJP-Regular.otf"),
        ("ko",   "NotoSansKR-Regular.otf"),
        ("hi",   "NotoSansDevanagari-Regular.ttf"),
        ("mr",   "NotoSansDevanagari-Regular.ttf"),
        ("ne",   "NotoSansDevanagari-Regular.ttf"),
        ("th",   "NotoSansThai-Regular.ttf"),
    ];

    private const string FallbackFontFile = "NotoSans-Regular.ttf";

    private readonly string _fontsDirectory;
    private readonly Dictionary<string, PdfEmbeddedFont?> _cache = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    public PdfFontLoader(string fontsDirectory)
    {
        _fontsDirectory = fontsDirectory;
    }

    /// <summary>
    /// Tries to load a font for the given BCP-47 language tag.
    /// Returns false without throwing when the font file is not found.
    /// </summary>
    public bool TryLoad(string? language, out PdfEmbeddedFont? font)
    {
        font = null;
        if (string.IsNullOrWhiteSpace(language)) return false;

        var fileName = ResolveFileName(language);
        var cacheKey = fileName;

        lock (_lock)
        {
            if (_cache.TryGetValue(cacheKey, out font))
                return font is not null;
        }

        var filePath = Path.Combine(_fontsDirectory, fileName);
        var isRtl = IsRtl(language);
        bool loaded = PdfEmbeddedFont.TryLoad(filePath, isRtl, out font);

        lock (_lock)
        {
            _cache[cacheKey] = font;
        }

        return loaded;
    }

    public static bool IsRtl(string? language) =>
        language is not null && RtlLanguages.Contains(language.Split('-')[0]);

    private static string ResolveFileName(string language)
    {
        // Match on the primary language subtag (e.g. "zh" from "zh-CN")
        var primary = language.Split('-')[0];

        foreach (var (prefix, file) in LanguageFontMap)
        {
            if (primary.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                return file;
        }
        return FallbackFontFile;
    }
}
