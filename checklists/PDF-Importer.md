# PDF Importer — Fidelity Improvement Checklist

## Goals

Convert a PDF file to PXA JSON that visually matches the original:
grouped paragraph text with wrapping, correct text/background colors,
rotation, table grids detected from vector paths, and header/footer
elements routed to `sharedElements`.

---

## Tasks

### 1. Paragraph Grouping [high — replaces one-element-per-line]

- [x] Switch from `GetWords()` line-groups to `page.Letters` letter-level processing
- [x] Group consecutive letters with same style into `LetterRun` records
- [x] Group runs into lines (same baseline Y ± 3 PDF pt tolerance)
- [x] Group lines into paragraph blocks (gap < 1.5 × dominant line height)
- [x] Emit one `richtext` element per paragraph (or `text` if uniform style)
- [x] Set element `Width = page content width` for text reflow / wrapping
- [ ] Test: import a multi-paragraph PDF — each paragraph should be ONE element

### 2. Text Color [medium]

- [x] Use `letter.Color.ToRGBValues()` (0–1 range) → hex string `#RRGGBB`
- [x] Set `style.color` on each element from dominant color in paragraph
- [x] Per-span color in `HtmlContent` for mixed-color paragraphs
- [ ] Test: import PDF with colored headings — colors should match

### 3. Text Background Color [medium]

- [x] Extract all filled non-clipping paths via `page.ExperimentalAccess.Paths`
- [x] Use `GetBoundingRectangle()` on each filled path
- [x] For each paragraph block: check bounding-box overlap with filled rects
- [x] Set `style.backgroundColor` when overlap found; else omit the key
- [ ] Test: import PDF with highlighted / shaded text blocks

### 4. Text Rotation [medium]

- [x] Read `letter.TextOrientation` for the dominant orientation of the paragraph
- [x] Map: Horizontal→0°, Rotate90→90°, Rotate180→180°, Rotate270→270°
- [x] Set `style.rotation` on element (PXA rotates around element center)
- [ ] Test: import PDF with rotated text (sideways column headers, watermarks)

### 5. Table Detection [high]

- [x] Extract stroked horizontal-line paths from `page.ExperimentalAccess.Paths`
- [x] Extract stroked vertical-line paths from same source
- [x] Cluster H-lines + V-lines by overlapping bounding regions → `TableRegion`
- [x] Compute sorted row Y-values and column X-values per region
- [x] Assign `page.Letters` to grid cells by coordinate containment
- [x] Emit `table` element with `CellData`, `ColumnWidths`
- [x] Exclude table-region letters from paragraph processing
- [ ] Test: import a PDF with a bordered data table — `table` element with correct cell text

### 6. Header / Footer Detection [medium]

- [x] Skip detection for single-page PDFs
- [x] Header zone: top 8 % of page (PDF Y > height × 0.92)
- [x] Footer zone: bottom 8 % of page (PDF Y < height × 0.08)
- [x] Collect paragraph elements in those zones during page processing
- [x] Deduplicate candidates by content text across pages
- [x] Add deduplicated elements to `DesignExportDto.SharedElements`
- [x] Omit those elements from per-page `Elements` lists
- [ ] Test: import multi-page PDF with page numbers in footer — numbers appear in sharedElements

### 7. Better Image Extraction [low]

- [x] Use `img.TryGetPng(out byte[] png)` for lossless extraction
- [x] Fall back to `img.RawBytes` as JPEG only when TryGetPng returns false / empty
- [ ] Test: import PDF with embedded PNG images — no unexpected JPEG artifacts

### 8. Font Style from Font Name [low]

- [x] Strip "ABCDEF+" embedded-subset prefix from raw font name
- [x] Detect bold: name contains "Bold", "-Bd"
- [x] Detect italic: name contains "Italic", "Oblique"
- [x] Apply `font-weight` / `font-style` on spans in `HtmlContent`
- [ ] Test: import PDF with bold and italic text — style carries through

---

## Completed (pre-existing)

- [x] **Type casing fix** (2026-05-22) — All 4 importers now emit lowercase type strings
- [x] **Letter fallback** (2026-05-22) — PdfImporter reconstructs text letter-by-letter for fonts without a ToUnicode map
