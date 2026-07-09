# Power Dox Automation / PXA - Architecture

## Naming Glossary

| Name | Meaning |
|------|---------|
| Power Dox Automation | Product and website name |
| PXA | Developer/API/CLI identity and future native file-format prefix |
| `PXA.*` | Additive public facade layer preferred for new developer-facing code |
| `Canvas.*` | Legacy implementation layer retained for compatibility until the physical rename phase |

## Dependency Direction

```text
PXA.* facade projects
   |
   v
PXA.WebApi
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

Rule: PXA is currently additive. Public PXA-facing projects delegate into the existing Canvas implementation
projects while the physical rename is deferred. Shared contracts still point inward toward `Canvas.Core` /
`PXA.Core`; infrastructure, importer, and migration projects may implement or consume Core contracts, but
Core must not know about their concrete dependencies.

---

## Project Responsibilities

### `src/PXA.*`
Layer: public Power Dox Automation facade and compatibility bridge.

- `PXA.Generator` exposes PDF/Word/Spreadsheet generation facades over the current renderer/exporter stack.
- `PXA.Importer` and `PXA.FileImporter.*` expose PXA-facing import contracts while delegating to the existing importers.
- `PXA.Migration.*` exposes provider registries and migration facades for PDF code, reports, and spreadsheets.
- `PXA.Application`, `PXA.Core`, `PXA.Domain`, and `PXA.Infrastructure.*` provide additive contracts/adapters while the legacy implementation remains in place.

These projects are the preferred public identity for new code. They must preserve compatibility with the
legacy `Canvas.*` projects until the later physical rename phase.

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

### `PXA.WebApi`
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

- `PdfDocumentRenderer` and `PdfFacade` render `DesignExportDto` content through the PXA-compatible PDF engine, currently backed by `Canvas.Pdf`.
- Service adapters apply page numbering, headers/footers, table flow, table of contents, watermarks, diagnostics, and page coverage queries.
- `PdfRendererCapabilities` declares supported PDF-renderer features.

### `Canvas/`
PXA-compatible PDF engine and compatibility/demo shell.

- `Canvas/Pdf/` contains the current direct PDF generation API, writer, page model, text/image/vector/form elements, encryption support, and technical documentation.
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

- `PXA.FileImporter.Pdf` / legacy `Canvas.FileImporter.Pdf`: PDF import adapter backed by `Canvas.Importer`.
- `PXA.FileImporter.*` / legacy `Canvas.FileImporter.Docx`: DOCX import.
- `PXA.FileImporter.*` / legacy `Canvas.FileImporter.Doc`: legacy Word `.doc` import.
- `PXA.FileImporter.*` / legacy `Canvas.FileImporter.Odt`: ODT import.
- `PXA.FileImporter.*` / legacy `Canvas.FileImporter.Pptx`: PPTX import.
- `PXA.FileImporter.*` / legacy `Canvas.FileImporter.Svg`: SVG import.
- `PXA.FileImporter.*` / legacy `Canvas.FileImporter.Image`: direct raster image import.
- `PXA.FileImporter.ImageAnalysis` / legacy `Canvas.FileImporter.ImageAnalysis`: deterministic image analysis pipeline.
- `PXA.FileImporter.ImageOcr` / legacy `Canvas.FileImporter.ImageOcr` and `.Worker`: OCR-based image conversion path and isolated worker.

### `src/Canvas.Importer`
Low-level PDF parsing, interpretation, editable model, and regeneration bridge.

- Handles tokenization, cross-reference/object parsing, stream decoding, content stream parsing, graphics interpretation, text/font reconstruction, editable PDF model, and bridge generation into the PDF output engine.
- This replaces the old `PdfImporter`/PdfPig-based importer path. Historical checklists may still mention PdfPig, but current architecture should treat the PXA importer facade plus legacy `Canvas.Importer` implementation as the source of truth for PDF import and editing/regeneration work.

---

## Migration Infrastructure

### Shared Migration Projects

- `src/Canvas.Migration.Abstractions`: migration result and diagnostic contracts.
- `src/Canvas.Migration.Roslyn`: shared C# source migration helpers.

### PDF Code Migration Providers

Each provider has an isolated legacy implementation project under `src/Canvas.Migration.<Provider>` and a matching `tests/Canvas.Migration.<Provider>.Tests` project. New public entry points should use the additive `PXA.Migration.*` facades.

Current providers include Syncfusion, iText7, Aspose.PDF, IronPDF, DevExpress PDF, Apryse, Foxit, DsPdf, GemBox.Pdf, Spire.PDF, PDFKit.NET, LEADTOOLS PDF, ActivePDF, PDFTools, and PDFTools Toolbox.

### Report-To-Design Migration

- `PXA.Migration.Report` / legacy `src/Canvas.Migration.DevExpressReport`: DevExpress XtraReport C# / REPX to editable PXA design.
- `PXA.Migration.Report` / legacy `src/Canvas.Migration.Rdl`: RDL/RDLC/Syncfusion/Bold Reports style XML to editable PXA design.
- `PXA.Migration.Report` / legacy `src/Canvas.Migration.Rpx`: ActiveReports/GrapeCity section report `.rpx` to editable PXA design.

These converters target `DesignExportDto`, not PXA-compatible PDF C# code.

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

1. `Canvas.Core` / `PXA.Core` must remain implementation-agnostic.
2. `Canvas.Application` / `PXA.Application` must not reference concrete Infrastructure, Importer, or Migration projects.
3. Infrastructure projects implement Core abstractions and should not reference one another unless a documented adapter boundary requires it.
4. File importer projects should reference `Canvas.FileImporter.Abstractions`, `Canvas.Core`, and only the parser libraries they need.
5. Migration provider projects must stay isolated from renderer/importer infrastructure except for shared migration abstractions and Roslyn helpers.
6. Report migration projects output `DesignExportDto`; PDF code migration projects output PXA-compatible PDF C# source.
7. `PXA.WebApi` is the current composition root and owns both legacy and PXA endpoint wiring.

---

## Validation

- `dotnet build Canvas.sln` must produce 0 errors for code changes.
- Run focused tests for the touched subsystem, then broader tests when shared contracts or endpoints change.
- Frontend changes should validate with `cd ui-designer-v2 && npm run build` and relevant Jest tests.
- Documentation changes should keep links relative and prefer `README.md`, this file, `CONTRIBUTING_RENDERERS.md`, and `TESTING.md` as the stable entry points.
