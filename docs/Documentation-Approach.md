# Documentation approach

How PVADE / PVAD documents its two API surfaces (the visual-designer element model and the
PVAD-compatible PDF C# API) and how AI agents consume it. Use this as the reference when adding new docs.

## How PDF SDK vendors document (and what we took)

| Pillar | What vendors do (IronPDF, Aspose, iText, Syncfusion, DevExpress) | PVAD |
| --- | --- | --- |
| **API reference** | Auto-generated from XML/Doxygen comments (DocFX). | XML comments on the active `PVAD.Pdf` implementation + `GenerateDocumentationFile`; `docs/docfx.json` builds the PVAD-facing reference. |
| **How-to guides** | Task-oriented articles with runnable snippets + expected output. | `docs/csharp-cookbook.md`; per-element design JSON + C# in the app docs. |
| **Example / demo gallery** | A categorized gallery of runnable examples. | Per-element **live render previews** in the in-app Elements Reference. |
| **AI-readable docs** | `llms.txt` and (increasingly) shipped MCP servers. | `llms.txt` / `llms-full.txt` + the `pvad-mcp` server in `tools/PVAD.Mcp`. |

## The single source of truth

Everything is anchored on the **element catalog** (`pvad-designer/src/docs/elementCatalog.ts`): one typed
entry per element with properties, format support, and examples. It drives:

- the in-app **Elements Reference** (`DocsPage.tsx`) and the Help dialog (`HelpModal.tsx`),
- the AI snapshot (`llms-full.txt`) and the JSON Schema's element-type enum,
- the **MCP server** (imported directly, so it cannot drift).

A drift-guard test (`__tests__/elementCatalog.test.ts`) fails if the catalog and the `ElementType` union
ever diverge, so adding an element forces a catalog entry.

## Where things live

| Audience | Artifact |
| --- | --- |
| Designer users | In-app docs at `/docs` (`DocsPage.tsx`), driven by the catalog, with live demos. |
| C# developers | `docs/csharp-cookbook.md` + the DocFX API reference (`docs/README.md` → build steps). |
| AI agents | `llms.txt` / `llms-full.txt`, `docs/schema/design-export.schema.json`, `docs/schema/openapi.json`, and the `pvad-mcp` server. |

## When you add a capability

1. New element type → add a catalog entry (the drift test enforces it); the in-app docs, `llms-full.txt`,
   schema enum, and MCP update from there.
2. New public PVAD-compatible PDF API → add XML doc comments + a cookbook recipe.
3. New endpoint → it appears in `docs/schema/openapi.json` automatically; reference it in `llms.txt`.
