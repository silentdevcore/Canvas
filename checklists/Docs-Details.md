# Docs Details — Detailed Documentation for C#, UI Designer & AI

Detailed, demo-rich documentation for Canvas's two API surfaces plus AI consumption. Mirrors how PDF SDK
vendors document (DocFX API reference + how-to guides + example gallery + `llms.txt`/MCP), anchored on a
single machine-readable **element catalog** so human docs, AI docs, and validation never drift.

## Context

Canvas has two documentable surfaces — the imperative **C# `Canvas.Pdf`** engine (`Canvas/Pdf/`, ~150
public methods, ~5–10% with XML doc comments) and the declarative **`DesignExportDto` element model** (37
types in `Canvas.Domain/ValueObjects/ElementType.cs`; properties in
`src/Canvas.Core/Contracts/DesignExportDto.cs`). Element metadata is currently scattered/duplicated across
the enum, `ElementDto`, `HelpModal.tsx`, and `DocsPage.tsx`. No DocFX, `llms.txt`, JSON-Schema, or MCP yet.

### Decisions (confirmed)
- AI output target: **both** declarative `DesignExportDto` JSON **and** imperative `Canvas.Pdf` C#.
- AI mechanism: **`llms.txt` + JSON Schema first, MCP as a follow-up phase**.
- Human docs home: **expand the in-app `DocsPage`, catalog-driven**.
- C# reference: **XML comments + DocFX, plus a hand-written cookbook**.

### How providers document (chosen blend)
IronPDF / Aspose / iText / Syncfusion / DevExpress combine **(a)** auto-generated API reference (DocFX),
**(b)** task-oriented how-to guides with runnable snippets + output, **(c)** a categorized example/demo
gallery, and increasingly **(d)** `llms.txt` + shipped MCP servers. We adopt all four.

---

## Phase 0 — Single source of truth: the element catalog
- [x] Create `ui-designer-v2/src/docs/elementCatalog.ts` — one typed entry per element (all 38 frontend
      `ElementType`s): `type`, `label`, `category`, `description`, `formatSupport` (pdf/word/html/excel),
      `bindable`, type-specific `properties[]`, an `example` ElementDto (+ `toDesign()` wrapper), and
      optional `csharpExample`. Shared props/style keys documented once in `COMMON_PROPERTIES`/`STYLE_KEYS`.
- [x] Seed from canonical sources: `ElementType.cs`, `DesignExportDto.cs` (`ElementDto` fields),
      `HelpModal.tsx` `ELEMENTS`, and the renderer switches for the support matrix.
- [x] `types.ts`: `ElementType` is now derived from a runtime `ELEMENT_TYPES` array (the authoritative list
      the catalog is drift-guarded against). Refactored `HelpModal.tsx` to render from the catalog (removed
      its duplicated `ELEMENTS` array). *DocsPage Elements Reference is rebuilt catalog-driven in Phase 1
      to avoid redundant churn.*
- [x] Drift-guard test `__tests__/elementCatalog.test.ts` (every `ElementType` ↔ exactly one entry, well-
      formed, lookup/grouping helpers). 4 tests green; full frontend suite 176 tests pass.

## Phase 1 — UI-designer docs (catalog-driven, demos + examples)
- [x] Rebuilt the `DocsPage.tsx` Elements Reference from the catalog: common-properties + style-keys tables,
      a support matrix (anchored links), and per-category **ElementCards** — each with description, a
      type-specific property table, a copy-paste **design JSON**, a **C#** example where it maps, and a
      **live "Render preview"** that POSTs to `/api/templates/render-design` and shows the PDF in an iframe
      (verified end-to-end: HTTP 200 → valid `%PDF`). Card styling added to `src/styles/docs.css`.
- [x] Live demo uses the existing render path (no new infra). Build-time PNG export deferred (the
      on-demand iframe preview covers it).
- [x] Added a **Data Binding & Expressions** section: tokens (`{{field}}`), the expression grammar
      (`$iif/$concat/$coalesce/$switch`, aggregates incl. computed-arg + `$group`, short-circuiting
      operators), and repeats + group aggregates. tsc clean; 176 frontend tests pass.

## Phase 2 — C# API docs (`Canvas.Pdf`): XML comments + DocFX + cookbook
- [x] `<GenerateDocumentationFile>true</GenerateDocumentationFile>` (+ `NoWarn CS1591` for incremental
      docs) on **`Canvas.Infrastructure.Pdf.csproj`** (where the linked `Canvas/Pdf/**` sources compile —
      *not* `Canvas.csproj`, which excludes `Pdf/**`) and `Canvas.Core.csproj`. Build clean; XML doc files
      produced for both.
- [x] XML comments on the high-traffic surface: `PdfDocument` (class + ctor + AddPage/AddPageRotated/Info/
      AddBookmark/AddTableOfContents/AddPageNumbers/AddTextWatermark/ToBytes), `PdfPage` (class +
      DrawText/DrawParagraph/DrawImage/DrawSimpleTable/AddWebLink/AddCheckBox), and the color structs.
      *(Remaining members documented incrementally; CS1591 keeps the build clean meanwhile.)*
- [x] `docs/docfx.json` + `toc.yml` + `index.md` + `api/index.md` + `docs/README.md` (documents
      `docfx metadata/build docs/docfx.json`); `docs/.gitignore` excludes the generated `api/*.yml` + `_site/`.
      *(DocFX tool not installable in this sandbox — config + command shipped; XML docs already generated.)*
- [x] C# cookbook `docs/csharp-cookbook.md`: categorized recipes (getting-started, text, paragraphs,
      shapes, images, tables, links/nav, forms, page numbers, watermark, TOC, encryption, metadata) with
      runnable snippets; points back to `samples/Canvas.Demo/Program.cs` and the API reference.

## Phase 3 — AI docs: `llms.txt` + JSON Schema (both surfaces)
- [ ] JSON Schema for `DesignExportDto` → `docs/schema/design-export.schema.json`; test validates catalog
      `exampleDesign` payloads + sample templates.
- [ ] Commit OpenAPI artifact `docs/schema/openapi.json` (from `AddOpenApi()` / `/openapi/v1.json`).
- [ ] `llms.txt` + `llms-full.txt` at repo root (served at `/llms.txt`): capability summary, element catalog,
      endpoints, C# cheatsheet, and end-to-end examples for **both** surfaces (design JSON → `/api/export`,
      and a `Canvas.Pdf` C# snippet).
- [ ] "AI usage" doc section: both codegen targets + validation loop (generate → validate → `/api/export`
      or `csharp-code-to-pdf`).

## Phase 4 — MCP server (follow-up)
- [ ] `tools/Canvas.Mcp` exposing `list_elements`, `get_element_schema(type)`, `get_example(type, surface)`,
      `search_docs(query)`, `get_csharp_api(symbol)`, `validate_design(json)`, `render_preview(design)`.
- [ ] Back it with the Phase 0 catalog, Phase 3 schema/OpenAPI, DocFX metadata, and existing endpoints.
- [ ] Install/config docs for Claude Desktop / Claude Code + an MCP smoke test.

## Phase 5 — Polish & verification
- [ ] `docs/Documentation-Approach.md`: provider comparison + chosen blend.
- [ ] Update `checklists/Documentation-Audit.md` source-of-truth rules (catalog, `llms.txt`, JSON Schema).

## Verification
- Drift-guard tests green; `HelpModal`/`DocsPage` render from the catalog (no duplicated arrays).
- `dotnet build` clean with `GenerateDocumentationFile`; DocFX builds without warnings on documented types.
- JSON-Schema test validates catalog examples + sample templates.
- Per-element `DocsPage` demos render via `/api/templates/render-design`.
- `llms.txt` link-check; OpenAPI + schema artifacts committed.
- (Phase 4) MCP smoke test returns expected data.
- `npm run build` + `npx tsc --noEmit` clean; backend `:5086` + frontend `:5173` respond.
