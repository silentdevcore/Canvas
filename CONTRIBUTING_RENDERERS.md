# Contributor Guide: Adding Renderers, Importers, and Migrations

This guide covers the main extension patterns in PVADE / PVAD:

- output renderers and exporters
- file importers
- PDF code migration providers
- report-to-design converters
- document operation endpoints

Keep new code inside the smallest project boundary that owns the feature, and update docs/checklists in the same change. PVAD is the technical identity; PVADE is the platform display name. New developer-facing APIs and implementation projects use `PVAD.*`. Preserve explicitly documented legacy readers and immutable historical evidence.

---

## Pattern A - Output Renderer / Export Format

### 1. Create or choose the infrastructure project

Use an existing infrastructure project when the format belongs to an established adapter family:

- `src/Infrastructure/PVAD.Infrastructure.Pdf` for PDF rendering services.
- `src/Infrastructure/PVAD.Infrastructure.Word` for DOCX/Word output.
- `src/Infrastructure/PVAD.Infrastructure.Spreadsheet` for XLSX output.
- `src/Infrastructure/PVAD.Infrastructure.Converters` for ODT, HTML, CSV, Markdown, image, TIFF, SVG/XML-style output.

Create `src/Infrastructure/PVAD.Infrastructure.<Format>` only when the format needs its own dependencies or boundary.

When the feature is public developer-facing API, add or update the corresponding `PVAD.Infrastructure.*` or
`PVAD.Generator` facade so new examples do not need to start from old PXA names.

Dependencies:

- Required: `PVAD.Core`
- Optional: `PVAD.Application` only when orchestration helpers are explicitly needed
- Avoid references to other infrastructure projects

### 2. Implement the exporter

Implement `IDocumentRenderer` or the existing format-specific equivalent from `PVAD.Core.Abstractions`.

Naming convention:

```text
<Format>DocumentExporter
```

Examples: `TiffDocumentExporter`, `OdtDocumentExporter`, `ExcelDocumentExporter`.

Implement supporting services where applicable:

- `IPageNumberingService`
- `IHeaderFooterService`
- `ITableFlowService`
- `ITableOfContentsService`
- `IWatermarkService`
- `IDiagnosticsReader`
- `IRendererCapabilities`

`IRendererCapabilities` must clearly mark supported and unsupported features.

### 3. Register the backend

Wire the exporter in `PVAD.WebApi/Program.cs` and expose it through `ExportController`.

Update:

- export format switch/dispatch
- DI registration
- format metadata from `GET /api/export/formats`, if relevant

### 4. Add the frontend format entry

In `pvad-designer/src/services/ExportService.ts`:

- add the format literal to the `ExportFormat` union
- add the extension to the extension map

In `pvad-designer/src/components/Editor/ExportModal.tsx`:

- add a format option in the appropriate group
- keep server-side/client-side behavior consistent with existing options

### 5. Add tests

Add or update tests in the closest project:

- `tests/PVAD.Export.Tests` for end-to-end export behavior
- a dedicated infrastructure test project if the renderer has substantial internal behavior
- snapshot tests only when output is stable enough to make them meaningful

### 6. Update docs

- `README.md`: export format table and API notes
- `PROJECT_SUMMARY.md`: feature inventory, if the format is user-facing
- `ARCHITECTURE.md`: infrastructure responsibilities, if a project/boundary changed
- relevant checklist under `checklists/`

---

## Pattern B - File Importer

New file importers should expose a PVAD-facing facade and use the dedicated PVAD implementation project pattern.

### 1. Create the importer project

Project layout:

```text
src/Importing/PVAD.FileImporter.<Format>/
tests/PVAD.FileImporter.<Format>.Tests/
```

Public facade target:

```text
src/Importing/PVAD.FileImporter.<Format>/
```

Dependencies:

- `PVAD.FileImporter`
- `PVAD.Core`
- parser/conversion packages needed by that format only
- `PVAD.Importer` only for PDF-specific importers that need the PDF parser/editor engine

Avoid placing new importers in `PVAD.Infrastructure.Converters`; that project now owns converter-style exporters and shared converter infrastructure, not the preferred importer boundary.

### 2. Implement `IFileImporter`

Implement the importer contract from `src/Importing/PVAD.FileImporter`.

Naming convention:

```text
<Format>FileImporter
```

The importer should return `DesignExportDto` and diagnostics/fallbacks consistent with existing importers. Preserve layout, page size, coordinates, images, text styles, and placeholders where fidelity is not yet possible.

### 3. Register the importer endpoint

Add or update `DocumentOpsController`:

```csharp
[HttpPost("import-<format>")]
[Consumes("multipart/form-data")]
public async Task<IActionResult> Import<Format>(IFormFile? file)
```

Validate:

- missing file
- extension and MIME type
- empty stream
- importer exceptions mapped to useful HTTP errors

Current import endpoints include:

- `import-pdf-engine`
- `import-docx`
- `import-doc`
- `import-odt`
- `import-image`
- `import-svg`
- `import-pptx`
- `import-image-analysis`

### 4. Add frontend support

Update `pvad-designer`:

- `ExportService.ts`: service method calling the endpoint
- `useTemplateLoader.ts`: file-extension dispatch
- relevant page/modal accept attributes
- user-facing diagnostics or loading state, if needed

### 5. Add tests

Add focused tests in `tests/PVAD.FileImporter.<Format>.Tests`.

Minimum coverage:

- valid sample imports to `DesignExportDto`
- invalid/empty input behavior
- key layout and element mapping assertions
- endpoint smoke test in `PVAD.Api.Tests` when the WebApi route is new

### 6. Update docs

- `README.md`: import format table and API reference
- `PROJECT_SUMMARY.md`: import inventory
- `ARCHITECTURE.md`: importer project responsibility
- importer checklist under `checklists/`

---

## Pattern C - PDF Code Migration Provider

Use this pattern when converting C# source from a third-party PDF library into PVAD-compatible PDF C# source.

### 1. Create provider project and tests

Project layout:

```text
src/PVAD.Migration.<Provider>/
tests/PVAD.Migration.<Provider>.Tests/
```

Public facade target:

```text
src/PVAD.Migration.<Provider>/ or provider registry under src/Migrations/PDF/PVAD.Migration.Pdf/
```

Dependencies:

- `PVAD.Migration.Abstractions`
- `PVAD.Migration.Roslyn` for C# source rewriting providers
- `PVAD.Core` only when DTO/contracts are required

Do not reference rendering/importer infrastructure from provider migration projects.

### 2. Define provider scope

Document:

- provider package/API identity
- namespaces and common classes
- deterministic mappings supported automatically
- unsupported/manual areas
- diagnostic IDs
- before/after samples

Keep the first slice conservative: document creation, page creation, simple text, simple shapes/images if deterministic, save/export, and diagnostics for everything else.

### 3. Implement conversion

Preferred implementation:

- Roslyn-backed rewriter under `src/PVAD.Migration.<Provider>`
- provider-neutral result and diagnostic contracts from `PVAD.Migration.Abstractions`
- no silent destructive rewrites
- preserve source where manual follow-up is required

### 4. Register in WebApi

Add the converter to the appropriate typed aggregator and register one
`MigrationProviderDescriptor` in `MigrationProviderCatalog`.

Ensure it appears in:

- `GET /api/pvad/v1/migrations/catalog`
- `POST /api/pvad/v1/migrations/{domain}/{kind}/{provider}/convert`
- `POST /api/pvad/v1/migrations/{domain}/{kind}/{provider}/preview`, when preview is supported

The descriptor is the source for domain, kind, canonical key, historical aliases,
inputs, artifacts, status, Documentation ID, feature gate, limits, and canonical
permission. Do not add a second frontend provider list. The legacy
`/api/migration/*` routes are compatibility adapters and must resolve through the
same registry and orchestration.

### 5. Add tests

Provider tests should cover:

- basic conversion sample
- unsupported/manual diagnostic sample
- namespace cleanup
- compile-shaped output when practical
- provider-specific edge cases documented in the checklist

### 6. Update docs

- provider checklist: `checklists/Code-Migration-<Provider>.md`
- overview checklist: `checklists/Code-Migrations.md`
- `PROJECT_SUMMARY.md` if adding a new provider family

---

## Pattern D - Report-To-Design Converter

Use this pattern when a reporting framework or report file format should become an editable PVAD design.

Existing examples:

- `PVAD.Migration.Report.Designer.DevExpress`: XtraReport C# and REPX
- `PVAD.Migration.Report.Designer.Rdl`: RDL/RDLC/Syncfusion/Bold Reports style XML
- `PVAD.Migration.Report.Designer.Rpx`: ActiveReports/GrapeCity section reports

### 1. Target `DesignExportDto`

Report converters output editable design JSON, not PVAD-compatible PDF C# source.

The converter should preserve:

- page size and margins
- regions/bands/header/footer semantics
- absolute element positions
- text, images, lines, rectangles, tables
- bindings/expressions where possible
- placeholders and diagnostics for unsupported report items

### 2. Keep the converter isolated

Project layout:

```text
src/PVAD.Migration.<ReportFormat>/
tests/PVAD.Migration.<ReportFormat>.Tests/
```

Use `PVAD.Migration.Abstractions` for diagnostics and `PVAD.Core` for `DesignExportDto`.

### 3. Wire endpoint routing

Register the converter in `MigrationProviderCatalog` with domain `report` and kind
`designer`. The canonical endpoint is:

```text
POST /api/pvad/v1/migrations/report/designer/{provider}/convert
```

Normal requests select a provider explicitly. Detection must be explicit and
ordered for the compatibility provider `auto` so similar formats do not steal
each other's inputs. The legacy `/api/migration/report-to-design` endpoint remains
an adapter for one major release.

### 4. Add tests

Cover:

- format detection
- page geometry/unit conversion
- each supported control/item
- unsupported item diagnostics
- invalid input
- end-to-end render through the real export pipeline when possible

### 5. Update docs

- dedicated checklist, for example `Code-Migration-<ReportFormat>.md`
- `README.md` and `PROJECT_SUMMARY.md` if user-facing
- `ARCHITECTURE.md` when adding a new project family

---

## Pattern E - Document Operation Endpoint

Use this pattern for operations such as clone, extract-pages, sign, find-replace, or future document transforms.

### 1. Add a use case

Place orchestration in:

```text
src/Core/PVAD.Application/UseCases/<Name>UseCase.cs
```

Keep concrete file-format work in infrastructure/importer projects.

### 2. Add the API route

Add the route to the owning controller, usually `DocumentOpsController`.

### 3. Add frontend service support

Add a static method in `pvad-designer/src/services/ExportService.ts` or a more specific service if one exists.

### 4. Add tests and docs

- Use-case tests in `PVAD.Application.Tests`
- endpoint tests in `PVAD.Api.Tests`
- README/API docs and relevant checklist entries

---

## Documentation Rules

- `README.md`: user-facing capabilities, endpoints, and documentation map.
- `ARCHITECTURE.md`: project boundaries and dependency direction.
- `PROJECT_SUMMARY.md`: compact inventory of projects, features, endpoints, and tests.
- `TESTING.md`: test groups and commands.
- `CONTRIBUTING_RENDERERS.md`: extension patterns.
- `checklists/*.md`: milestone history, implementation checklists, provider notes, and roadmap details.
