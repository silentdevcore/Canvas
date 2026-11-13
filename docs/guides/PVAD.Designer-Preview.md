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

Complete table cell matrices use the PDF-aligned renderer. Older incomplete
tables retain one shared compatibility renderer, including explicit false
font-style overrides and zero padding. Header/footer-only compatibility tables
do not insert an extra blank body row. Editing rows/columns through the
Inspector produces the normal canonical cell matrix.

Zoom scales the complete document content once, including compatibility-table
fonts, borders and padding. It does not apply separate, inconsistent scale
factors to individual cell properties.

## Output And Limits

Live Preview is a document-layout view, not the PDF file itself. Inspect the
actual exported PDF for PDF security, signatures, viewer-specific form focus,
archival conformance and accessibility validation.

The shared renderer prevents Canvas/Preview implementation drift; it is not a
claim that every historical or invalid document is repaired automatically.
The audited image cases cover contained and stretched output; full backend
cropping/focal parity for every `cover`/`none` combination remains separate
work. The existing date formatter behavior is consolidated, not replaced by
a complete implementation of arbitrary .NET date-format strings.

Native serif/monospace raster output on different operating systems also
requires separate font and platform validation. The scoped, owner-approved
bold-text raster exceptions do not grant universal font parity.
