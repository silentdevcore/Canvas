# PVADE / PVAD - Architecture

## Naming Glossary

| Name | Meaning |
|------|---------|
| PVADE | Platform, website and Designer display name |
| PVAD / `PVAD.*` | Active technical identity, projects, namespaces, APIs and CLI |
| PVADP / PVADS | PDF / Spreadsheet products and `.pvadp` / `.pvads` projects |
| PXA / `PXA.*` | Previous identity; bounded adapters and immutable history only |

## Dependency Direction

```text
PVAD.WebApi
   |
   v
PVAD.Application
   |
   v
PVAD.Core  <----------------------------------------------------+
   ^                                                          |
   |                                                          |
PVAD.Infrastructure.Pdf          PDF rendering/services         |
PVAD.Infrastructure.Word         DOCX export/import/signing     |
PVAD.Infrastructure.Spreadsheet  XLSX/CSV spreadsheet services  |
PVAD.Infrastructure.Converters   ODT/HTML/CSV/Markdown/Image    |
PVAD.FileImporter                Dedicated import adapters      |
PVAD.Importer                    Low-level PDF parser/editor     |
PVAD.Migration.*                 Vendor/report migration tools   |
PVAD.Domain                      Template/domain models          |
PVAD.Pdf                         Imperative PDF engine           |
```

Rule: `PVAD.Core` and `PVAD.Domain` stay implementation-agnostic. Infrastructure, importer, and migration projects
may consume core contracts, but core/domain projects must not depend on concrete renderers, parser engines,
WebApi, or vendor migration providers.

## Project Responsibilities

### `src/Core/PVAD.Core`

- `Contracts/DesignExportDto.cs` and `SpreadsheetDto.cs` are the canonical serialized contracts shared by renderers, importers, migrations, and the API.
- `Abstractions/` contains renderer, output, capability, document-service, and template-service contracts.
- `Capabilities/` describes supported renderer features and fallback behavior.
- `Primitives/` contains the expression evaluator, layout planner, formatters, and shared value objects.

### `src/Core/PVAD.Application`

- Use cases include export, validate, find-and-replace, clone, extract-pages, page numbering, table flow, headers/footers, watermarks, diagnostics, authentication, and template CRUD orchestration.
- Depends on `PVAD.Core` and `PVAD.Domain`; it should not reference concrete Infrastructure, Importer, or Migration projects.

### `src/Core/PVAD.Domain`

- Owns template metadata, designer element models, page settings, validation results, repository contracts, and domain value objects.

### `PVAD.WebApi`

- Presentation layer and composition root.
- Wires concrete infrastructure, importer, migration, template, auth, and PDF viewer services.
- Exposes canonical `/api/pvad/v1` routes and documented neutral routes; legacy `/api/pxa` adapters remain during the compatibility window.

## Rendering Infrastructure

### `src/Generation/PVAD.Pdf`

Imperative PDF engine: document/page model, writer, text/image/vector/form elements, encryption, metadata,
bookmarks, named destinations, headers/footers, page numbering, tables, table of contents, and watermarks.
New product-facing examples should create documents through `PVAD.Generator.Pdf.CreateDocument(...)`.

### `src/Infrastructure/PVAD.Infrastructure.Pdf`

- Renders `DesignExportDto` content through `PVAD.Pdf`.
- Service adapters apply page numbering, headers/footers, table flow, table of contents, watermarks, diagnostics, and page coverage queries.
- `PdfRendererCapabilities` declares supported PDF-renderer features.

### Other Export Infrastructure

- `src/Infrastructure/PVAD.Infrastructure.Word`: DOCX export/import support, OOXML styles, footnotes/endnotes, comments, content controls, protection, custom properties, and DOCX digital signing.
- `src/Infrastructure/PVAD.Infrastructure.Spreadsheet`: workbook authoring, import/export, calculations, validation, spreadsheet-to-design conversion, and Excel document export.
- `src/Infrastructure/PVAD.Infrastructure.Converters`: ODT, HTML, CSV, Markdown, image, TIFF, SVG/XML-style export adapters and shared converter capabilities.

## Import Infrastructure

- `src/Importing/PVAD.FileImporter`: shared importer abstraction plus PDF, DOCX, DOC, ODT, SVG, PPTX, and raster image adapters.
- `src/Importing/PVAD.FileImporter.ImageAnalysis`: deterministic raster analysis pipeline.
- `src/Importing/PVAD.FileImporter.ImageOcr` and `.Worker`: OCR-based image conversion path and isolated worker.
- `src/Importing/PVAD.Importer`: low-level PDF tokenizer, object parser, stream/content parser, graphics interpreter, editable PDF model, and regeneration bridge into `PVAD.Pdf`.

## Migration Infrastructure

- `src/Migrations/Common/PVAD.Migration.Abstractions`: migration result and diagnostic contracts plus shared helpers.
- `src/Migrations/Common/PVAD.Migration.Roslyn`: shared C# source migration helpers.
- `src/Migrations/PDF/PVAD.Migration.Pdf`: PDF provider registry/aggregator.
- `src/Migrations/Spreadsheet/PVAD.Migration.Spreadsheet`: spreadsheet provider registry/aggregator.
- `src/Migrations/Report/PVAD.Migration.Report`: report-to-design provider registry/aggregator.

Provider projects are isolated under `src/PVAD.Migration.<Provider>` with matching `tests/PVAD.Migration.<Provider>.Tests`
projects. PDF code migration providers output PVAD-compatible C# source; report converters output `DesignExportDto`.

## Frontend Boundaries

`pvad-designer` is the active frontend.

- `components/Editor/`: main editor, canvas, panels, export dialogs, code editor, localized properties, and modals.
- `components/Gallery/`: template browsing and cards.
- `pages/`: index, docs, template, create, importer, migrations.
- `services/`: export/import/migration/code-generation service calls.
- `store.ts`: Zustand editor state, pages, shared elements, undo/redo, bulk replacement.
- `types.ts`: frontend document and element contracts.

The older `ui-designer` tree has been removed. Historical checklists may still mention it when they describe
past implementation work, but all active frontend work belongs in `pvad-designer`.

## Boundary Rules

1. `PVAD.Core` / `PVAD.Domain` must remain implementation-agnostic.
2. `PVAD.Application` must not reference concrete Infrastructure, Importer, or Migration projects.
3. Infrastructure projects implement Core abstractions and should not reference one another unless a documented adapter boundary requires it.
4. File importer projects should reference `PVAD.Core` and only the parser libraries they need.
5. Migration provider projects must stay isolated from renderer/importer infrastructure except for shared migration abstractions and Roslyn helpers.
6. Report migration projects output `DesignExportDto`; PDF code migration projects output PVAD-compatible PDF C# source.
7. `PVAD.WebApi` is the composition root and owns endpoint wiring.

## Validation

- `dotnet build PVAD.sln` must produce 0 errors for code changes.
- Run focused tests for the touched subsystem, then broader tests when shared contracts or endpoints change.
- Frontend changes should validate with `cd pvad-designer && npm run build` and relevant Jest tests.
- Documentation changes should keep links relative and prefer `README.md`, this file, `CONTRIBUTING_RENDERERS.md`, and `TESTING.md` as stable entry points.
