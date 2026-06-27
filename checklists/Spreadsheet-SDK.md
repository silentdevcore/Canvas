# Spreadsheet Editor SDK — Excel-like spreadsheets in Canvas

An interactive in-app grid editor with live formulas, a backend `SpreadsheetDto` model, and `.xlsx`
import + export. Adds spreadsheet capability alongside the document designer.

## Context

Canvas can only *export* a `table` element to `.xlsx` via `ExcelDocumentExporter` (ClosedXML), storing
**literal values** — no formulas, no cell data types, no spreadsheet model, and **no xlsx import**. The
expression engine has **no A1 cell references**. This adds a real Excel-like experience.

### Decisions (confirmed)
- **Scope:** full — interactive editor **+** backend `SpreadsheetDto` model **+** `.xlsx` import & export.
- **Formulas:** **HyperFormula** (headless calc, ~390 Excel functions, A1 refs, dependency-graph recalc).
- **Grid:** **glide-data-grid** (MIT, canvas-rendered, virtualized, inline edit + range selection).
- **xlsx:** round-trip — ClosedXML importer + an enhanced exporter (real formulas via `cell.FormulaA1`).

> **License flag:** HyperFormula is GPLv3-or-commercial — fine for evaluation/OSS; a commercial license (or
> swapping to MIT `formulajs` + an in-house dependency graph) is a product/legal call before commercial ship.

---

## Phase 0 — Backend model (`Canvas.Core.Contracts`)
- [x] `SpreadsheetDto`/`SheetDto`/`SheetColumnDto`/`SheetRowDto`/`CellDto`/`DefinedNameDto` in
      `src/Canvas.Core/Contracts/SpreadsheetDto.cs` (sparse typed cells with `formula`/`numberFormat`/style,
      merges, frozen panes, defined names). Reuses `CellStyleDto`/`CellBorderSideDto`.
- [x] `A1Reference` helper in `src/Canvas.Core/Primitives/A1Reference.cs` (col↔letters, A1↔row/col).

## Phase 1 — Backend xlsx I/O + API
- [x] `ExcelWorkbookExporter` (SpreadsheetDto → ClosedXML): typed values, formulas via `cell.FormulaA1`,
      number formats, styles (mirrors `ExcelDocumentExporter` font/fill/border), merges, column/row sizing,
      frozen panes, defined names. JSON `object?` values unwrapped from `JsonElement`.
- [x] `ExcelWorkbookImporter` (ClosedXML → SpreadsheetDto): per cell — `FormulaA1` + cached value when
      `HasFormula`, else typed value; number format; style (fill/font/align/border); merges, column widths,
      frozen panes, defined names.
- [x] `SpreadsheetController` — `POST /api/spreadsheet/export` (→ `.xlsx`), `POST /api/spreadsheet/import`
      (multipart, seekable-copy → SpreadsheetDto). Registered `ExcelWorkbookExporter`/`Importer` in `Program.cs`.
- [x] `SpreadsheetRoundTripTests` (+ A1 tests): model → xlsx → model preserves text/number/boolean/date,
      **`=SUM(B1:B2)` formula + cached 30**, number format, bold+bg style, merge, column width, frozen rows.
      Full Export suite **193** green; solution builds clean.

## Phase 2 — Frontend foundation (`ui-designer-v2`)
- [ ] `src/spreadsheet/types.ts` (`Workbook`, `Sheet`, `Cell`) mirroring the DTOs.
- [ ] `useSpreadsheetStore` (`src/spreadsheet/store.ts`): sheets, active sheet, selection range, snapshot
      undo/redo + `persist` (mirror `src/store.ts`); cell CRUD, row/col insert/delete/resize, sheet ops.
- [ ] `src/spreadsheet/formulaEngine.ts` — HyperFormula wrapper (one HF sheet per `Sheet`; `setCellContents`
      on edit; computed values via `getCellValue`). HF owns the dependency graph; the store owns raw data.
- [ ] Add `glide-data-grid`; `SpreadsheetGrid.tsx` renders computed values, row/col headers, inline edit
      (writes formula/value → store + HF), range selection, column resize.

## Phase 3 — Editor UX
- [ ] `src/pages/SpreadsheetEditorPage.tsx` + route `/spreadsheet` (lazy, like `PdfViewerPage`) + `AppHeader`
      nav entry.
- [ ] Formula bar, sheet tabs (add/rename/switch), toolbar (insert/delete row+col, bold/italic/align,
      number-format presets), name box (A1 address). `src/styles/spreadsheet.css` (imported in `index.css`).

## Phase 4 — Import / export wiring
- [ ] `src/services/SpreadsheetService.ts` (mirror `ExportService.ts`): `exportXlsx` → download;
      `importXlsx` → hydrate store + recalc. Optional CSV. Wire toolbar Import/Export buttons.

## Phase 5 — Polish, tests, docs
- [ ] Frontend tests: store mutations + HF recalc (`=SUM(A1:A2)` updates on edit), A1 helper.
- [ ] Short "Spreadsheets" docs note; verify build/tests/tsc + end-to-end flow.

## Verification
- Backend: `dotnet test` round-trip preserves values/formulas/types/number-formats/styles/merges; `dotnet
  build Canvas.sln` clean.
- Frontend: `tsc --noEmit` + `jest` (store + recalc) green.
- E2E (app :5173, backend :5086): `/spreadsheet`, type `=SUM(A1:A2)` → live result; format currency;
  export → `.xlsx` opens in Excel with the formula; import a real `.xlsx` with formulas → preserved.
