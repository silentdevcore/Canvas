namespace PVAD.Pdf;

/// <summary>
/// Resolves BCP-47 language tags and font styles to packaged font files and caches them.
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

    private const string LegacyFallbackFontFile = "NotoSans-Regular.ttf";

    private readonly string _fontsDirectory;
    private readonly Dictionary<string, PdfEmbeddedFont?> _cache = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    public PdfFontLoader(string fontsDirectory)
    {
        _fontsDirectory = fontsDirectory;
    }

    /// <summary>
    /// Tries to load a regular font for the given BCP-47 language tag.
    /// Returns false without throwing when the font file is not found.
    /// </summary>
    public bool TryLoad(string? language, out PdfEmbeddedFont? font) =>
        TryLoad(language, bold: false, italic: false, out font);

    /// <summary>
    /// Tries to load a packaged font matching the requested language and style.
    /// Script-specific Noto fonts take precedence; Latin text uses Liberation Sans.
    /// </summary>
    public bool TryLoad(string? language, bool bold, bool italic, out PdfEmbeddedFont? font)
        => TryLoad(language, bold, italic, PdfFontFamily.Helvetica, out font);

    /// <summary>Loads a matching family without silently substituting a different Latin face.</summary>
    public bool TryLoad(string? language, bool bold, bool italic, PdfFontFamily family, out PdfEmbeddedFont? font)
    {
        font = null;
        foreach (var fileName in ResolveFileNames(language, bold, italic, family))
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(fileName, out font))
                {
                    if (font is not null) return true;
                    continue;
                }
            }

            var filePath = Path.Combine(_fontsDirectory, fileName);
            var loaded = PdfEmbeddedFont.TryLoad(filePath, IsRtl(language), out font);

            lock (_lock)
            {
                _cache[fileName] = font;
            }

            if (loaded) return true;
        }
        return false;
    }

    public static bool IsRtl(string? language) =>
        language is not null && RtlLanguages.Contains(language.Split('-')[0]);

    private static IEnumerable<string> ResolveFileNames(string? language, bool bold, bool italic, PdfFontFamily family)
    {
        var primary = language?.Split('-')[0] ?? string.Empty;

        foreach (var (prefix, file) in LanguageFontMap)
        {
            if (primary.Equals(prefix, StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
                yield break;
            }
        }

        var prefixName = family switch { PdfFontFamily.Times => "LiberationSerif", PdfFontFamily.Courier => "LiberationMono", _ => "LiberationSans" };
        yield return prefixName + ((bold, italic) switch
        {
            (true, true) => "-BoldItalic.ttf",
            (true, false) => "-Bold.ttf",
            (false, true) => "-Italic.ttf",
            _ => "-Regular.ttf"
        });

        if (bold || italic) yield return prefixName + "-Regular.ttf";
        if (family == PdfFontFamily.Helvetica) yield return LegacyFallbackFontFile;
    }
}
