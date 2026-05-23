# Word Export Element Support Matrix

Last updated: 2026-05-19

Legend:
- Supported: rendered with intended semantics.
- Partial: rendered with fallback/simplified semantics.
- Fallback: rendered as textual annotation instead of native semantic output.

## Supported

| Element Type | Status | Notes |
|---|---|---|
| text | Supported | Typography + X/Y flow positioning mapped. |
| richtext | Supported | Inline spans mapped for bold/italic/underline/strike/color/font-size. |
| link | Supported | External links become clickable hyperlinks; relative links fallback safely to text. |
| table | Supported | Fixed layout, column widths, header/zebra/border/alignment mapping. |
| image | Supported | Data URL + HTTP(S), anchored absolute positioning, z-order, fit mode mapping, fallback placeholder. |
| pagenumber | Supported | PAGE / NUMPAGES field mapping for current/total/pageOfTotal. |

## Partial

| Element Type | Status | Notes |
|---|---|---|
| signature | Partial | Rendered as styled label + signature line text fallback. |
| field | Partial | Rendered as styled label + required marker + underline placeholder. |
| checkbox | Partial | Rendered as unicode checkbox text fallback. |
| note | Partial | Rendered as shaded paragraphs with title/body; interactive behavior not applicable. |
| optionlist | Partial | Rendered as bullet/numbered text lines; advanced list formatting not yet mapped. |
| number | Partial | Rendered as plain text number; locale/currency formatting extensions pending. |

## Fallback

| Element Type | Status | Notes |
|---|---|---|
| rect | Fallback | Rendered as textual annotation (`[rect]`). |
| circle | Fallback | Rendered as textual annotation (`[circle]`). |
| line | Fallback | Rendered as textual annotation (`[line]`). |
| arrow | Fallback | Rendered as textual annotation (`[arrow]`). |
| draw | Fallback | Rendered as textual annotation (`[draw]`). |
| watermark | Fallback | Rendered as textual annotation (`[watermark]`). |
| highlight | Fallback | Rendered as textual annotation (`[highlight]`). |
| pageboundary | Fallback | Rendered as textual annotation (`[pageboundary]`). |
| area | Fallback | Rendered as textual annotation (`[area]`). |
| subsection | Fallback | Rendered as textual annotation (`[subsection]`). |

Fallback events are also persisted in DOCX package metadata (`Description`) under an `ExportWarnings:` summary.

## Unrecognized Element Types

Unknown element types are rendered as italic placeholder annotations (`[type]`), preventing hard export failures.