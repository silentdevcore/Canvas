# Power Dox Automation / PXA - Project Summary

## Overview

Power Dox Automation (PXA) is a document automation platform with a visual template designer, data binding, multi-language output, multi-format export/import, PDF code migration, and report-to-design migration. The active stack is a .NET 10 API plus the `ui-designer-v2` React/TypeScript frontend. PXA is currently additive: public `PXA.*` facades sit over the existing legacy `Canvas.*` implementation projects.

---

## Technology Stack

| Layer | Technology |
|-------|------------|
| Frontend | React 18, TypeScript, Vite, Zustand, react-icons |
| Backend | .NET 10, ASP.NET Core |
| PDF export | PXA-compatible PDF renderer/writer backed by legacy `Canvas.Pdf`, no external PDF library |
| PDF import/edit model | PXA importer facade backed by legacy `Canvas.Importer` low-level parser, scene graph, editing model, regeneration bridge |
| DOCX export/import | DocumentFormat.OpenXml 3.5.1 |
| DOCX digital signing | System.Security.Cryptography.Xml 10.0.8 |
| Excel export | ClosedXML |
| File importers | Dedicated `PXA.FileImporter.*` facades over legacy `Canvas.FileImporter.*` projects |
| Migration | `PXA.Migration.*` facades over legacy `Canvas.Migration.*` provider projects, Roslyn helpers, report converters |
| ODT import/export | System.IO.Compression + LINQ to XML |
| Image analysis/OCR | `PXA.FileImporter.ImageAnalysis`, `PXA.FileImporter.ImageOcr`, legacy Canvas implementations, isolated OCR worker |

---

## Backend Project Groups

| Group | Projects | Role |
|-------|----------|------|
| PXA public facades | `PXA.Generator`, `PXA.Importer`, `PXA.FileImporter.*`, `PXA.Migration.*`, `PXA.Core`, `PXA.Application`, `PXA.Domain`, `PXA.Infrastructure.*` | Additive public identity and compatibility bridges |
| Core | `Canvas.Core` | Contracts, DTOs, abstractions, capabilities, primitives |
| Application | `Canvas.Application` | Use-case orchestration |
| PDF engine | `Canvas`, `Canvas.Infrastructure.Pdf` | Legacy direct PDF generation API and renderer facade |
| Other exporters | `Canvas.Infrastructure.Word`, `Canvas.Infrastructure.Sheet`, `Canvas.Infrastructure.Converters` | DOCX/XLSX/ODT/HTML/CSV/Markdown/Image/TIFF and related services |
| File importers | `Canvas.FileImporter.Abstractions`, `Canvas.FileImporter.*` | PDF, DOCX, DOC, ODT, PPTX, SVG, Image, ImageAnalysis, OCR import paths |
| PDF importer SDK | `Canvas.Importer` | Legacy PDF parser, editable DOM, graphics interpretation, regeneration bridge |
| Migrations | `Canvas.Migration.Abstractions`, `Canvas.Migration.Roslyn`, `Canvas.Migration.*` | Legacy provider implementations for vendor code migration and report-to-design conversion |
| API | `PXA.WebApi` | Presentation layer and composition root with legacy and PXA route aliases |
| Domain compatibility | `Canvas.Domain` | Legacy/domain models used by compatibility paths |

---

## Frontend Structure (`ui-designer-v2/src/`)

| Folder | Contents |
|--------|----------|
| `components/Editor/` | Canvas editor, toolbar, inspector panels, export/code/modals |
| `components/Gallery/` | Template gallery and cards |
| `components/Layout/` | App shell/header |
| `components/CodeEditor/` | JSON/C# editor and PDF preview |
| `components/Preview/` | Live preview |
| `pages/` | Index, docs, template, create, importer, migrations |
| `services/` | Export/import/code-generation service calls |
| `hooks/` | Template/file loading hooks |
| `store.ts` | Zustand state, undo/redo, pages, shared elements |
| `types.ts` | Frontend design and element contracts |

---

## Feature Inventory

### Export Formats
PDF, DOCX, ODT, XLSX, HTML, CSV, Markdown, PNG, JPEG, TIFF.

### Import Formats And Conversion Inputs
PDF engine import, DOCX, DOC, ODT, SVG, PPTX, raster images, image analysis, OCR image conversion, and image-to-PDF conversion.

### PDF Code Migration Providers
Syncfusion PDF, iText7, Aspose.PDF, IronPDF, DevExpress PDF, Apryse, Foxit PDF SDK, DsPdf, GemBox.Pdf, Spire.PDF, PDFKit.NET, LEADTOOLS PDF, ActivePDF, PDFTools / Pdftools SDK, and PDFTools Toolbox.

### Report-To-Design Migration
DevExpress XtraReport / REPX, RDL/RDLC/Syncfusion/Bold Reports style XML, and ActiveReports/GrapeCity `.rpx`.

### Element Types
Text, rich text, image, table, rect, circle, line, barcode, QR code, signature, field, checkbox, repeater, chart, note, footnote, endnote, bookmark, comment, content control, and report/import placeholders where applicable.

### Document Operations
Find and replace, clone, extract pages, DOCX digital signing, template validation/rendering, C# code-to-PDF, C# code-to-JSON, raw design rendering, code migration, report-to-design migration, and migration preview.

---

## API Endpoints

```text
POST   /api/export                         Export design
POST   /api/export/multilanguage           Multi-language export
GET    /api/export/formats                 Supported export formats

POST   /api/document/find-replace          Find and replace
POST   /api/document/clone                 Clone design
POST   /api/document/extract-pages         Extract pages
POST   /api/document/sign-docx             Apply X.509 digital signature
POST   /api/document/convert-image-to-pdf  Convert raster image to PDF

POST   /api/document/import-pdf-engine     Import PDF through the PXA importer facade
POST   /api/document/debug-pdf-engine      Debug PDF importer output
POST   /api/document/import-docx           Import DOCX
POST   /api/document/import-doc            Import DOC
POST   /api/document/import-odt            Import ODT
POST   /api/document/import-image          Import raster image
POST   /api/document/import-svg            Import SVG
POST   /api/document/import-pptx           Import PPTX
POST   /api/document/import-image-analysis Import image through analysis pipeline

GET    /api/migration/frameworks           List migration frameworks
POST   /api/migration/convert              Convert vendor PDF code to PXA-compatible PDF C#
POST   /api/migration/report-to-design     Convert report source to DesignExportDto
POST   /api/migration/preview              Preview migrated PXA-compatible PDF code

GET    /api/templates                      List templates
POST   /api/templates                      Create template
GET    /api/templates/{id}                 Get template
PUT    /api/templates/{id}                 Update template
POST   /api/templates/render               Render with data
POST   /api/templates/render/async         Async render job
POST   /api/templates/validate             Validate template
POST   /api/templates/render-design        Render raw DesignExportDto
POST   /api/templates/csharp-code-to-pdf   Compile C# -> PDF
POST   /api/templates/csharp-to-json       Compile C# DTO -> DesignExportDto
POST   /api/templates/csharp-code-to-json  Compile raw C# -> JSON

POST   /api/auth/login
POST   /api/auth/logout
GET    /api/auth/me
```

Default local servers: frontend `http://localhost:5173`; API ports depend on launch profile, commonly `http://localhost:5274` or `http://localhost:5086`.

---

## Test Project Groups

| Group | Example projects |
|-------|------------------|
| Core/Application/API | `Canvas.Core.Tests`, `Canvas.Application.Tests`, `Canvas.Api.Tests` |
| PDF engine/export | `Canvas.Infrastructure.Pdf.Tests`, `Canvas.Export.Tests` |
| PDF importer SDK | `Canvas.Importer.Tests` |
| File importers | `Canvas.FileImporter.*.Tests` |
| Image analysis/OCR | `Canvas.FileImporter.ImageAnalysis.Tests`, `Canvas.FileImporter.ImageOcr.Tests` |
| PDF code migrations | `Canvas.Migration.*Pdf.Tests`, `Canvas.Migration.iText7.Tests`, provider-specific tests |
| Report migrations | `Canvas.Migration.DevExpressReport.Tests`, `Canvas.Migration.Rdl.Tests`, `Canvas.Migration.Rpx.Tests` |

See `TESTING.md` for command examples and validation policy.
