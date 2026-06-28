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
- [x] `/export?format=xlsx|xls|csv|tsv`; `/import` dispatches by file extension (`.xlsx`/`.xls`/`.csv`/`.tsv`,
      TSV reuses `CsvSheetIo` with a tab delimiter). DI registered. Tests: `.xls` round-trip, CSV + TSV
      quoting/types. `.ods`/HTML-import deferred (no good .NET ODS writer; the frontend already parses HTML/CSV).

## Phase 5 — DataTable / templating / polish — DONE
- [x] `SpreadsheetData`: `FromRows` (JSON row objects → bold header + typed rows = DataTable equivalent),
      `Fill` (`{{token}}` placeholder replacement in text cells, dotted paths resolve nested objects).
      Endpoints `POST /api/spreadsheet/from-data` and `/fill`. DI registered.
- [x] Docs: DocsPage "Spreadsheets" updated — round-trip/IO list (xlsx/xls/csv), new **Backend engine**
      subsection (calculate/render/sort/find-replace/from-data/fill), rich-features note, calc note.
- [x] Tests `FromRows_BuildsHeaderAndTypedRows`, `Fill_ReplacesTokens`. Full Export suite **203** green.
      Streaming: ClosedXML is in-memory (practical ~10⁵-row ceiling); a SAX 1M-row engine stays deferred.

## Deferred
Charts, pivot tables, images/shapes, digital signatures, full 1M-row streaming engine.

## Verification — all phases DONE
- `dotnet build Canvas.sln` clean; `Canvas.Export.Tests` **203** green (recalc, each rich-feature round-trip,
  `.xls` + CSV, render, from-data + fill).
- Live (`:5086`): `/calculate` computes chained deps; `/render?format=pdf` → `%PDF`; `/export?format=xls` →
  BIFF; `/from-data` builds a sheet; `/fill` resolves nested `{{user.name}}`.
