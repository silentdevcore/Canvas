# Power Dox Automation / PXA - Project Summary

## Overview

Power Dox Automation (PXA) is a document automation platform with a visual template designer, data binding, multi-language output, multi-format export/import, PDF code migration, and report-to-design migration. The active stack is a .NET 10 API plus the `ui-designer-v2` React/TypeScript frontend. The breaking PXA-to-PXA rename has been accepted: active source projects and namespaces use `PXA.*`.

---

## Technology Stack

| Layer | Technology |
|-------|------------|
| Frontend | React 18, TypeScript, Vite, Zustand, react-icons |
| Backend | .NET 10, ASP.NET Core |
| PDF export | PXA PDF renderer/writer, no external PDF library |
| PDF import/edit model | PXA importer low-level parser, scene graph, editing model, regeneration bridge |
| DOCX export/import | DocumentFormat.OpenXml 3.5.1 |
| DOCX digital signing | System.Security.Cryptography.Xml 10.0.8 |
| Excel export | ClosedXML |
| File importers | Dedicated `PXA.FileImporter.*` projects |
| Migration | `PXA.Migration.*` provider projects, Roslyn helpers, report converters |
| ODT import/export | System.IO.Compression + LINQ to XML |
| Image analysis/OCR | `PXA.FileImporter.ImageAnalysis`, `PXA.FileImporter.ImageOcr`, isolated OCR worker |

---

## Backend Project Groups

| Group | Projects | Role |
|-------|----------|------|
| Core | `PXA.Core` | Contracts, DTOs, abstractions, capabilities, primitives |
| Application | `PXA.Application` | Use-case orchestration |
| PDF engine | `PXA.Pdf`, `PXA.Infrastructure.Pdf`, `PXA.Generator` | PDF generation API, renderer facade, public generator entry point |
| Other exporters | `PXA.Infrastructure.Word`, `PXA.Infrastructure.Spreadsheet`, `PXA.Infrastructure.Converters` | DOCX/XLSX/ODT/HTML/CSV/Markdown/Image/TIFF and related services |
| File importers | `PXA.FileImporter.Abstractions`, `PXA.FileImporter.*` | PDF, DOCX, DOC, ODT, PPTX, SVG, Image, ImageAnalysis, OCR import paths |
| PDF importer SDK | `PXA.Importer` | PDF parser, editable DOM, graphics interpretation, regeneration bridge |
| Migrations | `PXA.Migration.Abstractions`, `PXA.Migration.Roslyn`, `PXA.Migration.*` | Provider implementations for vendor code migration and report-to-design conversion |
| API | `PXA.WebApi` | Presentation layer and composition root with legacy HTTP route aliases |
| Domain | `PXA.Domain` | Template/domain models and repositories |

---

## Frontend Structure (`ui-designer-v2/src/`)

| Folder | Contents |
|--------|----------|
| `components/Editor/` | PXA editor, toolbar, inspector panels, export/code/modals |
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

DevExpress XtraReport / REPX, RDL/RDLC/Syncfusion/Bold Reports style XML, JasperReports JRXML, ActiveReports/GrapeCity `.rpx`, ActiveReports JS JSON, FastReport FRX, Telerik TRDX, and Stimulsoft MRT.

### Element Types

Text, rich text, image, table, rect, circle, line, barcode, QR code, signature, field, checkbox, repeater, chart, note, footnote, endnote, bookmark, comment, content control, and report/import placeholders where applicable.

### Document Operations

Find and replace, clone, extract pages, DOCX digital signing, template validation/rendering, C# code-to-PDF, C# code-to-JSON, raw design rendering, code migration, report-to-design migration, migration preview, and PDF viewer operations.

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

POST   /api/document/import-pdf-engine     Import PDF through the PXA importer
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
| Core/Application/API | `PXA.Core.Tests`, `PXA.Application.Tests`, `PXA.Api.Tests` |
| PDF engine/export | `PXA.Infrastructure.Pdf.Tests`, `PXA.Export.Tests` |
| PDF importer SDK | `PXA.Importer.Tests` |
| File importers | `PXA.FileImporter.*.Tests` |
| Image analysis/OCR | `PXA.FileImporter.ImageAnalysis.Tests`, `PXA.FileImporter.ImageOcr.Tests` |
| PDF code migrations | `PXA.Migration.*Pdf.Tests`, `PXA.Migration.iText7.Tests`, provider-specific tests |
| Report migrations | `PXA.Migration.DevExpressReport.Tests`, `PXA.Migration.Rdl.Tests`, `PXA.Migration.Rpx.Tests` |

See `TESTING.md` for command examples and validation policy.
