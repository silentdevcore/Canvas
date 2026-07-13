# Spreadsheet Editor SDK — Excel-like spreadsheets in PXA

An interactive in-app grid editor with live formulas, a backend `SpreadsheetDto` model, and `.xlsx`
import + export. Adds spreadsheet capability alongside the document designer.

## Context

PXA can only *export* a `table` element to `.xlsx` via `ExcelDocumentExporter` (ClosedXML), storing
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

## Phase 0 — Backend model (`PXA.Core.Contracts`)
- [x] `SpreadsheetDto`/`SheetDto`/`SheetColumnDto`/`SheetRowDto`/`CellDto`/`DefinedNameDto` in
      `src/Core/PXA.Core/Contracts/SpreadsheetDto.cs` (sparse typed cells with `formula`/`numberFormat`/style,
      merges, frozen panes, defined names). Reuses `CellStyleDto`/`CellBorderSideDto`.
- [x] `A1Reference` helper in `src/Core/PXA.Core/Primitives/A1Reference.cs` (col↔letters, A1↔row/col).

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
- [x] `src/spreadsheet/types.ts` — wire types (`Workbook`/`SheetWire`/`Cell`, camelCase mirroring the DTOs)
      + working `SheetState` (cells keyed `"row:col"`) + `from/toWire` converters.
- [x] `useSpreadsheetStore` (`src/spreadsheet/store.ts`): sheets, active sheet, selection, snapshot
      undo/redo + `persist`; `setCellInput` (parses formula vs number/text), style + number-format, sheet
      add/rename/delete, `loadWorkbook`/`toWire`. Engine rebuilt on load/undo/import/rehydrate.
- [x] `src/spreadsheet/formulaEngine.ts` — HyperFormula wrapper (`gpl-v3` key; one HF sheet per `Sheet`;
      `setCellContents` on edit; live values via `getCellValue`, errors as `#CODE`).
- [x] `glide-data-grid` (+ peers `react-responsive-carousel`/`marked`/`lodash`); `SpreadsheetGrid.tsx`
      renders computed values, A/B/C headers + row numbers, inline edit (overlay shows the formula),
      per-cell style (bold/bg/color/align), frozen columns.

## Phase 3 — Editor UX
- [x] `src/pages/SpreadsheetEditorPage.tsx` + lazy route `/spreadsheet` in `App.tsx` + `AppHeader` nav entry
      (desktop + mobile).
- [x] Formula bar + A1 name box, sheet tabs (add/rename via dbl-click/delete), toolbar (undo/redo,
      bold/italic, align, number-format presets, Import/Export). `src/styles/spreadsheet.css`.

## Phase 4 — Import / export wiring
- [x] `src/services/SpreadsheetService.ts` (mirrors `ExportService`): `exportXlsx` → download .xlsx;
      `importXlsx` → `loadWorkbook` (rebuilds HF). Wired to the toolbar Import/Export buttons.

## Phase 5 — Polish, tests, docs
- [x] Frontend tests `__tests__/spreadsheetStore.test.ts` (HF recalc `=SUM(A1:A2)` 30→120, `IF`, parseInput,
      undo/redo, toWire). Backend `SpreadsheetRoundTripTests` (Phase 1). A1 tests (Phase 1).
- [x] Verified: tsc clean, **183** frontend tests, vite production build green, **live API round-trip**
      (export → `.xlsx` PK; import → formula `=SUM(A1:A2)` + cached 30); `/spreadsheet` route 200.
- [x] "Spreadsheets" section added to the in-app docs (`DocsPage.tsx`): overview, formulas (HyperFormula
      A1), the xlsx/csv/json import-export matrix, and the `/api/spreadsheet/*` endpoints + `SpreadsheetDto`
      model. Sidebar entry + scroll-spy wired. tsc clean.

## Rename: Sheet → Spreadsheet (proposed — awaiting confirmation)

Two distinct kinds of "Sheet". **A** is the recommended rename; **B** should stay (a workbook *contains*
sheets — that's correct Excel terminology, and `SheetDto`→`SpreadsheetDto` would collide with the workbook
type). Confirm the scope before executing.

### A) Rename the infrastructure project `PXA.Infrastructure.Sheet` → `PXA.Infrastructure.Spreadsheet` — DONE
- [x] Folder `src/PXA.Infrastructure.Sheet/` → `src/Infrastructure/PXA.Infrastructure.Spreadsheet/` (`git mv`).
- [x] `PXA.Infrastructure.Sheet.csproj` → `PXA.Infrastructure.Spreadsheet.csproj`.
- [x] `namespace …Sheet` → `…Spreadsheet` in all 5 files.
- [~] `SheetRendererCapabilities` class **left as-is** — a class identifier (scope B), not the project rename;
      only its own declaration exists. Rename later if desired.
- [x] `PXA.sln` project name + path entry.
- [x] `ProjectReference` paths in `PXA.WebApi/PXA.WebApi.csproj` + `PXA.Export.Tests.csproj`.
- [x] `using`/qualified `PXA.Infrastructure.Spreadsheet.*` in `Program.cs`, `SpreadsheetController.cs`, and
      the 3 test files. No stale `.Sheet` references remain.
- [x] `dotnet build PXA.sln` clean; `PXA.Export.Tests` **193** green.

### B) Per-sheet model identifiers — **kept, not renamed** (decided: "rename project only")
- [x] `SheetDto`/`SheetColumnDto`/`SheetRowDto` (`SpreadsheetDto.cs`) — **kept** (a Spreadsheet contains
      Sheets; `SheetDto→SpreadsheetDto` would collide with the workbook type).
- [x] Frontend `SheetState`/`SheetWire`/`SheetEngine`/`addSheet`/`activeSheet`/… — **kept** (worksheet semantics).

## Verification
- Backend: `dotnet test` round-trip preserves values/formulas/types/number-formats/styles/merges; `dotnet
  build PXA.sln` clean.
- Frontend: `tsc --noEmit` + `jest` (store + recalc) green.
- E2E (app :5173, backend :5086): `/spreadsheet`, type `=SUM(A1:A2)` → live result; format currency;
  export → `.xlsx` opens in Excel with the formula; import a real `.xlsx` with formulas → preserved.
