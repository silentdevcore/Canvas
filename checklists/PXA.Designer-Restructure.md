# PXA Designer Navigation Restructure Checklist

## Goal

Restructure PXA Designer's navigation: collapse the top nav from 7 items down to **Home, PDF, Spreadsheet, Documentation**, and turn PDF and Spreadsheet into hubs with their own collapsible (enable/disable) sidebars.

## Context

Exploration of `src/App.tsx`, `src/pages/*.tsx`, and `AppHeader.tsx` found almost every sidebar item already maps to an existing, working page — this was primarily a **routing/layout restructure**, not a rebuild. Full findings, mapping table, and reasoning live in the approved plan; this checklist tracks execution.

## Item Mapping

- [x] Create PDF → `CreatePage` blank (`/pdf/create`). Edit PDF → `CreatePage`'s `LiveCodeEditor` sub-view — already supported via the existing `?mode=code` search param it reads on mount, so `/pdf/edit` is just a `Navigate` redirect to `/pdf/create?mode=code`, zero changes to `CreatePage.tsx`. Use Template → `TemplatePage` (`/pdf/template`). Import PDF → `ImporterPage` (`/pdf/import`). Convert to PDF → new `ConvertToPdfPage` (`/pdf/convert`). PDF Viewer → `PdfViewerPage` (`/pdf/viewer`, still lazy). Migrations → `/pdf/migrations` → redirects to `/pdf/migrations/code` (`MigrationsPage mode="code" codeKind="pdf"`); `/pdf/migrations/designer` also available (not in the sidebar list itself, reachable from within the migrations view's own tabs).
- [x] Spreadsheet: Create → `SpreadsheetEditorPage` blank (`/spreadsheet/create`, still lazy). Edit → `SpreadsheetImportPage` with a new `variant="edit"` prop narrowing the file picker to `.json` (PXA Workbook) only, at `/spreadsheet/edit`. Import → the same component with `variant="import"` (default), accepting `.xlsx/.xls/.csv/.tsv/.json`, at `/spreadsheet/import`. Convert to Spreadsheet → disabled "Coming soon" sidebar entry, no route. Migrations → `/spreadsheet/migrations` → redirects to `/spreadsheet/migrations/code` (`MigrationsPage mode="code" codeKind="spreadsheet"`).
- [x] **Found during implementation**: `MigrationsHubPage.tsx` (the old `/migrations` domain-picker showing "PDF Migration"/"Spreadsheet Migration" cards) became genuinely unreachable once each hub's own sidebar makes that domain choice by which hub you're in — deleted it (confirmed via repo-wide grep it had no other references) rather than leave dead code behind.

## Phase 1 — Convert to PDF extraction

- [x] New `src/pages/ConvertToPdfPage.tsx`: extracted the image→PDF/OCR branch out of `ImporterPage.tsx` (page-size + OCR-language/preprocessing/confidence/layout options), calling the existing `ExportService.downloadImageOcrPdf` unchanged. Scoped to the download-PDF action only (dropped the old dual-purpose "open as editable design" option for this format — a page literally called "Convert to PDF" should only produce a PDF; opening an image as an editable design is still covered by Importer's plain "Image"/"Image (Smart)" formats). Reuses the existing `.importer-config-*`/`.importer-error`/`.importer-status*` CSS classes verbatim (already amber-themed) plus one small new `.importer-config-panel--standalone` modifier (full border-radius, since there's no card above it on this page).
- [x] `ImporterPage.tsx`: removed the extracted "Image OCR to PDF" format entirely; the other 8 formats (PDF/DOCX/PPTX/DOC/ODT/SVG/Image/Image-Smart) work exactly as before, and the config-panel JSX no longer needs its OCR-vs-else branching.
- [x] No changes to `ExportService.ts` itself.

## Phase 2 — Hub layout + sidebar component

- [x] New shared `src/components/Layout/HubSidebar.tsx` (+ `src/hooks/useSidebarCollapsed.ts`, + `src/styles/hub-layout.css`, imported from `index.css`), parameterized by an item list (`{ path, label, disabled? }`), used by both PDF and Spreadsheet hubs.
- [x] New `src/components/Layout/PdfLayout.tsx` and `SpreadsheetLayout.tsx`: each renders `AppHeader` once, `HubSidebar`, and an `<Outlet />`.
- [x] Collapse/expand toggle persisted via `localStorage`, one key per hub (`pxa-designer:pdf-sidebar-collapsed` / `...:spreadsheet-sidebar-collapsed`). Desktop: sidebar width animates to 0 when collapsed, with a small floating circular toggle button that stays visible (positioned via absolute + `overflow: visible` on the parent, so it isn't clipped at width 0). Mobile (`≤900px`): off-canvas drawer via `transform: translateX`, plus a click-to-close backdrop — same semantics (`collapsed` always means hidden) on both breakpoints, just a different CSS mechanism per breakpoint.
- [x] Styled entirely with this app's existing `var(--color-primary/-hover)`, `var(--radius-md)`, `var(--shadow-sm/md/lg)`, `var(--color-hairline)`, `var(--color-ink/body/muted)` tokens from the completed visual redesign — no new colors introduced.

## Phase 3 — AppHeader simplification

- [x] `AppHeader.tsx`: `activePage` union is now `'home' | 'pdf' | 'spreadsheet' | 'docs'`; both the desktop nav and the mobile slide-out menu render exactly Home/PDF/Spreadsheet/Documentation.

## Phase 4 — Route tree restructure

- [x] `App.tsx`: nested routes under `/pdf/*` (`create`, `edit`, `template`, `import`, `convert`, `viewer`, `migrations`, `migrations/code`, `migrations/designer`) wrapped in `PdfLayout`, and `/spreadsheet/*` (`create`, `edit`, `import`, `migrations`, `migrations/code`) wrapped in `SpreadsheetLayout`. Each hub has an `index` route redirecting to its own `create`.
- [x] `Navigate` redirects from every old flat route into the new tree, extending the exact pattern this file already used for the old flat migrations URLs: `/template`, `/create`, `/importer`, `/pdf-viewer`, `/migrations`, `/migrations/pdf(+/code,+/designer)`, `/migrations/spreadsheet(+/code,+/datasource)`, and the older `/migrations/code*`, `/migrations/designer`, `/migrations/format*` aliases. `/spreadsheet` itself needed no explicit redirect — it now matches the `SpreadsheetLayout` parent route directly, whose nested `index` route already redirects to `/spreadsheet/create`.
- [x] Removed the self-rendered `<AppHeader activePage="...">` from every page that's now nested inside a hub layout: `TemplatePage`, `ImporterPage`, `PdfViewerPage`, `SpreadsheetEditorPage`, `MigrationsPage`, `SpreadsheetImportPage` (which also dropped its `MigrationTabs` sub-tab bar — that was specific to living inside the old Migrations feature, no longer relevant now that it's a direct hub-sidebar destination). `IndexPage`/`DocsPage` keep their own `AppHeader` since they're not nested in a hub.
- [x] `CreatePage.tsx` still renders no header/chrome itself, but nesting it under `/pdf/*` means it now gains `PdfLayout`'s header + sidebar chrome around it — confirmed intentional, mitigated by the sidebar's collapse toggle.

## Verification

- [x] `cd pxa-designer && npx tsc --noEmit` clean.
- [x] `npm run build` clean (pre-existing chunk-size warning, unrelated).
- [x] `npm run test` (Jest, 19 suites/208 tests) — all pass. Had to update `src/__tests__/appRouteSmoke.test.tsx`: its one test asserting on the now-deleted `MigrationsHubPage`'s content was rewritten to assert on `MigrationsPage`'s "PDF Code Migration" heading instead (plus a `fetch` mock for the frameworks-list effect it triggers on mount, which the old test path never exercised); added two new smoke tests asserting the PDF and Spreadsheet sidebars render every expected item, routed through `/pdf/import` and `/spreadsheet/import` specifically (not the `create` index-redirects) since those are the two entry points already safely mocked in this test file — the real `CreatePage`/`SpreadsheetEditorPage` pull in unmocked dependencies (editor store effects, `@glideapps/glide-data-grid`'s ESM-only markdown dependency) that this test file's existing mock setup doesn't cover, and covering that gap is out of scope for a navigation-restructure pass.
- [ ] Manual `npm run dev` walkthrough — not done, no browser available in this environment. Recommend checking: old bookmarked URLs redirect correctly; PDF/Spreadsheet sidebars list the right items and the collapse toggle works and persists across a reload; `/pdf/edit` opens `CreatePage` with the code editor (not canvas) pre-selected; `/pdf/convert` still successfully generates a PDF from an uploaded image; `/spreadsheet/edit` only accepts `.json` while `/spreadsheet/import` accepts everything.

## Open Decisions (judgment calls made during planning/implementation — revisit if they don't feel right once used)

- [ ] "Edit Spreadsheet" reuses `SpreadsheetImportPage` narrowed to `.json` only, as the closest equivalent to Edit PDF's live inline code editor — not a byte-for-byte parallel experience, since no live-canvas-synced spreadsheet code editor exists today.
- [ ] "Convert to Spreadsheet" ships as a disabled/"Coming soon" sidebar entry rather than a real page, since no existing capability converts an external format into a spreadsheet the way image→PDF/OCR does.
- [ ] Whether to eventually build a real spreadsheet template gallery and/or a standalone read-only spreadsheet viewer, to make the two hubs fully parallel (explicitly out of scope for this pass).
- [ ] `MigrationsHubPage.tsx` was deleted as dead code rather than kept around unreferenced — flag if there was a reason to keep it (e.g. an external link pointing at the old `/migrations` domain-picker specifically) that wasn't visible from within this repo.
