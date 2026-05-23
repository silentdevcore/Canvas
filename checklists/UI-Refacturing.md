# UI Restructure & New Feature Wiring — Analysis & Plan

## Analysis

### Current State

| Area | File(s) | Lines | Issues |
|------|---------|-------|--------|
| Navigation header | IndexPage.tsx, TemplatePage.tsx, DocsPage.tsx | 3 × ~30 lines each | **Duplicated** — three separate copies of the same nav HTML |
| CSS | `src/styles/index.css` | **5 066 lines** | Monolith — one file for everything; hard to maintain |
| Home page copy | `IndexPage.tsx` | 339 | Says "PDF, JSON & image export" — we now export 10+ formats; 4 "coming soon" tool cards with no plan |
| Trust band | `IndexPage.tsx` | 207–221 | Stat says "PDF, JSON & image export" — stale |
| Sign DOCX | `ExportService.signDocx()` | — | **Full backend API exists, zero UI** — unreachable by users |
| Content Control inspector | `SimpleCanvas.tsx` | inspector ~5000–5200 | `contentControlTitle`, `contentControlTag`, `contentControlPlaceholder` missing from inspector |
| Bookmark inspector | `SimpleCanvas.tsx` | inspector | `bookmarkTarget` field missing |
| Comment inspector | `SimpleCanvas.tsx` | inspector | `commentDate`, `commentId` fields missing |
| Revision inspector | `SimpleCanvas.tsx` | inspector | `revisionId` field missing |
| ExportModal formats | `ExportModal.tsx` | 144 | Lists SVG but `ExportFormat` union in ExportService doesn't include `'svg'` |
| Auth buttons | IndexPage.tsx, TemplatePage.tsx | | Show "not implemented" toast — should be removed or handled gracefully |
| Hero cards | `IndexPage.tsx` | 140–175 | No "Import file" entry point from home page |
| Mobile menu | IndexPage.tsx, TemplatePage.tsx | | Duplicated mobile drawer logic |

### Features Complete in Backend but Missing from UI

| Feature | Backend | UI gap |
|---------|---------|--------|
| Digital signing | `POST /api/document/sign-docx` | No button, no modal, no flow |
| Content Control full fields | `contentControlTitle/Tag/Placeholder` on `SimpleElement` | Only type shown in inspector |
| Bookmark target | `bookmarkTarget` on `SimpleElement` | Not in inspector |
| Comment date + id | `commentDate`, `commentId` | Not in inspector |
| Revision id | `revisionId` | Not in inspector |
| Clone design | `ExportService.cloneDesign()` | API exists, no button in editor |
| Extract pages | `ExportService.extractPages()` | API exists, no UI in editor |

---

## Plan

### Phase 1 — Shared Components (Structural)

#### 1-A. Extract `<AppHeader>` component
**[high]**

Create `/ui-designer-v2/src/components/Layout/AppHeader.tsx`.

Props: `activePage: 'home' | 'templates' | 'docs'`

Moves the duplicated nav from IndexPage, TemplatePage, DocsPage into a single component. Includes:
- Logo button → `/`
- Nav links: Home, Templates, Docs, Blank canvas
- Mobile hamburger + drawer

**Files to modify:** IndexPage.tsx, TemplatePage.tsx, DocsPage.tsx (replace nav with `<AppHeader activePage="…" />`)

---

#### 1-B. Split `index.css` into feature modules
**[medium]**

Current: 1 file, 5 066 lines.

Create `src/styles/` sub-files and import them from the root `index.css`:

| File | Content | Approx. lines |
|------|---------|--------------|
| `variables.css` | CSS custom props, color palette, spacing, typography | ~80 |
| `base.css` | Reset, utilities, button system, form elements | ~215 |
| `nav.css` | `.pdf-nav`, `.pdf-logo`, `.pdf-nav-links`, mobile menu | ~120 |
| `home.css` | Hero, tools, trust band, category grid, features, security, usage strips | ~650 |
| `gallery.css` | Template browser toolbar, cards, detail panel, category filter | ~1 100 |
| `editor.css` | Canvas, topbar, tool panel, inspector, layers, pages panel, modals | ~2 200 |
| `docs.css` | DocsPage sidebar, content, code blocks, endpoint grid | ~520 |
| `responsive.css` | All `@media` breakpoints | ~200 |

**Files to create:** 8 CSS files under `src/styles/`  
**Files to modify:** `src/styles/index.css` (becomes the import aggregator)

---

### Phase 2 — Home Page & Navigation Updates

#### 2-A. Update IndexPage trust band and hero copy
**[medium]**

- Trust band stat: "PDF, JSON & image export" → "10 export formats — PDF, DOCX, ODT, TIFF and more"
- Hero eyebrow: add "Import existing PDFs, Word, and ODT files as editable templates"
- Hero cards: add a 4th card "Import document" (FiUpload) that navigates to `/template` and triggers the import file input
- Tool cards: remove the 4 "coming soon" stubs OR replace with real ones:
  - "Request signature" → link to digital signing flow (Phase 3-A)
  - "Organize pages" → extract pages (Phase 3-C)
  - Remove "Share" and "Protect" coming-soon cards (Document Protection is already in Page Settings)

**File:** `ui-designer-v2/src/pages/IndexPage.tsx`

---

#### 2-B. Remove / replace non-functional auth buttons
**[low]**

Replace "Log in" / "Sign up" with a single "GitHub" or "Learn more" link, or remove entirely from the nav.

**File:** `ui-designer-v2/src/components/Layout/AppHeader.tsx` (after 1-A)

---

### Phase 3 — New Feature UI

#### 3-A. Sign DOCX modal
**[high]**

Create `src/components/Editor/SignDocxModal.tsx`.

Trigger: new **"Sign"** button in `ExportModal.tsx`, visible only when the last export was DOCX (or always, with a note that the file must first be exported as DOCX).

Flow:
1. User clicks "Sign document" in ExportModal (or toolbar)
2. `SignDocxModal` opens with:
   - Instruction: "Upload a PFX/P12 certificate file to apply an X.509 digital signature"
   - Certificate file input (`.pfx`, `.p12`)
   - Password input (optional, type=password)
   - "Sign & Download" button → calls `ExportService.signDocx(blob, certFile, password)` → triggers download of `*_signed.docx`
   - Error display
3. After signing, show success state with file size

**Files to create:** `SignDocxModal.tsx`  
**Files to modify:** `ExportModal.tsx` (add Sign button), `SimpleCanvas.tsx` (pass docxBlob state to ExportModal)

---

#### 3-B. Complete inspector panels — missing fields
**[high]**

Patch `SimpleCanvas.tsx` inspector section for these element types:

**Content Control** (add to existing contentcontrol block):
- `contentControlTitle` — text input "Title"
- `contentControlTag` — text input "Tag / machine key"
- `contentControlPlaceholder` — text input "Placeholder text"

**Bookmark** (add to existing bookmark block):
- `bookmarkTarget` — text input "Link target (URL or #id)"

**Comment** (add to existing comment block):
- `commentDate` — date input "Date"
- `commentId` — text input "Comment ID" (auto-generated if blank)

**Revision** (add to existing revision block, shown when revisionType ≠ null):
- `revisionId` — text input "Revision ID"

**File to modify:** `SimpleCanvas.tsx` (inspector panels, ~lines 4543–5250)

---

#### 3-C. Clone & Extract Pages — editor UI
**[medium]**

Add two actions to the **page thumbnail strip** (bottom panel):

**Clone current design:**
- Three-dot menu on the template name breadcrumb → "Clone design"
- Calls `ExportService.cloneDesign(design)` → calls `bulkReplaceContent` with the cloned result
- Shows a small toast "Design cloned with new ID"

**Extract pages:**
- Right-click (context menu) on a page thumbnail → "Extract this page to new document"
- Calls `ExportService.extractPages(design, [pageNumber])` → opens result in a new tab or downloads as JSON

**File to modify:** `SimpleCanvas.tsx` (topbar breadcrumb menu + page thumbnail context menu)

---

#### 3-D. Fix SVG in ExportModal
**[low]**

`ExportModal.tsx` lists SVG but `ExportFormat` in `ExportService.ts` doesn't include `'svg'`.

Either:
- Add `'svg'` to `ExportFormat` union and add `svg: 'svg'` to `extMap` in ExportService (if backend supports it)
- OR remove SVG from ExportModal format list

Check `GET /api/export/formats` response to determine if the backend supports SVG; add or remove accordingly.

**Files to modify:** `ExportService.ts` and/or `ExportModal.tsx`

---

### Phase 4 — Mobile & Responsiveness

#### 4-A. Mobile editor improvements
**[low]**

- Currently the editor is desktop-only (no responsive layout below ~1024px)
- Add a "Mobile not supported" overlay below 768px with a link to the preview instead
- Or: hide the left tool panel and inspector on mobile, show a "tap to select tool" bottom sheet

**File to modify:** `SimpleCanvas.tsx` (add viewport check + overlay)

---

## Implementation Order

```
Phase 1-A  → AppHeader component                    (remove nav duplication)
Phase 1-B  → CSS module split                       (maintainability)
Phase 3-B  → Complete inspector fields              (quick wins, types already exist)
Phase 3-D  → Fix SVG ExportModal                    (quick fix)
Phase 2-A  → Update home page copy + hero cards     (user-visible polish)
Phase 2-B  → Remove auth placeholders               (UX cleanup)
Phase 3-A  → SignDocxModal                          (new feature, backend ready)
Phase 3-C  → Clone & Extract Pages UI               (new feature, backend ready)
Phase 4-A  → Mobile overlay                         (last — low priority)
```

---

## Files to Create

| File | Purpose |
|------|---------|
| `src/components/Layout/AppHeader.tsx` | Shared nav bar |
| `src/components/Editor/SignDocxModal.tsx` | Digital signing flow |
| `src/styles/variables.css` | CSS custom properties |
| `src/styles/base.css` | Reset + utilities |
| `src/styles/nav.css` | Navigation styles |
| `src/styles/home.css` | Landing page styles |
| `src/styles/gallery.css` | Template browser styles |
| `src/styles/editor.css` | Editor styles |
| `src/styles/docs.css` | Docs page styles |
| `src/styles/responsive.css` | Breakpoints |

## Files to Modify

| File | Changes |
|------|---------|
| `src/pages/IndexPage.tsx` | Use AppHeader, update trust band, update hero cards, fix tool cards |
| `src/pages/TemplatePage.tsx` | Use AppHeader |
| `src/pages/DocsPage.tsx` | Use AppHeader |
| `src/components/Editor/SimpleCanvas.tsx` | Complete inspector fields, clone/extract menus |
| `src/components/Editor/ExportModal.tsx` | Sign button, SVG fix |
| `src/services/ExportService.ts` | Add/remove SVG from ExportFormat |
| `src/styles/index.css` | Becomes import aggregator only |

---

## Verification

```bash
# TypeScript must be clean
cd ui-designer-v2 && npm run build

# Dev server — check all pages visually:
npm run dev
# / (home) — updated hero, trust band, tool cards
# /template — AppHeader, import file works
# /create — inspector has all new fields, export modal has Sign button
# /docs — AppHeader renders correctly
# Resize to 768px — mobile overlay shows in editor
```

---

## Bug Fix: Import type casing + PDF text extraction
**[high — bug fix, completed 2026-05-22]**

All four importers emitted PascalCase element type strings (`"Text"`, `"Image"`, `"Table"`)
which the frontend canvas renderer did not recognise (expects lowercase `"text"`, `"image"`, `"table"`).
Result was that all imported elements appeared as empty boxes with no content.

Also: PdfImporter now falls back to letter-by-letter text reconstruction when PdfPig's
`Word.Text` is empty (happens with PDFs whose embedded fonts lack a ToUnicode map).

### Files fixed
- `src/Canvas.Infrastructure.Converters/PdfImporter.cs` — type casing + `WordText()` fallback helper
- `src/Canvas.Infrastructure.Word/DocxImporter.cs` — "Text", "Table", "Image" → lowercase
- `src/Canvas.Infrastructure.Converters/DocImporter.cs` — "Text" → "text"
- `src/Canvas.Infrastructure.Converters/OdtImporter.cs` — "Text", "Image" → lowercase
