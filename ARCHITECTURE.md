# Power Dox Automation / PXA - Architecture

## Naming Glossary

| Name | Meaning |
|------|---------|
| Power Dox Automation | Product and website name |
| PXA | Developer/API/CLI identity and reserved future `.pxa` file-format prefix |
| `PXA.*` | Active backend namespaces, project names, and public developer-facing API |
| `PXA.*` | Historical project/namespace name removed by the breaking PXA rename |

## Dependency Direction

```text
PXA.WebApi
   |
   v
PXA.Application
   |
   v
PXA.Core  <----------------------------------------------------+
   ^                                                          |
   |                                                          |
PXA.Infrastructure.Pdf          PDF rendering/services         |
PXA.Infrastructure.Word         DOCX export/import/signing     |
PXA.Infrastructure.Spreadsheet  XLSX/CSV spreadsheet services  |
PXA.Infrastructure.Converters   ODT/HTML/CSV/Markdown/Image    |
PXA.FileImporter                Dedicated import adapters      |
PXA.Importer                    Low-level PDF parser/editor     |
PXA.Migration.*                 Vendor/report migration tools   |
PXA.Domain                      Template/domain models          |
PXA.Pdf                         Imperative PDF engine           |
```

Rule: `PXA.Core` and `PXA.Domain` stay implementation-agnostic. Infrastructure, importer, and migration projects
may consume core contracts, but core/domain projects must not depend on concrete renderers, parser engines,
WebApi, or vendor migration providers.

## Project Responsibilities

### `src/Core/PXA.Core`

- `Contracts/DesignExportDto.cs` and `SpreadsheetDto.cs` are the canonical serialized contracts shared by renderers, importers, migrations, and the API.
- `Abstractions/` contains renderer, output, capability, document-service, and template-service contracts.
- `Capabilities/` describes supported renderer features and fallback behavior.
- `Primitives/` contains the expression evaluator, layout planner, formatters, and shared value objects.

### `src/Core/PXA.Application`

- Use cases include export, validate, find-and-replace, clone, extract-pages, page numbering, table flow, headers/footers, watermarks, diagnostics, authentication, and template CRUD orchestration.
- Depends on `PXA.Core` and `PXA.Domain`; it should not reference concrete Infrastructure, Importer, or Migration projects.

### `src/Core/PXA.Domain`

- Owns template metadata, designer element models, page settings, validation results, repository contracts, and domain value objects.

### `PXA.WebApi`

- Presentation layer and composition root.
- Wires concrete infrastructure, importer, migration, template, auth, and PDF viewer services.
- Keeps legacy HTTP routes such as `/api/export` compatible while also exposing PXA aliases such as `/api/pxa/export`.

## Rendering Infrastructure

### `src/Generation/PXA.Pdf`

Imperative PDF engine: document/page model, writer, text/image/vector/form elements, encryption, metadata,
bookmarks, named destinations, headers/footers, page numbering, tables, table of contents, and watermarks.
New product-facing examples should create documents through `PXA.Generator.Pdf.CreateDocument(...)`.

### `src/Infrastructure/PXA.Infrastructure.Pdf`

- Renders `DesignExportDto` content through `PXA.Pdf`.
- Service adapters apply page numbering, headers/footers, table flow, table of contents, watermarks, diagnostics, and page coverage queries.
- `PdfRendererCapabilities` declares supported PDF-renderer features.

### Other Export Infrastructure

- `src/Infrastructure/PXA.Infrastructure.Word`: DOCX export/import support, OOXML styles, footnotes/endnotes, comments, content controls, protection, custom properties, and DOCX digital signing.
- `src/Infrastructure/PXA.Infrastructure.Spreadsheet`: workbook authoring, import/export, calculations, validation, spreadsheet-to-design conversion, and Excel document export.
- `src/Infrastructure/PXA.Infrastructure.Converters`: ODT, HTML, CSV, Markdown, image, TIFF, SVG/XML-style export adapters and shared converter capabilities.

## Import Infrastructure

- `src/Importing/PXA.FileImporter`: shared importer abstraction plus PDF, DOCX, DOC, ODT, SVG, PPTX, and raster image adapters.
- `src/Importing/PXA.FileImporter.ImageAnalysis`: deterministic raster analysis pipeline.
- `src/Importing/PXA.FileImporter.ImageOcr` and `.Worker`: OCR-based image conversion path and isolated worker.
- `src/Importing/PXA.Importer`: low-level PDF tokenizer, object parser, stream/content parser, graphics interpreter, editable PDF model, and regeneration bridge into `PXA.Pdf`.

## Migration Infrastructure

- `src/Migrations/Common/PXA.Migration.Abstractions`: migration result and diagnostic contracts plus shared helpers.
- `src/Migrations/Common/PXA.Migration.Roslyn`: shared C# source migration helpers.
- `src/Migrations/PDF/PXA.Migration.Pdf`: PDF provider registry/aggregator.
- `src/Migrations/Spreadsheet/PXA.Migration.Spreadsheet`: spreadsheet provider registry/aggregator.
- `src/Migrations/Report/PXA.Migration.Report`: report-to-design provider registry/aggregator.

Provider projects are isolated under `src/PXA.Migration.<Provider>` with matching `tests/PXA.Migration.<Provider>.Tests`
projects. PDF code migration providers output PXA-compatible C# source; report converters output `DesignExportDto`.

## Frontend Boundaries

`pxa-designer` is the active frontend.

- `components/Editor/`: main editor, canvas, panels, export dialogs, code editor, localized properties, and modals.
- `components/Gallery/`: template browsing and cards.
- `pages/`: index, docs, template, create, importer, migrations.
- `services/`: export/import/migration/code-generation service calls.
- `store.ts`: Zustand editor state, pages, shared elements, undo/redo, bulk replacement.
- `types.ts`: frontend document and element contracts.

The older `ui-designer` tree has been removed. Historical checklists may still mention it when they describe
past implementation work, but all active frontend work belongs in `pxa-designer`.

## Boundary Rules

1. `PXA.Core` / `PXA.Domain` must remain implementation-agnostic.
2. `PXA.Application` must not reference concrete Infrastructure, Importer, or Migration projects.
3. Infrastructure projects implement Core abstractions and should not reference one another unless a documented adapter boundary requires it.
4. File importer projects should reference `PXA.Core` and only the parser libraries they need.
5. Migration provider projects must stay isolated from renderer/importer infrastructure except for shared migration abstractions and Roslyn helpers.
6. Report migration projects output `DesignExportDto`; PDF code migration projects output PXA-compatible PDF C# source.
7. `PXA.WebApi` is the composition root and owns endpoint wiring.

## Validation

- `dotnet build PXA.sln` must produce 0 errors for code changes.
- Run focused tests for the touched subsystem, then broader tests when shared contracts or endpoints change.
- Frontend changes should validate with `cd pxa-designer && npm run build` and relevant Jest tests.
- Documentation changes should keep links relative and prefer `README.md`, this file, `CONTRIBUTING_RENDERERS.md`, and `TESTING.md` as stable entry points.
