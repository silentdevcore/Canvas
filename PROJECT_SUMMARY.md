# PVADE / PVAD - Project Summary

## Overview

PVADE is the platform brand; PVAD is its technical identity. It provides a visual template designer, data binding, multi-language output, multi-format export/import, PDF code migration, and report-to-design migration. The active stack is a .NET 10 API plus the `pvad-designer` React/TypeScript frontend. Projects and namespaces use `PVAD.*`; PXA names remain only in documented compatibility and historical contracts. PDF and Spreadsheet products are PVADP and PVADS.

---

## Technology Stack

| Layer | Technology |
|-------|------------|
| Frontend | React 18, TypeScript, Vite, Zustand, react-icons |
| Backend | .NET 10, ASP.NET Core |
| PDF export | PVAD PDF renderer/writer, no external PDF library |
| PDF import/edit model | PVAD importer low-level parser, scene graph, editing model, regeneration bridge |
| DOCX export/import | DocumentFormat.OpenXml 3.5.1 |
| DOCX digital signing | System.Security.Cryptography.Xml 10.0.8 |
| Excel export | ClosedXML |
| File importers | Dedicated `PVAD.FileImporter.*` projects |
| Migration | `PVAD.Migration.*` provider projects, Roslyn helpers, report converters |
| ODT import/export | System.IO.Compression + LINQ to XML |
| Image analysis/OCR | `PVAD.FileImporter.ImageAnalysis`, `PVAD.FileImporter.ImageOcr`, isolated OCR worker |

---

## Backend Project Groups

| Group | Projects | Role |
|-------|----------|------|
| Core | `PVAD.Core` | Contracts, DTOs, abstractions, capabilities, primitives |
| Application | `PVAD.Application` | Use-case orchestration |
| PDF engine | `PVAD.Pdf`, `PVAD.Infrastructure.Pdf`, `PVAD.Generator` | PDF generation API, renderer facade, public generator entry point |
| Other exporters | `PVAD.Infrastructure.Word`, `PVAD.Infrastructure.Spreadsheet`, `PVAD.Infrastructure.Converters` | DOCX/XLSX/ODT/HTML/CSV/Markdown/Image/TIFF and related services |
| File importers | `PVAD.FileImporter.Abstractions`, `PVAD.FileImporter.*` | PDF, DOCX, DOC, ODT, PPTX, SVG, Image, ImageAnalysis, OCR import paths |
| PDF importer SDK | `PVAD.Importer` | PDF parser, editable DOM, graphics interpretation, regeneration bridge |
| Migrations | `PVAD.Migration.Abstractions`, `PVAD.Migration.Roslyn`, `PVAD.Migration.*` | Provider implementations for vendor code migration and report-to-design conversion |
| API | `PVAD.WebApi` | Presentation layer and composition root with legacy HTTP route aliases |
| Domain | `PVAD.Domain` | Template/domain models and repositories |

---

## Frontend Structure (`pvad-designer/src/`)

| Folder | Contents |
|--------|----------|
| `components/Editor/` | PVADE editor, toolbar, inspector panels, export/code/modals |
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

POST   /api/document/import-pdf-engine     Import PDF through the PVAD importer
POST   /api/document/debug-pdf-engine      Debug PDF importer output
POST   /api/document/import-docx           Import DOCX
POST   /api/document/import-doc            Import DOC
POST   /api/document/import-odt            Import ODT
POST   /api/document/import-image          Import raster image
POST   /api/document/import-svg            Import SVG
POST   /api/document/import-pptx           Import PPTX
POST   /api/document/import-image-analysis Import image through analysis pipeline

GET    /api/migration/frameworks           List migration frameworks
POST   /api/migration/convert              Convert vendor PDF code to PVAD-compatible PDF C#
POST   /api/migration/report-to-design     Convert report source to DesignExportDto
POST   /api/migration/preview              Preview migrated PVAD-compatible PDF code

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
| Core/Application/API | `PVAD.Core.Tests`, `PVAD.Application.Tests`, `PVAD.Api.Tests` |
| PDF engine/export | `PVAD.Infrastructure.Pdf.Tests`, `PVAD.Export.Tests` |
| PDF importer SDK | `PVAD.Importer.Tests` |
| File importers | `PVAD.FileImporter.*.Tests` |
| Image analysis/OCR | `PVAD.FileImporter.ImageAnalysis.Tests`, `PVAD.FileImporter.ImageOcr.Tests` |
| PDF code migrations | `PVAD.Migration.*Pdf.Tests`, `PVAD.Migration.Pdf.Code.IText7.Tests`, provider-specific tests |
| Report migrations | `PVAD.Migration.Report.Designer.DevExpress.Tests`, `PVAD.Migration.Report.Designer.Rdl.Tests`, `PVAD.Migration.Report.Designer.Rpx.Tests` |

See `TESTING.md` for command examples and validation policy.
