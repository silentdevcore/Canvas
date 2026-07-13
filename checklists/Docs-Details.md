# Docs Details — Detailed Documentation for C#, UI Designer & AI

Detailed, demo-rich documentation for PXA's two API surfaces plus AI consumption. Mirrors how PDF SDK
vendors document (DocFX API reference + how-to guides + example gallery + `llms.txt`/MCP), anchored on a
single machine-readable **element catalog** so human docs, AI docs, and validation never drift.

## Context

PXA has two documentable surfaces — the imperative **C# `PXA.Pdf`** engine (`PXA/Pdf/`, ~150
public methods, ~5–10% with XML doc comments) and the declarative **`DesignExportDto` element model** (37
types in `PXA.Domain/ValueObjects/ElementType.cs`; properties in
`src/Core/PXA.Core/Contracts/DesignExportDto.cs`). Element metadata is currently scattered/duplicated across
the enum, `ElementDto`, `HelpModal.tsx`, and `DocsPage.tsx`. No DocFX, `llms.txt`, JSON-Schema, or MCP yet.

### Decisions (confirmed)
- AI output target: **both** declarative `DesignExportDto` JSON **and** imperative `PXA.Pdf` C#.
- AI mechanism: **`llms.txt` + JSON Schema first, MCP as a follow-up phase**.
- Human docs home: **expand the in-app `DocsPage`, catalog-driven**.
- C# reference: **XML comments + DocFX, plus a hand-written cookbook**.

### How providers document (chosen blend)
IronPDF / Aspose / iText / Syncfusion / DevExpress combine **(a)** auto-generated API reference (DocFX),
**(b)** task-oriented how-to guides with runnable snippets + output, **(c)** a categorized example/demo
gallery, and increasingly **(d)** `llms.txt` + shipped MCP servers. We adopt all four.

---

## Phase 0 — Single source of truth: the element catalog
- [x] Create `pxa-designer/src/docs/elementCatalog.ts` — one typed entry per element (all 38 frontend
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

## Phase 2 — C# API docs (`PXA.Pdf`): XML comments + DocFX + cookbook
- [x] `<GenerateDocumentationFile>true</GenerateDocumentationFile>` (+ `NoWarn CS1591` for incremental
      docs) on **`PXA.Infrastructure.Pdf.csproj`** (where the linked `PXA/Pdf/**` sources compile —
      *not* `PXA.csproj`, which excludes `Pdf/**`) and `PXA.Core.csproj`. Build clean; XML doc files
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
      runnable snippets; points back to `samples/PXA.Demo/Program.cs` and the API reference.

## Phase 3 — AI docs: `llms.txt` + JSON Schema (both surfaces)
- [x] `docs/schema/design-export.schema.json` (draft 2020-12) for `DesignExportDto`. Schema-driven test
      `__tests__/designSchema.test.ts`: the schema's element-type enum matches `ELEMENT_TYPES`, and every
      catalog example wrapped via `toDesign()` satisfies the schema's required-field + type-enum + numeric
      constraints (no `ajv` dependency — reads the real schema file).
- [x] OpenAPI artifact committed `docs/schema/openapi.json` (174 KB, from `/openapi/v1.json`).
- [x] `llms.txt` (concise) + `llms-full.txt` (all 38 elements with props/examples, both surfaces, the
      generate→validate→render loop, expression grammar, endpoints) at **repo root** (canonical for
      repo-reading agents + the MCP server; avoids the drift of duplicate served copies).
- [x] "AI & Codegen" section added to `DocsPage.tsx`: the two generation targets, the validation loop, and
      the machine-readable resources (llms.txt, schema, OpenAPI, catalog) + an MCP pointer. tsc clean;
      schema + catalog tests (6) green.

## Phase 4 — MCP server (follow-up)
- [x] `tools/PXA.Mcp` (TypeScript, `@modelcontextprotocol/sdk` v1.29, stdio) exposing tools
      `list_elements`, `get_element_schema(type)`, `get_example(type, surface)`, `search_docs(query)`,
      `validate_design(json)`, `render_preview(design)`, and resources `canvas://schema/design-export`,
      `canvas://openapi`, `canvas://docs/llms-full`, `canvas://docs/cookbook`.
- [x] Source of truth: imports the **element catalog directly** (run via `tsx`, which erases the type-only
      `ElementType` import) so it never drifts; reads the Phase 3 schema/OpenAPI/llms-full/cookbook from the
      repo; `render_preview` proxies `POST /api/templates/render-design`. `validate_design` reuses the
      schema's required-fields + type-enum rules (no external validator).
- [x] `README.md` (install + Claude Desktop/Code config) + `smoke.ts`. Smoke test green: 6 tools registered,
      38 elements, valid/invalid `validate_design`, resource exposed. `node_modules` git-ignored.

## Phase 5 — Polish & verification
- [x] `docs/Documentation-Approach.md`: how vendors document (4 pillars) + the chosen blend, the single
      source of truth (catalog), where each artifact lives, and the "when you add a capability" rules.
- [x] Updated `checklists/Documentation-Audit.md` source-of-truth rules: element catalog, design JSON
      schema, AI/MCP reference, C# DocFX + cookbook, and the documentation-strategy doc.
- [x] Final verification: frontend tsc clean (0 non-PdfViewer errors), **178** frontend tests pass, MCP
      smoke green, backend `:5086` + frontend `:5173` both 200. *(Pre-existing unrelated `PdfViewer.tsx`
      compile error — not part of this work — keeps one suite red.)*

## Verification
- Drift-guard tests green; `HelpModal`/`DocsPage` render from the catalog (no duplicated arrays).
- `dotnet build` clean with `GenerateDocumentationFile`; DocFX builds without warnings on documented types.
- JSON-Schema test validates catalog examples + sample templates.
- Per-element `DocsPage` demos render via `/api/templates/render-design`.
- `llms.txt` link-check; OpenAPI + schema artifacts committed.
- (Phase 4) MCP smoke test returns expected data.
- `npm run build` + `npx tsc --noEmit` clean; backend `:5086` + frontend `:5173` respond.
