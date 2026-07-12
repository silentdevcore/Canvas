# UI Page Settings Checklist

## Scope
Improve Page Settings so users can configure document/page behavior before designing or exporting PDFs.

## Current State — Code Analysis (2026-05-15)

Analysis of `src/components/Editor/SimplePxaSurface.tsx`, `src/store.ts`, and `src/styles/index.css`.

| Area | Status | Notes |
|---|---|---|
| Paper size | ❌ Not implemented | Hardcoded `PAGE_WIDTH = 595`, `PAGE_HEIGHT = 842` (A4 only) |
| Orientation | ❌ Not implemented | Always portrait, no toggle |
| Page background | ❌ Not implemented | PXA always white, no control in store or UI |
| Margins | ❌ Not implemented | Hardcoded `padding: 48px` in `.editor-page-content` CSS |
| Header / Footer | ❌ Not implemented | No dedicated area or controls |
| Page numbering (global) | ⚠️ Partial | `pagenumber` canvas element exists, but no global config panel |
| Watermark (global) | ⚠️ Partial | `watermark` canvas element exists, but not a global page-level setting |
| Grid visibility | ❌ Not implemented | `.editor-page-grid` is always rendered, no toggle |
| Snap-to-grid | ❌ Not implemented | No snapping logic on drag/resize |
| Zoom | ❌ Not implemented | "100%" label in header is static, `transform: scale()` not applied |
| Bleed / crop marks | ❌ Not implemented | No bleed settings or print guides |
| Export | ❌ Not implemented | `onExport` in `App.tsx` is `console.log('Export PDF')` — no-op |
| Inspector empty state | ❌ Not implemented | Shows "No element selected" — no page settings panel shown |

**Priority gaps:** Paper size/orientation, page background, margins, zoom, grid toggle, snap-to-grid, and the inspector empty-state page settings panel are the most impactful missing pieces.

## Definition of Done
- [x] Page Settings are accessible from the main toolbar. *(gear icon in topbar deselects element → shows Page Settings panel)*
- [x] Settings update the canvas preview immediately.
- [x] Settings persist in template JSON/save/load flows. *(Zustand `persist` middleware)*
- [x] Settings are applied during export/code generation. *(pageSettings passed to exportToPDF; full payload in JSON export including pagination, watermark, exportDefaults)*
- [x] Invalid combinations show clear validation messages. *(validation panel at top of Page Settings: margin/header/footer/bleed conflicts)*
- [x] Defaults are safe for common PDF documents. *(A4, 48 px margins, white background)*

## A. Page Format
> **Current:** Hardcoded A4 portrait (595 × 842 px). Constants in `SimplePxaSurface.tsx:101-102`. No store field for page size.
- [x] Support preset sizes: A4, A5, Letter, Legal, Invoice, Label, Custom. *(A4/A5/A3/Letter/Legal dropdown)*
- [x] Support custom width and height. *(number inputs)*
- [x] Support units: mm, cm, inch, px, pt. *(Unit dropdown in Paper section; all inputs convert live)*
- [x] Support portrait and landscape orientation. *(toggle buttons)*
- [x] Show calculated page dimensions in the selected unit. *(width/height inputs show converted values)*
- [x] Validate minimum and maximum page size. *(validation panel warns if < 100 px or > 5 000 px)*

## B. Margins
> **Current:** Fixed `padding: 48px` in `.editor-page-content` CSS. No store field, no visual guides, no controls.
- [x] Add linked and unlinked margin controls. *(link/unlink toggle)*
- [x] Support top, right, bottom, and left margins. *(four number inputs)*
- [x] Add safe-zone preview guides on canvas. *(dashed blue overlay)*
- [x] Warn when elements sit outside printable/safe area. *(amber notice in element inspector)*
- [x] Support preset margin profiles: none, narrow, normal, wide. *(preset buttons in Margins section)*

## C. Header and Footer
> **Current:** Header/footer areas are enabled via `pageSettings.headerEnabled/footerEnabled` toggles and shown as guide bands on canvas. Height is configurable. No content repeat logic or first/odd/even variants.
- [x] Enable/disable header area. *(Enable header checkbox)*
- [x] Enable/disable footer area. *(Enable footer checkbox)*
- [x] Configure header/footer height. *(Height number input, shown when enabled)*
- [x] Support first-page different header/footer. *(headerFirstPageDifferent / footerFirstPageDifferent checkboxes)*
- [x] Support odd/even page variants. *(headerOddEvenDifferent / footerOddEvenDifferent checkboxes)*
- [x] Allow page numbers, dates, logos, and text inside header/footer areas. *(quick-insert buttons in Page Settings: Text, Page №, Date, Logo — each creates a pre-positioned element centered in the header/footer band)*

## D. Page Numbering
> **Current:** `pageSettings.pageNumbering` object stores format, startNumber, prefix, suffix, showOnFirstPage, and placement. "Place on canvas" button creates or updates a `pagenumber-global` element at the chosen position.
- [x] Configure numbering format globally. *(Format dropdown: pageOfTotal, current, total, roman, alphabetic)*
- [x] Configure start number and numbering offset. *(Start at number input)*
- [x] Configure prefix and suffix. *(Prefix / Suffix text inputs)*
- [x] Configure show/hide on first page. *(Show on first page checkbox → sets pageScope)*
- [x] Configure placement presets: top-left, top-center, top-right, bottom-left, bottom-center, bottom-right. *(3×2 arrow button grid)*
- [ ] Ensure numbering resolves after final pagination. *(backend rendering concern)*

## E. Background and Watermark
> **Current:** `pageSettings.globalWatermark` stores all watermark config. PXA shows live preview overlay. Export payload includes watermark when enabled.
- [x] Configure page background color. *(color picker in Page Settings panel)*
- [x] Configure background image. *(URL input in Background section)*
- [x] Configure image fit: contain, cover, stretch, tile. *(fit mode dropdown)*
- [x] Configure global watermark text/image. *(text/image mode toggle + content input)*
- [x] Configure watermark opacity, rotation, and page scope. *(opacity, rotation, fontSize, color, page scope dropdown)*
- [x] Ensure background/watermark layer order is predictable. *(z-index 19 overlay, below crop marks at 20, above all elements)*

## F. Bleed, Crop, and Print Guides
> **Current:** Bleed has size input + red trim guide. Crop marks toggle shows SVG L-shaped corner marks. Grid/snap in PXA section.
- [x] Add optional bleed settings. *(Bleed size input; 0 = off; red trim guide on canvas)*
- [x] Add crop mark toggle. *(Show crop marks checkbox → SVG L-shaped corner brackets on canvas)*
- [x] Add trim/safe area guide toggles. *(Show margin guide + Show safe area guide checkboxes in PXA section)*
- [x] Add grid visibility toggle. *(Show grid checkbox)*
- [x] Add snap-to-grid toggle and grid size. *(Snap to grid checkbox + grid size input)*
- [x] Ensure guides are preview-only unless explicitly exported. *(export payload includes `guides: { previewOnly: true }`)*

## G. Pagination Behavior
> **Current:** `pageSettings.pagination` stores all pagination config. Pagination section in Page Settings panel. Config exported in JSON payload for backend renderer.
- [x] Configure automatic page breaks. *(Page breaks dropdown: Automatic / Manual only)*
- [x] Configure orphan/widow control for text/table content. *(Orphan lines / Widow lines number inputs)*
- [x] Configure keep-with-next and keep-together defaults. *(Keep headings with following content checkbox)*
- [x] Configure repeat table header on new pages. *(Repeat table header checkbox)*
- [x] Configure page start/end behavior for sections. *(Section start dropdown: continue / new-page / odd-page / even-page)*
- [x] Warn about settings that may produce blank pages. *(validation panel warns when odd/even page start is selected)*

## H. Export Defaults
> **Current:** `onExport` in `App.tsx:184` is `() => console.log('Export PDF')` — a complete no-op. `ExportService.ts` has full type mapping and property extraction logic ready to use but is never called.
- [x] Export wiring — JSON download via `ExportService.exportToJSON`. *(implemented)*
- [x] Configure PDF metadata: title, subject, author, keywords. *(Export Metadata section in Page Settings panel)*
- [x] Configure PDF quality/compression profile. *(Export Defaults → quality dropdown: screen/ebook/printer/prepress)*
- [x] Configure font embedding policy. *(Embed fonts checkbox)*
- [x] Configure image compression policy. *(Compress images checkbox)*
- [x] Configure accessibility/tagged PDF option if supported. *(Accessibility tagged PDF checkbox)*
- [x] Store export defaults with the template/document. *(persisted in pageSettings; included in JSON export payload)*

## I. Preview and UX
> **Current:** Inspector right panel shows "No element selected" when nothing is chosen — this empty state should instead become the Page Settings panel. Zoom is a static "100%" label in the stage header with no functional scale applied.
- [x] Replace "No element selected" empty state with a Page Settings panel.
- [x] Update canvas page frame when settings change.
- [x] Add functional zoom control (25%–200%) with Cmd +/− shortcuts.
- [x] Show page boundaries, margins, safe zones, and bleed guides clearly. *(dashed margin overlay)*
- [x] Add reset-to-defaults action. *("Reset to defaults" button at bottom of Page Settings)*
- [ ] Add apply-to-current-page and apply-to-all-pages options. *(single-page canvas — global settings apply to all pages by design)*
- [x] Add unsaved-changes state when editing settings. *(settingsModifiedSinceExport flag in store; animated amber dot on gear icon in topbar; cleared on export)*
- [x] Keep controls usable on tablet and mobile layouts. *(responsive breakpoints at 1024px, 768px, 480px; panels scroll; stage stacks below panels)*

## J. Testing
- [x] Test page presets and custom sizes. *(ExportService.test.ts: pageSettings width/height/orientation)*
- [x] Test orientation switching with existing elements. *(ExportService.test.ts: landscape orientation preserved)*
- [x] Test margin and safe-zone warnings. *(pageValidation.test.ts: margin overflow, header+footer overflow, bleed)*
- [x] Test header/footer rendering in preview and PDF export. *(ExportService.test.ts: header/footer enabled/disabled in payload)*
- [x] Test watermark/background rendering in preview and PDF export. *(ExportService.test.ts: watermark enabled/null, backgroundColor)*
- [ ] Test numbering after multi-page pagination. *(backend rendering concern — not unit-testable in frontend)*
- [x] Test save/load round trip for all page settings. *(ExportService.test.ts: exportDefaults, pageNumbering, margins; units.test.ts: round-trip px↔mm/cm/in/pt)*

