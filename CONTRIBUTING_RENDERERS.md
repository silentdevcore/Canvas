# Contributor Guide: Adding a New Renderer or Importer

This guide covers the two main extension patterns in Canvas: **output renderers** (export formats) and **document importers** (import formats).

---

## Pattern A — Output Renderer (new export format)

### 1) Create infrastructure project

Add a project under `src/Canvas.Infrastructure.<Format>`. Dependencies:
- Required: `Canvas.Core`
- Optional: `Canvas.Application` only if orchestration helpers are explicitly needed
- Never reference another Infrastructure project

### 2) Implement the exporter

Implement `IDocumentRenderer` (or a format-specific equivalent) from `Canvas.Core.Abstractions`.

Convention: name the class `<Format>DocumentExporter` (e.g. `TiffDocumentExporter`, `OdtDocumentExporter`).

Supported feature services to implement where applicable:
- `IPageNumberingService`
- `IHeaderFooterService`
- `IRendererCapabilities` — always required; declare every feature as supported or unsupported

### 3) Register in `Program.cs` / `Canvas.WebApi`

Add the new format string to the `format` switch in `ExportController` and wire up the DI registration.

### 4) Add frontend format entry

In `ExportService.ts`:
- Add the format literal to the `ExportFormat` union type
- Add the extension to `extMap`

In `ExportModal.tsx`:
- Add a `FormatOption` entry in the appropriate group (Documents / Images / Data)

### 5) Declare renderer capabilities

Provide an `IRendererCapabilities` implementation that clearly marks supported/unsupported features.

### 6) Add tests

Create/update `tests/Canvas.Export.Tests`:
- At minimum: round-trip test (DesignExportDto → bytes → verify structure)
- Snapshot test if the format supports stable binary/text output

### 7) Update docs

- `ARCHITECTURE.md` — add the new project under Infrastructure responsibilities
- `README.md` — add the format to the Export Formats table
- `CONTRIBUTING_RENDERERS.md` — no change needed unless the pattern itself evolves

---

## Pattern B — Document Importer (new import format)

### 1) Add the importer class

Place it in `src/Canvas.Infrastructure.Converters` (for generic formats) or the appropriate infrastructure project.

Convention: name the class `<Format>Importer` with a single static method:
```csharp
public static DesignExportDto Import(Stream stream, string? name = null)
```

Constraints:
- Iterator methods (`yield return`) cannot have `ref` parameters — use `List<T>` returns instead
- Do not use `switch` on OpenXML enum values (CS9135) — use `==` comparisons
- `StringValue` properties must be parsed via `double.TryParse(val?.Value, out var v)`, not `??`

### 2) Add the API endpoint

In `DocumentOpsController.cs`:
```csharp
[HttpPost("import-<format>")]
[Consumes("multipart/form-data")]
public async Task<IActionResult> Import<Format>(IFormFile? file)
```

Validate extension and content type, stream the file to the importer, return `Ok(design)`.

### 3) Add the frontend service method

In `ExportService.ts`:
```typescript
static async import<Format>(file: File): Promise<object> {
  return this._importFile(file, 'import-<format>');
}
```

### 4) Wire into `useTemplateLoader.ts`

Add an `else if (ext === '<ext>')` branch in `loadFromFile()`.

### 5) Update `TemplatePage.tsx`

Add the extension and MIME type to the `accept` attribute of the hidden file input.

### 6) Update docs

- `README.md` — add the format to the Import Formats table
- `ARCHITECTURE.md` — add to the Converters importer list if it lives there

---

## Pattern C — Document Operation endpoint (clone, extract-pages, sign, etc.)

### 1) Add a use case in `Canvas.Application`

Implement the business logic in `Canvas.Application.UseCases.<Name>UseCase`.

### 2) Add the API endpoint in `DocumentOpsController`

### 3) Add a static method in `ExportService.ts`

### 4) Update checklist and docs accordingly
