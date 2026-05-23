# UI Live Code Editor & Preview Checklist

## Scope
Add a "Code Editor" view where users write JSON directly to define a PDF design.
The preview updates live as they type (debounced). An Export button sends the JSON
to the `render-design` backend endpoint and downloads the PDF.

JSON format: the same payload that `ExportService.exportToPDF` sends to the backend —
`{ pages, sharedElements, pageSettings }` where each element is a `SimpleElement`-shaped object.

---

## A. Package & Infrastructure

- [x] Install `@monaco-editor/react` for the JSON code editor. *(Monaco = VS Code engine; gives syntax highlighting, JSON validation, autocomplete)*
- [x] Add `'code'` to the `ViewMode` union in `App.tsx`. *(alongside `'gallery' | 'editor' | 'preview'`)*
- [x] Create `src/components/CodeEditor/` folder with:
  - `LiveCodeEditor.tsx` — the top-level view component
  - `JsonEditorPane.tsx` — wraps Monaco editor
  - `CodePreviewPane.tsx` — wraps LivePreview for the parsed design
  - `starterTemplates.ts` — exported JSON starter objects

---

## B. Layout & Navigation

- [x] `LiveCodeEditor.tsx` renders a two-column split layout:
  - Left column (50 %): `JsonEditorPane` — Monaco editor
  - Right column (50 %): `CodePreviewPane` — live preview + error display
- [x] Top bar with:
  - ← Back button → returns to `'gallery'`
  - Centered title "Code Editor"
  - "Export PDF" button (disabled when JSON is invalid)
- [x] Add a "Code Editor" entry point in the gallery header (icon button or card). *(navigates to `'code'` view without requiring a template selection)*
- [x] Apply `framer-motion` `AnimatePresence` slide transition when entering/leaving the view. *(same pattern as other views in `App.tsx`)*

---

## C. Monaco Editor Pane (`JsonEditorPane.tsx`)

- [x] Mount `<Editor language="json" />` from `@monaco-editor/react` with:
  - `defaultValue` set to the active starter template
  - `theme="vs-dark"` (or `"light"` based on system preference)
  - `options`: `{ minimap: { enabled: false }, fontSize: 13, wordWrap: "on", lineNumbers: "on" }`
- [x] Register a JSON schema with Monaco so the editor validates against the `DesignExportDto` shape:
  - Required fields: `pages` (array), `pageSettings.width`, `pageSettings.height`
  - Element fields: `id`, `type`, `x`, `y`, `width`, `height`
- [x] Toolbar row above the editor:
  - "Format JSON" button — calls `editor.getAction('editor.action.formatDocument').run()`
  - "Copy" button — copies current editor content to clipboard
  - Starter template selector: "Blank", "Hello World", "Invoice", "Multi-page"
- [x] Emit `onChange(value: string)` debounced at 400 ms to the parent. *(use `useCallback` + `useRef` debounce, not a library)*

---

## D. JSON Parsing & Validation

- [x] On each debounced change, parse the string with `JSON.parse` inside a try/catch.
- [x] If parse fails: surface the syntax error message to `CodePreviewPane`.
- [x] If parse succeeds, validate the structure:
  - `pages` must be a non-empty array
  - Each page must have `id` (string) and `elements` (array)
  - Each element must have `id`, `type`, `x`, `y`, `width`, `height`
  - `pageSettings` must have numeric `width` and `height` (default to 595 × 842 if absent)
- [x] Expose a `ValidationResult { valid: boolean; errors: string[] }` object to `CodePreviewPane`.
- [x] "Export PDF" button is disabled (greyed) whenever `valid === false`.

---

## E. Live Preview Pane (`CodePreviewPane.tsx`)

- [x] When `valid === true`: render `<LivePreview>` with data extracted from the parsed JSON:
  - `pages`: map `json.pages` → `Page[]` (cast elements as `SimpleElement[]`)
  - `sharedElements`: `json.sharedElements ?? []`
  - `pageSettings`: build `PageSettings` object from `json.pageSettings`
  - `template`: minimal stub `{ id, name: json.name ?? 'Preview', ... }`
- [x] When `valid === false`: show an error panel instead of the preview:
  - Red border around the pane
  - List of `errors[]`
  - "Fix the JSON above to see the preview" hint
- [x] When JSON is empty or initial load: show a placeholder "Start typing JSON to see the preview" screen.
- [ ] Scale the preview to fit the pane width using `transform: scale(...)` so an A4 page always fits. *(currently uses LivePreview's built-in manual zoom — no auto-fit)*

---

## F. Export to PDF

- [x] Add `ExportService.exportJsonToPDF(payload: object, name?: string)` helper — POSTs raw JSON directly to `POST /api/templates/render-design` and triggers browser download.
- [x] Show loading state in the button while exporting. *(`isExporting` state, button label changes to "Generating…")*
- [x] On success: trigger browser download of the PDF.
- [x] On error: show an inline error message in the top bar.

---

## G. Starter Templates (`starterTemplates.ts`)

- [x] **Blank** — empty page, `pageSettings: { width: 595, height: 842 }`, no elements.
- [x] **Hello World** — one `text` element at (72, 72), one `rect` shape, one `image` placeholder.
- [x] **Invoice** — `text` title, `table` with header row and 3 data rows, `field` elements, `signature`.
- [x] **Multi-page** — two pages, `pagenumber` element scoped to `all`, `watermark` element.
- [x] Each template is a plain JS object that gets `JSON.stringify(obj, null, 2)` as the editor default value.

---

## H. UX Polish

- [x] Resizable splitter between editor and preview: drag handle at 50 % default, snaps to 30 %/70 % range.
- [ ] Editor gutter annotation for the first JSON parse error line. *(Monaco `editor.setModelMarkers(...)` API)*
- [x] Keyboard shortcut `⌘ Enter` / `Ctrl Enter` → force-refresh preview without waiting for debounce.
- [ ] "Open in Editor" button: parses the JSON, loads it into the visual editor via `setCurrentTemplate` + `setCurrentPage`. *(converts raw JSON pages/elements into Zustand store format)*
- [x] Remember last edited JSON in `localStorage` under key `canvas-code-editor-draft` so it survives page reload.
- [ ] Line count / character count display in the bottom status bar of the editor pane.
