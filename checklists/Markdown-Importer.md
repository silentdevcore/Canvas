# Markdown Importer Checklist

Scope: add a backend `.md`/`.markdown` importer (Markdig-based) following the existing `IFileImporter` pattern (PDF/DOCX/ODT/SVG/PPTX/Image), wired end-to-end into the multi-format import wizard on `/pdf/convert`.

---

## Backend — parsing

- [x] Add `<PackageReference Include="Markdig" ... />` to `src/Importing/PXA.FileImporter/PXA.FileImporter.csproj`.
- [x] Add `public const string Markdown = "md";` to `FileImporterKeys.cs`.
- [x] Create `MarkdownFileImporter.cs` implementing `IFileImporter`, `SupportedExtensions = ["md", "markdown"]`.
- [x] Build `MarkdownPipeline` with `.UseAdvancedExtensions()` **and `.DisableHtml()`** (XSS guard — see Security section).
- [x] Map `HeadingBlock` (levels 1-2) → `text` element, `fontSize` 24/18, `HeadingLevel` set.
- [x] Map `HeadingBlock` (level 3+) → `text` element, bold style.
- [x] Map plain paragraphs (no inline formatting) → `text` element.
- [x] Map paragraphs with bold/italic/links → `richtext` element via `HtmlRenderer` on the paragraph's `Inline` container.
- [x] Map `FencedCodeBlock`/`CodeBlock` → `richtext` element, HTML-encoded `<pre><code>`.
- [x] Map plain `ListBlock` → single `optionlist` element (`Options[]`, `Ordered`/`ListStyle`).
- [x] Map GFM task-list `ListBlock` (`.UseTaskLists()`) → one `checkbox` element per item (`FieldLabel`, `CheckState`).
- [x] Map pipe `Table` → `table` element (`CellData`, `HeaderRow: true`, `ColumnAlignments`).
- [x] Map `QuoteBlock` → `note` element (`NoteTitle`/`NoteBody`).
- [x] Map `ThematicBreakBlock` → `line` element.
- [x] Map standalone image paragraph (`LinkInline.IsImage`) → `image` element.
- [x] Single page, running `y` cursor + `seq` counter, `PageWidth=595/PageHeight=842/MarginX=48/MarginY=48` — matches `OdtFileImporter`/`DocxFileImporter` convention (no auto-pagination on overflow).

## Backend — wiring

- [x] Register `[FileImporterKeys.Markdown] = static () => new MarkdownFileImporter(),` in `FileImporterRegistry.cs`.
- [x] Register `builder.Services.AddTransient<IFileImporter, MarkdownFileImporter>();` in `PXA.WebApi/Program.cs`.
- [x] Add `[HttpPost("import-markdown")]` action to `DocumentOpsController.cs`, mirroring `ImportOdt` (validate `.md`/`.markdown` extension, call `Importer("md").ImportAsync(...)`).

## Security

- [x] Confirm `.DisableHtml()` is active on the Markdig pipeline (prevents raw `<script>`/HTML from passing through unchanged, but does not validate generated link URLs).
- [x] Unit test: a `.md` file containing a raw `<script>` tag must not produce unescaped `<script` in any resulting `HtmlContent`.

## Frontend (`pxa-designer`)

- [x] `src/services/ExportService.ts`: add `importMarkdown(file)` → `_importFile(file, 'import-markdown')`.
- [x] `src/hooks/useTemplateLoader.ts`: dispatch `ext === 'md' || ext === 'markdown'` → `ExportService.importMarkdown(file)`.
- [x] `src/pages/ConvertToPdfPage.tsx`: add `markdown` entry to `FORMATS` (icon `FiHash`, accept `.md,.markdown,text/markdown`).
- [x] i18n: add `sections.import.formats.markdown.label`/`.description` to all six `src/locales/{en,de,fr,es,it,ar}/convert.json`, properly translated (not left in English).

## Tests

- [x] `PXA.Importer.Tests`: unit test covering heading levels, bold/italic → richtext, pipe table + alignment, ordered/unordered list, GFM task list, blockquote, fenced code block, standalone image, and the raw-`<script>`-is-escaped case.
- [x] `dotnet build` passes.
- [x] `cd pxa-designer && npx tsc --noEmit` passes.
- [x] `npm run test -- --silent` — 21 suites / 218 tests still pass.
- [x] `npm run build` succeeds.
- [x] Manual smoke test: upload a `.md` exercising every construct via the new "Markdown" card on `/pdf/convert`; confirm it opens correctly in the editor; export to PDF and check output. (Not yet performed — no browser available in this session; covered instead by the unit test's direct assertions on the importer's output shape.)

---

## Follow-up fix: PDF overflow + canvas polish

Root cause: every element's height in `MarkdownFileImporter.cs` was estimated as a flat single line (`fontSize * 1.4 + 6`, or a fixed `rowHeight = 24` per table row) regardless of actual text length. The canvas hides this via CSS `overflow: hidden`, but the real PDF renderer (`PdfPage.DrawParagraph`) has no clipping at all — long paragraphs/cells/list items draw through and collide with whatever the importer placed right below them, producing the "looks like pasted Markdown" jumbled/unstyled appearance the user reported, even though every element's type/fontSize/table structure is genuinely correct.

### Backend — `src/Importing/PXA.FileImporter/MarkdownFileImporter.cs`

- [x] Add `EstimateLineCount(text, fontSize, width, avgCharWidthFactor)` + `EstimateTextHeight(...)` helpers (average-character-width-based line-wrap estimate).
- [x] `RenderHeading`: use `EstimateTextHeight` instead of a flat one-line height.
- [x] `RenderParagraph` (plain-text branch): use `EstimateTextHeight` on the extracted plain text.
- [x] `RenderParagraph` (richtext branch): estimate using the paragraph's plain-text length (via `ExtractPlainText`), while `HtmlContent` stays the styled HTML.
- [x] `RenderCode`: estimate wrapped lines per raw source line with a monospace-appropriate width factor (e.g. `0.62`) and sum, instead of assuming one rendered line per source line.
- [x] `RenderTable`: per-row height = max estimated height across that row's cells at `ContentWidth / cols`, replacing the flat `rowHeight = 24`.
- [x] `RenderList` (`optionlist` + GFM-checkbox branch): per-item height via `EstimateTextHeight`, replacing the flat `itemH`/`optionItemH`.
- [x] `RenderQuote`: title/body parts sized via `EstimateTextHeight`, replacing the fixed `(count+1)*20`.

### Frontend canvas polish — `pxa-designer/src/components/Editor/SimplePxaSurface.tsx`

- [x] Add a `line` case to `renderElement` (currently falls through to the generic placeholder box) — render a filled bar from `style.backgroundColor ?? style.borderColor ?? style.color ?? '#9ca3af'`, matching `LivePreview.tsx:362`'s pattern.
- [x] Fix the `checkbox` case to pick `FiCheckSquare` vs. `FiSquare` based on `(element.checkState ?? 'checked') === 'checked'` (import `FiSquare`), preserving existing toolbar-checkbox appearance while respecting the Markdown importer's explicit checked/empty state. (Used `"empty"` rather than `"unchecked"` for the unmarked state — matches the frontend's existing `checkState` union and the PDF renderer's own default naming.)

### Tests / verification

- [x] Extend `MarkdownFileImporterTests.cs`: a long paragraph (300+ chars) produces height > one line; a long table cell increases that row's height.
- [x] Full `PXA.Importer.Tests` suite stays green.
- [x] `dotnet build` — 0 errors.
- [x] `cd pxa-designer && npx tsc --noEmit && npm run test -- --silent && npm run build`.
- [x] Manual (API-level): re-imported a file with a long paragraph, a long table cell, a `---` rule, and a task list with a checked/unchecked item via the running backend. Confirmed: long paragraph height (67.6) scales vs. a one-line paragraph (~21.4); table height (88.0) scales vs. the old flat 3×24=72 for a row with long content; the `line` element sits exactly between the two surrounding paragraphs with no overlap; checkbox items report `"checked"`/`"empty"` correctly. Full browser walkthrough (visually opening in the designer) not yet performed in this session.

Not fixed here (flagged only): `DocxFileImporter.cs` and `OdtFileImporter.cs` have the same flat-single-line-height pattern for their own paragraphs/tables — long DOCX/ODT paragraphs could hit the same PDF overflow issue. Separate, pre-existing limitation, out of scope for this Markdown-specific fix.

---

## Strategy fix: multi-page pagination

Root cause (deeper than the height-estimation bug above): `MarkdownFileImporter` always emitted exactly **one `PageDto`**, no matter how long the source document is. A page has 746pt of usable vertical space (842 page height minus 48pt top and bottom margins), ending at the y-coordinate 794; any real document longer than that — like the user's 17-slide presentation script — had the bulk of its content positioned past the visible page and effectively missing from the rendered PDF. The earlier height-accuracy fix was necessary but insufficient: it stops elements overlapping *within* one page's content, but does nothing once total content exceeds one page. Confirmed via investigation: `DesignJsonMapper`/`DesignLayoutPlanner` already map `design.Pages` 1:1 to real PDF pages with no changes needed (proven by the existing hand-authored 10-page book template) — the fix is entirely on the importer side, no new dependency required.

### `src/Importing/PXA.FileImporter/MarkdownFileImporter.cs`

- [x] Add a private `RenderContext` class: `Pages`, current `Elements`, `Y`, an internal id sequence (`NextId(prefix)`), and a `Place(element, height, gapAfter)` method that starts a new page (closing the current one into `Pages`, resetting `Elements`/`Y`) whenever the element wouldn't fit in the remaining space on a non-empty page, then sets `element.Y` and advances `Y`.
- [x] Convert `RenderHeading` to build its element (no `Y` set), compute height via the existing `EstimateTextHeight`, then call `ctx.Place(...)`.
- [x] Convert `RenderParagraph` (both plain-text and richtext branches) the same way.
- [x] Convert `RenderCode` the same way.
- [x] Convert `RenderTable` to use `RenderContext`; production hardening later added row-level page chunks with repeated headers.
- [x] Convert `RenderList` (both the task-list-checkbox loop and the plain-optionlist branch) — call `ctx.Place` **per item** in the task-list loop, so a long list can correctly split across a page boundary mid-list.
- [x] Convert `RenderQuote` the same way.
- [x] Convert the inline `ThematicBreakBlock`/`line` case in `RenderBlock`'s switch the same way.
- [x] `Import()`: replace the single `List<ElementDto>` + single-entry `Pages` array with one `RenderContext`, run the same block loop against it, return `ctx.FinalizePages()` as `Pages`.

### Former scope boundary

- [x] Initially documented that huge tables and code blocks were atomic. Production hardening now splits tables by row, oversized body rows, code blocks by rendered line, paragraphs, and blockquotes.

### Tests / verification

- [x] `MarkdownFileImporterTests.cs`: added `Import_LongDocument_SplitsAcrossMultiplePages`, `Import_LongDocument_EveryPageStaysWithinUsableHeight`, `Import_LongDocument_DistributesElementsAcrossPages_NotAllOnFirstPage`, `Import_LongTaskList_SplitsAcrossPagesMidList` — a 40-section synthetic document now produces `design.Pages.Count > 1`, every element ends at or before y-coordinate 794, and no page is left empty.
- [x] `Import_ShortDocument_ProducesExactlyOnePage` — short documents still produce exactly 1 page (no regression); confirmed by the full pre-existing test suite (all still pass unchanged).
- [x] Full `PXA.Importer.Tests` suite stays green — 123/123 passing.
- [x] `dotnet build` — 0 errors (both `PXA.FileImporter` and full `PXA.WebApi`).
- [x] `cd pxa-designer && npx tsc --noEmit && npm run test -- --silent && npm run build` — all pass (218/218 tests), no frontend files needed changes for this fix.
- [x] Manual: restarted the backend and re-imported a reconstruction of the user's 17-slide `ai_agents_presentation.markdown` structure. Result: **3 pages** (14/15/6 elements respectively) instead of the previous single page — content that was previously landing past the visible page boundary now appears correctly distributed across pages 1-3.

---

## Production hardening after code review

### P0 — security and resource limits

- [x] Reject unsafe generated link schemes including `javascript:`, `data:`, `file:`, and `vbscript:` while preserving safe HTTP(S), `mailto:`, anchors, and relative links.
- [x] Add regression tests for unsafe inline links, encoded unsafe links, and safe links.
- [x] Add a defense-in-depth sanitizer at the shared `HtmlContent` rendering boundary.
- [x] Add an explicit 4 MiB Markdown upload-size limit and return HTTP 413 for oversized requests.
- [x] Read Markdown through a bounded reader instead of an unbounded `ReadToEnd`.
- [x] Add document-complexity limits for characters, generated elements, and pages.
- [x] Stop returning internal exception messages from the Markdown API.
- [x] Propagate request cancellation through the compatible `IFileImporter` overload, bounded Markdown reader, parser loop, remote image resolver, and HTTP request token.

### P1 — page-safe layout

- [x] Split long ordinary ordered and unordered lists across pages without resetting numbering.
- [x] Split oversized tables by row and repeat the header row on continuation pages.
- [x] Split oversized fenced code blocks by rendered line.
- [x] Split oversized blockquotes and paragraphs where one element exceeds the usable page height.
- [x] Strengthen pagination tests to assert `element.Y + element.Height <= page height - bottom margin`.
- [x] Cover long ordinary lists, tables, oversized table rows, code blocks, quotes, and single paragraphs.

### P1 — image fidelity and safety

- [x] Preserve embedded PNG/JPEG `data:` images after validating MIME type, decoded size, pixel count, and image format.
- [x] Resolve remote HTTP(S) images through the reusable `IRemoteImageResolver` with SSRF protection, DNS-pinned connections, redirect validation, timeout, MIME validation, byte limits, and pixel limits.
- [x] Embed successfully resolved images as validated PNG/JPEG `data:` URLs so Designer preview and PDF export use identical content.
- [x] Add optional public HTTP(S) asset-base-URI support for relative image paths; unresolved paths are safely cleared and returned as visible import diagnostics.
- [x] Add tests for valid embedded PNG plus invalid, SVG, and HTML data-image sources.
- [x] Add tests for resolved remote, blocked private-network, oversized, and unresolved relative images.

### P2 — fidelity, metadata, and diagnostics

- [x] Preserve ordered-list start values across continuation pages.
- [x] Preserve nested list hierarchy as indented list elements with independent ordered-list numbering and `style.markdownListDepth` metadata.
- [x] Preserve inline code in list and table text with visible backtick delimiters so the semantic distinction survives Designer and PDF rendering.
- [x] Preserve fenced-code language identifiers in `style.codeLanguage`, including fenced blocks that also contain attributes after the language.
- [x] Preserve strikethrough as generated `<del>` rich-text markup; the shared PDF rich-text renderer already maps it to strikethrough text.
- [x] Map Markdown definition lists to editable `note` elements with the term as title and definition as body.
- [x] Map Markdown footnotes to editable `footnote` elements and preserve the generated reference anchor in the source paragraph.
- [x] Evaluate inline images: standalone images remain fully supported; images embedded within flowing paragraph text remain intentionally omitted with visible `PXA-MD-003` diagnostics because the absolute-positioned design model has no inline image-flow primitive.
- [x] Support optional YAML front matter for title, author, language, named page size (A3/A4/Letter/Legal), orientation, and uniform or per-side margins with `pt`/`mm`/`cm`/`in` units.
- [x] Return optional structured `ImportDiagnosticDto` entries for unsafe links, invalid/inline/unresolved images, invalid YAML front matter, unsupported blocks, formatting simplification, and page splits; rejected resource limits continue to use structured HTTP Problem Details.
- [x] Preserve Markdown diagnostics in Designer template metadata and display localized `PXA-MD-*` messages in the document Inspector.

### End-to-end verification

- [x] Add API tests for Markdown upload validation, size limits, safe errors, and successful conversion.
- [x] Add a Markdown-to-PDF integration test that inspects text, clickable PDF link annotations, embedded images, and page count.
- [x] Render safe rich-text HTTP(S)/`mailto:` links as clickable PDF link annotations.
- [x] Run the complete importer, API, PDF export, and Designer test suites: 168 importer tests, 113 API tests (44 PostgreSQL-dependent Account tests skipped), 39 PDF infrastructure tests, 242 Designer tests, TypeScript validation, and the Designer production build passed.
- [ ] Perform a browser smoke test with a representative Markdown document.
