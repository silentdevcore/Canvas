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

## Phase 2 — Rich Excel features (model + ClosedXML round-trip) — DONE
- [x] Model: `PageSetupDto`, `ProtectionDto`, `AutoFilterRange`, `ConditionalFormatDto[]`,
      `DataValidationDto[]`, `OutlineLevel` on column/row, `Comment`/`Hyperlink` on cells.
- [x] Exporter maps all (page setup, protect, auto-filter, comments, hyperlinks, grouping, + cellIs/
      colorScale conditional formats, list/number data validations). Importer round-trips page setup,
      auto-filter, comments, hyperlinks, protection, grouping (CF/validation are export-only/best-effort).
- [x] `SpreadsheetOperations` (sort range by key column; find/replace across text + formulas) + endpoints
      `POST /api/spreadsheet/sort` and `/find-replace`. DI registered.
- [x] Tests: `RichFeatures_RoundTrip`, `SortRange_OrdersRowsByKeyColumn`, `FindReplace_ReplacesTextAndFormulas`.
      Full Export suite **198** green.

## Phase 3 — Rendering (sheet → PDF / PNG / HTML) on Canvas.Pdf — DONE
- [x] `POST /api/spreadsheet/render?format=pdf|html|png|jpeg&sheet=0` — maps the sheet to a **gridlined**
      table (`SpreadsheetToDesignConverter(..., gridlines: true)` → header row + light borders) and reuses
      the existing renderers: PDF via `DesignJsonMapper.MapToPdfDocument` (Canvas.Pdf), html/png/jpeg via
      `ExportDocumentUseCase`. No new rendering code.
- [x] Unit test for the gridlines option; **live-verified**: render → `%PDF` (1355 B), `<!DOCTYPE html>`,
      PNG (`89504e47`).

## Phase 4 — Format breadth — DONE
- [x] `XlsWorkbookIo` — legacy `.xls` (BIFF8) read/write via **NPOI 2.8** ↔ `SpreadsheetDto` (values,
      formulas, merges, column widths; styles not carried for `.xls`). `CsvSheetIo` — server-side RFC-4180
      CSV export (computed values) + import (number/text detection).
- [x] `/export?format=xlsx|xls|csv`; `/import` dispatches by file extension (`.xlsx`/`.xls`/`.csv`). DI
      registered. Tests: `.xls` round-trip (values/formula/merge), CSV quoting/types. `.ods`/HTML-import
      deferred (no good .NET ODS writer; the frontend already parses HTML/CSV).

## Phase 5 — DataTable / templating / polish
- [ ] DataTable/JSON-array ⇄ sheet; smart-marker templating (reuse `CanvasExpressionEvaluator`); streaming
      note + streaming CSV; docs (DocsPage Spreadsheets + backend-capabilities note).

## Deferred
Charts, pivot tables, images/shapes, digital signatures, full 1M-row streaming engine.

## Verification
- `dotnet build Canvas.sln` clean; `Canvas.Export.Tests` green (recalc, each feature round-trip, `.xls`,
  render PDF/PNG/HTML).
- Live (`:5086`): `/calculate` returns computed values; `/render?format=pdf` → `%PDF`; `/import` `.xls` → DTO.
