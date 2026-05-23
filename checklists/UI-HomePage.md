# UI HomePage Checklist

## Scope
Audit and fix all navigation, links, and interactive elements on the home/gallery page (`TemplateGallery.tsx`) and its sub-pages. The app uses a local `viewMode` state machine — no React Router. Every issue below was found by reading the source directly.

## Current State — Code Analysis (2026-05-15)

| Element | Location | Status | Issue |
|---|---|---|---|
| Log in button | Header right | ❌ Broken | No `onClick` — does nothing |
| Sign up button | Header right | ❌ Broken | No `onClick` — does nothing |
| Menu (hamburger) | Header far right | ❌ Broken | No `onClick` — mobile menu never opens |
| "Request signature" tool card | Tools section | ⚠️ Misleading | Opens generic editor — no signature-specific flow |
| "Share" tool card | Tools section | ⚠️ Misleading | Opens generic editor — no share/send functionality |
| "Organize" tool card | Tools section | ⚠️ Misleading | Opens generic editor — no organize/folder functionality |
| "Protect" tool card | Tools section | ⚠️ Misleading | Opens generic editor — no password/protection functionality |
| PRO badge on templates | Template grid | ⚠️ Misleading | Clicking a PRO card opens editor normally — no paywall or upsell |
| Hero upload card | Hero section | ⚠️ Copy mismatch | Says "Drop document here to upload" but no file upload exists — just picks first template |
| Trust band numbers | Trust band | ⚠️ Fake data | "4.8/5 rating", "56M+ documents" are placeholder copy, not real metrics |
| Feature cards | Features section | ℹ️ Informational | No actions — fine as-is |
| Onboarding wizard | App.tsx | ❌ Unused | `OnboardingWizard` component exists but is never shown — dead code |

---

## A. Broken Header Buttons

> **Current:** The header shows "Log in", "Sign up", and a hamburger menu button. None have `onClick` handlers. Clicking them does nothing.

- [x] **Log in** — shows a "Auth not yet implemented." toast. *(TemplateGallery.tsx)*
- [x] **Sign up** — same toast. 
- [x] **Menu (hamburger)** — opens a full-screen mobile menu drawer with the four nav links and auth buttons. `mobileMenuOpen` state + `pdf-mobile-menu` overlay component.
- [x] CSS added: `.pdf-mobile-menu`, `.pdf-mobile-menu-header`, `.pdf-mobile-nav`, `.pdf-mobile-nav-actions`, `.pdf-toast` + keyframe animation.

## B. Misleading Tool Cards

- [x] **"Edit PDF"** — opens editor directly.
- [x] **"Create form"** — navigates to create-form sub-page.
- [x] **"Request signature"**, **"Share"**, **"Organize"**, **"Protect"** — `comingSoon: true` in `TOOL_LINKS`. Cards render dimmed, no hover lift, click shows a toast. CSS: `.pdf-tool-card.is-coming-soon`.

## C. PRO Template Badges

- [x] Removed `isPro` field from all 7 entries in `TEMPLATES` array and from `TemplateCard` interface. PRO badge JSX removed from `TemplateCard.tsx`. `thumbnail?: string` made optional in `types.ts`.

## D. Hero Upload Card Copy Mismatch

- [x] Icon changed from `FiUploadCloud` → `FiLayout`. Copy changed to "Start from a template" / "Pick a template from the library and open it in the editor" / "Browse templates". No fake upload implied.

## E. Fake Trust Band Metrics

- [x] Replaced placeholder stats with accurate ones derived from actual app capabilities: "7 templates", "PDF, JSON & image export", "100% browser-based — no server upload required".

## F. Dead Onboarding Wizard

- [x] Removed `OnboardingWizard` import from `App.tsx`, removed `'onboarding'` from `ViewMode` union, removed the `onboarding` AnimatePresence block and `handleOnboardingComplete`. `OnboardingWizard.tsx` file left on disk but is no longer wired into the app.

## G. Missing "No results" State Polish

- [x] Added "Show all templates" `pdf-outline-button` inside `.pdf-empty-results`. Clicking it calls `clearSearch()` which resets both `searchQuery` and `selectedCategory` to defaults.

## H. Missing Active State on Nav Links

- [x] "Tools" and "Create form" buttons get `className={activePage === ... ? 'is-active' : ''}`. CSS: `.pdf-nav-links button.is-active` — blue text + 2px blue bottom border.
- [x] Anchor links (Templates, Features) — `IntersectionObserver` in `useEffect` watches `#templates` and `#features` with `rootMargin: '-20% 0px -60% 0px'`. `activeSection` state drives `is-active` class on those nav links.

## I. "Create Form" Page — CTA Duplication

- [x] Bottom strip CTA changed from "Create form → opens editor" to "Browse all templates → sets `activePage('home')` and scrolls to top". Now the two CTAs have distinct purposes.

## J. Thumbnail Images Are Missing

- [x] Removed `thumbnail` property from `TEMPLATES` array and made it `optional` in `types.ts`. `TemplateCard.tsx` no longer references it. Template cards now render with category-specific colour accents in the existing `pdf-document-miniature` CSS placeholder — no broken `<img>` tags. `CATEGORY_CONFIG` map in `TemplateCard.tsx` defines accent/bg/text/icon per category (invoice=blue/FiDollarSign, receipt=green/FiShoppingBag, certificate=purple/FiAward, card=orange/FiCreditCard, letter=slate/FiMail, report=indigo/FiBarChart2, label=teal/FiTag). Category icon shown large (opacity 0.22) in miniature preview and small in the card title row.

## K. No "Blank Canvas" Option

- [x] Added `onBlankStart` prop to `TemplateGalleryProps` and `handleBlankStart` in `App.tsx`. Hero section now shows two side-by-side cards (`pdf-hero-cards` grid): "Start from a template" and "Blank canvas". Blank card creates a template with an empty elements array and goes straight to the editor.

---

## New Features — Analysis (2026-05-15)

### L. C# Code Converter (Developer Integration Section)

> **Goal:** Show users how to consume the exported JSON in C#. A static code panel on the home page displays generated C# DTO classes that mirror the template export schema (`TemplateDocument`, `Page`, `Element`).

**Analysis:**
- No live generation needed — the export schema is fixed (`id`, `name`, `pages[]`, `elements[]`, `type`, `x`, `y`, `width`, `height`, `content`). A static but realistic code snippet is sufficient and honest.
- Best placement: new `#developers` section between `#features` and `#security`, inside a tabbed code viewer (`JSON` tab / `C#` tab). The two are naturally paired as "output format" + "how to consume it".
- A tab switcher (`activeCodeTab` state: `'json' | 'csharp'`) with syntax-highlighted `<pre>` blocks covers both without a heavy library.
- No backend or real codegen needed. The snippet is representative, not dynamic.

**Plan:**
- [x] Add `activeCodeTab` state to `TemplateGallery`.
- [x] Add `#developers` section JSX with tab switcher and two `<pre>` code blocks.
- [x] CSS: `.pdf-dev-section`, `.pdf-code-tabs`, `.pdf-code-tab`, `.pdf-code-tab.is-active`, `.pdf-code-block`.

### M. JSON Export Preview (paired with C# in Developer Section)

> **Goal:** Show the JSON structure produced by "Export to JSON" so developers understand what the API produces.

**Analysis:**
- The exported JSON shape is already defined in `ExportService.exportToJSON`. A fixed representative snippet is accurate and doesn't go stale with normal feature work.
- Lives in the same `#developers` section as the C# tab (see L above) — no separate section needed.
- Snippet shows: `id`, `name`, `category`, `pages[0].elements[0]` with all key fields. Enough for a developer to understand the schema without dumping the full object.

**Plan:**
- [x] Covered by the same implementation as L above (JSON is the first tab).

### N. User Usage Info (Session Activity Strip)

> **Goal:** Show the user lightweight session stats — how many documents they've opened and what the last template was — using `localStorage` since there is no auth.

**Analysis:**
- Three stats are derivable from `localStorage` without auth: documents opened this session, last template name, and session start time ("active for X min").
- A `useEffect` in `TemplateGallery` reads `canvas_docs_opened` and `canvas_last_template` from `localStorage` on mount and re-reads after `onTemplateSelect` fires (via a local `usageStats` state).
- `App.tsx` increments `canvas_docs_opened` and writes `canvas_last_template` whenever `handleTemplateSelect` or `handleBlankStart` is called.
- Placement: a slim `pdf-usage-strip` above the `#templates` section — visible only when at least one document has been opened (`canvas_docs_opened > 0`), otherwise hidden so a first-time visitor sees a clean page.
- No fake or placeholder data — if nothing has been opened the strip is invisible.

**Plan:**
- [x] `App.tsx`: write to `localStorage` in `handleTemplateSelect` and `handleBlankStart`.
- [x] `TemplateGallery`: add `usageStats` state (`{ count: number; lastName: string | null }`), read from `localStorage` on mount and on `activePage` change (so strip refreshes when user returns from editor).
- [x] Add `pdf-usage-strip` section JSX (hidden when `count === 0`).
- [x] CSS: `.pdf-usage-strip`, `.pdf-usage-stat`.
