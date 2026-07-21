# PXA Designer Home Page Restructure Checklist

## Goal

Restructure PXA Designer's Home page (`IndexPage.tsx`) to full parity with the hub navigation restructure: more description, direct links to every reachable item in both the PDF and Spreadsheet hubs (not just a subset), fixed routing to the new direct paths instead of the old redirect-through routes.

## Context

Home used to link to only 4 of the PDF hub's 7 sidebar items and had **zero presence for Spreadsheet at all** (no link, no section). Every `navigate()` call in `IndexPage.tsx`, and all three exports of `useTemplateLoader.ts` (shared by every page that loads a template), targeted old flat routes that only worked via `App.tsx`'s backward-compat redirects. Full research, mapping tables, and reasoning live in the approved plan; this checklist tracks execution.

## Step 1 — Fix shared routing at the source

- [x] `src/hooks/useTemplateLoader.ts`: all 3 `navigate('/create'...)` calls (in `loadTemplate`, `loadBlank`, `loadFromFile`) now go to `navigate('/pdf/create'...)`, preserving the `?mode=code` query param on the `loadBlank('code')` case. Benefits every caller app-wide, not just Home.

## Step 2 — Fix Home's own remaining stale routes

- [x] Hero "Start from a template" → `/pdf/template`.
- [x] "Browse all templates" toolbar button → `/pdf/template`.
- [x] Category card clicks → `/pdf/template?category=${cat.id}` (direct link, no redirect hop).
- [x] Security strip "Start designing" → `/pdf/template`.
- [x] `handleToolClick`'s `'Edit PDF'` case (now the `Edit PDF` tool item's `path`): repointed from `/template` to `/pdf/edit` — first time this label's destination matches its name.

## Step 3 — Full "PDF tools" grid (replaced the old 4-item `TOOL_LINKS`)

- [x] One card per PDF hub sidebar item, each with a one-sentence description and direct link: Create PDF (`/pdf/create`), Edit PDF (`/pdf/edit`), Use Template (`/pdf/template`), Import PDF (`/pdf/import`), Convert to PDF (`/pdf/convert`), PDF Viewer (`/pdf/viewer`), Migrations (`/pdf/migrations`).
- [x] Kept "Sign DOCX" as an 8th card with its existing informational-toast behavior (no hub-sidebar/route equivalent — an Export-modal action).

## Step 4 — New "Spreadsheet tools" section (first-ever Spreadsheet presence on Home)

- [x] Structurally identical section to PDF tools, new `id="spreadsheet-tools"`: Create Spreadsheet (`/spreadsheet/create`), Edit Spreadsheet (`/spreadsheet/edit`), Import Spreadsheet (`/spreadsheet/import`), Convert to Spreadsheet (disabled/"coming soon", reusing the sidebar's own `.is-coming-soon` treatment), Migrations (`/spreadsheet/migrations`).

## Step 5 — More description

- [x] Added a 4th entry to `FEATURE_CARDS` — "Spreadsheet formulas & formatting" — so the static features section describes the whole product, not just PDF/DOCX.

## Step 6 — Data/dispatch cleanup

- [x] Both tool grids share one `ToolLink` type (`{ label, copy, icon, path?, disabled?, onClick? }`) and one `renderToolGrid()` function — no hand-duplicated JSX between the PDF and Spreadsheet sections.
- [x] `handleToolClick` replaced with a small `(tool: ToolLink) => void` dispatcher: disabled → no-op, `onClick` override → used (only "Sign DOCX"), else `navigate(tool.path)`.
- [x] `src/styles/home.css`: confirmed the existing `.pdf-tool-card.is-coming-soon` rule (already used by `ImporterPage`'s format grid pattern) covers the new disabled "Convert to Spreadsheet" card — no new CSS needed.

## Explicitly out of scope (left as-is)

- [x] Trust band, template category grid layout, security strip layout, `UsageStrip` — untouched beyond the route fixes in Step 2.
- [x] `loadFromFile` not updating `canvas_docs_opened`/`canvas_last_template` (so imported documents don't show in the Usage strip) — pre-existing quirk, not part of this restructure.

## Verification

- [x] `cd pxa-designer && npx tsc --noEmit` clean.
- [x] `npm run build` clean.
- [x] `npm run test` (Jest, 19 suites/209 tests) — all pass. Added a new smoke test in `src/__tests__/appRouteSmoke.test.tsx` asserting Home (`/`) shows both the "Every PDF tool you need" and "Every spreadsheet tool you need" section headings plus representative cards from each grid — a regression guard against this gap reappearing.
- [ ] Manual `npm run dev` walkthrough — not done, no browser available in this environment. Recommend checking: every new/changed card actually navigates where labeled; hero and category-grid clicks still work; "Sign DOCX" toast still fires; the disabled "Convert to Spreadsheet" card is visibly non-interactive and matches the sidebar's own disabled styling.
