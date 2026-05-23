# UI Fixes Checklist

## Scope
Improve the element inspector and canvas toolbar with missing typography controls, rotation, background/transparency, border, spacing, element identity, and alignment tools.

## Current State — Code Analysis (2026-05-15)

Analysis of `src/components/Editor/SimpleCanvas.tsx` and `src/types.ts`.

| Area | Status | Notes |
|---|---|---|
| Font family | ❌ Not implemented | `fontFamily` set only at element creation (Arial hardcoded); no inspector dropdown to change it |
| Font styles (bold/italic/underline) | ❌ Not implemented | `fontWeight` stored in style but no bold/italic toggle buttons; `fontStyle`, `textDecoration` not in `SimpleElement` at all |
| Text alignment | ❌ Not implemented | `textAlign` not in `SimpleElement.style`; no left/center/right controls in inspector |
| Rotation | ⚠️ Partial | Rotation control exists only for `watermark` element type (line 3764); text, image, shape elements have no rotation input |
| Background color | ⚠️ Partial | `backgroundColor` in style for some elements; shapes use `fill`; no unified background color control per element type |
| Background transparency | ❌ Not implemented | No "transparent" toggle or alpha slider for element background; opacity exists separately but doesn't target background only |
| Border | ⚠️ Partial | Border controls (color/width/style/radius) exist for `shape`, `rect`, `circle`; missing for `text`, `image`, `button`, `field`, and others |
| Spacing / Padding | ❌ Not implemented | No `padding` field in `SimpleElement`; no inner spacing control in inspector |
| Element name (custom label) | ❌ Not implemented | `SimpleElement` has no `name` field; inspector shows type only (not editable) |
| Element type display | ⚠️ Partial | Type shown in inspector header but not human-readable (shows `'text'` not `'Text Block'`) |
| Alignment tools | ❌ Not implemented | No align left/center/right/top/middle/bottom toolbar buttons; no distribute-evenly; no align-to-page |

---

## A. Typography

> **Current:** `fontFamily` is hardcoded to Arial at creation. `fontWeight` is stored but has no toggle button. `fontStyle`, `textDecoration`, `textAlign`, `lineHeight`, and `letterSpacing` are absent from both `SimpleElement` and the inspector.

- [x] Add font family dropdown to inspector for `text`, `richtext`, `button`, `field`, `date`, `pagenumber` elements. *(FONT_FAMILIES preset list, shared Typography section in inspector)*
- [x] Add bold / italic / underline toggle buttons (icon buttons, not dropdowns). *(`editor-toggle-btn` with active state)*
- [x] Add text alignment buttons: left, center, right, justify — for `text`, `richtext`, `button`. *(4-button toggle group)*
- [x] Add line height input (e.g. 1.0 – 3.0 multiplier). *(lineHeight input, 0.8–4 range)*
- [x] Add letter spacing input (px or em). *(letterSpacing input)*
- [x] Persist all new fields in `SimpleElement.style` and include in `ExportService` property extraction. *(text case in ExportService updated)*

## B. Rotation

> **Current:** Rotation input exists only inside the `watermark` inspector block. All other element types (text, image, shape, rect, circle, etc.) have no rotation control.

- [x] Add rotation input (0–360°) to the common "Position & Size" section of the inspector, visible for all element types.
- [x] Apply rotation via `transform: rotate(Xdeg)` on the canvas element wrapper.
- [x] Add a reset-to-zero button next to the rotation input.

## C. Background Color and Transparency

> **Current:** Background color (`fill` for shapes, `backgroundColor` for buttons/notes) varies per element type. There is no general background control for text or image elements, and no per-element transparency toggle for background specifically.

- [x] Add a unified "Background" section in the inspector for all element types that can have a background. *(BACKGROUND_TYPES set; shared Background section)*
- [x] Color picker + background opacity % slider. *(backgroundOpacity 0–100 input)*
- [x] "Transparent / None" toggle that clears the background. *(checkbox sets backgroundColor to 'transparent')*
- [x] Include `backgroundColor` and `backgroundOpacity` in `ExportService` via `sharedStyle()`.
- [x] Normalise shape elements: shapes now read `backgroundColor` first, fall back to `fill` for backward compat. Inspector background section replaces per-type Fill Color inputs. ExportService shape/rect/circle cases migrated to `backgroundColor`.

## D. Border

> **Current:** Border controls (color, width, style, radius) exist for `shape`, `rect`, `circle`. They are missing in the inspector for `text`, `image`, `button`, `field`, `checkbox`, `dropdown`, `table`, and others.

- [x] Move border controls (color, width, style, radius) into a shared "Border" section for all element types in `BORDER_TYPES`.
- [x] Add `borderStyle` options: none, solid, dashed, dotted, double.
- [x] `borderRadius` available for all border-capable element types.
- [x] Include all border properties in `ExportService` via `sharedStyle()`.

## E. Spacing / Padding

> **Current:** `SimpleElement` has no `padding` field. Elements have no inner spacing control. The canvas renders elements with no internal padding by default.

- [x] Add padding fields (`paddingTop/Right/Bottom/Left`) to `SimpleElement.style`.
- [x] Add four padding inputs in the inspector for `PADDING_TYPES`. *(top/right/bottom/left)*
- [x] Apply padding as CSS `padding` on the rendered text element.
- [x] Include padding in `ExportService` via `sharedStyle()`.
- [x] Linked/unlinked toggle for equal padding on all sides. *(link icon button collapses to single "All sides" input; unlink shows four independent inputs)*

## F. Element Type and Element Name

> **Current:** The inspector header shows the raw `type` string (e.g. `text`, `qrcode`). `SimpleElement` has no `name` field, so elements cannot be given a human-readable custom label. The type is fixed after creation.

- [x] Add `name?: string` field to `SimpleElement` interface.
- [x] Show a human-readable type label in the inspector header (e.g. `text` → **Text Block**, `qrcode` → **QR Code**). *(`ELEMENT_TYPE_LABELS` map)*
- [x] Add an editable "Element name" input at the top of the inspector — borderless inline input with placeholder. *(`editor-element-name-input`)*
- [x] Include `name` in the export payload under each element's properties. *(via `base` in `extractElementProperties`)*
- [x] Reflect the custom name in the layer panel. *(Layers tab in inspector: shows element name or type label, index, visibility toggle, lock toggle; click to select, shift+click to multi-select)*

## G. Alignment Tools

> **Current:** No alignment toolbar exists. Elements can only be positioned by dragging or editing X/Y inputs.

- [x] Add an alignment toolbar that appears when an element is selected with 6 buttons:
  - Align left edge, align horizontal center, align right edge *(to page margins)*
  - Align top edge, align vertical center, align bottom edge *(to page margins)*
- [x] Align relative to page margins by default. *(`pageSettings.margins` used as reference)*
- [x] Multi-select: shift+click on canvas or in Layers panel adds to selection; `selectedElementIds` Set tracks all selected elements.
- [x] Alignment toolbar uses `alignSelected()` — works on all selected elements simultaneously, relative to page margins.
- [x] Distribute evenly buttons appear automatically when ≥ 3 elements are selected (horizontal and vertical).
- [ ] Keyboard shortcut hints on hover. *(deferred — titles are shown as native tooltips)*

## H. Fix Table

> **Current:** The table element renders a plain `<tbody>` only — no `<thead>` or `<tfoot>`. All cells show a generic "Cell" placeholder regardless of position. The inspector exposes only rows, columns, borderWidth, borderColor, and cellPadding. There is no header row distinction, no row styling, no alternating-row colors, no per-column width control, and no cell content/data model.

- [x] Add `headerRow?: boolean` and `footerRow?: boolean` flags to `SimpleElement` (table). *(toggled in inspector; default false)*
- [x] Render `<thead>` when `headerRow` is true — first data row displayed bold, distinct background. *(e.g. `#f1f5f9` header bg)*
- [x] Render `<tfoot>` when `footerRow` is true — last data row displayed in footer section.
- [x] Add header row style: bold text + configurable header background color. *(two inspector inputs: bold toggle + color picker)*
- [x] Add alternating row colors (zebra stripes): even-row background color input + toggle. *(`zebraColor?: string`, `zebraEnabled?: boolean` on element)*
- [x] Add per-column width control: `columnWidths?: number[]` stored on element; inspector shows one number input per column (px). *(drag handle deferred)*
- [x] Add cell content model: `cellData?: string[][]` stored on element; mini grid of inputs in inspector for cell-by-cell editing.
- [x] Add per-column text alignment: `columnAlignments?: ('left' | 'center' | 'right')[]`; shown as alignment toggle per column in inspector.
- [x] Include all new table properties in `ExportService` `table` case.

## I. Fix Preview

> **Current:** `LivePreview.tsx` renders elements with a minimal subset of styles. It does not apply the typography properties added in Section A (fontFamily, fontStyle, textDecoration, textAlign, lineHeight, letterSpacing), does not apply rotation, still reads `style.fill` for shapes instead of the normalized `backgroundColor`, shows hidden elements, and mixes Tailwind utility classes with the custom CSS that the editor uses — producing visual inconsistencies between canvas and preview.

- [x] Apply all typography fields in the preview text renderer: `fontFamily`, `fontStyle`, `textDecoration`, `textAlign`, `lineHeight`, `letterSpacing`. *(mirror the canvas `renderElement` text case)*
- [x] Apply `transform: rotate(Xdeg)` for `style.rotation` on each rendered element wrapper. *(added to `wrapperStyle()` helper)*
- [x] Normalize shape/rect/circle background in preview: read `style.backgroundColor ?? style.fill ?? 'transparent'`. *(fixes old saved designs)*
- [x] Filter out hidden elements (`element.hidden === true`) before rendering. *(`visibleElements` filter per page)*
- [x] Apply border properties from `sharedStyle` in preview: `borderWidth`, `borderColor`, `borderStyle`, `borderRadius`. *(added to `wrapperStyle()`)*
- [x] Apply padding from `sharedStyle` in preview: `paddingTop/Right/Bottom/Left`. *(added to `wrapperStyle()`)*
- [x] Apply `backgroundOpacity` in preview: convert to CSS `opacity` on the background or use `rgba` via the color. *(`hexToRgba` helper added to `LivePreview.tsx`; `wrapperStyle()` uses `rgba(r,g,b,opacity)` when `backgroundOpacity < 1`)*
- [x] Replace Tailwind utility classes on element wrappers with explicit inline styles to match the canvas rendering. *(LivePreview fully rewritten with inline styles; Tailwind retained only in header/info bar)*

## J. Multi Pages

> **Current:** The Zustand store holds a single flat `elements: SimpleElement[]` array. There is no concept of pages — adding elements always adds to the same single page. The editor has no page navigation UI, the preview renders a single page, and ExportService produces a single-page payload.

- [x] Restructure store: replace `elements: SimpleElement[]` with `pages: { id: string; elements: SimpleElement[] }[]` and add `currentPageIndex: number`. *(v3→v4 migration in store's `migrate` function wraps old `elements` into `pages[0]`)*
- [x] Add derived helper `currentElements` = `pages[currentPageIndex].elements` derived in App.tsx and passed as `elements` prop.
- [x] Add page navigation UI in the editor: page strip at the bottom of the canvas with numbered tabs and "Page N / M" indicator in stage header.
- [x] Add / delete page actions: "+" button adds a blank page; delete button removes page with `window.confirm` guard; last page cannot be deleted.
- [x] Duplicate page action: copies all elements of a page into a new page inserted after current.
- [x] Reorder pages: drag-to-reorder in the page strip. *(HTML5 drag-and-drop on page thumbnails; `onPageMove={movePageTo}` wired in `App.tsx`; `.is-dragging` / `.is-drag-over` CSS states)*
- [x] Update `LivePreview.tsx` to render all pages in sequence, separated by a label and margin.
- [x] Update `ExportService.convertElementsToTemplate` to accept `pages[]`; emits both a `pages` array and a flat `elements` list for backward compat.
- [x] Update `ExportService.validateForExport` — called in LivePreview with flattened elements from all pages.
- [x] Update canvas header element count / status bar to show "Page N / M" in the stage header.

## K. Undo / Redo

> **Current:** No history. Any accidental move, delete, or resize is permanent unless the user manually recreates the element.

- [x] Add `undoStack: Template[]` and `redoStack: Template[]` to the Zustand store (max 50 entries each). *(not persisted — `partialize` excludes them)*
- [x] Add `snapshotHistory()` action that pushes the current template onto the undo stack and clears the redo stack.
- [x] Add `undo()` action: pops undo stack → restores template; pushes old template onto redo stack.
- [x] Add `redo()` action: pops redo stack → restores template; pushes restored template onto undo stack.
- [x] Snapshot is taken automatically on: `addElement`, `deleteElement`, `reorderElement`, `addPage`, `deletePage`, `duplicatePage`, `movePageTo`, `addSharedElement`, `deleteSharedElement`.
- [x] Snapshot is taken at drag **start** and resize **start** (one undo step per interaction, not per pixel).
- [x] `⌘Z` / `Ctrl+Z` → undo; `⌘⇧Z` / `Ctrl+Y` → redo. *(global keyboard handler in SimpleCanvas)*
- [x] Undo and Redo icon buttons added to the topbar.
- [x] Switching templates resets both stacks.

## L. Right-click Context Menu

> **Current:** Right-clicking on elements or the canvas has no effect (browser default context menu appears).

- [x] Right-clicking an element shows a context menu with: Copy, Duplicate, Paste (disabled when clipboard empty), Lock/Unlock, Hide/Show, Bring to Front, Bring Forward, Send Backward, Send to Back, Delete.
- [x] Right-clicking empty canvas shows: Paste, Select All.
- [x] A transparent backdrop closes the menu on click-outside.
- [x] Disabled items styled non-interactive (grey, no hover). *(`.editor-context-menu-item.disabled`)*
- [x] Keyboard shortcut hints displayed inline in menu items.
- [x] Clipboard state (`clipboard: SimpleElement | null`) lives in component state; `⌘C` copies selected element, `⌘V` pastes with 16 px offset.

## M. Marquee (Rubber-band) Selection

> **Current:** Multi-select only possible via Shift+click on individual elements.

- [x] Pointer-down on empty canvas starts a marquee drag.
- [x] Dragging renders a dashed blue selection rectangle (`.editor-marquee-rect`) as an absolutely positioned overlay inside the page content.
- [x] On pointer-up, all elements (page + shared) whose bounding boxes intersect the marquee are added to `selectedElementIds`.
- [x] Short pointer events (< 4 px movement) are treated as a plain click and clear the selection.
- [x] `⌘A` / `Ctrl+A` selects all elements on the current page and all shared elements.
- [x] Group drag: dragging any element that is part of a multi-selection moves all selected elements together, preserving their relative positions. *(delta computed from pointer start position, not element position, so it stays stable throughout the drag)*
