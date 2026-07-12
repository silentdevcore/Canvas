# Migration Types: Code Migration + DataSource / Format Migration

Reorganizes the Migrations area into two clear **types**, each with its own view + sub-tabs. Frontend-only IA
change; reuses existing converters/endpoints (no backend rewrite). See plan
`~/.claude/plans/can-you-analyse-migrations-valiant-beaver.md`.

## Taxonomy
- **Code Migration** (`/migrations/code`) — library C# → PXA code. Sub-tabs: **PDF** (15) | **Spreadsheet** (4),
  filtered by the backend `kind` field.
- **DataSource / Format Migration** (`/migrations/format`) — a source file/format → PXA. Sub-tabs:
  **Report designers** (→ design) | **Documents** (.pdf/.docx/.pptx/.odt/img → design) | **Spreadsheets**
  (.xlsx/.xls/.csv → workbook).

## Tasks — DONE
- [x] `components/Migrations/MigrationTabs.tsx` — shared sub-tab bar (`tabs[]` w/ `to`|`onClick`, active) +
      `formatTabs(active)` helper + `.mgr-subtabs` CSS.
- [x] `MigrationsHubPage.tsx` — two type cards (Code → `/migrations/code`, Format → `/migrations/format`).
- [x] `MigrationsPage.tsx` (mode `code`) — **route-based** PDF | Spreadsheet sub-tabs labeled
      "PDF Migration" / "Spreadsheet Migration" at `/migrations/code/pdf` + `/migrations/code/spreadsheet`
      (`codeKind` prop drives `kindFilter`; an effect keeps the selected framework valid). Optgroup removed.
- [x] `MigrationsPage.tsx` (mode `designer`) — Format sub-tab bar (active = Report designers); heading renamed.
- [x] `ImporterPage.tsx` — Format sub-tab bar (active = Documents); `activePage="migrations"`.
- [x] `SpreadsheetImportPage.tsx` (new) — .xlsx/.xls/.csv/.tsv/.json uploader → `SpreadsheetService.importXlsx`
      / `jsonToWorkbook` → `loadWorkbook` → `navigate('/spreadsheet')` + Format sub-tab bar.
- [x] `App.tsx` — `/migrations/format` → redirect designer; `/migrations/format/{designer,documents,spreadsheet}`;
      back-compat `/migrations/designer` → `…/format/designer`, `/importer` → `…/format/documents`.
- [x] `AppHeader.tsx` — removed the standalone Importer nav (folded into Migrations).

## Verification — DONE
- `npx tsc --noEmit` clean; `npm run build` ✓ (874ms). No backend changes.

## Out of scope
Backend `category` API; net-new datasource/connection migration; converter/endpoint logic changes.
