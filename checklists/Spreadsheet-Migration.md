# Spreadsheet Framework Migration + Canonical Workbook JSON

Migrate C# code from other spreadsheet libraries (ClosedXML, EPPlus, GemBox.Spreadsheet, Aspose.Cells)
into Canvas spreadsheet code, and formalize a **versioned "Canvas Workbook JSON"** as the canonical
interchange + migration target. Built in-house, reusing the existing Roslyn PDF-migration architecture.

## Context
- **De-facto JSON exists but is informal:** `SpreadsheetDto` serialized camelCase *is* the workbook JSON,
  but has no version field, no `$schema`, no published JSON Schema (only designs do:
  `docs/schema/design-export.schema.json`), and the frontend wire type
  (`ui-designer-v2/src/spreadsheet/types.ts`) has drifted to a **subset** — it omits every Phase-2 field
  (pageSetup, protection, conditionalFormats, dataValidations, column/row `outlineLevel`, cell
  `comment`/`hyperlink`). Editor-saved JSON is lossy vs. what the backend holds.
- **Migration architecture exists — for PDF, not spreadsheets:** `Canvas.Migration.Roslyn/CSharpSourceMigration.cs`
  (base) → per-library `CSharpSyntaxRewriter` (e.g. `GemBoxPdfMigration`) → emits Canvas.Pdf code;
  `ICodeConverter` registered in `MigrationService`, exposed at `GET /api/migration/frameworks` +
  `POST /api/migration/convert`. 15 PDF libs ship this way. **No spreadsheet migration, and no fluent
  Canvas spreadsheet authoring API to rewrite *into*.**

### Decisions (confirmed)
- Code-library migration (not file-format). Formalize a versioned Canvas Workbook JSON. All four sources:
  ClosedXML, EPPlus, GemBox.Spreadsheet, Aspose.Cells.

> **Diagnostic to surface:** ClosedXML's formula engine covers common functions but not Aspose's ~450 —
> exotic Aspose code migrates structurally but may compute differently / `#ERROR` on `/calculate`.

---

## Pillar A — Canonical "Canvas Workbook JSON" — DONE (commit pending)
- [x] **A1 Version the model** — `SchemaVersion` (default `"1.0"`, `const CurrentSchemaVersion`) + optional
      `Schema` (`[JsonPropertyName("$schema")]`) on `SpreadsheetDto`. Defaults keep old payloads loading.
- [x] **A2 Frontend/backend parity (lossless JSON)** — `types.ts` wire types gained `comment`/`hyperlink`
      (Cell), `outlineLevel` (column/row), `autoFilterRange`/`pageSetup`/`protection`/`conditionalFormats`/
      `dataValidations` (sheet), `$schema`/`schemaVersion` (workbook). `SheetState` keeps raw columns/rows +
      advanced fields as passthrough; `sheetToWire` merges col widths over preserved metadata; store threads
      `definedNames`+`schemaVersion`. Jest: advanced fields survive load→save round-trip.
- [x] **A3 Publish JSON Schema** — `docs/schema/canvas-workbook.schema.json` (mirrors design schema). Sync
      test `canvasWorkbookSchema.test.ts` (cell-type enum in lock-step + required-field constraints). 20 jest green.
- [x] **A4 Document** — DocsPage "Workbook JSON (canonical format)" subsection; `llms.txt` entry; MCP
      `workbook-schema` resource (`canvas://schema/canvas-workbook`).
- [x] `POST /api/spreadsheet/validate` + import major-version warning — done in `Spreadsheet-Polish.md`
      Phase 1 (`SpreadsheetValidator`, frontend version guard, editor "Validate" button).

## Pillar B — Code-library migration (Roslyn rewriters → Canvas authoring API)
- [x] **B1 Canvas spreadsheet authoring API (the rewrite target)** — `CanvasWorkbookBuilder.cs`:
      `CanvasWorkbook` (`AddSheet`, `Sheet`, `ToWorkbook`, `ToXlsx`, `Save` by extension xlsx/xls/csv/tsv),
      `CanvasWorksheet` (`Cell("A1")`/`Cell(r,c)`, `Range(..).Merge()`, `Column(i).Width/Hidden/OutlineLevel`,
      `Freeze`), `CanvasCell` (`Value` w/ type inference, `Formula`, `NumberFormat`, `Comment`, `Hyperlink`,
      `Style(s => ...)`), `CanvasCellStyle` (Bold/Italic/Background/Color/Font/FontSize/Align). Test
      `CanvasWorkbook_FluentApi_BuildsCalculableWorkbook` (build → .xlsx round-trip + `/calculate` = 20).
- [x] **B2/B3 ClosedXML** (reference impl) — `Canvas.Migration.ClosedXmlSpreadsheet` (Roslyn rewriter),
      `CANMIGCLXL`; `ClosedXmlSpreadsheetConverter` registered in `MigrationService` (Status `full`). Handles
      new workbook, AddSheet, Value/Formula/Width/Height property→method, Bold/Italic/FontSize style lambdas,
      1-based→0-based index shift, SaveAs→Save, usings swap; diagnostics for the rest. 3 tests + live convert.
      See `checklists/Spreadsheet-Migration-ClosedXML.md`.
- [x] **B2/B3 EPPlus** — `Canvas.Migration.EpplusSpreadsheet`, `CANMIGEPPL`. `Cells[..]` indexer→`Cell(..)`,
      `pkg.Workbook.Worksheets.Add`→`AddSheet`, `Merge=true`→`Range(..).Merge()`, value/formula/style/SaveAs,
      index shift. Converter registered; 3 tests. See `checklists/Spreadsheet-Migration-EPPlus.md`.
- [x] **B2/B3 GemBox.Spreadsheet** — `Canvas.Migration.GemBoxSpreadsheet`, `CANMIGGBSS`. Drops SetLicense;
      ExcelFile→CanvasWorkbook, Cells[..]→Cell(..) (0-based, no shift), Font.Weight→Bold(), value/formula/save.
      Converter registered; 2 tests. See `checklists/Spreadsheet-Migration-GemBox.md`.
- [x] **B2/B3 Aspose.Cells** — `Canvas.Migration.AsposeCells`, `CANMIGASPC`. Workbook→CanvasWorkbook,
      `Worksheets[0]`→`AddSheet`, `Cells[..]`→`Cell(..)` (0-based), `PutValue`→`Value`, Formula→method,
      `SetColumnWidth`→`Column().Width()`, save; GetStyle/SetStyle + charts diagnosed. Registered; 2 tests.
      See `checklists/Spreadsheet-Migration-Aspose.md`.
- [x] Each rewriter emits `MigrationResult { MigratedCode, Diagnostics }`; unsupported calls become
      `Warning`/`Info` diagnostics, never silent drops. `GeneratePreview` uses the base informational page.
- [x] **B4 Per-library checklists** — `Spreadsheet-Migration-{ClosedXML,EPPlus,GemBox,Aspose}.md`.

## Status — all done
All four converters live at `GET /api/migration/frameworks` + `POST /api/migration/convert` (Status `full`).
10 migration unit tests green; `dotnet build Canvas.sln` clean. Live-verified each emits Canvas code.

## Follow-up — migration list categorization — DONE
- [x] `ICodeConverter.Kind` ("pdf" default | "spreadsheet"); exposed via `FrameworkInfo.Kind` + the
      `/frameworks` endpoint (`kind`). New `BaseSpreadsheetConverter` (the 4 extend it) sets Kind +
      renders a spreadsheet-appropriate preview (sheets/cells counts, not PDF draw-call replay).
- [x] Frontend `MigrationsPage`: `<optgroup>` split ("PDF libraries → Canvas.Pdf" vs "Spreadsheet libraries
      → Canvas spreadsheet"); output pane labels "Canvas Spreadsheet Code" for spreadsheet kind.
- [x] Live: 15 pdf + 4 spreadsheet tagged; spreadsheet `/preview` → valid `%PDF`. tsc clean.

## Sequencing (phase-by-phase, commit each)
1. Pillar A (A1→A4). 2. B1 authoring API. 3. ClosedXML (B2/B3 + checklist). 4. EPPlus → GemBox → Aspose.
5. Wrap-up docs (Migration page lists the 4 new spreadsheet sources).

## Critical files
- **Format:** `src/Canvas.Core/Contracts/SpreadsheetDto.cs`; `docs/schema/canvas-workbook.schema.json` (new);
  `ui-designer-v2/src/spreadsheet/{types.ts,io.ts,store}`; `ui-designer-v2/src/pages/DocsPage.tsx`;
  `llms.txt`; `tools/Canvas.Mcp`.
- **Authoring API:** `src/Canvas.Infrastructure.Spreadsheet/CanvasWorkbookBuilder.cs` (new).
- **Migration (reuse):** `src/Canvas.Migration.Roslyn/CSharpSourceMigration.cs`;
  clone `src/Canvas.Migration.GemBoxPdf/GemBoxPdfMigration.cs`; `Canvas.Migration.Abstractions` (`MigrationDiagnostic`);
  new `src/Canvas.Migration.<Lib>Spreadsheet/`; `Canvas.WebApi/Services/{ICodeConverter.cs,MigrationService.cs,Converters/}`.

## Verification
- **Format:** `Canvas.Export.Tests` JSON round-trip preserves Phase-2 fields + `schemaVersion`; sample
  validates against `canvas-workbook.schema.json`. Frontend `jest` round-trip lossless; `tsc` clean.
- **Authoring API:** unit test builds via `CanvasWorkbook` → `.xlsx`; `/calculate` computes a fluent formula.
- **Migration:** per-lib unit test (source snippet → expected Canvas code + diagnostics, e.g. chart →
  `CANMIG…` Warning). Live: `/api/migration/frameworks` lists 4 new sources; `/api/migration/convert`
  returns Canvas code for a ClosedXML sample. `dotnet build Canvas.sln` clean; full suite green.

## Deferred
File-format migration (`.ods`, Google Sheets, SpreadsheetML 2003, `.numbers`); charts + pivots in the
authoring API; executing arbitrary migrated user code for live preview (preview stays example/best-effort).
