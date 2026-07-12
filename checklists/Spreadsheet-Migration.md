# Spreadsheet Framework Migration + Canonical Workbook JSON

Migrate C# code from other spreadsheet libraries (ClosedXML, EPPlus, GemBox.Spreadsheet, Aspose.Cells)
into PXA spreadsheet code, and formalize a **versioned "PXA Workbook JSON"** as the canonical
interchange + migration target. Built in-house, reusing the existing Roslyn PDF-migration architecture.

## Context
- **De-facto JSON exists but is informal:** `SpreadsheetDto` serialized camelCase *is* the workbook JSON,
  but has no version field, no `$schema`, no published JSON Schema (only designs do:
  `docs/schema/design-export.schema.json`), and the frontend wire type
  (`ui-designer-v2/src/spreadsheet/types.ts`) has drifted to a **subset** — it omits every Phase-2 field
  (pageSetup, protection, conditionalFormats, dataValidations, column/row `outlineLevel`, cell
  `comment`/`hyperlink`). Editor-saved JSON is lossy vs. what the backend holds.
- **Migration architecture exists — for PDF, not spreadsheets:** `PXA.Migration.Roslyn/CSharpSourceMigration.cs`
  (base) → per-library `CSharpSyntaxRewriter` (e.g. `GemBoxPdfMigration`) → emits PXA.Pdf code;
  `ICodeConverter` registered in `MigrationService`, exposed at `GET /api/migration/frameworks` +
  `POST /api/migration/convert`. 15 PDF libs ship this way. **No spreadsheet migration, and no fluent
  PXA spreadsheet authoring API to rewrite *into*.**

### Decisions (confirmed)
- Code-library migration (not file-format). Formalize a versioned PXA Workbook JSON. All four sources:
  ClosedXML, EPPlus, GemBox.Spreadsheet, Aspose.Cells.

> **Diagnostic to surface:** ClosedXML's formula engine covers common functions but not Aspose's ~450 —
> exotic Aspose code migrates structurally but may compute differently / `#ERROR` on `/calculate`.

---

## Pillar A — Canonical "PXA Workbook JSON" — DONE (commit pending)
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

## Pillar B — Code-library migration (Roslyn rewriters → PXA authoring API)
- [x] **B1 PXA spreadsheet authoring API (the rewrite target)** — `PxaWorkbookBuilder.cs`:
      `PxaWorkbook` (`AddSheet`, `Sheet`, `ToWorkbook`, `ToXlsx`, `Save` by extension xlsx/xls/csv/tsv),
      `PxaWorksheet` (`Cell("A1")`/`Cell(r,c)`, `Range(..).Merge()`, `Column(i).Width/Hidden/OutlineLevel`,
      `Freeze`), `PxaCell` (`Value` w/ type inference, `Formula`, `NumberFormat`, `Comment`, `Hyperlink`,
      `Style(s => ...)`), `PxaCellStyle` (Bold/Italic/Background/Color/Font/FontSize/Align). Test
      `PxaWorkbook_FluentApi_BuildsCalculableWorkbook` (build → .xlsx round-trip + `/calculate` = 20).
- [x] **B2/B3 ClosedXML** (reference impl) — `PXA.Migration.ClosedXmlSpreadsheet` (Roslyn rewriter),
      `CANMIGCLXL`; `ClosedXmlSpreadsheetConverter` registered in `MigrationService` (Status `full`). Handles
      new workbook, AddSheet, Value/Formula/Width/Height property→method, Bold/Italic/FontSize style lambdas,
      1-based→0-based index shift, SaveAs→Save, usings swap; diagnostics for the rest. 3 tests + live convert.
      See `checklists/Spreadsheet-Migration-ClosedXML.md`.
- [x] **B2/B3 EPPlus** — `PXA.Migration.EpplusSpreadsheet`, `CANMIGEPPL`. `Cells[..]` indexer→`Cell(..)`,
      `pkg.Workbook.Worksheets.Add`→`AddSheet`, `Merge=true`→`Range(..).Merge()`, value/formula/style/SaveAs,
      index shift. Converter registered; 3 tests. See `checklists/Spreadsheet-Migration-EPPlus.md`.
- [x] **B2/B3 GemBox.Spreadsheet** — `PXA.Migration.GemBoxSpreadsheet`, `CANMIGGBSS`. Drops SetLicense;
      ExcelFile→PxaWorkbook, Cells[..]→Cell(..) (0-based, no shift), Font.Weight→Bold(), value/formula/save.
      Converter registered; 2 tests. See `checklists/Spreadsheet-Migration-GemBox.md`.
- [x] **B2/B3 Aspose.Cells** — `PXA.Migration.AsposeCells`, `CANMIGASPC`. Workbook→PxaWorkbook,
      `Worksheets[0]`→`AddSheet`, `Cells[..]`→`Cell(..)` (0-based), `PutValue`→`Value`, Formula→method,
      `SetColumnWidth`→`Column().Width()`, save; GetStyle/SetStyle + charts diagnosed. Registered; 2 tests.
      See `checklists/Spreadsheet-Migration-Aspose.md`.
- [x] Each rewriter emits `MigrationResult { MigratedCode, Diagnostics }`; unsupported calls become
      `Warning`/`Info` diagnostics, never silent drops. `GeneratePreview` uses the base informational page.
- [x] **B4 Per-library checklists** — `Spreadsheet-Migration-{ClosedXML,EPPlus,GemBox,Aspose}.md`.

## Status — all done
All four converters live at `GET /api/migration/frameworks` + `POST /api/migration/convert` (Status `full`).
10 migration unit tests green; `dotnet build PXA.sln` clean. Live-verified each emits PXA code.

## Follow-up — migration list categorization — DONE
- [x] `ICodeConverter.Kind` ("pdf" default | "spreadsheet"); exposed via `FrameworkInfo.Kind` + the
      `/frameworks` endpoint (`kind`). New `BaseSpreadsheetConverter` (the 4 extend it) sets Kind +
      renders a spreadsheet-appropriate preview (sheets/cells counts, not PDF draw-call replay).
- [x] Frontend `MigrationsPage`: `<optgroup>` split ("PDF libraries → PXA.Pdf" vs "Spreadsheet libraries
      → PXA spreadsheet"); output pane labels "PXA Spreadsheet Code" for spreadsheet kind.
- [x] Live: 15 pdf + 4 spreadsheet tagged; spreadsheet `/preview` → valid `%PDF`. tsc clean.

## Follow-up — split Migrations into PDF and Spreadsheet domains (+ grid previewer)
Reorganize Migrations **domain-first** (replacing the Code-vs-Format split). Two top-level areas, each with
its own sub-tabs; Spreadsheet Code Migration gets its own view + a non-PDF previewer. Plan mirrored from
`~/.claude/plans/can-you-analyse-migrations-valiant-beaver.md`.

- **PDF Migration** (`/migrations/pdf`): Code (`/pdf/code`, 15 libs → PXA.Pdf) · UI-Designer (`/pdf/designer`, report files → design).
- **Spreadsheet Migration** (`/migrations/spreadsheet`): Code (`/spreadsheet/code`, 4 libs → PXA spreadsheet API, **grid preview**) · Datasource (`/spreadsheet/datasource`, .xlsx/.csv → workbook).
- Document importer stays standalone at `/importer` (out of scope).

- [x] Routing (`App.tsx`): `/migrations/pdf/{code,designer}`, `/migrations/spreadsheet/{code,datasource}`,
      `/importer` standalone + back-compat redirects (`/migrations/code*`, `/migrations/designer`, `/migrations/format/*`).
- [x] Hub (`MigrationsHubPage.tsx`): two cards — PDF Migration, Spreadsheet Migration.
- [x] `MigrationTabs.tsx`: `pdfTabs(code|designer)` + `sheetTabs(code|datasource)` (replaced `formatTabs`).
- [x] `MigrationsPage.tsx`: domain sub-tab bar (derived from `codeKind`/`mode`); spreadsheet-preview branch
      ("Spreadsheet Preview" label, iframe renders the returned HTML grid, "Open in PDF Viewer" hidden).
- [x] Backend grid previewer: `BaseSpreadsheetConverter.GeneratePreview` → `ReplaySpreadsheetCalls`
      (`AddSheet`/`Cell("A1"|r,c).Value`/`Formula`) → styled HTML `<table>`; `MigrationController` + `MigrationService.GetKind`
      set content-type (`text/html` spreadsheet, `application/pdf` pdf).
- [x] `SpreadsheetImportPage` → `sheetTabs('datasource')`; `ImporterPage` standalone (format tabs dropped, nav restored).
- [x] Verified: tsc + vite build + `dotnet build PXA.sln` clean; live — spreadsheet `/preview` → `text/html`
      with `<table>` (Item/Coffee/SUM); PDF `/preview` → `application/pdf`.

## Sequencing (phase-by-phase, commit each)
1. Pillar A (A1→A4). 2. B1 authoring API. 3. ClosedXML (B2/B3 + checklist). 4. EPPlus → GemBox → Aspose.
5. Wrap-up docs (Migration page lists the 4 new spreadsheet sources).

## Critical files
- **Format:** `src/PXA.Core/Contracts/SpreadsheetDto.cs`; `docs/schema/canvas-workbook.schema.json` (new);
  `ui-designer-v2/src/spreadsheet/{types.ts,io.ts,store}`; `ui-designer-v2/src/pages/DocsPage.tsx`;
  `llms.txt`; `tools/PXA.Mcp`.
- **Authoring API:** `src/PXA.Infrastructure.Spreadsheet/PxaWorkbookBuilder.cs` (new).
- **Migration (reuse):** `src/PXA.Migration.Roslyn/CSharpSourceMigration.cs`;
  clone `src/PXA.Migration.GemBoxPdf/GemBoxPdfMigration.cs`; `PXA.Migration.Abstractions` (`MigrationDiagnostic`);
  new `src/PXA.Migration.<Lib>Spreadsheet/`; `PXA.WebApi/Services/{ICodeConverter.cs,MigrationService.cs,Converters/}`.

## Verification
- **Format:** `PXA.Export.Tests` JSON round-trip preserves Phase-2 fields + `schemaVersion`; sample
  validates against `canvas-workbook.schema.json`. Frontend `jest` round-trip lossless; `tsc` clean.
- **Authoring API:** unit test builds via `PxaWorkbook` → `.xlsx`; `/calculate` computes a fluent formula.
- **Migration:** per-lib unit test (source snippet → expected PXA code + diagnostics, e.g. chart →
  `CANMIG…` Warning). Live: `/api/migration/frameworks` lists 4 new sources; `/api/migration/convert`
  returns PXA code for a ClosedXML sample. `dotnet build PXA.sln` clean; full suite green.

## Deferred
File-format migration (`.ods`, Google Sheets, SpreadsheetML 2003, `.numbers`); charts + pivots in the
authoring API; executing arbitrary migrated user code for live preview (preview stays example/best-effort).
