# Canvas — Enterprise Document Automation Platform

A production-ready document automation platform with a visual template designer, a rich expression engine, and multi-format export/import. Comparable to commercial tools like CraftMyPDF, built on .NET 10 + React/TypeScript.

---

## Features

### Visual Template Designer
- Drag-and-drop canvas with pixel-precise positioning and resize handles
- Element library: Text, RichText, Image, Table, Barcode, QR Code, Signature, Field, Checkbox, Shape (Rect, Circle, Line), Repeater, Chart, Note
- Word/DOCX-specific elements: Footnote, Endnote, Bookmark, Comment, Content Control
- Inspector panels per element type; layer ordering; multi-select
- Real-time data-binding preview with JSON payloads

### Template Engine
- `{{ expression }}` data binding with JSON-path resolution and fallbacks
- Safe JavaScript expression evaluation (sandboxed)
- Conditional show/hide per element
- Repeater loops for tables and sections
- Value formatters: currency, date, number, text, custom

### Export Formats
| Format | Notes |
|--------|-------|
| PDF | Custom .NET renderer, no external library |
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
| PDF | UglyToad.PdfPig — words grouped into Text elements, images as base64 |
| DOCX | OpenXML SDK — paragraphs, tables, inline images, typography |
| DOC | Pure C# CFBF parser — reads WordDocument stream via FIB offsets |
| ODT | System.IO.Compression + LINQ to XML — paragraphs, styles, draw:frame images |

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
cd Canvas.WebApi
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

POST /api/document/import-pdf     # Import PDF → DesignExportDto
POST /api/document/import-docx    # Import DOCX → DesignExportDto
POST /api/document/import-doc     # Import DOC (Word 97-2003) → DesignExportDto
POST /api/document/import-odt     # Import ODT → DesignExportDto
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
Canvas.WebApi                  ← ASP.NET Core presentation layer
  ↓
Canvas.Application             ← Use-case orchestration (FindAndReplace, Clone, ExtractPages, …)
  ↓
Canvas.Core                    ← Contracts, DTOs, abstractions (IDocumentRenderer, etc.)
  ↑ (all infrastructure projects implement Canvas.Core contracts)
Canvas.Infrastructure.Pdf      ← Custom PDF renderer (no external PDF library)
Canvas.Infrastructure.Word     ← DOCX exporter, DocxImporter, DigitalSigningService, style/footnote/comment services
Canvas.Infrastructure.Sheet    ← XLSX exporter via ClosedXML
Canvas.Infrastructure.Converters ← ODT, HTML, CSV, Markdown, Image, TIFF exporters; PDF/DOC/ODT importers
Canvas.Domain                  ← Domain models
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
| DOCX export/import | DocumentFormat.OpenXml 3.5.1 |
| DOCX signing | System.Security.Cryptography.Xml 10.0.8 (RSA-SHA256 XML-DSig) |
| Excel | ClosedXML |
| PDF import | UglyToad.PdfPig 1.7.0-custom-5 |
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
cd Canvas.WebApi && dotnet publish -c Release

# Docker
docker build -t canvas-api ./Canvas.WebApi
docker build -t canvas-ui ./ui-designer-v2
docker-compose up -d
```
