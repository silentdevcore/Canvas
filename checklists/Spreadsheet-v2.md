# Spreadsheet v2 — editing essentials

Makes the Spreadsheet Editor ([Spreadsheet-SDK.md](Spreadsheet-SDK.md)) feel like a real spreadsheet:
full cell formatting, row/column ops, clipboard, number-format display, merge/freeze, a selection status
bar, and a backend bridge to embed a sheet into a document/PDF.

## Context

Today: glide-data-grid + HyperFormula recalc, a toolbar (undo/redo, bold/italic/align, number-format
presets, import/export xlsx/csv/json), single-cell selection. The `CellStyle`/`CellStyleDto` model already
carries `fontFamily`/`fontSize`/`bold`/`italic`/`color`/`backgroundColor`/`textAlign` and these round-trip
through `.xlsx` — so most formatting work is **UI + grid rendering**, not new data.

---

## Foundation — range selection — DONE
- [x] `range: {r0,c0,r1,c1}` tracked in the store from glide's `onGridSelectionChange` (`sel.current.range`);
      `selectRange` action + a `mutateRange` helper apply edits across the selection (or active cell).

## #6 — Full cell formatting — DONE
- [x] Grid renders the real style: `buildTheme` builds `baseFontStyle`
      (`${italic}${weight} ${fontSize}px ${fontFamily}`) + `bgCell`/`textDark`, so font family/size/italic/
      color/fill all show (previously only bold/bg/color/align).
- [x] Toolbar (range-aware via `applyStyle`): font-family dropdown, font-size dropdown, bold, italic,
      **text color** + **fill color** pickers, align. Number format via `applyNumberFormat`. *(Borders UI
      deferred — model supports them; pickers cover the common cases.)*
- [x] Store `applyStyle(patch)`/`applyNumberFormat(fmt)` over the range; test confirms range styling.

## #5 — Selection status bar — DONE
- [x] `selectionStats()` (Sum / Average / Count of numeric computed cells in the range); shown in the
      bottom bar with the A1 range label. Test included.

## #1 — Insert / delete / resize rows & columns
- [ ] Store actions `insertRow/deleteRow/insertCol/deleteCol` routed through HyperFormula
      `addRows`/`removeRows`/`addColumns`/`removeColumns` so **formula references shift**; remap the store's
      `cells` keys + `colWidths` accordingly; snapshot for undo.
- [ ] Column/row resize → persist into `colWidths` (+ row heights). Toolbar + header context actions. Tests
      (insert a row above a `=SUM` → the range expands correctly).

## #2 — Copy / cut / paste (Excel-compatible)
- [ ] Copy/cut the selected range to the clipboard as **TSV** (paste into Excel works); paste TSV from the
      clipboard into the grid (paste from Excel works). Keyboard Ctrl+C/X/V; `Delete` clears the range.

## #3 — Number-format-aware display
- [ ] Apply each cell's `numberFormat` to its on-screen value (currency / percent / date / decimals) so the
      grid matches Excel and the exported file. A small format-code → formatter mapper (cover the toolbar
      presets first; degrade gracefully on unknown codes).

## #4 — Merge / freeze UI
- [ ] Merge / unmerge the selected range (update `merges`; render spans in glide). Freeze rows/cols toggle
      (set `frozenRows`/`frozenCols`; glide `freezeColumns` + frozen rows).

## #7 — Backend: spreadsheet → document / PDF bridge
- [ ] `POST /api/spreadsheet/to-design` (or a converter) mapping a `SheetDto` → a Canvas `table` `ElementDto`
      (CellData + per-cell styles + column widths) inside a `DesignExportDto`, so a sheet can be embedded in
      a PDF/Word document via the existing exporters. Optional direct **PDF export** of a sheet via
      `Canvas.Pdf` `DrawSimpleTable`.

## Verification
- Frontend: `tsc --noEmit` + `jest` (formatting/range/rows-cols/clipboard/number-format) green; vite build green.
- Backend (if #7): round-trip/mapping test; `dotnet build` clean.
- E2E (`/spreadsheet`): set font family/size/color on a range; insert/delete rows shifts a `=SUM`; copy a
  range to Excel and back; a currency cell displays formatted; merge + freeze; status bar shows Sum/Avg/Count.
