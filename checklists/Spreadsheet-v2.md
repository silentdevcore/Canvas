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

## #1 — Insert / delete / resize rows & columns — DONE
- [x] Store `insertRow/deleteRow/insertCol/deleteCol` via `rowColOp`: shift the store's `cells`/`colWidths`
      (`shiftCellsForRowCol`) **and** HyperFormula (`addRows`/`removeRows`/`addColumns`/`removeColumns`), then
      pull the **ref-shifted formula sources** back via `getFormula`; snapshot for undo. Toolbar buttons
      (+Row/−Row/+Col/−Col at the active cell).
- [x] Column resize → `setColWidth` (glide `onColumnResize`) persisted into `colWidths` (round-trips to xlsx).
- [x] Tests: insert a row above `=SUM(A1:A2)` → becomes `=SUM(A2:A3)` (still 30); delete a column shifts left.

## #2 — Copy / cut / paste (Excel-compatible) — DONE
- [x] Uses glide's built-in clipboard: `getCellsForSelection` enables **Ctrl+C/X** copying the range as TSV
      (computed values → pastes into Excel); `onPaste` → `pasteValues` writes a TSV block from the clipboard
      (paste from Excel works, formulas included), one undo step; `onDelete` → `clearRange` (content only,
      keeps styling). Test: paste a block incl. `=A1+B1` → computes 3; clearRange clears it.

## #3 — Number-format-aware display — DONE
- [x] `numberFormat.ts` (`formatCellValue`) maps format codes to display: number (`#,##0.00`), currency
      (`"€"#,##0.00`), percent (`0.00%`), date (`dd.MM.yyyy` tokens); degrades to the raw string otherwise.
      The grid's `displayData` is formatted while the edit overlay keeps the raw value/formula. Test covers
      all four presets + passthrough.

## #4 — Merge / freeze UI — DONE
- [x] `mergeSelection`/`unmergeSelection` (update `merges` in A1, via centralized `toA1Range`/`parseA1Range`)
      and `setFrozen(rows, cols)`. Toolbar: Merge / Unmerge / Freeze (to selection) / Unfreeze.
- [x] Grid renders **horizontal merges** (glide column `span`) + **frozen columns** (`freezeColumns`). Test
      covers merge/unmerge/freeze. *Library limit:* glide has no vertical-merge or frozen-row rendering —
      those are stored and **export correctly to `.xlsx`** (Excel renders them; verified by the Phase-1
      backend round-trip test).

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
