# Power Dox Automation / PXA — Enterprise Document Automation Platform

A production-ready document automation platform with a visual template designer, a rich expression engine, and multi-format export/import. Comparable to commercial tools like CraftMyPDF, built on .NET 10 + React/TypeScript.

---

## Naming Glossary

| Name | Meaning |
|------|---------|
| Power Dox Automation | Product and website name |
| PXA | Developer-facing identity for APIs, packages, CLI, schemas, and future `.pxa` files |
| `PXA.*` | Additive public facade projects for new code |
| `Canvas.*` | Legacy implementation namespaces/projects kept compatible during the rename window |

---

## Features

### Visual Template Designer
- Drag-and-drop canvas with pixel-precise positioning and resize handles
- Element library: Text, RichText, Image, Table, Barcode, QR Code, Signature, Field, Checkbox, Shape (Rect, Circle, Line), Repeater, Chart, Note
- Word/DOCX-specific elements: Footnote, Endnote, Bookmark, Comment, Content Control
- Inspector panels per element type; layer ordering; multi-select
- Real-time data-binding preview with JSON payloads
- **Multi-language text** — per-element language tag (BCP-47) and LTR/RTL direction; canvas preview uses native browser shaping; PDF export embeds Noto fonts
- **Document localization** — active language tabs per document; `{{KEY}}` template variables with Global (all languages, each fills own value) or Own (single-language) scope; Export Code panel shows language-specific JSON and C# code; multi-language ZIP export

### Template Engine
- `{{ expression }}` data binding with JSON-path resolution and fallbacks
- Safe JavaScript expression evaluation (sandboxed)
- Conditional show/hide per element
- Repeater loops for tables and sections
- Value formatters: currency, date, number, text, custom

### Export Formats
| Format | Notes |
|--------|-------|
| PDF | Custom .NET renderer, no external library. Per-element: embedded Noto fonts (Arabic, Hebrew, CJK, Devanagari, Thai, Cyrillic), UTF-16BE encoding, RTL visual order. Document-level: `{{KEY}}` substitution with Global/Own property scopes; single-language or multi-language ZIP export |
| DOCX | Full OOXML: styles, footnotes, endnotes, bookmarks, comments, content controls, track changes, protection, digital signature |
| ODT | ODF 1.3 ZIP with draw frames for pixel-accurate layout |
| XLSX | Excel via ClosedXML |
| HTML | Inline-styled |
| CSV | Flat table export |
| Markdown | Text-based |
| PNG / JPEG / TIFF | Page-by-page image export; multi-page TIFF for print/archival |

### Import Formats
| Format | Notes |
|--------|-------|
| PDF | PXA importer facade backed by the legacy `Canvas.Importer` editable PDF model - page tree, text, paths, inline images, XObject images, marked content, clipping, colors, fonts, shading operators; bridge regeneration currently covers text, vector paths, JPEG/Flate XObject images, soft masks, and compatibility-preserved shading/resource cases |
| DOCX | OpenXML SDK — paragraphs, tables, inline images, typography |
| DOC | Pure C# CFBF parser — reads WordDocument stream via FIB offsets |
| ODT | System.IO.Compression + LINQ to XML — paragraphs, styles, draw:frame images |
| SVG | Dedicated SVG importer for vector-oriented PXA designs |
| PPTX | PowerPoint slide import into PXA pages/elements |
| Images | Raster image import plus image-analysis/OCR conversion paths |

### Document Operations (API)
- **Find & Replace** — plain-text, case-sensitive, whole-word, regex modes
- **Clone** — deep copy with fresh IDs and optional new name
- **Extract Pages** — subset of pages into a new document
- **Digital Signing** — OOXML XML-DSig (RSA-SHA256) with X.509/PFX certificate

### Document Features
- Track Changes / Revisions — per-element revision metadata + `<w:ins>` / `<w:del>`
- Document Protection — readOnly / comments / trackedChanges / formFields modes + password hash
- Named Styles — paragraph, character, list, table styles with inheritance (`basedOn` / `nextStyle`)
- Custom Document Properties — key/value pairs for DMS integration
- Footnotes & Endnotes, Bookmarks, Word-native Comments, Content Controls, Auto-Hyphenation

---

## Quick Start

### Prerequisites
- Node.js 18+
- .NET 10 SDK

### Frontend
```bash
cd ui-designer-v2
npm install
npm run dev
```
Opens at **http://localhost:5173**

### Backend
```bash
cd PXA.WebApi
dotnet run
```
API at **http://localhost:5274** — Swagger UI at http://localhost:5274/swagger

---

## API Reference

### Export
```
POST /api/export                  # Export design to PDF/DOCX/ODT/XLSX/HTML/CSV/Markdown/PNG/JPEG/TIFF
GET  /api/export/formats          # List supported export formats
```

### Document Operations
```
POST /api/document/find-replace   # Find and replace across all text elements
POST /api/document/clone          # Deep-clone a design with a new ID
POST /api/document/extract-pages  # Extract a page subset into a new design
POST /api/document/sign-docx      # Apply X.509 digital signature to a DOCX

POST /api/document/import-pdf-engine      # Import PDF -> DesignExportDto through the PXA importer facade
POST /api/document/debug-pdf-engine       # Debug PDF importer output
POST /api/document/import-docx    # Import DOCX → DesignExportDto
POST /api/document/import-doc     # Import DOC (Word 97-2003) → DesignExportDto
POST /api/document/import-odt     # Import ODT → DesignExportDto
POST /api/document/import-svg     # Import SVG -> DesignExportDto
POST /api/document/import-pptx    # Import PPTX -> DesignExportDto
POST /api/document/import-image   # Import raster image -> DesignExportDto
POST /api/document/import-image-analysis  # Import raster image through analysis pipeline
POST /api/document/convert-image-to-pdf   # Convert raster image to PDF
```

### Migrations
```
GET  /api/migration/frameworks        # List supported code migration frameworks
POST /api/migration/convert           # Convert vendor PDF code to PXA-compatible PDF C#
POST /api/migration/report-to-design  # Convert report source (XtraReport/REPX/RDL/RPX) to DesignExportDto
POST /api/migration/preview           # Render migrated PXA-compatible PDF code as PDF preview
```

### Templates
```
GET    /api/templates             # List templates
POST   /api/templates             # Create template
GET    /api/templates/{id}        # Get template
POST   /api/templates/render      # Render template with data
POST   /api/templates/render/async# Async render job
POST   /api/templates/validate    # Validate template
POST   /api/templates/render-design  # Render raw DesignExportDto
POST   /api/templates/csharp-code-to-pdf   # Compile C# → PDF
POST   /api/templates/csharp-to-json       # Compile C# → DesignExportDto JSON
POST   /api/templates/csharp-code-to-json  # Compile raw C# → JSON
```

### Auth
```
POST /api/auth/login
POST /api/auth/logout
GET  /api/auth/me
```

---

## Architecture

```
PXA.WebApi                  ← ASP.NET Core presentation layer with legacy and PXA API aliases
  ↓
Canvas.Application             ← Use-case orchestration (legacy project name)
  ↓
Canvas.Core                    ← Contracts, DTOs, abstractions (legacy project name)
  ↑ (all infrastructure projects implement Canvas.Core contracts)
PXA.* facade projects          ← Additive public PXA identity for generator/importer/migration surfaces
Canvas.Infrastructure.Pdf      ← Custom PDF renderer (legacy project name)
Canvas.Importer                ← Editable PDF parsing/model/rewrite/regeneration bridge internals
Canvas.Infrastructure.Word     ← DOCX exporter, DocxImporter, DigitalSigningService, style/footnote/comment services
Canvas.Infrastructure.Sheet    ← XLSX exporter via ClosedXML
Canvas.Infrastructure.Converters ← ODT, HTML, CSV, Markdown, Image, TIFF exporters; PDF/DOC/ODT importers
Canvas.Domain                  ← Legacy/domain compatibility models
```

Frontend (`ui-designer-v2/`):
```
src/
  components/
    Editor/       ← SimpleCanvas, ExportModal, FindReplaceModal, inspector panels
    Gallery/      ← TemplatePage, CategoryFilter, TemplateCard
  services/       ← ExportService (export, import, sign, find-replace, clone, extract)
  hooks/          ← useTemplateLoader (loadTemplate, loadBlank, loadFromFile)
  store.ts        ← Zustand store with undo/redo and bulkReplaceContent
  types.ts        ← SimpleElement, Page, PageSettings, NamedStyle, CustomProperty, …
  pages/          ← TemplatePage, EditorPage, HomePage
```

---

## Technology Stack

| Layer | Technology |
|-------|-----------|
| Frontend | React 18, TypeScript, Vite, Zustand, react-icons |
| Backend | .NET 10, ASP.NET Core |
| PDF export | Custom .NET renderer (no iTextSharp/PDFsharp) |
| PDF edit/regenerate | PXA importer facade backed by `Canvas.Importer` + `Canvas.Infrastructure.Pdf` bridge |
| DOCX export/import | DocumentFormat.OpenXml 3.5.1 |
| DOCX signing | System.Security.Cryptography.Xml 10.0.8 (RSA-SHA256 XML-DSig) |
| Excel | ClosedXML |
| PDF import | PXA importer facade backed by `Canvas.Importer` low-level parser/object graph/editor pipeline |
| DOC import | Pure C# CFBF parser |
| ODT import/export | System.IO.Compression + LINQ to XML (ODF 1.3) |
| Image export | System.Drawing / SkiaSharp |

---

## Testing

```bash
# Backend
dotnet test Canvas.sln

# Frontend
cd ui-designer-v2 && npm test
```

Test projects: `Canvas.Core.Tests`, `Canvas.Application.Tests`, `Canvas.Infrastructure.Pdf.Tests`, `Canvas.Export.Tests`, `Canvas.Api.Tests`.

---

## Deployment

```bash
# Frontend production build
cd ui-designer-v2 && npm run build

# Backend publish
cd PXA.WebApi && dotnet publish -c Release

# Docker
docker build -t pxa-api ./PXA.WebApi
docker build -t pxa-ui ./ui-designer-v2
docker-compose up -d
```

### Multi-Language PDF — Font Files

To enable non-Latin PDF export, place Noto font files in the `fonts/` directory next to the published assembly (or configure `Pdf:FontsDirectory` in `appsettings.json`). See [`fonts/README.md`](fonts/README.md) for the full list and download instructions.

---

## Further Documentation

| Document | Description |
|---|---|
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Project layer diagram and responsibilities |
| [`PROJECT_SUMMARY.md`](PROJECT_SUMMARY.md) | Compact project inventory, endpoints, project groups, and test groups |
| [`Canvas/TECHNICAL_DOCUMENTATION.md`](Canvas/TECHNICAL_DOCUMENTATION.md) | Legacy-path technical reference for the PXA-compatible PDF engine (per-element multi-language § 25; document localization § 26) |
| [`ui-designer-v2/MULTILANGUAGE.md`](ui-designer-v2/MULTILANGUAGE.md) | UI guide for per-element language and RTL controls |
| [`fonts/README.md`](fonts/README.md) | Noto font download and deployment instructions |
| [`CONTRIBUTING_RENDERERS.md`](CONTRIBUTING_RENDERERS.md) | How to add renderers, importers, migrations, and report converters |
| [`TESTING.md`](TESTING.md) | Test structure and conventions |
| [`checklists/Documentation-Audit.md`](checklists/Documentation-Audit.md) | Documentation ownership map and update tracker |
| [`checklists/CanvasPdf-Provider-Feature-Gaps.md`](checklists/CanvasPdf-Provider-Feature-Gaps.md) | PXA-compatible PDF feature gaps compared with major PDF providers |

### Documentation Map

| Topic | Start here |
|---|---|
| Architecture and boundaries | [`ARCHITECTURE.md`](ARCHITECTURE.md) |
| Product/project inventory | [`PROJECT_SUMMARY.md`](PROJECT_SUMMARY.md) |
| PDF engine API | [`docs/index.md`](docs/index.md), [`Canvas/TECHNICAL_DOCUMENTATION.md`](Canvas/TECHNICAL_DOCUMENTATION.md), and [`Canvas/README.md`](Canvas/README.md) |
| Renderer/importer/migration contributions | [`CONTRIBUTING_RENDERERS.md`](CONTRIBUTING_RENDERERS.md) |
| Test commands and project groups | [`TESTING.md`](TESTING.md) |
| PDF provider migration status | [`checklists/Code-Migrations.md`](checklists/Code-Migrations.md) |
| Report migration status | [`checklists/Code-Migration-DevExpressReport.md`](checklists/Code-Migration-DevExpressReport.md), [`checklists/Code-Migration-SyncfusionRdl.md`](checklists/Code-Migration-SyncfusionRdl.md), [`checklists/Code-Migration-ActiveReportsRpx.md`](checklists/Code-Migration-ActiveReportsRpx.md) |
| PDF encryption | [`checklists/Pdf-Encryption.md`](checklists/Pdf-Encryption.md) |
| Importer roadmap | [`checklists/UI-Importer-Features.md`](checklists/UI-Importer-Features.md), [`checklists/Importer-New-Featuers.md`](checklists/Importer-New-Featuers.md), [`checklists/PDF-Importer.md`](checklists/PDF-Importer.md) |
| UI and localization | [`ui-designer-v2/MULTILANGUAGE.md`](ui-designer-v2/MULTILANGUAGE.md), [`checklists/UI-Improvements-2026.md`](checklists/UI-Improvements-2026.md), [`checklists/multi-languages.md`](checklists/multi-languages.md) |
