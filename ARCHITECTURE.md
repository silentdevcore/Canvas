# Canvas - Architecture

## Dependency Direction

```text
Canvas.WebApi
   |
   v
Canvas.Application
   |
   v
Canvas.Core  <----------------------------------------------------+
   ^                                                             |
   |                                                             |
Canvas.Infrastructure.Pdf          PDF rendering/services         |
Canvas.Infrastructure.Word         DOCX export/import/signing     |
Canvas.Infrastructure.Sheet        XLSX export                    |
Canvas.Infrastructure.Converters   ODT/HTML/CSV/Markdown/Image    |
Canvas.FileImporter.*              Dedicated import adapters      |
Canvas.Importer                    Low-level PDF parser/editor     |
Canvas.Migration.*                 Vendor/report migration tools   |
Canvas.Domain                      Legacy/domain compatibility     |
```

Rule: shared contracts point inward toward `Canvas.Core`. Infrastructure, importer, and migration projects may implement or consume Core contracts, but Core must not know about their concrete dependencies.

---

## Project Responsibilities

### `src/Canvas.Core`
Layer: contracts and primitives.

- `Contracts/DesignExportDto.cs` is the canonical serialized design contract shared by renderers, importers, migrations, and the API.
- `Abstractions/` contains renderer, output, capability, document-service, and template-service contracts.
- `Capabilities/` describes supported renderer features and fallback behavior.
- `Primitives/` contains the template expander, expression evaluator, formatter engine, and shared value objects.

Must not reference Application, Infrastructure, Importer, Migration, OpenXML, PDF parsing libraries, or WebApi projects.

### `src/Canvas.Application`
Layer: use-case orchestration.

- Use cases include render/export, validate, find-and-replace, clone, extract-pages, page numbering, table flow, headers/footers, watermarks, diagnostics, and template CRUD orchestration.
- References `Canvas.Core` and keeps business workflow code independent of concrete output formats.

### `Canvas.WebApi`
Layer: presentation and composition root.

- `ExportController` exposes export and multi-language export.
- `DocumentOpsController` exposes document operations plus file import endpoints.
- `ImageConversionController` exposes image-to-PDF conversion.
- `MigrationController` exposes code migration, report-to-design migration, framework metadata, and PDF preview.
- `TemplatesController` exposes template CRUD/render/validate and C# scripting endpoints.
- `AuthController` exposes demo authentication endpoints.

The WebApi project is the only place that wires concrete infrastructure, importer, and migration services together.

---

## Rendering Infrastructure

### `src/Canvas.Infrastructure.Pdf`
PDF output adapter and service facade.

- `PdfDocumentRenderer` and `PdfFacade` render `DesignExportDto` content through the custom `Canvas.Pdf` engine.
- Service adapters apply page numbering, headers/footers, table flow, table of contents, watermarks, diagnostics, and page coverage queries.
- `PdfRendererCapabilities` declares supported PDF-renderer features.

### `Canvas/`
`Canvas.Pdf` engine and compatibility/demo shell.

- `Canvas/Pdf/` contains the direct PDF generation API, writer, page model, text/image/vector/form elements, encryption support, and technical documentation.
- `Canvas/Program.cs` is a small demo shell.

### Other Export Infrastructure

- `src/Canvas.Infrastructure.Word`: DOCX export/import support, OOXML styles, footnotes/endnotes, comments, content controls, protection, custom properties, and DOCX digital signing.
- `src/Canvas.Infrastructure.Sheet`: XLSX export through ClosedXML.
- `src/Canvas.Infrastructure.Converters`: ODT, HTML, CSV, Markdown, image, TIFF, SVG/XML-style export adapters and shared converter capabilities.

---

## Import Infrastructure

### `src/Canvas.FileImporter.Abstractions`
Shared importer abstraction for file-to-`DesignExportDto` adapters.

- Importers should depend on this project plus `Canvas.Core`.
- New import formats should use a dedicated `src/Canvas.FileImporter.<Format>` project and a matching test project.

### Dedicated Importer Projects

- `Canvas.FileImporter.Pdf`: PDF import adapter backed by `Canvas.Importer`.
- `Canvas.FileImporter.Docx`: DOCX import.
- `Canvas.FileImporter.Doc`: legacy Word `.doc` import.
- `Canvas.FileImporter.Odt`: ODT import.
- `Canvas.FileImporter.Pptx`: PPTX import.
- `Canvas.FileImporter.Svg`: SVG import.
- `Canvas.FileImporter.Image`: direct raster image import.
- `Canvas.FileImporter.ImageAnalysis`: deterministic image analysis pipeline.
- `Canvas.FileImporter.ImageOcr` and `.Worker`: OCR-based image conversion path and isolated worker.

### `src/Canvas.Importer`
Low-level PDF parsing, interpretation, editable model, and regeneration bridge.

- Handles tokenization, cross-reference/object parsing, stream decoding, content stream parsing, graphics interpretation, text/font reconstruction, editable PDF model, and bridge generation into the PDF output engine.
- This replaces the old `PdfImporter`/PdfPig-based importer path. Historical checklists may still mention PdfPig, but current architecture should treat `Canvas.Importer` as the source of truth for PDF import and editing/regeneration work.

---

## Migration Infrastructure

### Shared Migration Projects

- `src/Canvas.Migration.Abstractions`: migration result and diagnostic contracts.
- `src/Canvas.Migration.Roslyn`: shared C# source migration helpers.

### PDF Code Migration Providers

Each provider has an isolated project under `src/Canvas.Migration.<Provider>` and a matching `tests/Canvas.Migration.<Provider>.Tests` project.

Current providers include Syncfusion, iText7, Aspose.PDF, IronPDF, DevExpress PDF, Apryse, Foxit, DsPdf, GemBox.Pdf, Spire.PDF, PDFKit.NET, LEADTOOLS PDF, ActivePDF, PDFTools, and PDFTools Toolbox.

### Report-To-Design Migration

- `src/Canvas.Migration.DevExpressReport`: DevExpress XtraReport C# / REPX to editable Canvas design.
- `src/Canvas.Migration.Rdl`: RDL/RDLC/Syncfusion/Bold Reports style XML to editable Canvas design.
- `src/Canvas.Migration.Rpx`: ActiveReports/GrapeCity section report `.rpx` to editable Canvas design.

These converters target `DesignExportDto`, not `Canvas.Pdf` C# code.

---

## Frontend Boundaries

`ui-designer-v2` is the active frontend.

- `components/Editor/`: main editor, canvas, panels, export dialogs, code editor, localized properties, and modals.
- `components/Gallery/`: template browsing and cards.
- `pages/`: index, docs, template, create, importer, migrations.
- `services/`: export/import/migration/code-generation service calls.
- `store.ts`: Zustand editor state, pages, shared elements, undo/redo, bulk replacement.
- `types.ts`: frontend document and element contracts.

The older `ui-designer` tree remains in the repository for legacy/reference work. New UI documentation should prefer `ui-designer-v2` unless a checklist explicitly targets the old app.

---

## Boundary Rules

1. `Canvas.Core` must remain implementation-agnostic.
2. `Canvas.Application` must not reference concrete Infrastructure, Importer, or Migration projects.
3. Infrastructure projects implement Core abstractions and should not reference one another unless a documented adapter boundary requires it.
4. File importer projects should reference `Canvas.FileImporter.Abstractions`, `Canvas.Core`, and only the parser libraries they need.
5. Migration provider projects must stay isolated from renderer/importer infrastructure except for shared migration abstractions and Roslyn helpers.
6. Report migration projects output `DesignExportDto`; PDF code migration projects output `Canvas.Pdf` C# source.
7. `Canvas.WebApi` is the composition root and owns endpoint wiring.

---

## Validation

- `dotnet build Canvas.sln` must produce 0 errors for code changes.
- Run focused tests for the touched subsystem, then broader tests when shared contracts or endpoints change.
- Frontend changes should validate with `cd ui-designer-v2 && npm run build` and relevant Jest tests.
- Documentation changes should keep links relative and prefer `README.md`, this file, `CONTRIBUTING_RENDERERS.md`, and `TESTING.md` as the stable entry points.
