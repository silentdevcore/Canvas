# Canvas — Project Summary

## Overview

Canvas is a production-ready document automation platform: a visual drag-and-drop template designer (React/TypeScript) backed by a clean-architecture .NET 10 API. It covers the full lifecycle — design → data binding → multi-format export — and rivals commercial solutions like CraftMyPDF.

---

## Technology Stack

| Layer | Technology |
|-------|-----------|
| Frontend | React 18, TypeScript, Vite, Zustand, react-icons |
| Backend | .NET 10, ASP.NET Core |
| PDF export | Custom .NET renderer (no iTextSharp/PDFsharp) |
| DOCX export/import | DocumentFormat.OpenXml 3.5.1 |
| DOCX digital signing | System.Security.Cryptography.Xml 10.0.8 |
| Excel export | ClosedXML |
| PDF import | UglyToad.PdfPig 1.7.0-custom-5 |
| DOC import | Pure C# CFBF parser |
| ODT import/export | System.IO.Compression + LINQ to XML (ODF 1.3) |
| TIFF export | System.Drawing (baseline RGB, multi-page) |

---

## Backend Projects

| Project | Role |
|---------|------|
| `Canvas.Core` | Contracts, DTOs, abstractions — no external deps |
| `Canvas.Application` | Use-case orchestration (FindAndReplace, Clone, ExtractPages) |
| `Canvas.Infrastructure.Pdf` | Custom PDF renderer |
| `Canvas.Infrastructure.Word` | DOCX export + import + digital signing |
| `Canvas.Infrastructure.Sheet` | XLSX export |
| `Canvas.Infrastructure.Converters` | ODT/HTML/CSV/Markdown/Image/TIFF export; PDF/DOC/ODT import |
| `Canvas.Domain` | Domain models |
| `Canvas.WebApi` | ASP.NET Core presentation layer; composition root |

---

## Frontend Structure (`ui-designer-v2/src/`)

| Folder | Contents |
|--------|---------|
| `components/Editor/` | SimpleCanvas, ExportModal, FindReplaceModal, inspector panels |
| `components/Gallery/` | TemplatePage, TemplateCard, CategoryFilter |
| `services/ExportService.ts` | export, import, sign, find-replace, clone, extract-pages |
| `hooks/useTemplateLoader.ts` | loadTemplate, loadBlank, loadFromFile (PDF/DOCX/DOC/ODT) |
| `store.ts` | Zustand store: undo/redo, bulkReplaceContent |
| `types.ts` | SimpleElement, Page, PageSettings, NamedStyle, CustomProperty, … |

---

## Feature Inventory

### Export formats
PDF, DOCX, ODT, XLSX, HTML, CSV, Markdown, PNG, JPEG, TIFF

### Import formats
PDF, DOCX, DOC (Word 97-2003), ODT

### Element types
text, richtext, image, table, rect, circle, line, barcode, qrcode, signature, field, checkbox, repeater, chart, note, footnote, endnote, bookmark, comment, contentcontrol

### Document operations (API)
Find & Replace, Clone, Extract Pages, Digital Signing (X.509/RSA-SHA256)

### Document features (DOCX)
- Named styles (paragraph, character, list, table) with `basedOn` / `nextStyle`
- Track Changes / Revisions (`<w:ins>` / `<w:del>` / `<w:rPrChange>`)
- Document Protection (readOnly / comments / trackedChanges / formFields + password)
- Footnotes & Endnotes
- Bookmarks
- Word-native Comments
- Content Controls (rich text, plain text, date picker, combo box, picture)
- Custom Document Properties (text / number / boolean / date)
- Auto-Hyphenation

### Template engine
- `{{ expression }}` data binding with JSON-path resolution
- Safe sandboxed expression evaluation
- Conditional show/hide
- Repeater loops
- Formatters: currency, date, number, text

---

## API Endpoints

```
POST   /api/export                        Export (all formats)
GET    /api/export/formats                Supported formats

POST   /api/document/find-replace         Find and replace
POST   /api/document/clone                Clone design
POST   /api/document/extract-pages        Extract pages
POST   /api/document/sign-docx            Apply X.509 digital signature

POST   /api/document/import-pdf           Import PDF
POST   /api/document/import-docx          Import DOCX
POST   /api/document/import-doc           Import DOC
POST   /api/document/import-odt           Import ODT

GET    /api/templates                     List templates
POST   /api/templates                     Create template
GET    /api/templates/{id}                Get template
POST   /api/templates/render              Render with data
POST   /api/templates/render/async        Async render job
POST   /api/templates/validate            Validate template
POST   /api/templates/render-design       Render raw DesignExportDto
POST   /api/templates/csharp-code-to-pdf  Compile C# → PDF
POST   /api/templates/csharp-to-json      Compile C# → DesignExportDto
POST   /api/templates/csharp-code-to-json Compile C# → JSON

POST   /api/auth/login
POST   /api/auth/logout
GET    /api/auth/me
```

Servers: frontend **http://localhost:5173** — API **http://localhost:5274**

---

## Test Projects

| Project | Scope |
|---------|-------|
| `Canvas.Core.Tests` | Primitives, expression engine |
| `Canvas.Application.Tests` | Use cases with test doubles |
| `Canvas.Infrastructure.Pdf.Tests` | PDF renderer + golden snapshot |
| `Canvas.Export.Tests` | Export integration (all formats) |
| `Canvas.Api.Tests` | API endpoint integration |
