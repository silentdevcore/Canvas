using PXA.Core.Contracts;

namespace PXA.Infrastructure.Converters;

/// <summary>
/// Resolves the Google Fonts the editor loads so exporters can reference the same
/// typefaces. Without this, a standalone export falls back to a default font (often a
/// larger-looking serif), making web-font text render wrong/oversized.
/// </summary>
internal static class GoogleFontCss
{
    // The exact request the editor loads (mirrors ui-designer-v2/index.html), so emitted
    // specs use the correct weights per family and never 400.
    private const string AllFontsUrl =
        "https://fonts.googleapis.com/css2?family=Barlow+Condensed:wght@400;700&family=Barlow:wght@400;700&family=Cabin:wght@400;700&family=Cardo:wght@400;700&family=Comfortaa:wght@400;700&family=Cormorant+Garamond:ital,wght@0,400;0,700;1,400&family=Crimson+Text:ital,wght@0,400;0,600;1,400&family=DM+Mono:wght@400;500&family=DM+Sans:wght@400;500;700&family=DM+Serif+Display:ital@0;1&family=Dancing+Script:wght@400;700&family=EB+Garamond:ital,wght@0,400;0,700;1,400&family=Exo+2:wght@400;700&family=Figtree:wght@400;700&family=Fira+Code:wght@400;500&family=Fira+Sans:ital,wght@0,400;0,700;1,400&family=IBM+Plex+Mono:wght@400;500&family=IBM+Plex+Sans:wght@400;700&family=IBM+Plex+Serif:wght@400;700&family=Inter+Tight:wght@400;700&family=Inter:wght@400;500;700&family=Josefin+Sans:wght@400;700&family=Josefin+Slab:wght@400;700&family=Jost:wght@400;700&family=Karla:wght@400;700&family=Lato:wght@400;700&family=Lexend:wght@400;700&family=Libre+Baskerville:ital,wght@0,400;0,700;1,400&family=Libre+Franklin:wght@400;700&family=Lobster&family=Lora:ital,wght@0,400;0,700;1,400&family=Manrope:wght@400;700&family=Merriweather:ital,wght@0,400;0,700;1,400&family=Montserrat:wght@400;700&family=Mulish:wght@400;700&family=Noto+Mono&family=Noto+Sans:wght@400;700&family=Noto+Serif:wght@400;700&family=Nunito+Sans:wght@400;700&family=Nunito:wght@400;700&family=Open+Sans:wght@400;700&family=Oswald:wght@400;700&family=Outfit:wght@400;700&family=PT+Mono&family=PT+Sans:ital,wght@0,400;0,700;1,400&family=PT+Serif:ital,wght@0,400;0,700;1,400&family=Pacifico&family=Playfair+Display:ital,wght@0,400;0,700;1,400&family=Plus+Jakarta+Sans:wght@400;700&family=Poppins:wght@400;700&family=Raleway:wght@400;700&family=Red+Hat+Display:wght@400;700&family=Red+Hat+Text:wght@400;700&family=Righteous&family=Roboto+Condensed:wght@400;700&family=Roboto+Mono:wght@400;500&family=Roboto+Slab:wght@400;700&family=Roboto:wght@400;700&family=Sora:wght@400;700&family=Source+Code+Pro:wght@400;500&family=Source+Sans+3:wght@400;700&family=Source+Serif+4:ital,wght@0,400;0,700;1,400&family=Space+Grotesk:wght@400;700&family=Space+Mono:wght@400;700&family=Spectral:ital,wght@0,400;0,700;1,400&family=Titillium+Web:wght@400;700&family=Ubuntu:wght@400;700&family=Work+Sans:wght@400;700&display=swap";

    // Maps a font-family display name ("Playfair Display") to its css2 fragment
    // ("Playfair+Display:ital,wght@0,400;0,700;1,400").
    private static readonly Dictionary<string, string> Fragments = Parse();

    private static Dictionary<string, string> Parse()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var query = AllFontsUrl[(AllFontsUrl.IndexOf('?') + 1)..];
        foreach (var part in query.Split('&'))
        {
            if (!part.StartsWith("family=", StringComparison.Ordinal)) continue;
            var fragment = part["family=".Length..];
            var name = fragment.Split(':')[0].Replace('+', ' ');
            dict[name] = fragment;
        }
        return dict;
    }

    /// <summary>
    /// Returns a Google Fonts css2 stylesheet URL for the (non-system) font families used by
    /// the elements, or null when only system fonts (Arial, Helvetica, …) are in use.
    /// </summary>
    internal static string? BuildUrl(IEnumerable<ElementDto> elements)
    {
        var used = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var el in elements)
        {
            // Text/element font and the table cell font are stored under different keys.
            foreach (var key in (ReadOnlySpan<string>)["fontFamily", "cellFontFamily"])
            {
                var family = el.Style.GetStr(key, "");
                if (string.IsNullOrWhiteSpace(family)) continue;

                // A font-family value may be a stack ("Poppins, sans-serif"); use the first.
                var first = family.Split(',')[0].Trim().Trim('\'', '"');
                if (Fragments.TryGetValue(first, out var fragment))
                    used.Add(fragment);
            }
        }

        if (used.Count == 0) return null;

        return "https://fonts.googleapis.com/css2?"
            + string.Join("&", used.Select(f => "family=" + f))
            + "&display=swap";
    }
}
