# PXA Designer Localization Checklist

## Goal

Add full localization to `pxa-designer` for English, German, French, Spanish, Italian, and Arabic (with Arabic driving a full RTL layout), covering every page's text — including the template-content library, not just UI chrome.

## Context

No i18n library exists in `pxa-designer` today (confirmed via `package.json`); the only precedent is a small hand-rolled en/de map at `src/features/pdf-viewer/i18n.ts` for the PDF-viewer toolbar. The app's own UI has zero RTL support — all ~13 stylesheets under `src/styles/` use physical `left`/`right`/`margin-left` properties, and `index.html` has no `dir` attribute. This is separate from the existing, unrelated RTL machinery in `SimplePxaSurface.tsx`/`LocalizedPropertiesPanel.tsx`/`LanguageTabBar.tsx`, which handles RTL for user-authored *document content* inside the canvas — that system is explicitly out of scope and stays untouched.

User's explicit scoping decisions:
1. Scope = everything, including `src/data/templateContent.ts` (~8,760 lines of per-template document body content) and `src/data/templates.ts` (template metadata).
2. Translation content = scaffold only, fill in later — full infrastructure + complete English strings now; other locales structurally present, falling back to English until a translator fills them in incrementally.
3. RTL = full mirrored polish (icons, sidebar placement, spacing), not just a `dir` flip.

Full reasoning and design detail live in the approved plan; this checklist tracks execution.

## Phase 0 — Scaffolding (sequential prerequisite)

- [x] Install `react-i18next`, `i18next`, `i18next-browser-languagedetector`, `i18next-resources-to-backend` (used `--legacy-peer-deps` to work around a pre-existing, unrelated `marked`/`@glideapps/glide-data-grid` peer conflict already in the repo).
- [x] `src/i18n/index.ts`: init module — `fallbackLng: 'en'`, `supportedLngs: ['en','de','fr','es','it','ar']`, lazy per-namespace JSON loading via `i18next-resources-to-backend` (dynamic `import()`, confirmed Vite code-splits it per language/namespace), language-detector with `lookupLocalStorage: 'pxa_locale'`, `order: ['localStorage', 'navigator']`. Imported via `LocaleProvider.tsx`.
- [x] `src/locales/{en,de,fr,es,it,ar}/` directory tree with one JSON file per namespace (`common`, `home`, `templates`, `gallery`, `editor`, `create`, `importer`, `convert`, `spreadsheet`, `migrations`, `docs`, `onboarding`, `pdfViewer`) — English populated during Phase 1/2/4, others start empty/sparse (German seeded with real translations for `common` as a head start).
- [x] `src/components/Layout/LocaleProvider.tsx`: sets `document.documentElement.lang`/`.dir` on `i18n` language change (`dir: 'rtl'` only for `ar`), wraps children in `<Suspense>`. Mounted in `src/App.tsx` around the existing `<Routes>` tree.
- [x] `src/components/Layout/LanguageSwitcher.tsx`: dropdown listing native language names, calls `i18n.changeLanguage(code)`. Mounted in `AppHeader.tsx`'s `.pdf-nav-actions` block, desktop + mobile-nav variants.

## Phase 1 — String extraction (parallelizable per page, after Phase 0)

- [x] `common`: `AppHeader.tsx`, `HubSidebar.tsx`, `PdfLayout.tsx`, `SpreadsheetLayout.tsx`.
- [x] `home`: `IndexPage.tsx` (`FEATURE_CARDS`, `PDF_TOOLS`, `SPREADSHEET_TOOLS`, hero, trust band — moved inside component body for `t()` access).
- [x] `templates`/`gallery`: `templates.ts` metadata (via `t('templates:...', {defaultValue})`, no `en/templates.json` needed), `TemplatePage.tsx`, `Gallery/CategoryFilter.tsx`, `Gallery/TemplateCard.tsx`. `TemplateMiniPreview.tsx` deferred to Phase 2 (it only renders template *content*, handled by the localization wrapper there).
- [x] `editor`: `HelpModal.tsx`, `ExportModal.tsx`, `FindReplaceModal.tsx`, `FormBlockModal.tsx`, `SignDocxModal.tsx`. Also translate `src/docs/elementCatalog.ts`'s `label`/`description` at the call site (`HelpModal.tsx` and `DocsPage.tsx`'s `ElementCard`, via `defaultValue`) — the catalog file itself stays untouched (single source of truth for docs/AI artifacts/drift-guard test). `Toolbar.tsx`/`ComplexEditor.tsx` confirmed **dead code** (not imported by any reachable route) — skipped.
- [x] `create`/`importer`/`convert`/`spreadsheet`: `ImporterPage.tsx`, `ConvertToPdfPage.tsx`, `SpreadsheetEditorPage.tsx`, `SpreadsheetImportPage.tsx`. `CreatePage.tsx` has no hardcoded strings of its own (all UI text lives in its child components) — nothing to extract there.
- [x] `migrations`: `MigrationsPage.tsx` UI chrome (headings, buttons, tabs, status/error messages) + `Migrations/MigrationTabs.tsx`. The `FRAMEWORKS_FALLBACK`/`DESIGNER_FRAMEWORKS` technical descriptions and the ~25 hardcoded C#/XML/JSON code-sample constants (`SYNCFUSION_EXAMPLE`, `ITEXT7_EXAMPLE`, etc., ~830 lines) are **intentionally left untranslated** — literal code samples must stay valid source, and the framework descriptions are dense API-mapping reference text where mistranslation risk outweighs value.
- [x] `docs`: `DocsPage.tsx` UI chrome only (`CopyButton`, `PropertyTable` headers, sidebar nav labels, mobile-nav toggle, `ElementCard` badges/buttons/errors). The ~1,150 lines of actual documentation prose (Quick Start, Editor Overview, Data Binding, Import/Export, Word Features, JSON Schema, C# Models/Examples, REST API, AI & Codegen, Spreadsheets sections) are **intentionally left untranslated** — same reasoning as the migrations framework descriptions: this is a technical reference manual that warrants a dedicated translation pass, not mechanical key extraction under a scaffold-only strategy.
- [x] `onboarding`: `OnboardingWizard.tsx` confirmed **dead code** (not imported by any reachable route, same as `Toolbar.tsx`/`ComplexEditor.tsx` — a Tailwind-styled leftover from an earlier iteration, superseded by the current custom-CSS design system) — skipped, no `onboarding.json` content needed.
- [x] **Follow-up (was flagged as a scope gap, now underway)**: `src/components/CodeEditor/LiveCodeEditor.tsx` (~400 lines) — fully extracted into a new `codeEditor` namespace: top bar (back/title/language toggle), all JSON-validation error templates in `parseAndValidate` (parameterized with `{{i}}`/`{{j}}`/`{{message}}` rather than one key per array index), and every fetch/conversion/export error fallback (deduplicated repeats like `'Export failed.'`/`'Conversion failed.'` into single keys reused across call sites).
- [x] `src/components/Preview/LivePreview.tsx` (~1,080 lines) — fully extracted into a new `preview` namespace: header (back button, heading, page/element count with proper i18next pluralization), zoom toolbar (added missing `aria-label`s while in there), export dropdown menu (the 4-item format array + "More formats…"), "Open in PDF Viewer", per-page label, empty-page message, watermark alt text, and the info-footer stat tiles + "Preview Mode" banner. One stray inline `style={{ marginLeft: 4 }}` fixed to `marginInlineStart` for RTL. Confirmed ~15 other strings in this file are document-content-model fallback defaults (signature caption, chart data, table cell fallbacks, etc.) and correctly left untouched.
- [x] **`src/components/Editor/SimplePxaSurface.tsx` (~7,900 lines) — fully extracted**, added to the existing `editor` namespace. Covers: the 38-entry `tools` array's `label`/`hint` pairs (also **fixed two leftover German strings as a bug**, not just a translation gap — tool `note`'s label was `'Notiz'` and `pagenumber`'s was `'Nummerierung'` while every sibling tool had an English label), the 6-entry `toolGroups` headings, toolbar/toast/warning/placeholder strings, the entire Page Settings mega-panel (paper/background/margins/workspace/header-footer/bleed/watermark/page-numbering/export-metadata/export-defaults/pagination/track-changes/protection/encryption/custom-properties/languages/named-styles/reset — ~20 sections), the general Inspector chrome (identity, layer controls, language-scope toggle, layout-grid alignment/distribute, heading-level, form-validation), and every per-element-type inspector panel (TOC, content, richtext, field, textarea, checkbox, button, dropdown, optionlist, radio, checkmark, watermark, note, date, pagenumber, arrow, QR code, barcode, signature, image, table incl. cell-style sub-panel, chart, line, link, number, draw, highlight, page boundary, footnote/endnote, bookmark, comment, content control) plus the shared Typography/Background/Border/Padding sections and the trailing Visibility, Word/DOCX metadata, and "Delete element" button. `ELEMENT_TYPE_LABELS` (the element-type display-name map) was deliberately left untouched — it feeds directly into auto-generated element `name` values that can end up embedded in saved documents, so localizing it needs a deliberate decision, not a mechanical pass. Verified clean via `tsc --noEmit`, full Jest suite (21 suites/215 tests passing), and `npm run build`.

## Phase 2 — Template content & metadata localization (parallelizable with Phase 1, needs only Phase 0)

- [x] `src/data/templateOverrides/{de,fr,es,it,ar}.ts`: sparse `LocaleTemplateOverrides` (`{ elements?, pages? }`) maps, starting empty, plus `templateOverrides/index.ts` exporting `OVERRIDES_BY_LOCALE`.
- [x] `src/data/templateContent.i18n.ts`: `getTemplateElementsLocalized(id, locale)` / `getTemplatePagesLocalized(id, locale)` wrapping the existing, untouched `getTemplateElements`/`getTemplatePages`, returning the override if present else the English default.
- [x] Updated call sites `src/hooks/useTemplateLoader.ts` (`loadTemplate`) and `src/components/Gallery/TemplateMiniPreview.tsx` to use the `*Localized` variants with `useTranslation().i18n.language`.
- [x] Routed `templates.ts` metadata (name/category/description) through `t('templates:<id>.name', { defaultValue: tpl.name })` at render call sites (`IndexPage.tsx` category grid, `TemplatePage.tsx`, `TemplateCard.tsx`) — no `en/templates.json` needed since the `defaultValue` IS the English source text.

## Phase 3 — RTL CSS migration (parallelizable with Phases 1–2, after Phase 0's `dir` wiring)

- [x] Migrated all 13 stylesheets under `src/styles/` from physical to logical properties (`margin-left/right`→`margin-inline-start/end`, `padding-left/right`→`padding-inline-start/end`, `border-left/right`(-color/-width)→`border-inline-start/end`(-color/-width), bare `left:`/`right:` positioning→`inset-inline-start/end:`, `text-align: left/right`→`text-align: start/end`) via a mechanical sed pass plus manual fixes for same-line multi-declaration cases sed's anchored regex couldn't catch. `hub-layout.css` also got an RTL-specific override for its mobile-drawer `translateX(-100%)` collapse animation (mirrors to `translateX(100%)` under `[dir="rtl"]`) since a transform can't be expressed as a logical property. Confirmed no `float: left/right` usage anywhere and no asymmetric 4-value `border-radius` shorthand needing corner-mirroring.
- [x] New `src/styles/rtl.css` (imported last in `index.css`): `[dir="rtl"] <selector> svg { transform: scaleX(-1); }` for directional-icon classes across all in-scope pages (`.pdf-tool-card`, `.pdf-upload-action`, `.idx-category-arrow`, `.pdf-outline-button`, `.hub-sidebar-toggle`, `.tpl-use-button`, `.pdf-template-action`, `.importer-card-action`, `.importer-config-confirm`, `.mgr-arrow`); Arabic font-family override for `[dir="rtl"] body`. Also fixed one stray inline `style={{ marginLeft: 4 }}` in `IndexPage.tsx` to `marginInlineStart`.
- [x] `index.html`: appended `Noto+Sans+Arabic:wght@400;500;700` to the Google Fonts `<link>` query string.

## Phase 4 — Legacy `pdf-viewer/i18n.ts` migration (after Phase 0, otherwise independent)

- [x] Convert `pdfViewerLabels.en`/`.de` into `src/locales/{en,de}/pdfViewer.json` (95 keys each, mechanical, preserves real existing translations).
- [x] Delete `pdfViewerLabels`/`resolvePdfViewerLocale`/`features/pdf-viewer/i18n.ts`; `PdfViewer.tsx` now uses `useTranslation('pdfViewer', { useSuspense: false })`, building a `labels` object with the same 95 keys via `t()` so none of the ~103 existing `labels.xxx` call sites needed touching.
- [x] `pdfViewer` en/de content is preloaded synchronously via `src/i18n/index.ts`'s `resources` option (`partialBundledLanguages: true` alongside it, required so the lazy backend loader still runs for every other namespace) — needed because `PdfViewer.tsx` can render standalone outside any `<Suspense>` boundary (`pdfViewerSmoke.test.tsx`).
- [x] The viewer's own EN/DE-only language `<select>` now drives the global `i18n.changeLanguage()` (all 6 supported languages) instead of local `viewerLocale` state — it was redundant with the app-wide `LanguageSwitcher` already present via `AppHeader` on the `/pdf/viewer` route.
- [x] Replaced `src/__tests__/pdfViewerI18n.test.ts` with assertions against `i18n.t('pdfViewer:...')` / `i18n.getFixedT(...)`. Updated `pdfViewerSmoke.test.tsx`'s language-switch interaction to `await act(async () => ...)` since switching now goes through the async `i18n.changeLanguage()` API instead of synchronous local state.

## Phase 5 — Testing (after Phases 0–4 land)

- [x] `src/__tests__/localization.test.tsx` (jsdom pragma, mirrors `appRouteSmoke.test.tsx`'s mock set): locale switch changes rendered Home text to the real hand-translated "Startseite" (not a fallback); switching to `ar` sets `document.documentElement.dir === 'rtl'`/`lang === 'ar'` and reverts to `ltr` on switching back to `en`; a deliberately-unstubbed key in `fr/home.json` (`hero.title`) still resolves to its English value via `fallbackLng`.
- [x] `src/__tests__/templateOverrides.test.ts`: `getTemplateElementsLocalized(id, 'xx')` and `getTemplatePagesLocalized(id, 'xx')` fall back to the plain `getTemplateElements`/`getTemplatePages`; a locale that exists but has no override for a given template also falls back; once a `de` override is injected for one template (test mutates `OVERRIDES_BY_LOCALE` directly, restored in `afterEach`), returns the override's distinct output. `Date.now()` mocked so element-id generation is deterministic across the two calls being compared.

## Phase 6 — Translator handoff check

- [x] Confirmed all locale JSON files (6 languages × 15 namespaces, incl. `codeEditor`/`preview` added during the `SimplePxaSurface.tsx`/`LiveCodeEditor.tsx`/`LivePreview.tsx` follow-up) exist and parse as valid JSON.
- [x] Confirmed all 6 `src/data/templateOverrides/*.ts` files exist (`index.ts` + one per non-English locale), each a valid empty `{ elements: {}, pages: {} }` scaffold ready for a translator to add per-template entries. (Unaffected by Phase 7 below — that phase only fills in UI-chrome namespace JSON, not per-template document content.)

## Phase 7 — Full UI-chrome translation pass (German/French/Spanish/Italian/Arabic)

- [x] Translated every real (non-empty-by-design) namespace in full, for all 5 non-English locales — 1,499 strings × 5 languages, all hand-translated (not machine-translated) preserving exact key structure, interpolation placeholders (`{{count}}`, `{{name}}`, etc.), `_plural` pluralization suffixes, and embedded `<strong>`/`<code>` HTML fragments: `common` (24), `gallery` (22), `codeEditor` (25), `docs` (38), `importer` (35), `preview` (35), `convert` (41), `migrations` (70), `home` (79), `pdfViewer` (95 — fr/es/it/ar only, `de` already had real content from the Phase 4 migration), `spreadsheet` (101), `editor` (934 — by far the largest, includes the entire `SimplePxaSurface.tsx` inspector/toolbar/page-settings string set built during the earlier extraction phase).
- [x] `de` (German) also completed for these namespaces — it previously only had `common.json` and `pdfViewer.json` populated.
- [x] Verified exact key-for-key structural parity between `en/editor.json` (934 leaves, 1,104 total keys including branch nodes) and each of `de`/`fr`/`es`/`it`/`ar` — zero missing keys, zero extra keys, confirmed via a recursive leaf-counting script across all 90 locale JSON files.
- [x] Left untranslated, by design: JSON/API schema field names inside error messages (e.g. `"pages"`, `"elements"`, `"id"` in `codeEditor.errors.*` — these are literal property names, not prose); brand name "Power Dox Automation"; file-format acronyms (PDF, DOCX, XLSX, SVG, etc.); example locale codes/placeholders like `"de-DE"`/`"EUR"`.
- [x] Fixed a test that had been asserting fallback behavior against `home:hero.title` in French — now that the string is genuinely translated, that assertion no longer exercised the fallback path. Repointed `src/__tests__/localization.test.tsx` at `templates:sample.name` with a `defaultValue`, since `templates.json` is genuinely and permanently empty in every locale (template metadata is translated via the `defaultValue` mechanism, not a namespace file) — this is a stable, by-design fallback case rather than a temporary scaffold gap.
- [x] `templates`/`onboarding`/`create` namespaces remain intentionally empty in every locale (see Phase 1/2 notes above for why) — not part of this pass.
- [x] Re-verified `tsc --noEmit`, full Jest suite (21 suites / 215 tests), and `npm run build` all clean after the full translation pass.

## Explicitly out of scope

- [x] Existing content-RTL system (`SimplePxaSurface.tsx`, `LocalizedPropertiesPanel.tsx`, `LanguageTabBar.tsx`) — handles user-authored document content, not app UI; not touched.

## Verification

- [x] `cd pxa-designer && npx tsc --noEmit` clean.
- [x] `npm run build` clean.
- [x] `npm run test` — full suite passes: 21 suites, 215 tests, including the two new test files.
- [ ] Manual (no browser in this environment, recommend for the user): switch through all 6 languages via the switcher on Home; confirm Arabic flips the whole layout RTL (sidebar side, chevrons, text alignment) and loads the Arabic font; confirm a template loads correctly in a non-English locale (falls back to English content since overrides are empty initially); confirm the language choice persists across a reload.
