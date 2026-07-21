# PXA Designer Visual Redesign Checklist

## Goal

Make PXA Designer (`pxa-designer/`, the React+TS+Vite document-automation editor app) feel more friendly and beautiful, using the installed `pxa-designer/DESIGN.md` (Expo-inspired design language: pure-white canvas, near-black ink, Inter type with tight negative letter-spacing, JetBrains Mono for code, hairline borders, restrained single-tier shadows, 8px/12px radii) as the reference, adapted with a distinct warm orange/amber accent instead of Expo's black-only CTA — deliberately different from the cool blue already used across PXA.Company/Account/Admin, so Designer reads as its own creative tool within the product family.

## Context

Current look: `pxa-designer/src/styles/variables.css` holds a single, consistently-referenced token set — generic Tailwind-blue primary (`#3b82f6`), system-font sans stack, a 4-tier heavy box-shadow scale, 0.25rem–1rem radius scale. Nearly every surface (`base.css`, `home.css`, `editor.css`, `gallery.css`, `docs.css`, `modals.css`, `migrations.css`, `importer.css`, `pdf-viewer.css`, ~8,000+ combined lines) consumes these same `var(--color-*)`/`var(--space-*)`/`var(--radius-*)`/`var(--shadow-*)` names rather than hardcoding values. Retokenizing `variables.css` alone cascades the new look across almost the entire app for free — the highest-leverage, lowest-risk first move. Scope is deliberately split into Phase 1 (tokens + shared shell + landing page) now, with the remaining per-surface files as an explicit, separately-approved follow-on.

## Phase 1 — Tokens, Shell, Landing Page

- [x] Update `pxa-designer/src/styles/variables.css`:
  - [x] Accent: `--color-primary: #d97706` (amber-600), `--color-primary-hover: #b45309` (amber-700), `--color-primary-light: #fef3c7` (amber-100).
  - [x] Shift `--color-warning` from `#f59e0b` to `#ca8a04` (yellow-600) so it no longer sits almost on top of the new amber primary; `--color-warning-light` moved to `#fef9c3` for the same reason.
  - [x] Keep `--color-success` (`#16a34a`) and `--color-error` (`#dc2626`) standard.
  - [x] Add warm-stone neutrals: `--color-ink: #171717`, `--color-body: #57534e`, `--color-muted: #a8a29e`, `--color-hairline: #e7e5e4`, `--color-hairline-soft: #f5f5f4`, `--color-canvas-soft: #fafaf9`. Existing `--color-gray-*` scale kept as-is (not renamed).
  - [x] Bumped radius scale: `--radius-sm: 4px`, `--radius-md: 8px`, `--radius-lg: 12px`, `--radius-xl: 16px`, `--radius-2xl: 24px`, new `--radius-pill: 9999px`.
  - [x] Softened shadow scale to a flatter, hairline-first model (`--shadow-sm`/`--shadow-md` now single soft-drop tiers; `--shadow-lg`/`--shadow-xl` lightly softened).
  - [x] `--font-family-sans` set to `'Inter', -apple-system, ...`, `--font-family-mono` to `'JetBrains Mono', 'Fira Code', monospace`.
- [x] `pxa-designer/index.html`'s Google Fonts link: appended `;600;800` to the Inter weight list (800 added after discovering the existing font-weight 800/850 rules throughout home.css would otherwise fall back to a synthesized bold once Inter became the primary font), added `&family=JetBrains+Mono:wght@400;500`; also pointed the `body` inline style's font-family at Inter first.
- [x] Restyled the shared nav (`.pdf-nav`/`.pdf-logo`/`.pdf-nav-links`, rendered by `AppHeader.tsx` on every page) — found living in `home.css`, not `base.css` as originally assumed, but confirmed as the single, non-duplicated definition. No JSX/class-name changes in `AppHeader.tsx`.
- [x] Restyled the landing page (`IndexPage.tsx` via `home.css` only, no JSX changes): `home.css` turned out to use ~58 hardcoded hex colors throughout (not CSS variables as first assumed from exploration) — applied a full, consistent cool-blue-and-gray → warm-amber-and-stone palette substitution across the whole file (nav, hero, tool cards, trust band, template/feature/security/usage sections), left the small distinct "info" accent family (`#0284c7` etc.) and semantic error/warning colors untouched, and added tight negative letter-spacing to the hero H1 and section H2s.

## Phase 2 — Remaining Surfaces (done)

Same technique as Phase 1: each file turned out to use hand-authored hardcoded hex colors (not shared tokens as originally assumed from exploration), so each got its own full cool-blue/gray → warm-amber/stone substitution, including `rgba()` triplets, with a per-file check for genuinely distinct semantic/accent families that must NOT be converted.

- [x] `editor.css` (2,462 lines, ~91 distinct hex values) — converted the primary-blue family (multiple drifted shades: `#1769e0`, `#3b82f6`, `#2563eb`, `#1d4ed8`, `#1e40af`, `#155eef`, `#1d6fff` and their tints) to amber, and cool grays to warm stone. Left untouched: a distinct violet/indigo accent family (`#6366f1`, `#4f46e5`, `#7c3aed`, etc. — a separate "smart feature" accent), semantic red/green/warning colors, and the Monaco/VS-Code-style dark code-editor theme colors (`#1e1e1e`, `#252526`, `#3a3a3a`, etc. — code editors conventionally keep their own theme independent of app chrome).
- [x] `gallery.css`, `importer.css`, `spreadsheet.css` — same substitution, smaller/cleaner files, no ambiguous colors found.
- [x] `docs.css` — same substitution, but with one important distinction: the same old blue hex (`#1d6fff`/`#1d4ed8`) was reused for **two different roles** in this file — general brand-accent usage (active sidebar link, step-number badges, inline-code text — converted to amber) versus a genuine semantic quad (`.docs-callout--info` and `.docs-method--get`, paired with tip=green/warning=amber/danger=red callouts and POST/PUT method badges — **left as blue**, since making "info" and "GET" look like the brand's primary amber would blur an intentional convention).
- [x] `modals.css` — same substitution; left an extensive indigo/violet family untouched (likely a distinct AI/smart-feature modal accent).
- [x] `pdf-viewer.css` — converted the cool ink/gray family; also fixed the known pre-existing drift here: `var(--color-bg, #f5f7fb)`/`var(--color-text, #172033)` reference variable names that don't exist in `variables.css`, so they always fell through to the hardcoded fallback — updated those fallback values to match the new palette (didn't wire up the missing variable names themselves, out of scope for a color pass).
  - Initially left the file's cyan/teal family (`#0e7490`/`#155e75`/`#ecfeff`) untouched as a blanket "distinct annotation accent" — on closer look this was wrong: it mixed two different roles. **Document-content colors** (search-match highlight `#fde047`, sticky-note/freeText annotation-type backgrounds, ink-stroke erase glow) are genuinely content-representation and correctly stay untouched. But `.pdfv-button-primary`, `.pdfv-button.is-active`, `.pdfv-radio.is-active`, `.pdfv-thumbnail.is-active`, the empty-state icon, the resize handle, the annotation-selection outline, and the match-count banner are all **toolbar/UI chrome** — the same "primary button"/"active state" role converted to amber everywhere else in the app. Converted those (plus their `rgba(14, 116, 144, ...)` equivalents) to amber; left the genuine content colors alone.
- [x] `migrations.css` — this one already used `var(--color-primary, ...)`/`var(--color-primary-50, ...)` fallback patterns; the first already auto-resolves via the retokenized `--color-primary` but its stale fallback literal was updated too for consistency, and `--color-primary-50` (a variable name that doesn't exist in `variables.css`, so always uses its fallback) had its fallback hex updated to a light amber tint.

All of the above reused the exact palette from Phase 1 (`#d97706`/`#b45309`/`#fef3c7`/`#fde9c7` for the accent family, `#171717`/`#1c1917`/`#292524`/`#44403c`/`#57534e`/`#78716c`/`#a8a29e` for ink/body/muted, `#e7e5e4`/`#d6d3d1`/`#f5f5f4`/`#fafaf9` for hairlines/soft surfaces) — no new colors introduced.

## Verification

- [x] `cd pxa-designer && npm run type-check` clean.
- [x] `npm run build` clean (pre-existing chunk-size warning unrelated to this change).
- [x] Confirmed via the full built CSS output (all chunks) that the old brand blue (`#1d6fff`/`#2563eb`/`#3b82f6`/`#1769e0`) is gone app-wide, except the one intentionally-preserved `.docs-callout--info` semantic color — plus unrelated template *content* data (`templates.ts`, `jsonToCode.ts`, `LivePreview.tsx` — sample document colors, not app chrome).
- [x] Re-ran `npm run type-check` and `npm run build` after Phase 2 — both clean.
- [ ] `npm run dev` manual visual check — not done, no browser available in this environment. Recommend checking the home page, the template editor, docs, migrations, importer, and PDF viewer pages to confirm everything reads consistently.
- [x] No automated visual-regression tooling exists in this repo (consistent with every other site) — confirmed this stays a manual browser check.

## Deferred Decisions

- [ ] Whether to eventually rename `--color-gray-*` tokens to the new warm-stone names for full consistency, or keep both scales indefinitely.
- [ ] Whether to extend amber-accenting to component-level details beyond color (e.g. radius/shadow/spacing polish) on the newly-converted surfaces, similar to what Phase 1 did for the landing page's hero/card patterns.
