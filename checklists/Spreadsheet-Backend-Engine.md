# Powerful Backend Spreadsheet Engine

Grows the backend spreadsheet from an `.xlsx` round-trip into an engine (server-side calculation, rich
Excel features, rendering, format breadth), built **in-house** on ClosedXML + Canvas.Pdf + NPOI.
Benchmarked against GemBox.Spreadsheet and Aspose.Cells.

## Context

Today: `ExcelWorkbook{Exporter,Importer}` (ClosedXML, `.xlsx` only) + `SpreadsheetToDesignConverter`.
Formulas compute client-side (HyperFormula); the backend stores formula strings + a cached value. Missing
vs. GemBox/Aspose: server-side calc, conditional formatting, data validation, sort/filter, page setup,
protection, grouping, comments, rendering (PDF/PNG/HTML), and `.xls`/`.ods`/backend CSV/HTML.

### Decisions (confirmed)
- **In-house:** ClosedXML + Canvas.Pdf (render) + NPOI (`.xls`). No commercial dependency.
- **Phase 1 = server-side calculation engine.**
- **Defer charts + pivot tables.**

> **Known limit:** ClosedXML's formula engine covers common functions but not Aspose's full ~450; unsupported
> functions fall back to the cached value / `#ERROR`.

---

## Phase 1 — Server-side calculation engine — DONE
- [x] `SpreadsheetCalculator`: builds the workbook via `ExcelWorkbookExporter.Build` (extracted) →
      `RecalculateAllFormulas()` → writes each formula cell's computed value back into `CellDto.Value`
      (`XLCellValue` typed read; errors → `#CODE`, unsupported fn → `#ERROR`).
- [x] `POST /api/spreadsheet/calculate`; `recalculate=true` query option on `/export`. Registered in DI.
- [x] Test `Calculate_ComputesFormulasServerSide` (`=SUM`/`=IF`/div-by-zero). **Live verified**: chained
      deps compute (`A2==A1*B1==30`, `A3==SUM(A1:A2)==40`, `A4==IF(...)=="hi"`). Solution builds clean.

## Phase 2 — Rich Excel features (model + ClosedXML round-trip)
- [ ] Model: `conditionalFormats[]`, `dataValidations[]`, `autoFilter`, `PageSetupDto`, `ProtectionDto`,
      row/col grouping levels, `comment`, `hyperlink`.
- [ ] Exporter/importer map each via ClosedXML; ops endpoints `sort` / `filter` / `find-replace`.
- [ ] Round-trip tests per feature.

## Phase 3 — Rendering (sheet → PDF / PNG / HTML) on Canvas.Pdf
- [ ] `SpreadsheetRenderer`: PDF (gridlines, print area, paging, freeze headers), HTML `<table>`, PNG/JPEG
      (rasterize the PDF). `POST /api/spreadsheet/render?format=pdf|png|html|jpeg`. Tests.

## Phase 4 — Format breadth
- [ ] Backend CSV/TSV; `.xls` read/write via NPOI ↔ `SpreadsheetDto`; HTML import; `.ods` deferred.
      `format` param on `/import` + `/export`; tests.

## Phase 5 — DataTable / templating / polish
- [ ] DataTable/JSON-array ⇄ sheet; smart-marker templating (reuse `CanvasExpressionEvaluator`); streaming
      note + streaming CSV; docs (DocsPage Spreadsheets + backend-capabilities note).

## Deferred
Charts, pivot tables, images/shapes, digital signatures, full 1M-row streaming engine.

## Verification
- `dotnet build Canvas.sln` clean; `Canvas.Export.Tests` green (recalc, each feature round-trip, `.xls`,
  render PDF/PNG/HTML).
- Live (`:5086`): `/calculate` returns computed values; `/render?format=pdf` → `%PDF`; `/import` `.xls` → DTO.
