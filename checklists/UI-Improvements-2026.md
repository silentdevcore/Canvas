# UI Improvements 2026

Six targeted improvement areas for the UI Designer v2. Each section contains a
checklist of concrete tasks ready for implementation.

---

## 1. Draw Lines, Arrows, and Freehand with Mouse

`line`, `arrow`, and `draw` elements currently exist as pre-configured blocks that
are placed instantly from the toolbar. The `draw` element requires manually entering
an SVG `pathData` string. No interactive mouse-draw flow exists — users cannot drag
to define geometry directly on the canvas.

- [x] Add a `drawingMode: 'line' | 'arrow' | 'draw' | null` state to the editor store
- [x] When a line / arrow / draw tool is clicked in the toolbar, activate draw mode instead of placing a default element
- [x] Change the canvas cursor to `crosshair` while draw mode is active
- [x] **Line:** on `mousedown` record the start point; on `mousemove` render a ghost preview; on `mouseup` create the final `line` element with computed `x`, `y`, `width`, `height`, and rotation angle
- [x] **Arrow:** same flow as Line but create an `arrow` element; auto-set `arrowDirection` from the drag vector angle
- [x] **Freehand:** on `mousedown` start recording; on `mousemove` append points to a running SVG path string; on `mouseup` create a `draw` element with the completed `pathData`
- [x] Press `Escape` while drawing to cancel and return to select mode without placing an element
- [x] After placing the element, exit draw mode automatically and select the newly created element
- [x] Show a small "Drawing…" indicator badge at the canvas edge while draw mode is active
- [x] Write component tests for line, arrow, and freehand placement via simulated mouse events

---

## 2. Direct Form / Address Form Integration

Individual form elements (`field`, `checkbox`, `radio`, `dropdown`, `signature`) can
only be added one at a time. There is no concept of a pre-built block of related
fields. The 10 form templates in the template library are full-page templates, not
reusable inline sub-blocks.

- [x] Define a `formBlock` container element type that groups child field elements
- [x] Design the data structure: `{ blockType: 'address' | 'contact' | 'personal' | 'custom', fields: SimpleElement[] }`
- [x] Add an "Insert Form Block" button to the Form Elements toolbar group
- [x] Build an "Insert Form Block" modal with the following pre-built block options:
  - **Address block:** Name, Street, City, Postal Code, Country (all `field` elements)
  - **Contact block:** Phone, Email, Website
  - **Personal block:** Date of Birth, Gender (dropdown), Nationality
  - **Custom block:** user defines the number of fields and their labels
- [x] When a block is inserted, place its child fields as a vertically-stacked group with automatic 56 px spacing
- [x] Add `tabIndex` (tab order) to form elements, editable in the inspector panel
- [x] Add per-field validation rules: Required, Min/Max length, Regex pattern — configurable in the inspector
- [x] Render validation badges visually in the canvas (red asterisk for required fields)
- [x] Export form metadata (field names, types, required flag, tab order) in the JSON export
- [x] Write tests for block insertion, field layout spacing, and validation rule export

---

## 3. Table of Contents in the UI

No automatic Table of Contents (TOC) generation exists. A "TABLE OF CONTENTS" text
element appears only as a static placeholder in one template. Text elements have no
semantic heading levels (`h1` / `h2` / `h3`). The `bookmark` element exists but is
not connected to any TOC generation pipeline.

- [x] Add a `headingLevel: 1 | 2 | 3 | null` property to `SimpleElement` in `types.ts`
- [x] Show a "Heading Level" selector in the inspector when a `text` or `richtext` element is selected
- [x] Add a `toc` element type that renders a visual placeholder box labelled "Table of Contents"
- [x] Add a "Table of Contents" tool to the "Advanced Document Elements" toolbar group
- [x] On preview / export: scan all elements across all pages for `headingLevel != null`, sorted by page order
- [x] Generate TOC entries with: heading text, indentation level, and page number
- [x] In the PDF / Word export pipeline, wire the `toc` element to the backend bookmark / cross-reference mechanism
- [x] Add an "Update TOC" action button in the inspector when a `toc` element is selected
- [x] Show a warning in the inspector if no heading-level elements exist when a `toc` element is on the canvas
- [x] Write tests: TOC scans headings in correct page order, entries have correct page numbers, update action refreshes content

---

## 4. Additional Templates (Presentations, Books, Landscape)

The page settings offer portrait/landscape orientation and presets A4, A5, A3,
Letter, Legal. All 16 template categories are document-oriented. No presentation
(16:9), book, or social-media format exists. The template gallery has no
dimension/format filter.

- [x] Add new page size presets to the page settings picker:
  - `Presentation 16:9` — 1280 × 720 pt
  - `Presentation 4:3` — 1024 × 768 pt
  - `Book A5` — 420 × 595 pt (portrait)
  - `Landscape A4` — 842 × 595 pt
  - `Landscape A3` — 1191 × 842 pt
  - `Social Media Square` — 1080 × 1080 pt
- [x] Add two new template categories with starter templates:
  - **Presentations** — 5 templates: title slide, content slide, agenda slide, section divider, thank-you slide
  - **Books / Chapters** — 3 templates: chapter cover page, body text page, bibliography page
- [x] Add a **Format filter** to the template gallery (All, Portrait, Landscape, Square, Widescreen)
- [x] When a presentation-format template is loaded, default ruler units to pixels and hide margin guides
- [x] Add a slide-navigation thumbnail strip when page count > 1 and the format is widescreen
- [x] Show a page dimensions badge in the canvas header for any non-A4 size
- [x] Write tests: new presets produce correct width/height values, format filter returns the correct template subset

---

## 5. F1 Key Help Popup Dialog

The keyboard shortcut handler in `SimplePxaSurface.tsx` has no F1 binding. No help modal
component exists in the codebase. The `DocsPage` is a separate route that requires
leaving the editor entirely to access.

- [x] Add `F1` to the keyboard shortcut handler in `SimplePxaSurface.tsx`
- [x] Create `pxa-designer/src/components/Editor/HelpModal.tsx` with four tabs:
  - **Quick Start** — 5-step visual guide: open template → add elements → edit properties → preview → export
  - **Keyboard Shortcuts** — table of all shortcuts (Undo, Redo, Copy, Paste, Duplicate, Delete, Arrow nudge, Zoom, F1)
  - **Elements Reference** — list of all element types with a one-liner description and PDF / Word support badge
  - **FAQ** — 8 common questions: how to export, how to add a background image, how to use `{{variables}}`, how to add a page, how to set up multi-language, how to draw, how to generate a TOC, how to insert an address form
- [x] Add a `?` icon button to the top-right of the editor header that also opens the modal
- [x] Make the modal context-sensitive: if an element is selected, open on the "Elements Reference" tab and scroll to that element type
- [x] Trap keyboard focus inside the modal; close on `Escape` or backdrop click
- [x] Add an "Open full documentation" link at the modal bottom that navigates to `DocsPage`
- [x] Store `helpModalOpen` in the editor Zustand store (not local state) so any component can trigger it
- [x] Write tests: F1 opens modal, Escape closes it, element-selected context opens the correct tab

---

## 6. PDF Element Compatibility

39 element types are available in the toolbar. The analysis below shows which are fully
rendered in PDF export, which are placeholders, and which have no handler at all.
Audit is based on the `case` handlers in `PXA.WebApi/Infrastructure/DesignJsonMapper.cs`
(PDF renderer) and `src/Infrastructure/PXA.Infrastructure.Word/WordDocumentExporter.cs` (Word renderer).

### Support Matrix

| Element | PDF | Word | Notes |
|---------|-----|------|-------|
| `text` | ✅ Full | ✅ Full | Typography, padding, borders, background |
| `richtext` | ✅ Full | ✅ Full | HTML via RichTextRenderer, v1 and v2 modes |
| `image` | ⚠️ Partial | ✅ Full | PDF: `data:` URLs only — `http/https` URLs render as a grey placeholder |
| `table` | ✅ Full | ✅ Full | Zebra striping, headers, alignment |
| `field` | ✅ Full | ✅ Full | Label + input box + placeholder text |
| `checkbox` | ✅ Full | ✅ Full | Box with check / cross / dot variants |
| `radio` | ✅ Full | ✅ Full | Bullet / numbered list with selected state |
| `optionlist` | ✅ Full | ✅ Full | Numbered / bulleted list items |
| `button` | ✅ Full | ✅ Full | Rounded rect + label |
| `signature` | ✅ Full | ✅ Full | Label + underline + "Sign here" prompt |
| `dropdown` | ✅ Full | ⚠️ Partial | PDF: box + chevron. Word: no case — falls to default placeholder |
| `shape` / `rect` | ✅ Full | ⚠️ FidelityV2 only | Word renders only when FidelityV2 mode is enabled |
| `circle` | ✅ Full | ⚠️ FidelityV2 only | Same condition as shape/rect |
| `line` | ✅ Full | ⚠️ FidelityV2 only | Dash styles supported in PDF |
| `arrow` | ✅ Full | ⚠️ FidelityV2 only | Configurable start / end arrowhead markers in PDF |
| `watermark` | ✅ Full | ⚠️ Skipped | Word: explicitly skipped with warning in FidelityV2 mode |
| `highlight` | ✅ Full | ⚠️ Skipped | Word: skipped with warning in FidelityV2 mode |
| `checkmark` | ✅ Full | ⚠️ Skipped | Word: skipped with warning in FidelityV2 mode |
| `note` | ✅ Full | ✅ Full | PDF: yellow box with title + body. Word: coloured paragraphs |
| `date` | ✅ Full | ❌ Missing | PDF: static or runtime date with timezone + locale. Word: no case handler |
| `pagenumber` | ✅ Full | ✅ Full | PDF: formatted numbers. Word: native PAGE / NUMPAGES fields |
| `toc` | ✅ Full | ✅ Full | PDF: bookmarks + leader dots + clickable links. Word: native TOC field |
| `qrcode` | ✅ Full | ❌ Missing | PDF: QRCoder library. Word: no case handler |
| `barcode` | ✅ Full | ❌ Missing | PDF: ZXing (CODE128, EAN-13, UPC-A, PDF417…). Word: no case handler |
| `chart` | ⚠️ Placeholder | ❌ Missing | PDF: draws `[Chart (type)]` box only. Word: no case handler |
| `draw` | ⚠️ Placeholder | ⚠️ Skipped | PDF: draws `[Drawing]` box. Word: skipped entirely |
| `subsection` | ⚠️ Placeholder | ⚠️ Skipped | PDF: dashed outline border only, no fill |
| `area` | ⚠️ Placeholder | ⚠️ Skipped | PDF: dashed outline border only |
| `pageboundary` | ⚠️ No-op | ⚠️ Skipped | PDF: empty `break` — nothing drawn at all |
| `link` | ❌ Missing | ✅ Full | PDF: **no case handler**. Word: hyperlink with URL validation |
| `number` | ❌ Missing | ✅ Full | PDF: **no case** — falls through to default silently. Word: renders numeric value |
| `footnote` | ❌ Missing | ✅ Full | PDF: no case. Word: native FootnoteService |
| `endnote` | ❌ Missing | ✅ Full | PDF: no case. Word: native FootnoteService |
| `bookmark` | ❌ Missing | ✅ Full | PDF: no case. Word: native bookmark anchor |
| `comment` | ❌ Missing | ✅ Full | PDF: no case. Word: native CommentService |
| `contentcontrol` | ❌ Missing | ✅ Full | PDF: no case. Word: native SDT structured data tag |

**Legend:** ✅ Full  ⚠️ Partial / Placeholder / Skipped  ❌ No handler

### Gap Summary

**7 elements have no PDF case handler** (fall through to default — nothing rendered):
`link`, `number`, `footnote`, `endnote`, `bookmark`, `comment`, `contentcontrol`

**4 elements render a placeholder box in PDF** (not real content):
`chart`, `draw`, `subsection`, `area`

**3 elements have no Word handler:**
`qrcode`, `barcode`, `date`

**PDF-viable additions** (implementable without Word-specific APIs):
- `link` → `page.AddWebLink()` + underlined blue text
- `number` → same render path as `text`, reading `el.NumberValue`
- `bookmark` → `document.AddNamedDestination()` (API already available)
- `footnote` / `endnote` → superscript reference number in body + footnote text at page bottom
- `comment` → margin annotation box with author + timestamp
- `draw` → parse SVG `pathData`, map to `page.DrawBezierCurve()` / `DrawLine()` calls
- `chart` → requires server-side chart-to-PNG (SkiaSharp / ImageSharp)

---

### Implementation Checklist

- [x] Audit all 39 element types against a PDF / Word support matrix
- [x] Add `link` → PDF: underlined blue text + `page.AddWebLink()`
- [x] Add `number` → PDF: render using same path as `text`, reading `el.NumberValue`
- [x] Add `bookmark` → PDF: call `document.AddNamedDestination(el.bookmarkName, pageIndex)`
- [x] Add `footnote` / `endnote` → PDF: superscript marker in body, footnote text block at page bottom
- [x] Add `comment` → PDF: margin annotation box with author and timestamp
- [x] Implement `draw` → PDF: parse `pathData` SVG commands into `DrawBezierCurve` / `DrawLine` calls
- [x] Implement `chart` → PDF: render chart to PNG server-side via SkiaSharp, embed with `DrawImage`
- [x] Add Word support for `qrcode` and `barcode` (generate PNG via existing libs, embed in DOCX)
- [x] Add Word support for `date` (use Word DATE field or static formatted string)
- [x] Add Word support for `dropdown` as a native content control (currently falls to default)
- [x] Add a "document mode" toggle (PDF / Word) to the editor top bar
- [x] When mode = PDF: hide the entire "Word / DOCX Elements" toolbar group
- [x] Show an inline warning banner when an unsupported element is on the canvas and user switches to PDF mode
- [x] Update the DocsPage element reference table with "Supported in PDF" / "Supported in Word" columns
- [x] Add a `supportedOutputs: ('pdf' | 'word')[]` field to each tool-definition object in `SimplePxaSurface.tsx`
- [x] Write unit tests: switching to PDF mode hides Word tools; switching back restores them

---

---

## 7. Bug Fixes & Label Localisation — 2026-05-30

Seven reported issues in the form-field rendering, canvas editor, and PDF export pipeline.

### Issue Analysis

| # | Area | Root Cause |
|---|------|-----------|
| 1 | Text fill option | `field`/`textarea` inspector had no "Fill background" toggle; PDF mapper already handles `backgroundColor: transparent` |
| 2 | Text height | `case "field"` and `case "textarea"` in `DesignJsonMapper.cs` used hardcoded `20 px` for the label row height. CSS grid gives 26 px (8 padding + 12 label + 6 gap), causing AcroForm widget to overlap the label |
| 3 | Position retention | `getSurfacePoint` divided raw pointer-to-rect offset by 1 (no zoom factor). `getBoundingClientRect()` returns **scaled** visual pixels; dividing by `zoomLevel` was missing, so drag/resize coordinates were `zoomLevel`× wrong at any zoom ≠ 1 |
| 4 | Visible/Invisible | The canvas rendering loop had no `!el.hidden` check — hidden elements were rendered identically to visible ones. No visibility toggle existed in the element properties inspector |
| 5 | Button actions | `case "button"` in `DesignJsonMapper.cs` drew the visual shape only; no link annotation was added. Inspector exposed a single URL string; no page-navigation, submit, or reset options |
| 6 | German labels | `label: 'Markieren'`, `label: 'Ankreuzen'`, and `fieldLabel: 'Auswahl'` were hardcoded German strings in the toolbar tool definitions |
| 7 | Label localisation | `SignatureLabel` was not substituted in `SubstituteElement` (backend). Frontend rendering used raw `element.fieldLabel` / `element.signatureLabel` instead of `resolveContent()`, so `{{KEY}}` placeholders were never expanded in canvas preview |

### Implementation Checklist

- [x] Add "Fill background" checkbox to `field` and `textarea` inspector blocks (`SimplePxaSurface.tsx`)
- [x] Fix label row offset: `20` → `26` in `case "field"` and `case "textarea"` (`DesignJsonMapper.cs`); use named constant `labelOffset`
- [x] Fix `getSurfacePoint` to divide by `zoomLevel` so drag/resize coordinates are correct at all zoom levels
- [x] Add `is-hidden` CSS class to hidden canvas elements (opacity 0.3 + amber dashed outline); add "Visible in output" checkbox to inspector
- [x] Expand button inspector to Action-type select (`none | url | page | submit | reset`) with conditional URL / page-number input
- [x] Add PDF link annotation in `case "button"`: `page.AddWebLink()` for URL actions, `page.AddPageLink()` for `page:N` actions
- [x] Replace German toolbar labels: `Markieren` → `Highlight`, `Ankreuzen` → `Checkmark`, `Auswahl` → `Selection`
- [x] Add `el.SignatureLabel = Substitute(...)` to `SubstituteElement` in `DesignJsonMapper.cs`
- [x] Wrap `element.signatureLabel`, `element.fieldLabel` in `resolveContent()` at all rendering sites in `SimplePxaSurface.tsx`
- [x] Add `{{key}}` hint text to label inputs in inspector (field, textarea, checkbox, checkmark, signature)

---

## Completion Status

**All 7 sections fully implemented — 2026-05-30**

| Section | Items | Status |
|---------|-------|--------|
| 1. Draw Lines, Arrows, Freehand | 9 | ✅ Complete |
| 2. Form / Address Block Integration | 9 | ✅ Complete |
| 3. Table of Contents | 10 | ✅ Complete |
| 4. Additional Templates | 7 | ✅ Complete |
| 5. F1 Help Modal | 8 | ✅ Complete |
| 6. PDF Element Compatibility | 17 | ✅ Complete |
| 7. Bug Fixes & Label Localisation | 10 | ✅ Complete |

### Notable Additions (Section 6)

- **Interactive PDF forms:** `field` (single-line), `textarea` (multiline), and `dropdown` elements are now fillable AcroForm widgets in PDF — backed by `PdfTextFieldAnnotation`, `PdfMultilineTextFieldAnnotation`, and `PdfComboBoxAnnotation` in `PXA/Pdf/`.
- **`supportedOutputs`** added to all 38 tool definitions in `SimplePxaSurface.tsx` (`['pdf', 'word']` for 35 tools; `['word']` for `footnote`, `endnote`, `contentcontrol`).
- All PDF case handlers implemented for: `link`, `number`, `bookmark`, `footnote`, `endnote`, `comment`, `draw` (SVG path parsing), `chart` (SkiaSharp PNG render).
- All Word case handlers implemented for: `qrcode`, `barcode`, `date`, `dropdown` (native SDT content control).
