# PXA Designer "Canvas" Legacy Key Audit Checklist

## Goal

Audit `pxa-designer/` for remaining "canvas" occurrences against `checklists/Rename-To-PXA.md`'s compatibility rules, and close the real gaps found (not just cosmetic ones).

## Context

`checklists/Rename-To-PXA.md` (1128 lines) declares the PXA rename complete, with remaining "canvas" text limited to "cosmetic prose... and legacy localStorage keys" (with PXA-primary + Canvas-legacy-fallback keys implied as the intended final shape, per its last entry). Auditing the actual current source found that claim mostly holds, but not entirely.

## Audit findings

- [x] Confirmed legitimate, protected, no action needed: HTML `<canvas>` elements (`LivePreview.tsx`, `SimplePxaSurface.tsx` barcode rendering), the `html2canvas` library (`ExportService.ts`), third-party API names appearing in migration code samples (`PdfCanvas`, `page.Canvas` — iText7/Spire.PDF, `MigrationsPage.tsx`), CSS/comments describing the document editing surface (`.editor-canvas-element` and friends in `editor.css`, `PxaSurface.tsx`, `SimplePxaSurface.tsx`, `HelpModal.tsx`, `ElementBoundary.tsx`, `elementCatalog.ts`, `DocsPage.tsx`), "off-canvas drawer" as a UI-pattern term (`hub-layout.css`), and template sample content quoting the "blank canvas" idiom (`templateContent.ts`).
- [x] **Correction**: "Blank canvas" as a *visible* feature label (`IndexPage.tsx`'s home card, `useTemplateLoader.ts`'s stat string, `DocsPage.tsx`'s quick-start copy, `home.css` comment) was originally classified above as a protected feature label — that was wrong. Unlike the generic drawing-surface usages above, this is user-facing product copy that echoes the old "Canvas" brand name, and the rest of the app already says "blank document" consistently (`IndexPage.tsx`'s `TOOL_LINKS` copy: "Start a blank document…"). Renamed to "Blank document" in all four places; see Fixes.
- [x] Confirmed already correctly migrated (new `pxa-` key primary, `??` fallback read from the old `canvas-` key): `LiveCodeEditor.tsx` (`pxa-code-editor-draft-v2` / legacy `canvas-code-editor-draft-v2`, plus the lang key), `ExportModal.tsx` (`LAST_FORMAT_KEY` / legacy `canvas_export_format`).
- [x] **Gap found**: `useTemplateLoader.ts` (`loadTemplate`/`loadBlank`) and `IndexPage.tsx`'s `UsageStrip` read/write `canvas_docs_opened`/`canvas_last_template` only — no `pxa_` primary key exists.
- [x] **Real bug found** (not just naming): `MigrationsPage.tsx`'s report-designer hand-off flow writes `pxa_last_template` — a key nothing else reads — so the "last opened template" Home stat silently never updates for templates loaded via migration hand-off.
- [x] **Gap found**: `spreadsheet/store.ts`'s Zustand `persist` middleware uses `name: 'canvas-spreadsheet'` as the real localStorage key holding a user's in-progress spreadsheet draft, with no `pxa-` migration.
- [x] **Confirmed safe to rename outright** (no persistence, no compatibility concern): `'canvas-imported-font-faces-preview'`/`'canvas-imported-font-faces-editor'` in `LivePreview.tsx`/`SimplePxaSurface.tsx` are ephemeral DOM `<style>` element ids only (confirmed via `utils/importedFonts.ts`), never read back across sessions.

## Fixes

- [x] `src/hooks/useTemplateLoader.ts`: `loadTemplate` and `loadBlank` write `pxa_docs_opened`/`pxa_last_template` (new primary); reads fall back to `canvas_docs_opened`/`canvas_last_template` for values written before this change.
- [x] `src/pages/IndexPage.tsx`'s `UsageStrip`: same read-with-fallback change for both keys.
- [x] `src/pages/MigrationsPage.tsx`: no change needed — it already wrote the intended `pxa_last_template` name; Step 2 gives it a consistent reader.
- [x] `src/spreadsheet/store.ts`: added a custom `StateStorage` (`spreadsheetStorage`) whose `getItem` reads `pxa-spreadsheet` falling back to `canvas-spreadsheet`, wired in via `storage: createJSONStorage(...)`; renamed `name: 'canvas-spreadsheet'` → `name: 'pxa-spreadsheet'`. The `createJSONStorage` factory throws when `localStorage` is undefined (mirroring zustand's own default-storage guard) so `persist` degrades to a no-op in the Jest/node test environment instead of throwing `ReferenceError: localStorage is not defined`.
- [x] `src/components/Preview/LivePreview.tsx` + `src/components/Editor/SimplePxaSurface.tsx`: renamed the `installImportedFontFaces(...)` id strings to `pxa-imported-font-faces-preview`/`pxa-imported-font-faces-editor` outright.
- [x] `src/pages/IndexPage.tsx` (home card label), `src/hooks/useTemplateLoader.ts` (`recordDocOpened('Blank canvas')` stat string), `src/pages/DocsPage.tsx` (quick-start copy), `src/styles/home.css` (comment): renamed "Blank canvas" → "Blank document" outright — visible user-facing copy, not a persisted key, so no fallback needed.

## Explicitly out of scope

- [x] Every "confirmed legitimate" item in the Audit Findings section above — not touched, per `Rename-To-PXA.md`'s own compatibility rules (don't rename HTML canvas, `html2canvas`, third-party API names, or generic drawing-surface terminology).

## Verification

- [x] `cd pxa-designer && npx tsc --noEmit` clean.
- [x] `npm run build` clean.
- [x] `npm run test` — full suite passes, 19 suites / 209 tests (had to add the `localStorage`-availability guard above after an initial run surfaced 13 failures in `spreadsheetStore.test.ts`, which runs in a plain node/non-jsdom Jest environment).
- [ ] Manual check (no browser available in this environment) — recommend: usage stats still appear/update on Home after loading a template; loading a template via report-designer migration hand-off now correctly updates Home's "last opened template" (previously silently broken); an existing spreadsheet draft saved under the old `canvas-spreadsheet` key still opens correctly after this change.
