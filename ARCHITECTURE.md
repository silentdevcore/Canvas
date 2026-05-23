# Canvas — Architecture

## Dependency direction

```
Canvas.WebApi
   ↓
Canvas.Application
   ↓
Canvas.Core  ←────────────────────────────────────────┐
   ↑                                                   │
Canvas.Infrastructure.Pdf       (PDF rendering)        │
Canvas.Infrastructure.Word      (DOCX export/import)   │
Canvas.Infrastructure.Sheet     (XLSX export)          │
Canvas.Infrastructure.Converters (ODT/HTML/CSV/Markdown/Image/TIFF export; PDF/DOC/ODT import)
Canvas.Domain                   (domain models)        │
```

Rule: all dependencies point inward toward `Canvas.Core`. Infrastructure projects never reference each other or Application.

---

## Project responsibilities

### `src/Canvas.Core`
Layer: Domain / contracts

- `Contracts/DesignExportDto.cs` — canonical serialisation contract shared by all renderers and the API
- `Abstractions/` — `IDocumentRenderer`, `IOutputWriter`, feature service interfaces
- `Capabilities/` — `IRendererCapabilities`
- `Primitives/` — `TemplateExpander`, expression and formatter engine

Must not reference Application or Infrastructure projects.

---

### `src/Canvas.Application`
Layer: Use-case orchestration

Use cases: `FindAndReplaceUseCase`, `CloneTemplateUseCase`, `ExtractPagesUseCase`.

References only `Canvas.Core`.

---

### `src/Canvas.Infrastructure.Pdf`
Layer: Infrastructure adapter — PDF output

- `PdfDocumentRenderer` — custom PDF renderer writing ISO 32000 directly (no external PDF library)
- `PdfFacade` — stable orchestration entry point
- `PdfPageNumberingService`, `PdfHeaderFooterService`, `PdfTableFlowService`
- `PdfRendererCapabilities`

---

### `src/Canvas.Infrastructure.Word`
Layer: Infrastructure adapter — DOCX output and input

**Export:**
- `WordDocumentExporter` — OOXML DOCX output via DocumentFormat.OpenXml 3.5.1
- `StyleDefinitionService` — builds `styles.xml` from `NamedStyleDto` (paragraph, character, list, table); supports `basedOn` / `nextStyle`
- `FootnoteService` — manages `footnotes.xml` and `endnotes.xml`
- `CommentService` — manages `comments.xml`
- `DocumentProtectionService` — writes `<w:documentProtection>` to `settings.xml`
- `CustomPropertiesService` — writes `custom.xml` (text / number / boolean / date)
- `RichTextSpanParser` — HTML → OOXML run conversion

**Import:**
- `DocxImporter` — DOCX → `DesignExportDto`: paragraphs, tables, inline images, typography from RunProperties, page dimensions from SectionProperties

**Signing:**
- `DigitalSigningService` — OOXML XML-DSig (RSA-SHA256): loads PFX via `X509CertificateLoader`, SHA-256 digests all parts, writes `_xmlsignatures/sig1.xml`, patches `[Content_Types].xml` and `_rels/.rels`

Dependencies: `DocumentFormat.OpenXml 3.5.1`, `System.Security.Cryptography.Xml 10.0.8`

---

### `src/Canvas.Infrastructure.Sheet`
Layer: Infrastructure adapter — XLSX output

- `ExcelDocumentExporter` — Excel workbook output via ClosedXML

---

### `src/Canvas.Infrastructure.Converters`
Layer: Infrastructure adapter — all other formats

**Exporters:**
- `OdtDocumentExporter` — ODF 1.3 ZIP with draw frames (pixel-accurate layout)
- `HtmlDocumentExporter` — inline-styled HTML
- `CsvDocumentExporter` — flat CSV
- `MarkdownDocumentExporter` — Markdown
- `ImageDocumentExporter` — PNG / JPEG page images
- `TiffDocumentExporter` — multi-page TIFF (baseline RGB); multi-page exports zipped

**Importers:**
- `PdfImporter` — UglyToad.PdfPig: groups words by baseline Y into Text elements; images as base64 data URIs
- `DocImporter` — pure C# CFBF parser: reads WordDocument stream via FIB offsets; falls back to printable-text scan
- `OdtImporter` — `ZipArchive` + LINQ to XML: parses `content.xml`/`styles.xml`; extracts `draw:frame` images

---

### `Canvas.WebApi`
Layer: Presentation

Controllers:
- `ExportController` — `POST /api/export`, `GET /api/export/formats`
- `DocumentOpsController` — find-replace, clone, extract-pages, sign-docx, import-pdf/docx/doc/odt
- `TemplatesController` — CRUD, render, validate, C# scripting endpoints
- `AuthController` — login, logout, me

---

### `Canvas.Domain`
Layer: Domain models (separate from Core contracts; used by legacy/compatibility paths)

---

## Boundary rules

1. `Canvas.Application` must not reference any Infrastructure project.
2. `Canvas.Core` must remain implementation-agnostic (no OpenXML, no PdfPig, etc.).
3. Infrastructure projects implement `Canvas.Core` contracts; they must not reference each other.
4. `Canvas.WebApi` is the only composition root — it wires everything together.

---

## Validation

- `dotnet build Canvas.sln` — must produce 0 errors on every change
- All test projects must pass: `Canvas.Core.Tests`, `Canvas.Application.Tests`, `Canvas.Infrastructure.Pdf.Tests`, `Canvas.Export.Tests`, `Canvas.Api.Tests`
- Dependency references are one-directional and minimal
