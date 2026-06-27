# Spreadsheet Import / Export Formats

Expands the Spreadsheet Editor SDK's file in/out. Today only `.xlsx` round-trips; this adds the common
lightweight formats (CSV, JSON) and tracks the heavier ones (xls/ods/pdf) as deferred.

## Context

The Spreadsheet SDK ([Spreadsheet-SDK.md](Spreadsheet-SDK.md)) round-trips a `SpreadsheetDto` workbook to
`.xlsx` via ClosedXML (`ExcelWorkbookExporter`/`ExcelWorkbookImporter`, `POST /api/spreadsheet/export` +
`/import`). The frontend has `SpreadsheetService.exportXlsx`/`importXlsx` wired to the toolbar. The store
already exposes `toWire()` (model) and `loadWorkbook()`, and HyperFormula provides computed cell values.

## Current support
- [x] **`.xlsx` export** — typed values, **formulas** (`cell.FormulaA1`), number formats, styles, merges,
      column widths, frozen panes. `POST /api/spreadsheet/export`.
- [x] **`.xlsx` import** — preserves formulas + cached values, types, number formats, styles, merges.
      `POST /api/spreadsheet/import`.

---

## Phase 1 — CSV (recommended; no new dependency) — DONE
Plain values only (CSV has no formulas/styles). Export uses HyperFormula **computed** values so the file
matches what's on screen. Implemented in `ui-designer-v2/src/spreadsheet/io.ts`.
- [x] **CSV export** (client-side): `sheetToCsv(sheet, getComputed)` — RFC 4180 quoting (wrap fields with
      `,`/`"`/newline; double embedded quotes); exports the active sheet's computed values via `downloadText`.
- [x] **CSV import** (client-side): `parseCsv` (RFC 4180-aware) + `csvToSheet` (number vs text per cell) →
      `workbookToWire` → `loadWorkbook`.
- [x] Toolbar: single **Import** accepts `.xlsx/.csv/.json` (dispatch by extension); **Export ▾** menu
      (Excel / CSV / JSON). Menu styling in `spreadsheet.css`.
- [x] Tests `__tests__/spreadsheetIo.test.ts`: quoting/escaping, embedded commas/newlines, number-vs-text.

## Phase 2 — JSON workbook (recommended; trivial, full fidelity) — DONE
The native `Workbook` model — lossless save/load, offline, no backend.
- [x] **JSON export**: `workbookToJson(toWire())` → download `<name>.json`.
- [x] **JSON import**: `jsonToWorkbook` (validates `sheets`) → `loadWorkbook`.
- [x] In the Export menu + Import dispatch; test confirms a round-trip preserves formulas + styles.

## Deferred (need new libraries or are lower priority)
- [ ] **`.xls` (legacy Excel)** — ClosedXML is `.xlsx`-only; would need NPOI (new dependency). Defer until
      a real need.
- [ ] **`.ods` (OpenDocument Spreadsheet)** — not supported by ClosedXML; needs another library. Defer.
- [ ] **PDF export** — render the active sheet to PDF via the existing `Canvas.Pdf` engine (read-only).
      Feasible later; reuses `DrawSimpleTable`/cell drawing.
- [ ] **TSV** — trivial variant of CSV if requested (tab delimiter).

## Verification
- CSV: export a sheet with text + numbers + a formula → file has computed values, correct quoting; import
  it back → matching cells. Frontend `jest` round-trip test green.
- JSON: export → import reproduces the workbook (formulas + styles intact); `tsc`/`jest` green.
- Manual: `/spreadsheet` toolbar → format menu offers xlsx / csv / json; each downloads + re-imports.
