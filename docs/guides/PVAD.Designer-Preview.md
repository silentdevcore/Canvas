# Designer Canvas And Live Preview

Canvas and Live Preview use the same printable element renderer. Selection
handles, drag controls, bookmark markers and page-boundary markers belong to
the editor only; they are not additional PDF content.

## Check A Language Version

1. Configure the document languages and localized properties in Page Settings.
2. Select the language tab whose document you want to inspect.
3. Place a property token such as `{{TITLE}}` in a text block or form label.
4. Open Live Preview. It uses the same display-field substitutions, element
   components and language-specific geometry as the Canvas.
5. Export from Preview. Its PDF, print, image, JSON and format-modal operations
   receive the displayed target language. An explicit target language in the
   document settings takes precedence in Preview.

For global properties, the selected base language is tried first, followed by
the configured system language when the selected value is empty or missing.
Own-language properties are available only to their owner language. Unknown
tokens remain visible rather than silently becoming unrelated content.

Property keys are case-insensitive. A regional language such as `fr-FR` uses
the base `fr` property values. Language-specific geometry checks an exact tag
first and then the base language. Other-language elements are excluded from
Preview, as are hidden elements. Hidden elements may remain visible as editing
ghosts in the Canvas.

Display-field substitution does not translate stable element IDs, asset IDs,
form field names or binding paths. Table datasets retain their separate data
binding pipeline; this renderer does not add UI-only token expansion to cells.
Preview and PDF rendering do not rewrite the saved template or Code Workspace
canonical document when substituting a language value.

## Images And Tables

Images share clipping, fit behavior and focus coordinates between Canvas and
Preview. A focus coordinate of zero means the edge, not the default center.
The preserve-aspect-ratio option selects contained rendering. Missing or
unavailable images show a local placeholder; creating an empty image does not
contact an external placeholder website. Replacing a failed image can recover
without reopening the document.

The same geometry is used by normal and generated C# PDF export for embedded
PNG and JPEG images, including PNG alpha transparency:

| Fit | Result |
| --- | --- |
| `contain` (default) | Preserve the aspect ratio and show the entire image; unused space remains transparent. |
| `cover` | Preserve the aspect ratio and fill the box; clip the excess. |
| `fill` | Stretch to the box dimensions without preserving the aspect ratio. |
| `none` | Keep natural pixel dimensions at one point per pixel and clip only what exceeds the box. |

Focus percentages align the remaining space: `0` selects left/top, `50` centers,
and `100` selects right/bottom. The same rule positions a small image within a
larger box or selects the visible part of an oversized image. Values outside
0-100 are clamped; missing or non-finite values use 50. The legacy
`preserveAspectRatio: true` option takes precedence and selects `contain`, even
when another fit mode was saved. Language-specific image bounds use the same
effective page coordinates as other elements.

For example, an image with `fitMode: "cover"`, `focalX: 100`, `focalY: 0`, and
`preserveAspectRatio: false` fills its box while retaining the right/top edge.
Image data is embedded directly in the PDF without temporary image files.

Complete table cell matrices use the PDF-aligned renderer. Older incomplete
tables retain one shared compatibility renderer, including explicit false
font-style overrides and zero padding. Header/footer-only compatibility tables
do not insert an extra blank body row. Editing rows/columns through the
Inspector produces the normal canonical cell matrix.

Zoom scales the complete document content once, including compatibility-table
fonts, borders and padding. It does not apply separate, inconsistent scale
factors to individual cell properties.

## Dates, Timezones And Binding

1. Insert a Date element and select it.
2. In the Inspector, choose Static, Render or Binding mode.
3. For Render or Binding, set the format, locale and timezone below.
4. Open Preview and export the actual PDF to inspect the result.

Static mode preserves the entered content verbatim, including an intentionally
empty string. A missing mode is also static. Render mode captures the current
instant once for the document, not separately for every element or page.
Preview captures an instant when its document/language changes; it is not a
live ticking clock. An export performed later has its own render instant and
may legitimately show a later time.

New elements and missing settings use `yyyy-MM-dd`, `en-US` and `UTC`.
Existing explicitly saved formats, locales and timezones remain in the JSON.
The portable contract uses a Gregorian calendar and Latin digits. It supports
English, German, French, Spanish, Italian and Arabic language families. Regional
tags use the family format; `en-GB` additionally uses a day-first short date.
AM/PM spacing is defined by the shared contract rather than the host platform.

| Pattern | Example at 2026-11-13T12:04:05.120Z |
| --- | --- |
| `yyyy-MM-dd` | `2026-11-13` |
| `dd.MM.yyyy HH:mm` | `13.11.2026 12:04` in UTC |
| `yyyy-MM-dd HH:mm:ss.fff zzz` | `2026-11-13 13:04:05.120 +01:00` in Europe/Berlin |
| `D`, locale `fr-FR` | `vendredi 13 novembre 2026` |
| `O`, timezone UTC | `2026-11-13T12:04:05.120+00:00` |

Custom patterns support `y` through `yyyy`, `M` through `MMMM`, `d` through
`dddd`, `H`/`HH`, `h`/`hh`, `m`/`mm`, `s`/`ss`, `f`/`ff`/`fff`,
`F`/`FF`/`FFF`, `t`/`tt`, `z`/`zz`/`zzz` and `K`. Month and weekday names
follow the locale. `/` uses the family's date separator and `:` is the time
separator. Quote literal words, for example `yyyy-MM-dd 'at' HH:mm`, or escape
a character with a backslash. `%M` requests a single custom month token instead
of the standard month/day format. Literal `T` and `Z` may be unquoted inside
custom patterns; a literal `Z` alone does not convert the timezone.

Standard formats are `d`, `D`, `t`, `T`, `g`, `G`, `f`, `F`, `m`/`M`, `y`/`Y`,
`s`, `o`/`O`, `u`, `r`/`R` and `U`. The last three UTC forms (`u`, `r`/`R`,
`U`) convert to UTC; RFC output uses English. Portable `o`/`O` has millisecond
precision, not seven fractional digits. Patterns are limited to 256 characters.

Timezone values use IANA IDs, such as `Europe/Berlin`, `America/New_York` and
`Asia/Kathmandu`. UTC is independent of the browser and server host timezone.
Daylight-saving and fractional-hour offsets use the platform timezone database.
The shared aliases also recognize GMT, Etc/UTC and Windows identifiers
W. Europe Standard Time, Eastern Standard Time and Pacific Standard Time.
Other Windows identifiers fall back to UTC with a warning in the Inspector.

For Binding mode, use a plain property path such as `Invoice.Date`. Matching
custom properties and localized property keys are case-insensitive. For example,
a custom property named `Invoice.Date` with value `2026-11-13T12:00:00Z`, format
`dd.MM.yyyy HH:mm` and timezone `Europe/Berlin` produces `13.11.2026 13:00`.
Resolved ISO content from an existing binding pipeline is also accepted.

Bound values must be an ISO calendar date (`2026-11-13`) or a timestamp with
seconds and an explicit offset (`2026-11-13T12:00:00Z` or
`2026-11-13T12:00:00+02:00`). Up to seven input fractional digits are accepted;
formatted output is limited to milliseconds. Date-only values keep their
calendar day without timezone conversion. Ambiguous local dates, offset-free
timestamps and invalid calendar dates do not become the current date. They show
the saved fallback text, or an unresolved binding marker if no fallback exists.

The Inspector warns about invalid formats, unsupported locales, unknown
timezones and unresolved dates in all six Designer languages. Invalid formats
use ISO output; unsupported locales use English. Warnings are not printed in
the PDF. Arabic month/weekday output uses the packaged Arabic font; mixed
Arabic and numeric text is reordered into directional runs before shaping,
preserving digit order and logical PDF text. No source template is rewritten.

## Output And Limits

Live Preview is a document-layout view, not the PDF file itself. Inspect the
actual exported PDF for PDF security, signatures, viewer-specific form focus,
archival conformance and accessibility validation.

The shared renderer prevents Canvas/Preview implementation drift; it is not a
claim that every historical or invalid document is repaired automatically.
The cropping/focal parity pack covers all four fit modes, wide and tall PNGs,
small natural-size images, JPEGs and focus edges. It does not certify arbitrary
remote image URLs, EXIF autorotation, rounded image masks or all decorative
border/padding combinations. Use tenant-bound assets for production documents.
Separate source-crop editing fields are not part of this image-box fit contract.
The portable date contract is not a complete implementation of arbitrary .NET date-format
strings, calendars or every regional culture. Unsupported variants use the
documented fallback and warning rather than host-specific formatting.

Native serif/monospace raster output on different operating systems also
requires separate font and platform validation. The scoped, owner-approved
bold-text raster exceptions do not grant universal font parity.
