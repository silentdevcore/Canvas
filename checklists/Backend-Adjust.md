# Backend-Adjust Checklist

Analysis of gaps between the frontend UI designer and the backend PDF renderer (`DesignJsonMapper.cs`).
All fixes go in `PXA.WebApi/Infrastructure/DesignJsonMapper.cs` unless stated otherwise.

---

## 1. Critical / Coordinate Issues

- [x] **Y-axis coordinate flip** — CSS Y is top-down; PDF Y is bottom-up. Added `TextY()` and `RectBottomY()` helpers that convert every element's CSS coordinates to PDF coordinates using `pageH - cssY`.
- [x] **Text Y-axis offset** — Switched from `DrawText` to `DrawParagraph` throughout; baseline computed as `pageH - cssTop - fontSize * 0.72`.
- [x] **Line element renders diagonally instead of horizontally** — Fixed: if width ≥ height, draw horizontal through vertical midpoint; otherwise draw vertical through horizontal midpoint.
- [x] **Page margins not applied** — `PageSettingsDto` has `Margins` but they are never read; elements draw from raw x/y regardless of margin offset.

---

## 2. Missing Element Types (no case in switch)

These element types exist in the frontend but fall through the switch silently — nothing is drawn.

| Type | Status | Notes |
|---|---|---|
| `chart` | ✅ Placeholder | Dashed box + "Chart (bar/line/pie)" label |
| `subsection` | ✅ Implemented | Dashed gray rectangle outline |
| `area` | ✅ Implemented | Dashed gray rectangle outline |
| `button` | ✅ Implemented | Rounded filled rect + centered label |
| `dropdown` | ✅ Implemented | Rect with label + "v" chevron |
| `optionlist` | ✅ Implemented | Bullet text lines (clips to element height) |
| `radio` | ✅ Implemented | "o"/"*" prefix + option text |
| `arrow` | ✅ Implemented | Horizontal line + filled triangle arrowhead |
| `date` | ✅ Implemented | Reads dateMode, formats with dateFormat/locale |
| `checkmark` | ✅ Implemented | Box + check/cross/dot based on checkState |

---

## 3. Placeholder Elements (drawn as outline only)

These types have a gray rectangle placeholder. Implement real rendering where feasible.

- [x] **`image`** — `data:` URL images decoded and embedded via temp file. External URL images fall back to placeholder (server-side HTTP fetch is out of scope).
- [x] **`qrcode`** — QRCoder `PngByteQRCode` → temp PNG → `DrawImage`.
- [x] **`barcode`** — ZXing.Net `BitMatrix` → custom PNG encoder → temp PNG → `DrawImage`.
- [x] **`watermark`** — Implemented: draws rotated text at 45° using element's content and fontSize.
- [x] **`highlight`** — Implemented: filled rect with backgroundColor (default yellow `#fef08a`), no border.
- [x] **`pagenumber`** — Implemented: reads numberingFormat, prefix, suffix, startNumber.
- [x] **`pageboundary`** — Structural marker, renders nothing (correct behavior).
- [ ] **`draw`** — Placeholder drawn. SVG pathData replay not yet implemented.

---

## 4. Partially-Rendered Elements — Missing Properties

### 4.1 `text`
- [x] `fontFamily` — mapped: serif→Times, mono→Courier, otherwise Helvetica
- [x] `textAlign` — mapped to `PdfTextAlignment.Left/Center/Right/Justify` via `DrawParagraph`
- [x] `lineHeight` — passed through via `PdfParagraphOptions.LineHeight`
- [x] `letterSpacing` — mapped to `CharacterSpacing`
- [x] `textDecoration` — `underline` / `line-through` supported natively
- [x] `rotation` — `transform: rotate(Ndeg)` parsed and applied via `RotationDegrees`
- [x] Multi-line wrapping — `DrawParagraph` handles word-wrap within `el.Width`
- [ ] `opacity` — not supported (PDF engine has no per-element opacity for text)
- [x] `padding` — `paddingLeft`/`paddingTop` applied as coordinate offset

### 4.2 `richtext`
- [x] HTML stripped to plain text via `StripHtml`; drawn with paragraph word-wrap
- [x] Inline bold/italic/color spans from HTML are not preserved — full HTML parser needed for fidelity

### 4.3 `field`
- [x] Label reads `style["color"]`
- [x] Box reads `style["backgroundColor"]`
- [x] Height guard added (`boxH = max(h-20, 2)`)
- [x] `required` asterisk color is not red — drawn with `#ef4444` as a separate `DrawText` call
- [x] Placeholder text inside the box is not drawn

### 4.4 `checkbox`
- [x] `checkState` fully implemented: checked (✓ lines), cross (×), dot (filled circle)
- [x] Box reads `style["borderColor"]`, `style["backgroundColor"]`
- [x] Label reads `style["fontSize"]`, `style["color"]`

### 4.5 `signature`
- [x] Line color reads `style["borderColor"]` then `style["color"]`
- [x] Y position computed from `el.Height`, not hardcoded
- [x] No "Sign here" prompt inside the signing area

### 4.6 `rect` / `shape`
- [x] `borderRadius` — `DrawRoundedRectangle` used when radius > 0
- [x] Transparent background handled (`fill: false` when `backgroundColor` is missing/transparent)
- [ ] `opacity` — not supported
- [x] `borderStyle` (dashed/dotted) — applied via `PdfStrokeStyle.DashArray`

### 4.7 `circle`
- [x] `borderWidth` read from style
- [x] Transparent background handled
- [x] Ellipse (width ≠ height) — `DrawPolygon` with 32-point approximation via `EllipsePoints()`

### 4.8 `table`
- [x] Zebra striping via `AlternateRowFillColor` using `zebraEnabled`/`zebraColor`
- [x] Footer row via `HasFooterRow`
- [x] Custom header background via `HeaderFillColor` + `headerBgColor`
- [x] Column alignment via `ColumnAlignments` mapping
- [x] Font size from style
- [x] Border color from style
- [x] Now uses `DrawSimpleTable` — built-in word-wrap, padding, vertical alignment

### 4.9 `note`
- [x] `noteAuthor` drawn at bottom of note
- [x] Body uses `DrawParagraph` for word-wrap
- [x] Colors read from style

---

## 5. Global Style Properties

| CSS property | Key in style dict | Status |
|---|---|---|
| `opacity` | `"opacity"` | ❌ Not supported by PDF engine at element level |
| `rotation` / `transform` | `"transform"` | ✅ Parsed and applied to text elements |
| `borderRadius` | `"borderRadius"` | ✅ Applied via `DrawRoundedRectangle` |
| `borderStyle` | `"borderStyle"` | ✅ `PdfStrokeStyle.DashArray` for dashed/dotted |
| `boxShadow` | `"boxShadow"` | — Acceptable to skip (not standard in PDF) |
| `padding` | `"padding"`, `"paddingLeft"` | ✅ `paddingLeft`/`paddingTop` applied as offset |
| `zIndex` | element order | ✅ Draw order matches layer order |
| `visibility` | `"visibility"` | ✅ `hidden` elements already filtered by `el.Hidden` |

---

## 6. `date` Element ✅ Implemented

- [x] `dateMode == "render"` → uses `DateTime.UtcNow`, formatted with `dateFormat` and `locale`
- [x] `dateMode == "static"` → uses `el.Content`
- [x] Falls back to `el.FallbackText` if content is empty
- [x] Drawn as paragraph with full style support
- [x] Timezone from `el.Timezone` applied via `TimeZoneInfo.FindSystemTimeZoneById()`

---

## 7. `pagenumber` Element ✅ Implemented

- [x] `"current"` → page index (1-based + startNumber offset)
- [x] `"total"` → total page count
- [x] `"pageOfTotal"` → `"{n} / {total}"`
- [x] `"roman"` → lowercase roman numerals (`i`, `ii`, `iii` …)
- [x] `"alphabetic"` → alphabetic (`a`, `b`, `c` …)
- [x] `prefix` and `suffix` applied
- [x] `startNumber` offset applied

---

## 8. `DesignExportDto` / `ElementDto` — Missing DTO Properties

The following `SimpleElement` fields exist on the frontend but are **absent from `ElementDto.cs`**:

| Field | Type | Usage |
|---|---|---|
| `barcodeType` | `string` | Barcode format (Code128, QR, etc.) |
| `chartType` | `string` | `"bar"`, `"line"`, `"pie"` |
| `chartData` | `object` | Chart dataset |
| `options` | `string[]` | Dropdown / radio / optionlist choices |
| `selectedValue` | `string` | Pre-selected option |
| `multiSelect` | `bool` | Multi-select flag |
| `ordered` | `bool` | Ordered list |
| `fitMode` | `string` | Image fit mode |
| `cropX/Y/W/H` | `double` | Image crop region |
| `focalX/Y` | `double` | Image focal point |
| `watermarkMode` | `string` | `"text"` or `"image"` |
| `pageScope` | `string` | Which pages to apply scope |
| `pageRange` | `string` | Explicit page range |
| `arrowMode` | `string` | `"straight"`, `"elbow"`, `"curved"` |
| `startMarker` / `endMarker` | `string` | Arrowhead markers |
| `drawTool` | `string` | `"pen"`, `"highlighter"`, `"eraser"` |
| `pathData` | `string` | SVG path string for draw element |
| `dateMode` | `string` | `"static"`, `"render"`, `"binding"` |
| `dateFormat` | `string` | Date format pattern |
| `locale` | `string` | Locale for date/number formatting |
| `timezone` | `string` | Timezone for render-time date |
| `fallbackText` | `string` | Fallback for unresolved bindings |
| `markMode` | `string` | `"rectangle"` or `"text"` for highlight |
| `checkState` | `string` | `"checked"`, `"cross"`, `"dot"`, `"empty"` |
| `pageBoundaryMode` | `string` | `"start"` or `"end"` |
| `numberingFormat` | `string` | Page number format |
| `startNumber` | `int` | Page number offset |
| `prefix` / `suffix` | `string` | Page number decorators |
| `headerBgColor` | `string` | Custom table header color |
| `zebraEnabled` | `bool` | Zebra striping toggle |
| `zebraColor` | `string` | Zebra row background color |
| `columnAlignments` | `string[]` | Per-column text alignment |
| `noteCollapsed` | `bool` | Note display state (PDF: ignore) |
| `qrSize` | `int` | QR module size hint |

**Status**: All 35 fields added to `ElementDto.cs` as of 2026-05-15.

---

## 9. Priority Order for Fixes

| Priority | Fix | Status |
|---|---|---|
| 🔴 P1 | Y-axis coordinate flip (CSS → PDF) | ✅ Done |
| 🔴 P1 | Line orientation (diagonal → horizontal/vertical) | ✅ Done |
| 🔴 P1 | Text baseline compensation | ✅ Done (via `TextY` helper) |
| 🔴 P1 | Add missing DTO fields (`ElementDto.cs`) | ✅ Done |
| 🟠 P2 | Multi-line text wrapping | ✅ Done (DrawParagraph) |
| 🟠 P2 | `date` element implementation | ✅ Done |
| 🟠 P2 | `pagenumber` element implementation | ✅ Done |
| 🟠 P2 | `checkbox` check state rendering | ✅ Done |
| 🟠 P2 | Table: zebra, footer, header color, column alignment | ✅ Done (DrawSimpleTable) |
| 🟠 P2 | `watermark` text rendering | ✅ Done |
| 🟡 P3 | `fontFamily` mapping | ✅ Done |
| 🟡 P3 | `textAlign` in text elements | ✅ Done |
| 🟡 P3 | `borderRadius` (DrawRoundedRectangle) | ✅ Done |
| 🟡 P3 | `arrow` element (line + arrowhead) | ✅ Done |
| 🟡 P3 | `button`, `dropdown`, `radio`, `optionlist` rendering | ✅ Done |
| 🟡 P3 | `highlight`, `checkmark`, `subsection`, `area` | ✅ Done |
| 🟡 P3 | `richtext` HTML parsing (preserve line breaks, paragraphs, list items) | ✅ Done (HtmlToText) |
| 🟡 P3 | `richtext` inline bold/italic/color spans | ✅ Done (RichTextRenderer) |
| 🟡 P3 | `circle` ellipse support (width ≠ height) | ✅ Done (DrawPolygon with 32 pts) |
| 🟡 P3 | `opacity` support globally | ❌ Not supported by engine |
| 🔵 P4 | `image` data-URL embedding | ✅ Done (base64 decode → temp PNG/JPEG) |
| 🔵 P4 | `qrcode` generation (QRCoder) | ✅ Done (PngByteQRCode → temp PNG) |
| 🔵 P4 | `barcode` generation (ZXing.Net) | ✅ Done (BitMatrix → MatrixToPng → temp PNG) |
| 🔵 P4 | `draw` path replay (SVG pathData) | ❌ Placeholder |
| ⚪ P5 | `borderStyle` dashed / dotted for rect/shape | ✅ Done (PdfStrokeStyle.DashArray) |
| ⚪ P5 | `padding` inset for text elements | ✅ Done (padLeft/padTop offset) |
| ⚪ P5 | `timezone` for date render mode | ✅ Done (TimeZoneInfo.FindSystemTimeZoneById) |
| ⚪ P5 | `required` asterisk in red (#ef4444) | ✅ Done (separate DrawText call) |
| ⚪ P5 | `boxShadow` | — Skip (not standard in PDF) |
| ⚪ P5 | Arrow `startMarker`/`endMarker` (none/arrow/dot) | ✅ Done |
| ⚪ P5 | `pageScope` for watermarks (all/first/odd/even/range) | ✅ Done (MatchesPageScope helper) |
