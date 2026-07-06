# Contributor Guide: Adding Renderers, Importers, and Migrations

This guide covers the main extension patterns in Power Dox Automation / PXA:

- output renderers and exporters
- file importers
- PDF code migration providers
- report-to-design converters
- document operation endpoints

Keep new code inside the smallest project boundary that owns the feature, and update docs/checklists in the same change. PXA is currently additive: prefer public `PXA.*` facades for new developer-facing APIs while using the existing `Canvas.*` projects as the implementation boundary until the later physical rename.

---

## Pattern A - Output Renderer / Export Format

### 1. Create or choose the infrastructure project

Use an existing infrastructure project when the format belongs to an established adapter family:

- `src/Canvas.Infrastructure.Pdf` for PDF rendering services.
- `src/Canvas.Infrastructure.Word` for DOCX/Word output.
- `src/Canvas.Infrastructure.Sheet` for XLSX output.
- `src/Canvas.Infrastructure.Converters` for ODT, HTML, CSV, Markdown, image, TIFF, SVG/XML-style output.

Create `src/Canvas.Infrastructure.<Format>` only when the format needs its own dependencies or boundary.

When the feature is public developer-facing API, add or update the corresponding `PXA.Infrastructure.*` or
`PXA.Generator` facade so new examples do not need to start from legacy `Canvas.*` names.

Dependencies:

- Required: `Canvas.Core`
- Optional: `Canvas.Application` only when orchestration helpers are explicitly needed
- Avoid references to other infrastructure projects

### 2. Implement the exporter

Implement `IDocumentRenderer` or the existing format-specific equivalent from `Canvas.Core.Abstractions`.

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

Wire the exporter in `Canvas.WebApi/Program.cs` and expose it through `ExportController`.

Update:

- export format switch/dispatch
- DI registration
- format metadata from `GET /api/export/formats`, if relevant

### 4. Add the frontend format entry

In `ui-designer-v2/src/services/ExportService.ts`:

- add the format literal to the `ExportFormat` union
- add the extension to the extension map

In `ui-designer-v2/src/components/Editor/ExportModal.tsx`:

- add a format option in the appropriate group
- keep server-side/client-side behavior consistent with existing options

### 5. Add tests

Add or update tests in the closest project:

- `tests/Canvas.Export.Tests` for end-to-end export behavior
- a dedicated infrastructure test project if the renderer has substantial internal behavior
- snapshot tests only when output is stable enough to make them meaningful

### 6. Update docs

- `README.md`: export format table and API notes
- `PROJECT_SUMMARY.md`: feature inventory, if the format is user-facing
- `ARCHITECTURE.md`: infrastructure responsibilities, if a project/boundary changed
- relevant checklist under `checklists/`

---

## Pattern B - File Importer

New file importers should expose a PXA-facing facade and use the dedicated legacy implementation project pattern while the physical rename is deferred.

### 1. Create the importer project

Project layout:

```text
src/Canvas.FileImporter.<Format>/
tests/Canvas.FileImporter.<Format>.Tests/
```

Public facade target:

```text
src/PXA.FileImporter.<Format>/
```

Dependencies:

- `Canvas.FileImporter.Abstractions`
- `Canvas.Core`
- parser/conversion packages needed by that format only
- `Canvas.Importer` only for PDF-specific importers that need the PDF parser/editor engine

Avoid placing new importers in `Canvas.Infrastructure.Converters`; that project now owns converter-style exporters and legacy/shared converter infrastructure, not the preferred importer boundary.

### 2. Implement `IFileImporter`

Implement the importer contract from `src/Canvas.FileImporter.Abstractions`.

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

Update `ui-designer-v2`:

- `ExportService.ts`: service method calling the endpoint
- `useTemplateLoader.ts`: file-extension dispatch
- relevant page/modal accept attributes
- user-facing diagnostics or loading state, if needed

### 5. Add tests

Add focused tests in `tests/Canvas.FileImporter.<Format>.Tests`.

Minimum coverage:

- valid sample imports to `DesignExportDto`
- invalid/empty input behavior
- key layout and element mapping assertions
- endpoint smoke test in `Canvas.Api.Tests` when the WebApi route is new

### 6. Update docs

- `README.md`: import format table and API reference
- `PROJECT_SUMMARY.md`: import inventory
- `ARCHITECTURE.md`: importer project responsibility
- importer checklist under `checklists/`

---

## Pattern C - PDF Code Migration Provider

Use this pattern when converting C# source from a third-party PDF library into PXA-compatible PDF C# source.

### 1. Create provider project and tests

Project layout:

```text
src/Canvas.Migration.<Provider>/
tests/Canvas.Migration.<Provider>.Tests/
```

Public facade target:

```text
src/PXA.Migration.<Provider>/ or provider registry under src/PXA.Migration.Pdf/
```

Dependencies:

- `Canvas.Migration.Abstractions`
- `Canvas.Migration.Roslyn` for C# source rewriting providers
- `Canvas.Core` only when DTO/contracts are required

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

- Roslyn-backed rewriter under `src/Canvas.Migration.<Provider>`
- provider-neutral result and diagnostic contracts from `Canvas.Migration.Abstractions`
- no silent destructive rewrites
- preserve source where manual follow-up is required

### 4. Register in WebApi

Add the converter to the DI setup used by `MigrationService`.

Ensure it appears in:

- `GET /api/migration/frameworks`
- `POST /api/migration/convert`
- `POST /api/migration/preview`, when preview can replay generated PXA-compatible PDF calls

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

Use this pattern when a reporting framework or report file format should become an editable PXA design.

Existing examples:

- `PXA.Migration.Report` / legacy `Canvas.Migration.DevExpressReport`: XtraReport C# and REPX
- `PXA.Migration.Report` / legacy `Canvas.Migration.Rdl`: RDL/RDLC/Syncfusion/Bold Reports style XML
- `PXA.Migration.Report` / legacy `Canvas.Migration.Rpx`: ActiveReports/GrapeCity section reports

### 1. Target `DesignExportDto`

Report converters output editable design JSON, not PXA-compatible PDF C# source.

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
src/Canvas.Migration.<ReportFormat>/
tests/Canvas.Migration.<ReportFormat>.Tests/
```

Use `Canvas.Migration.Abstractions` for diagnostics and `Canvas.Core` for `DesignExportDto`.

### 3. Wire endpoint routing

Register detection/conversion in `MigrationController` behind:

```text
POST /api/migration/report-to-design
```

Detection must be explicit and ordered so similar formats do not steal each other's inputs.

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
src/Canvas.Application/UseCases/<Name>UseCase.cs
```

Keep concrete file-format work in infrastructure/importer projects.

### 2. Add the API route

Add the route to the owning controller, usually `DocumentOpsController`.

### 3. Add frontend service support

Add a static method in `ui-designer-v2/src/services/ExportService.ts` or a more specific service if one exists.

### 4. Add tests and docs

- Use-case tests in `Canvas.Application.Tests`
- endpoint tests in `Canvas.Api.Tests`
- README/API docs and relevant checklist entries

---

## Documentation Rules

- `README.md`: user-facing capabilities, endpoints, and documentation map.
- `ARCHITECTURE.md`: project boundaries and dependency direction.
- `PROJECT_SUMMARY.md`: compact inventory of projects, features, endpoints, and tests.
- `TESTING.md`: test groups and commands.
- `CONTRIBUTING_RENDERERS.md`: extension patterns.
- `checklists/*.md`: milestone history, implementation checklists, provider notes, and roadmap details.
