# Spreadsheet Polish — remaining follow-ups

Closes the loose ends after the backend engine + migration suite + Workbook JSON: validation/versioning,
deeper migration fidelity, and surfacing the now-lossless advanced features in the editor UI. Builds on
`Spreadsheet-Backend-Engine.md`, `Spreadsheet-Migration.md`. Phase-by-phase, commit each.

## Phase 1 — Validation & versioning — DONE
- [x] `SpreadsheetValidator` — structural + version checks → `{ severity, path, message }` (unknown cell
      type, formula missing `=`, negative row/col, unparseable merge, newer major `schemaVersion`).
- [x] `POST /api/spreadsheet/validate` → `{ valid, version, supportedVersion, issues[] }`. DI + test +
      live-verified. **Surfaced in the editor**: a toolbar "Validate" button (`SpreadsheetService.validate`)
      shows a dismissible result banner (valid ✓ / error+warning counts + the first issues with their paths).
- [x] Frontend `jsonToWorkbook` throws a clear error on a newer major `schemaVersion`
      (`CURRENT_SCHEMA_VERSION = '1.0'`); jest covers it. (8 io tests green.)
- [x] `.ods`: no solid .NET ODS writer — stays deferred (documented here + in Spreadsheet-Migration).

## Phase 2 — Deeper migration fidelity (rewriters) — DONE
- [x] **Horizontal alignment** → `.Style(s => s.Align("left|center|right"))`: ClosedXML
      `Style.Alignment.Horizontal` + EPPlus `Style.HorizontalAlignment` (enum rightmost name → value).
- [x] **Fill/background color** (ClosedXML `Style.Fill.BackgroundColor = XLColor.Name`) → `.Style(s =>
      s.Background("#RRGGBB"))` via a named-color lookup (~16 common names); unknown colors → `CANMIGCLXL023`.
- [x] **Named ranges**: `PxaWorkbook.DefineName(name, refersTo)` builder method; ClosedXML
      `wb.NamedRanges.Add("X", "Sheet!A1")` → `wb.DefineName(...)`.
- [x] Tests added (ClosedXML 4, EPPlus 4, GemBox 3). Horizontal alignment now covered for ClosedXML +
      EPPlus + **GemBox** (`Style.HorizontalAlignment`). Aspose alignment + GemBox/Aspose fill colour stay
      diagnosed (detached `GetStyle/SetStyle` and `FillPattern.SetSolid` idioms diverge).

## Phase 3 — Editor UI for advanced features (make the lossless format editable) — DONE (CF/validation deferred)
- [x] Store actions `setCellMeta(row,col,{comment,hyperlink})` + `patchSheet({pageSetup,protection,autoFilterRange})`
      (undo-snapshotted, like `setFrozen`).
- [x] Toolbar **Cell ▾** popover — comment (textarea) + hyperlink on the active cell.
- [x] Toolbar **Sheet ▾** popover — page setup (orientation, header, footer), protection toggle,
      auto-filter range — wired to the `SheetState` passthrough fields (no new model).
- [x] `tsc` clean; jest `setCellMeta + patchSheet land in the exported wire` (22 frontend tests green) — the
      lossless wire (Pillar A2) + backend exporter carry them through `.xlsx`.
- [x] Range-scoped **conditional formatting** + **data validation** rule editors — toolbar "Rules ▾" popover
      lists/removes existing rules and adds new ones to the current selection (`addConditionalFormat`/
      `removeConditionalFormat`/`addDataValidation`/`removeDataValidation` store actions). CF: cellIs
      (operator+value+value2) / colorScale + colour; DV: list (comma source) / numeric (operator+value1/2).
      Store test confirms add/remove reach the wire (13 store tests green). In-place edit / colorScale dual
      colours / dataBar remain out of scope.

## Verification
- `dotnet build PXA.sln` clean; `PXA.Export.Tests` + migration test projects green; frontend `tsc` +
  `jest` green. Live (`:5086`): `/validate` flags a bad workbook; a migrated sample carries fill/alignment +
  named ranges; the editor sets page setup/CF/validation that survives an `.xlsx` round-trip.

## Deferred
`.ods` (no solid .NET writer); charts/pivots; executing migrated code for live preview.
