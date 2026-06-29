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
- [ ] (Optional, deferred) `POST /api/spreadsheet/validate` + import major-version warning.

## Pillar B — Code-library migration (Roslyn rewriters → Canvas authoring API)
- [ ] **B1 Canvas spreadsheet authoring API (the rewrite target)** — new
      `src/Canvas.Infrastructure.Spreadsheet/CanvasWorkbookBuilder.cs` fluent `CanvasWorkbook`/`CanvasWorksheet`
      wrapping `SpreadsheetDto` + `ExcelWorkbookExporter`/`SpreadsheetCalculator`. Common ops:
      `AddSheet`, `Cell("A1")`/`Cell(r,c)`, `.Value()`/`.Formula()`/`.Style(...)`/`.NumberFormat()`,
      `Range(..).Merge()`, `Column(i).Width()`, `Save("x.xlsx")`, `.ToWorkbook()` (DTO/JSON), `.ToPdf()`.
      Unit tests (build → export `.xlsx`; `/calculate` computes a fluent-written formula).
- [ ] **B2/B3 ClosedXML** (reference impl, lowest-risk) — `Canvas.Migration.ClosedXmlSpreadsheet` (Roslyn
      rewriter subclass of `CSharpSourceMigration`, clone `GemBoxPdfMigration` structure), diag prefix
      `CANMIGCLXL`; `ICodeConverter` in `Canvas.WebApi/Services/Converters/`, registered in `MigrationService`;
      rewriter unit test + endpoint smoke. Mapping: `Worksheets.Add`→`AddSheet`; `Cell("A1").Value=`→`.Cell("A1").Value()`;
      `Range(..).Merge()`; `SaveAs`→`Save`.
- [ ] **B2/B3 EPPlus** — `Canvas.Migration.EpplusSpreadsheet`, `CANMIGEPPL`. `pkg.Workbook.Worksheets.Add`;
      `Cells["A1"].Value=`; `Cells["A1:B1"].Merge=true`; `pkg.SaveAs`.
- [ ] **B2/B3 GemBox.Spreadsheet** — `Canvas.Migration.GemBoxSpreadsheet`, `CANMIGGBSS`. `new ExcelFile()`;
      `Worksheets.Add`; `Cells["A1"].Value=`/`[r,c]`; `Cells[..].Style.Font.Weight`; `wb.Save`.
- [ ] **B2/B3 Aspose.Cells** — `Canvas.Migration.AsposeCells`, `CANMIGASPC`. `new Workbook()`;
      `Worksheets[0]`; `Cells["A1"].PutValue(..)`/`[r,c]`; `wb.Save`.
- [ ] Each rewriter emits `MigrationResult { MigratedCode, Diagnostics }`; unsupported calls (charts,
      pivots, exotic Aspose functions) → `Warning`/`Error` diagnostics, never silent drops. `GeneratePreview`
      best-effort (build example via `CanvasWorkbook` → render); ship `Status="skeleton"` until sample-validated.
- [ ] **B4 Per-library checklists** — `checklists/Spreadsheet-Migration-<Lib>.md` each (detection, API
      mapping table, diagnostics range, status), analogue of `Designer-Migration-<Name>.md`.

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
